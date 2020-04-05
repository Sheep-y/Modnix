using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Sheepy.Modnix {

   public class LoaderSettings {
      public int SettingVersion = 20200329;
      public SourceLevels LogLevel = SourceLevels.Information;
      // For mod manager
      public bool CheckUpdate = true;
      public DateTime? LastCheckUpdate = null;
      public string UpdateChannel = "release";
      public string GamePath = null;
      public bool MinifyLoaderPanel = false;
      public bool MinifyGamePanel = true;
      // For mod loader, set by manager
      public Dictionary< string, ModSettings > Mods;
   }

   public class ModSettings {
      public bool Disabled;
      public SourceLevels? LogLevel;
      public long? LoadIndex;
      public bool IsDefaultSettings => ! Disabled && LogLevel == null && LoadIndex == null;
   }

   public class ModEntry : ModSettings {
      public readonly string Path;
      public readonly ModMeta Metadata;
      public ModEntry Parent;
      public List<ModEntry> Children;

      public ModEntry ( ModMeta meta ) : this( null, meta ) { }
      public ModEntry ( string path, ModMeta meta ) {
         Path = path;
         Metadata = meta ?? throw new ArgumentNullException( nameof( meta ) );
      }

      public string Key { get { lock ( Metadata ) { return ModScanner.NormaliseModId( Metadata.Id ); } } }
      internal DateTime? LastModified => Path == null ? (DateTime?) null : new FileInfo( Path ).LastWriteTime;
      internal List< Assembly > ModAssemblies = null;

      public long Index { get { lock ( Metadata ) { return LoadIndex ?? Metadata.LoadIndex; } } }

      #region API
      private static readonly Dictionary<string,Func<object,object>> ApiExtension = new Dictionary<string, Func<object, object>>();
      private static readonly Dictionary<string,ModEntry> ApiExtOwner = new Dictionary<string, ModEntry>();

      public object ModAPI ( string action, object param = null ) { try {
         if ( ! LowerAndIsEmpty( action, out action ) ) {
            switch ( action ) {
               case "assembly"    : return GetAssembly( param );
               case "assemblies"  : return GetAssemblies( param );
               case "config"      : return LoadConfig( param );
               case "config_save" : return SaveConfig( param );
               case "dir"         : return GetDir( param );
               case "log"         : CreateLogger().Log( param ); return true;
               case "logger"      : return GetLogFunc( param );
               case "mod_info"    : return new ModMeta().ImportFrom( GetMod( param )?.Metadata );
               case "mod_list"    : return ListMods( param );
               case "path"        : return GetPath( param );
               case "reg_action"  : return RegisterAction( param );
               case "reg_handler" : return RegisterHandler( param );
               case "unreg_action": return UnregisterAction( param );
               case "version"     : return GetVersion( param );
               default:
                  Func<object,object> handler;
                  lock ( ApiExtension ) ApiExtension.TryGetValue( action, out handler );
                  if ( handler != null ) return handler( param );
                  break;
            }
         }
         Warn( "Unknown api action '{0}'", action );
         return null;
      } catch ( Exception ex ) { Error( ex ); return null; } }

      private static bool LowerAndIsEmpty ( object param, out string text ) {
         text = param?.ToString().Trim().ToLowerInvariant();
         return string.IsNullOrWhiteSpace( text );
      }

      private static Assembly GameAssembly;

      private Assembly GetAssembly ( object target ) => GetAssemblies( target )?.FirstOrDefault();

      private IEnumerable < Assembly > GetAssemblies ( object target ) {
         if ( LowerAndIsEmpty( target, out string id ) ) return ModAssemblies ?? Enumerable.Empty<Assembly>();
         switch ( id ) {
            case "loader" : case "modnix" :
               return new Assembly[]{ Assembly.GetExecutingAssembly() };
            case "phoenixpoint" : case "phoenix point" : case "game" :
               if ( GameAssembly == null ) // No need to lock. No conflict.
                  GameAssembly = Array.Find( AppDomain.CurrentDomain.GetAssemblies(), e => e.FullName.StartsWith( "Assembly-CSharp," ) );
               return new Assembly[]{ GameAssembly };
            default:
               return ModScanner.GetModById( id )?.ModAssemblies ?? Enumerable.Empty<Assembly>();
         }
      }

      private Version GetVersion ( object target ) {
         if ( LowerAndIsEmpty( target, out string id ) ) lock ( Metadata ) return Metadata.Version;
         return ModScanner.GetVersionById( id );
      }

      private ModEntry GetMod ( object target ) {
         if ( LowerAndIsEmpty( target, out string id ) ) return this;
         return ModScanner.GetModById( id );
      }

      private string GetPath ( object target ) {
         if ( LowerAndIsEmpty( target, out string id ) ) return Path;
         switch ( id ) {
            case "mods_root" : return ModLoader.ModDirectory;
            case "loader" : case "modnix" :
               return Assembly.GetExecutingAssembly().Location;
            case "phoenixpoint" : case "phoenix point" : case "game" :
               return Process.GetCurrentProcess().MainModule?.FileName;
            default :
               return ModScanner.GetModById( id )?.Path;
         }
      }

      private string GetDir ( object target ) {
         var path = GetPath( target );
         if ( path == null || object.ReferenceEquals( path, ModLoader.ModDirectory ) ) return path;
         return System.IO.Path.GetDirectoryName( path );
      }

      private static IEnumerable<string> ListMods ( object target ) {
         var list = ModScanner.EnabledMods.Select( e => { lock ( e.Metadata ) return e.Metadata.Id; } );
         if ( target == null ) return list;
         if ( target is string txt ) return list.Where( e => e.IndexOf( txt, StringComparison.OrdinalIgnoreCase ) >= 0 );
         if ( target is Regex reg ) return list.Where( e => reg.IsMatch( e ) );
         return null;
      }
      #endregion

      #region API Extension
      private string RegAction;

      private object RegisterAction ( object param ) {
         lock ( ApiExtension ) {
            if ( LowerAndIsEmpty( param, out RegAction ) ) return false;
            if ( ! RegAction.Contains( "." ) || RegAction.Length < 3 ) return false;
            return ! ApiExtension.ContainsKey( RegAction );
         }
      }

      private object RegisterHandler ( object param ) { try {
         string cmd;
         lock ( ApiExtension ) cmd = RegAction;
         if ( cmd == null )
            throw new ApplicationException( "reg_handler without reg_action" );
         if ( ! ( param is Func<object,object> func ) )
            throw new ApplicationException( "reg_handler must be Func< object, object >" );
         lock ( ApiExtension ) {
            if ( ApiExtension.ContainsKey( cmd ) )
               throw new ApplicationException( "Cannot re-register api action " + cmd );
            ApiExtension.Add( cmd, func );
            ApiExtOwner.Add( cmd, this );
            RegAction = null;
         }
         Info( "Registered api action {0}", cmd );
         return true;
      } catch ( ApplicationException ex ) {
         Warn( ex.Message );
         return false;
      } }

      private object UnregisterAction ( object param ) {
         if ( LowerAndIsEmpty( param, out string cmd ) ) return false;
         ModEntry owner;

         lock ( ApiExtension ) ApiExtOwner.TryGetValue( cmd, out owner );
         if ( owner != this ) {
            Warn( $"unreg_action '{cmd}' " + owner == null ? "not found." : "not owner" );
            return false;
         }
         lock ( ApiExtension ) {
            ApiExtension.Remove( cmd );
            ApiExtOwner.Remove( cmd );
         }
         Info( "Unregistered api action {0}", cmd );
         return true;
      }
      #endregion

      #region Logger
      internal LoggerProxy Logger; // Created when and only when an initialiser accepts a logging function

      private Logger CreateLogger () {
         lock ( this ) {
            if ( Logger != null ) return Logger;
            Logger = new LoggerProxy( ModLoader.Log ){ Level = LogLevel ?? ModLoader.Settings.LogLevel };
         }
         var filters = Logger.Filters;
         filters.Add( LogFilters.IgnoreDuplicateExceptions );
         filters.Add( LogFilters.AutoMultiParam );
         filters.Add( LogFilters.AddPrefix( Metadata.Id + ModLoader.LOG_DIVIDER ) );
         return Logger;
      }

      private Delegate GetLogFunc ( object param ) {
         string txt = null;
         if ( param is Type t ) txt = t.Name;
         else if ( param is string s ) txt = s;
         else return null;
         CreateLogger();
         switch ( txt ) {
            case "TraceEventType" : return (Action<TraceEventType,object,object[]>) Logger.Log;
            case "SourceLevels"   : return (Action<SourceLevels,object,object[]>) Logger.Log;
            case "TraceLevel"     : return (Action<TraceLevel,object,object[]>) Logger.Log;
         }
         return null;
      }

      internal void Info  ( object msg, params object[] augs ) => CreateLogger().Info ( msg, augs );
      internal void Warn  ( object msg, params object[] augs ) => CreateLogger().Warn ( msg, augs );
      internal void Error ( object msg ) => CreateLogger().Error( msg );
      #endregion

      #region Config
      private bool ConfigChecked;

      private object LoadConfig ( object param ) { try {
         if ( param == null ) param = typeof( JObject );
         string txt;
         if ( param is Type type ) {
            if ( type == typeof( string ) )
               return GetConfigText();
            var confFile = CheckConfigFile();
            if ( type.FullName.Equals( Metadata.ConfigType ) && confFile == null )
               return Activator.CreateInstance( type ); // Skip text conversion when using ConfigType and no config file found.
            txt = GetConfigText( confFile );
            if ( txt == null ) return Activator.CreateInstance( type );
            param = ModMetaJson.Parse( txt, type );
         } else {
            txt = GetConfigText();
            if ( txt == null ) return param;
            JsonConvert.PopulateObject( txt, param, ModMetaJson.JsonOptions );
         }
         lock ( Metadata )
            if ( Metadata.ConfigType == null ) RunCheckConfig( param.GetType() );
         return param;
      } catch ( Exception e ) { Error( e ); return null; } }

      private void RunCheckConfig ( Type confType ) {
         lock ( Metadata ) {
            if ( ConfigChecked ) return;
            ConfigChecked = true;
         }
         Task.Run( () => { try {
            string confText;
            lock ( Metadata ) confText = GetDefaultConfigText();
            if ( confText == null ) return;
            CreateLogger().Info( "Verifying DefaultConfig in background" );
            var newInstance = Activator.CreateInstance( confType );
            var newText = ModMetaJson.Stringify( newInstance );
            var confObj = ModMetaJson.Parse( confText, confType );
            confText = ModMetaJson.Stringify( confObj );
            if ( confText.Equals( newText, StringComparison.Ordinal ) ) return;
            Warn( "Default config mismatch.\nGot: {0}\nNew: {1}", confText, newText );
         } catch ( Exception ex ) { Info( "Error when verifying config: {0}", ex ); }
         } );
      }

      private Task SaveConfig ( object param ) { try {
         if ( param == null ) return null;
         return Task.Run( () => {
            if ( ! ( param is string str ) )
               str = ModMetaJson.Stringify( param );
            WriteConfigText( str );
         } );
      } catch ( Exception e ) { Error( e ); return null; } }

      public bool HasConfig { get { lock ( Metadata ) {
         return Metadata.ConfigType != null || Metadata.DefaultConfig != null || Metadata.ConfigText != null || CheckConfigFile() != null;
      } } }

      public string GetConfigFile () { try {
         if ( Path == null ) return null;
         var name = System.IO.Path.GetFileNameWithoutExtension( Path );
         /*
         if ( name.Equals( "mod_info", StringComparison.OrdinalIgnoreCase ) )
            name = "mod_init";
         */
         return System.IO.Path.Combine( System.IO.Path.GetDirectoryName( Path ), name + ".conf" );
      } catch ( Exception ex ) { Error( ex ); return null; } }

      public string CheckConfigFile () { try {
         var confFile = GetConfigFile();
         /*
         if ( confFile == null || ! File.Exists( confFile ) )
            confFile = Path.Combine( Path.GetDirectoryName( path ), "mod_init.conf" );
         */
         return File.Exists( confFile ) ? confFile : null;
      } catch ( Exception ex ) { Error( ex ); return null; } }

      public string GetDefaultConfigText () { try {
         var meta = Metadata;
         lock ( meta ) {
            if ( meta.DefaultConfigText != null ) return meta.DefaultConfigText;
            if ( meta.ConfigType != null && ModAssemblies != null ) try {
               foreach ( var asm in ModAssemblies ) {
                  var type = asm.GetType( meta.ConfigType );
                  if ( type == null ) continue;
                  meta.DefaultConfig = Activator.CreateInstance( type );
                  break;
               }
            } catch ( Exception ex ) { Error( ex ); }
            if ( meta.DefaultConfig == null ) return null;
            return meta.DefaultConfigText = ModMetaJson.Stringify( meta.DefaultConfig );
         }
      } catch ( Exception ex ) { Error( ex ); return null; } }

      public string GetConfigText ( string confFile = null ) { try {
         var meta = Metadata;
         lock ( meta )
            if ( meta.ConfigText != null )
               return meta.ConfigText;
         if ( confFile == null ) confFile = CheckConfigFile();
         return meta.ConfigText = confFile != null ? File.ReadAllText( confFile, Encoding.UTF8 ) : GetDefaultConfigText();
      } catch ( Exception ex ) { Error( ex ); return null; } }

      public string CacheDefaultConfigText ( string config ) {
         if ( config == null ) return null;
         lock ( Metadata ) return Metadata.DefaultConfigText = config;
      }

      public void WriteConfigText ( string str ) { try {
         if ( string.IsNullOrWhiteSpace( str ) ) return;
         var path = GetConfigFile();
         CreateLogger().Info( $"Writing {str.Length} chars to {path}" );
         File.WriteAllText( path, str, Encoding.UTF8 );
         lock ( Metadata ) Metadata.ConfigText = str;
      } catch ( Exception ex ) { Error( ex ); } }
      #endregion

      private List<LogEntry> Notices;

      public IEnumerable<LogEntry> GetNotices () => Notices == null ? Enumerable.Empty<LogEntry>() : Notices;

      public void AddNotice ( TraceEventType lv, string reason, params object[] augs ) { lock ( Metadata ) {
         if ( Notices == null ) Notices = new List<LogEntry>();
         Notices.Add( new LogEntry{ Level = lv, Message = reason, Args = augs } );
      } }

      public override string ToString () { lock ( Metadata ) {
         var txt = "Mod " + Metadata.Name;
         if ( Metadata.Version != null ) txt += " " + ModMetaJson.TrimVersion( Metadata.Version );
         if ( Disabled ) txt += " (Disabled)";
         return txt;
      } }
   }

   public class ModMeta {
      public string   Id;
      public Version  Version;

      public TextSet   Name;
      public string[]  Lang;
      public string    Duration;
      public TextSet   Description;
      public TextSet   Author;
      public TextSet   Url;
      public TextSet   Contact;
      public TextSet   Copyright;

      public AppVer[]  Requires;
      public AppVer[]  Disables;
      public long      LoadIndex;

      public string[]  Mods;
      public DllMeta[] Dlls;

      public   string  ConfigType;
      public   object  DefaultConfig;
      internal string  DefaultConfigText;
      internal string  ConfigText;

      internal bool HasContent => Mods == null && Dlls == null;

      internal ModMeta ImportFrom ( ModMeta overrider ) {
         lock ( this ) if ( overrider == null ) return this;
         lock ( overrider ) {
            CopyNonNull( overrider.Id, ref Id );
            CopyNonNull( overrider.Version, ref Version );
            CopyNonNull( overrider.Name, ref Name );
            CopyNonNull( overrider.Lang, ref Lang );
            CopyNonNull( overrider.Duration, ref Duration );
            CopyNonNull( overrider.Description, ref Description );
            CopyNonNull( overrider.Author, ref Author );
            CopyNonNull( overrider.Url, ref Url );
            CopyNonNull( overrider.Contact, ref Contact );
            CopyNonNull( overrider.Copyright, ref Copyright );
            CopyNonNull( overrider.Requires, ref Requires );
            CopyNonNull( overrider.Disables, ref Disables );
            CopyNonNull( overrider.LoadIndex, ref LoadIndex );
            CopyNonNull( overrider.Mods, ref Mods );
            CopyNonNull( overrider.Dlls, ref Dlls );
            CopyNonNull( overrider.ConfigType, ref ConfigType );
            CopyNonNull( overrider.DefaultConfig, ref DefaultConfig );
         }
         lock ( this ) return this;
      }

      internal ModMeta EraseModsAndDlls () { lock ( this ) {
         Mods = null;
         Dlls = null;
         return this;
      } }

      private static void CopyNonNull<T> ( T from, ref T to ) {
         if ( from != null ) to = from;
      }

      #region Normalise
      public ModMeta Normalise () { lock ( this ) {
         Id = NormString( Id );
         NormTextSet( ref Name );
         if ( Name == null && Id != null )
            Name = new TextSet{ Default = Id };
         NormStringArray( ref Lang );
         Duration = NormString( Duration );
         NormTextSet( ref Description );
         NormTextSet( ref Author );
         NormTextSet( ref Url );
         NormTextSet( ref Contact );
         NormTextSet( ref Copyright );
         NormAppVer( ref Requires );
         NormAppVer( ref Disables );
         NormStringArray( ref Mods );
         NormDllMeta( ref Dlls );
         ConfigType = NormString( ConfigType );
         if ( ConfigType != null ) DefaultConfig = null;
         return this;
      } }

      private static string NormString ( string val ) {
         if ( val == null ) return null;
         val = val.Trim();
         if ( val.Length == 0 ) return null;
         return val;
      }

      private static void NormStringArray ( ref string[] val ) {
         if ( val == null ) return;
         val = val.Select( NormString ).Where( e => e != null ).ToArray();
         if ( val.Length == 0 ) val = null;
      }

      private static void NormTextSet ( ref TextSet val ) {
         if ( val == null ) return;
         var dict = val.Dict;
         if ( dict != null ) {
            foreach ( var pair in dict.ToArray() ) {
               string key = NormString( pair.Key ), txt = NormString( pair.Value );
               if ( key == null || txt == null ) dict.Remove( pair.Key );
               if ( pair.Key == key && pair.Value == txt ) continue;
               dict.Remove( pair.Key );
               dict[ key ] = txt;
            }
            if ( dict.Count == 0 ) val.Dict = dict = null;
         }
         val.Default = NormString( val.Default );
         if ( val.Default == null ) {
            val.Default = dict?.First().Value;
            if ( val.Default == null ) val = null;
         }
      }

      private static void NormAppVer ( ref AppVer[] val ) {
         if ( val == null ) return;
         for ( int i = val.Length - 1 ; i >= 0 ; i-- ) {
            val[i].Id = NormString( val[i].Id );
            if ( val[i].Id == null ) val[i] = null;
         }
         if ( val.Any( e => e == null ) )
            val = val.Where( e => e != null ).ToArray();
         if ( val.Length == 0 ) val = null;
      }

      private void NormDllMeta ( ref DllMeta[] val ) {
         if ( val == null ) return;
         for ( int i = val.Length - 1 ; i >= 0 ; i-- ) {
            val[i].Path = NormString( val[i].Path );
            if ( val[i].Path == null ) val[i] = null;
         }
         if ( val.Any( e => e == null || e.Path == null ) )
            val = val.Where( e => e?.Path != null ).ToArray();
         if ( val.Length == 0 ) val = null;
      }
      #endregion
   }

   public class TextSet {
      public string Default { get; set; }
      public Dictionary<string, string> Dict;
      public override string ToString () => ToString( null );
      public string ToString ( string preferred, string fallback = null ) {
         if ( preferred == null ) return Default;
         if ( Dict != null ) {
            if ( Dict.TryGetValue( preferred, out string txt ) ) return txt;
            if ( fallback != null && Dict.TryGetValue( fallback, out string eng ) ) return eng;
         }
         return Default;
      }
   }

   public class AppVer {
      public string Id;
      public Version Min;
      public Version Max;
   }

   public class DllMeta {
      public string Path;
      public Dictionary< string, HashSet< string > > Methods;
   }
}