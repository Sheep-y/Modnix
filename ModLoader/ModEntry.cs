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
      public int SettingVersion = 20200403;
      public SourceLevels LogLevel = SourceLevels.Information;
      // For mod manager
      public bool CheckUpdate = true;
      public DateTime? LastCheckUpdate = null;
      public string UpdateChannel = "dev";
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
      private static readonly Dictionary<string,MethodInfo> NativeApi = new Dictionary<string, MethodInfo>();
      private static readonly Dictionary<string,Func<string,object,object>> ApiExtension = new Dictionary<string, Func<string, object, object>>();
      private static readonly Dictionary<string,ModEntry> ApiExtOwner = new Dictionary<string, ModEntry>();

      private static void AddNativeApi ( string command, string method ) {
         NativeApi.Add( command, typeof( ModEntry ).GetMethod( method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static ) );
      }

      internal static void InitiateNativeApi () { lock ( NativeApi ) {
         if ( NativeApi.Count > 0 ) return;
         AddNativeApi( "api_add"   , nameof( AddApi ) );
         AddNativeApi( "api_info"  , nameof( InfoApi ) );
         AddNativeApi( "api_list"  , nameof( ListApi ) );
         AddNativeApi( "api_remove", nameof( RemoveApi ) );
         AddNativeApi( "assembly"  , nameof( GetAssembly ) );
         AddNativeApi( "assemblies", nameof( GetAssemblies ) );
         AddNativeApi( "config"    , nameof( LoadConfig ) );
         AddNativeApi( "dir"       , nameof( GetDir ) );
         AddNativeApi( "log"       , nameof( DoLog ) );
         AddNativeApi( "logger"    , nameof( GetLogger ) );
         AddNativeApi( "mod_info"  , nameof( GetModInfo ) );
         AddNativeApi( "mod_list"  , nameof( ListMods ) );
         AddNativeApi( "path"      , nameof( GetPath ) );
         AddNativeApi( "stacktrace", nameof( Stacktrace ) );
         AddNativeApi( "version"   , nameof( GetVersion ) );
      } }

      public object ModAPI ( string action, object param = null ) { try {
         action = action.Trim();
         IsMultiPart( action, out string cmd, out string spec );
         if ( ! LowerAndIsEmpty( cmd, out cmd ) ) {
            switch ( cmd ) {
               case "api_add"    : return AddApi( spec, param );
               case "api_info"   : return InfoApi( param );
               case "api_list"   : return ListApi( param );
               case "api_remove" : return RemoveApi( spec );
               case "assembly"   : return GetAssembly( param );
               case "assemblies" : return GetAssemblies( param );
               case "config"     : return LoadConfig( spec, param );
               case "dir"        : return GetDir( param );
               case "log"        : return DoLog( spec, param );
               case "logger"     : return GetLogger( param );
               case "mod_info"   : return GetModInfo( param );
               case "mod_list"   : return ListMods( param );
               case "path"       : return GetPath( param );
               case "stacktrace" : return Stacktrace( spec );
               case "version"    : return GetVersion( param );
               default:
                  Func<string,object,object> handler;
                  lock ( ApiExtension ) ApiExtension.TryGetValue( cmd, out handler );
                  if ( handler != null ) return handler( spec, param );
                  break;
            }
         }
         Warn( "Unknown api action '{0}'", action );
         return null;
      } catch ( Exception ex ) { Error( ex ); return null; } }

      private static bool IsMultiPart ( string text, out string firstWord, out string rest ) {
         int pos = text.IndexOf( ' ' );
         if ( pos <= 0 ) {
            firstWord = text;
            rest = "";
            return false;
         }
         firstWord = text.Substring( 0, pos );
         rest = text.Substring( pos + 1 ).TrimStart();
         return true;
      }

      private static bool LowerAndIsEmpty ( object param, out string text ) {
         text = param?.ToString().Trim().ToLowerInvariant();
         return string.IsNullOrEmpty( text );
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

      private static IEnumerable<string> ListMods ( object target ) =>
         FilterStringList( ModScanner.EnabledMods.Select( e => e.Metadata.Id ), target );

      private static IEnumerable<string> FilterStringList ( IEnumerable<string> list, object param ) {
         if ( param == null ) return list;
         if ( param is string txt ) return list.Where( e => e.IndexOf( txt, StringComparison.OrdinalIgnoreCase ) >= 0 );
         if ( param is Regex reg ) return list.Where( e => reg.IsMatch( e ) );
         return null;
      }
      #endregion

      #region API Extension
      private bool AddApi ( string name, object param ) { try {
         if ( IsMultiPart( name, out name, out string type ) )
            throw new ArgumentException( $"Unknown specifier '{type}'." );
         if ( LowerAndIsEmpty( name, out string cmd ) || ! cmd.Contains( "." ) || cmd.Length < 3  )
            throw new ArgumentException( $"Invalid name for api_add, need a dot and at least 3 chars. Got '{cmd}'." );
         if ( ! ( param is Func<string,object,object> func3 ) ) {
            if ( param is Func<object,object> func2 )
               func3 = ( _, augs ) => func2( augs );
            else
               throw new ArgumentException( "api_add parameter must be Func< object, object > or Func< string, object, object >" );
         }
         lock ( ApiExtension ) {
            if ( ApiExtension.ContainsKey( cmd ) )
               throw new InvalidOperationException( $"Cannot re-register api 'cmd'." );
            ApiExtension.Add( cmd, func3 );
            ApiExtOwner.Add( cmd, this );
         }
         Info( "Registered api '{0}'", cmd );
         return true;
      } catch ( Exception ex ) {
         Warn( ex.Message );
         return false;
      } }

      private bool RemoveApi ( object param ) { try {
         if ( LowerAndIsEmpty( param, out string cmd ) ) return false;
         if ( IsMultiPart( cmd, out cmd, out string type ) )
            throw new ArgumentException( $"Unknown specifier '{type}'." );
         ModEntry owner;
         lock ( ApiExtension ) ApiExtOwner.TryGetValue( cmd, out owner );
         if ( owner != this )
            throw new UnauthorizedAccessException( $"api_remove '{cmd}' by not owner" );
         lock ( ApiExtension ) {
            ApiExtension.Remove( cmd );
            ApiExtOwner.Remove( cmd );
         }
         Info( "Unregistered api action {0}", cmd );
         return true;
      } catch ( Exception ex ) {
         Warn( ex.Message );
         return false;
      } }

      private MethodInfo InfoApi ( object param ) {
         if ( param == null ) return null;
         string name = param?.ToString().Trim().ToLowerInvariant();
         if ( NativeApi.TryGetValue( name, out MethodInfo info ) ) return info;
         if ( ApiExtension.TryGetValue( name, out Func<string,object,object> func ) ) return func?.Method;
         return null;
      }

      private IEnumerable<string> ListApi ( object param ) {
         InitiateNativeApi();
         string[] list;
         lock ( ApiExtension ) list = ApiExtension.Keys.ToArray();
         return FilterStringList( NativeApi.Keys.Concat( list ), param );
      }
      #endregion

      #region Logger
      internal LoggerProxy Logger; // Created when and only when an initialiser accepts a logging function

      private Logger CreateLogger () {
         lock ( this ) {
            if ( Logger != null ) return Logger;
            Logger = new LoggerProxy( ModLoader.Log ){ Level = LogLevel ?? ModLoader.Settings.LogLevel };
         }
         Logger.Filters.Add( LogFilters.IgnoreDuplicateExceptions() );
         Logger.Filters.Add( LogFilters.AddPrefix( Metadata.Id + ModLoader.LOG_DIVIDER ) );
         return Logger;
      }

      private Delegate GetLogger ( object param ) {
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

      private ModMeta GetModInfo ( object param ) {
         ModEntry mod = GetMod( param );
         if ( mod == null ) return Metadata;
         return new ModMeta().ImportFrom( mod.Metadata );
      }

      private bool DoLog ( string level, object param ) {
         if ( LowerAndIsEmpty( level, out level ) ) level = param is Exception ? "e" : "i";
         var lv = TraceEventType.Information;
         switch ( level[0] ) {
            case 'c' : lv = TraceEventType.Critical; break;
            case 'e' : lv = TraceEventType.Error; break;
            case 't' : lv = TraceEventType.Transfer; break;
            case 'v' : lv = TraceEventType.Verbose; break;
            case 'w' : lv = TraceEventType.Warning; break;
            case 'f':
               if ( "flush".Equals( level ) ) {
                  lock ( this ) if ( Logger == null ) return true;
                  Logger.Verbo( "Flushing log.{0}{1}", param == null ? "" : " Reason: ", param );
                  Logger.Flush();
                  return true;
               }
               break;
         }
         CreateLogger().Log( lv, param );
         return true;
      }

      private StackTrace Stacktrace ( string name ) {
         var trace = new StackTrace( 2 );
         Func<string> DumpTrace = () =>  "Stacktrace" + trace.ToString() ;
         DoLog( name, DumpTrace );
         return trace;
      }

      internal void Info  ( object msg, params object[] augs ) => CreateLogger().Info ( msg, augs );
      internal void Warn  ( object msg, params object[] augs ) => CreateLogger().Warn ( msg, augs );
      internal void Error ( object msg ) => CreateLogger().Error( msg );
      #endregion

      #region Config
      private bool ConfigChecked;

      private Type GetConfigType ( Type type ) {
         if ( type != null ) return type;
         var typeName = Metadata.ConfigType;
         if ( ! string.IsNullOrEmpty( typeName ) ) {
            foreach ( var asm in ModAssemblies ) {
               type = asm.GetType( typeName );
               if ( type != null ) return type;
            }
         }
         return typeof( JObject );
      }

      private object LoadConfig ( string profile, object param ) { try {
         if ( profile?.Length > 0 && ! LowerAndIsEmpty( profile, out profile ) ) {
            switch ( profile ) {
               case "save" : case "write" :  return SaveConfig( param );
               case "delete" :  return DeleteConfig();
            }
         }
         var isDefault = "default".Equals( profile );
         var confFile = isDefault ? null : CheckConfigFile();
         var type = param as Type;
         if ( type?.FullName.Equals( Metadata.ConfigType ) == true && confFile == null )
            return Activator.CreateInstance( type ); // Skip text conversion when using ConfigType and not reading from config file

         string txt = isDefault ? GetDefaultConfigText() : GetConfigText( confFile );
         if ( param == null || type != null ) { // No param or param is a Type, need to create new instance.
            if ( type == typeof( string ) ) return txt;
            if ( txt == null ) return Activator.CreateInstance( GetConfigType( type ) );
            if ( param == null && ( txt.IndexOf( '{' ) < 0 || txt.IndexOf( '}' ) < 0 ) ) return txt;
            param = ModMetaJson.Parse( txt, GetConfigType( type ) );
         } else {
            if ( txt == null || isDefault ) return param;
            if ( param is string ) return txt;
            JsonConvert.PopulateObject( txt, param, ModMetaJson.JsonOptions );
         }
         if ( Metadata.ConfigType == null ) RunCheckConfig( param?.GetType() );
         return param;
      } catch ( Exception e ) { Error( e ); return null; } }

      private void RunCheckConfig ( Type confType ) {
         if ( confType == null ) return;
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

      private Task SaveConfig ( object param ) {
         if ( param == null ) return null;
         var syn = new object();
         lock ( syn ) return Task.Run( () => { lock ( syn ) {
            if ( ! ( param is string str ) )
               str = ModMetaJson.Stringify( param );
            WriteConfigText( str );
         } } );
      }

      private bool DeleteConfig () {
         var confFile = CheckConfigFile();
         if ( confFile == null ) return true;
         File.Delete( confFile );
         lock ( Metadata ) {
            Metadata.ConfigText = null;
            if ( Metadata.ConfigType != null ) Metadata.DefaultConfig = null;
         }
         return ! File.Exists( confFile );
      }

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