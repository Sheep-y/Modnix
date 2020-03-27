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
      public int SettingVersion = 20200319;
      public SourceLevels LogLevel = SourceLevels.Information;
      // For mod manager
      public bool CheckUpdate = true;
      public DateTime? LastCheckUpdate = null;
      public string UpdateChannel = "release";
      public string GamePath = null;
      // For mod loader, set by manager
      public Dictionary< string, ModSettings > Mods;
   }

   public class ModSettings {
      public bool Disabled;
      public SourceLevels LogLevel = SourceLevels.Information;
      public long? Priority;
   }

   public class ModEntry : ModSettings {
      public readonly string Path;
      public readonly ModMeta Metadata;

      public ModEntry ( ModMeta meta ) : this( null, meta ) { }
      public ModEntry ( string path, ModMeta meta ) {
         Path = path;
         Metadata = meta ?? throw new ArgumentNullException( nameof( meta ) );
      }

      public object ModAPI ( string action, object param = null ) { try {
         switch ( action ) {
            case "assembly"    : return GetAssembly( param );
            case "config"      : return LoadConfig( param );
            case "config_save" : return SaveConfig( param );
            case "mod_info"    : return new ModMeta().ImportFrom( GetMod( param )?.Metadata );
            case "mod_list"    : return ListMods( param );
            case "path"        : return GetPath( param );
            case "log"         : CreateLogger().Log( param ); return true;
            case "logger"      : return GetLogFunc( param );
            case "version"     : return GetVersion( param );
         }
         CreateLogger().Warn( "Unknown api action {0}", action );
         return null;
      } catch ( Exception ex ) { ModLoader.Log.Error( ex ); return null; } }

      private Assembly GetAssembly ( object target ) {
         var id = target?.ToString();
         if ( string.IsNullOrWhiteSpace( id ) ) return ModAssembly;
         switch ( id ) {
            case "loader" : case "modnix" :
               return Assembly.GetExecutingAssembly();
            case "phoenixpoint" : case "phoenix point" : case "game" :
               return null; // TODO: implement
         }
         return ModScanner.GetModById( id )?.ModAssembly;
      }

      private Version GetVersion ( object target ) {
         var id = target?.ToString();
         if ( string.IsNullOrWhiteSpace( id ) ) lock ( Metadata ) return Metadata.Version;
         return ModScanner.GetVersionById( id );
      }

      private ModEntry GetMod ( object target ) {
         var id = target?.ToString();
         if ( string.IsNullOrWhiteSpace( id ) ) return this;
         return ModScanner.GetModById( id );
      }

      private string GetPath ( object target ) {
         var id = target?.ToString();
         if ( string.IsNullOrWhiteSpace( id ) ) return Path;
         switch ( id ) {
            case "mods_root" : return ModLoader.ModDirectory;
            case "phoenixpoint" : case "phoenix point" : case "game" :
               return null; // TODO: implement
         }
         return ModScanner.GetModById( id )?.Path;
      }

      private static IEnumerable<string> ListMods ( object target ) {
         var list = ModScanner.EnabledMods.Select( e => { lock ( e.Metadata ) return e.Metadata.Id; } );
         if ( target == null ) return list;
         if ( target is string txt ) return list.Where( e => e.IndexOf( txt, StringComparison.OrdinalIgnoreCase ) >= 0 );
         if ( target is Regex reg ) return list.Where( e => reg.IsMatch( e ) );
         return null;
      }

      private object LoadConfig ( object param ) { try {
         if ( param == null ) param = typeof( JObject );
         string txt = GetConfigText();
         if ( param is Type type ) {
            if ( type == typeof( string ) )
               return txt;
            return JsonConvert.DeserializeObject( txt, type, ModMetaJson.JsonOptions );
         }
         JsonConvert.PopulateObject( txt, param, ModMetaJson.JsonOptions );
         return param;
      } catch ( Exception e ) { Error( e ); return null; } }

      private Task SaveConfig ( object param ) { try {
         if ( param == null ) return null;
         return Task.Run( () => {
            if ( ! ( param is string str ) )
               str = JsonConvert.SerializeObject( param, Formatting.Indented, ModMetaJson.JsonOptions );
            File.WriteAllText( GetConfigFile(), str, Encoding.UTF8 );
            lock ( Metadata ) Metadata.ConfigText = str;
         } );
      } catch ( Exception e ) { Error( e ); return null; } }

      private Logger CreateLogger () {
         lock ( this ) {
            if ( Logger != null ) return Logger;
            Logger = new LoggerProxy( ModLoader.Log ){ Level = LogLevel };
         }
         var filters = Logger.Filters;
         filters.Add( LogFilters.IgnoreDuplicateExceptions );
         filters.Add( LogFilters.AutoMultiParam );
         filters.Add( LogFilters.AddPrefix( Metadata.Id + "┊" ) );
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

      internal void Error ( object err ) => CreateLogger().Error( err );

      internal DateTime? LastModified => Path == null ? (DateTime?) null : new FileInfo( Path ).LastWriteTime;
      internal LoggerProxy Logger; // Created when and only when an initialiser accepts a logging function
      internal Assembly ModAssembly;
      internal string Key { get { lock ( Metadata ) { return ModScanner.NormaliseModId( Metadata.Id ); } } }

      public ModEntry Parent;
      public List<ModEntry> Children;
      public List<LogEntry> Notices;

      public bool HasConfig { get { lock ( Metadata ) {
         return Metadata.DefaultConfig != null || Metadata.ConfigText != null || CheckConfigFile() != null;
      } } }

      
      public string GetConfigFile () { try {
         if ( Path == null ) return null;
         var name = System.IO.Path.GetFileNameWithoutExtension( Path );
         /*
         if ( name.Equals( "mod_info", StringComparison.OrdinalIgnoreCase ) )
            name = "mod_init";
         */
         return System.IO.Path.Combine( System.IO.Path.GetDirectoryName( Path ), name + ".conf" );
      } catch ( Exception ex ) { CreateLogger().Error( ex ); return null; } }

      public string CheckConfigFile () { try {
         var confFile = GetConfigFile();
         /*
         if ( confFile == null || ! File.Exists( confFile ) )
            confFile = Path.Combine( Path.GetDirectoryName( path ), "mod_init.conf" );
         */
         return File.Exists( confFile ) ? confFile : null;
      } catch ( Exception ex ) { CreateLogger().Error( ex ); return null; } }

      public string GetDefaultConfigText () { try {
         var meta = Metadata;
         lock ( meta ) {
            if ( meta.DefaultConfig == null ) return null;
            return meta.ConfigText = ModMetaJson.Stringify( meta.DefaultConfig );
         }
      } catch ( Exception ex ) { CreateLogger().Error( ex ); return null; } }

      public string GetConfigText () { try {
         var meta = Metadata;
         lock ( meta )
            if ( meta.ConfigText != null )
               return meta.ConfigText;
         var confFile = CheckConfigFile();
         if ( confFile != null )
            return File.ReadAllText( confFile, Encoding.UTF8 );
         return GetDefaultConfigText();
      } catch ( Exception ex ) { CreateLogger().Error( ex ); return null; } }

      public void WriteConfigText ( string str ) { try {
         if ( string.IsNullOrWhiteSpace( str ) ) return;
         var path = GetConfigFile();
         CreateLogger().Info( $"Writing {str.Length} chars to {path}" );
         File.WriteAllText( path, str, Encoding.UTF8 );
      } catch ( Exception ex ) { CreateLogger().Error( ex ); } }

      public long GetPriority () { lock ( Metadata ) { return Priority ?? Metadata.Priority; } }

      internal void AddNotice ( TraceEventType lv, string reason, params object[] augs ) { lock ( Metadata ) {
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
      public long      Priority;

      public string[]  Mods;
      public DllMeta[] Dlls;

      public   object  DefaultConfig;
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
            CopyNonNull( overrider.Priority, ref Priority );
            CopyNonNull( overrider.Mods, ref Mods );
            CopyNonNull( overrider.Dlls, ref Dlls );
            CopyNonNull( overrider.DefaultConfig, ref DefaultConfig );
            CopyNonNull( overrider.ConfigText, ref ConfigText );
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