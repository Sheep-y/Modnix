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
using System.Threading;
using System.Threading.Tasks;

namespace Sheepy.Modnix {
   using API_Func = Func< string, object, object >;

   public class LoaderSettings {
      public int SettingVersion = 20200428;
      public SourceLevels LogLevel = SourceLevels.Information;
      // For mod manager
      public bool LogMonitor = false;
      public bool CheckUpdate = true;
      public DateTime? LastCheckUpdate = null;
      public string UpdateChannel = "dev";
      public string GamePath = null;
      public double ModInfoWeight = -1;
      public double ModListWeight = -1;
      public bool MinifyLoaderPanel = false;
      public bool MinifyGamePanel = true;
      public bool MaximiseWindow = false;
      public double WindowLeft = -1;
      public double WindowTop = -1;
      public double WindowWidth = -1;
      public double WindowHeight = -1;
      public string OfflineParameter = "";
      public string SteamCommand = "steam://rungameid/839770";
      public string EgsCommand = "com.epicgames.launcher://apps/Iris?action=launch";
      public string EgsParameter = "";
      // For mod loader, set by manager
      public Dictionary< string, ModSettings > Mods;
   }

   public class ModSettings {
      public bool Disabled;
      public SourceLevels? LogLevel;
      public long? LoadIndex;
      public bool GetIsDefaultSettings () => ! Disabled && ! LogLevel.HasValue && ! LoadIndex.HasValue;
   }

   public class ModEntry : ModSettings {
      public readonly string Path;
      public readonly ModMeta Metadata;

      public ModEntry ( ModMeta meta ) : this( null, meta ) { }
      public ModEntry ( string path, ModMeta meta ) {
         Path = path;
         Metadata = meta ?? throw new ArgumentNullException( nameof( meta ) );
      }

      public bool IsModPack { get { lock ( Metadata ) return Metadata.Mods != null; } }
      public string Key { get { lock ( Metadata ) return ModScanner.NormaliseModId( Metadata.Id ); } }
      public long Index { get { lock ( Metadata ) return LoadIndex ?? Metadata.LoadIndex; } }

      public string Dir => System.IO.Path.GetDirectoryName( Path );
      internal DateTime? LastModified => Path == null ? (DateTime?) null : new FileInfo( Path ).LastWriteTime;
      internal List< Assembly > ModAssemblies; // Use List insead of HashSet to preserve order.

      private bool _IsUnloaded;
      public bool IsUnloaded { get { lock ( this ) return _IsUnloaded; } private set => _IsUnloaded = value; }

      #region API Framework
      private static readonly Dictionary<string,MethodInfo> NativeApi = new Dictionary<string, MethodInfo>();
      private static readonly Dictionary<string,API_Func> ApiExtension = new Dictionary<string, API_Func>();
      private static readonly Dictionary<string,KeyValuePair<ModEntry,MethodInfo>> ApiExtOwner = new Dictionary<string, KeyValuePair<ModEntry,MethodInfo>>();

      private static void AddNativeApi ( string command, string method ) {
         NativeApi.Add( command, typeof( ModEntry ).GetMethod( method, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static ) );
      }

      internal static void InitiateNativeApi () { lock ( NativeApi ) {
         if ( NativeApi.Count > 0 ) return;
         AddNativeApi( "api_add"   , nameof( AddApi ) );
         AddNativeApi( "api_info"  , nameof( InfoApi ) );
         AddNativeApi( "api_list"  , nameof( ListApi ) );
         AddNativeApi( "api_remove", nameof( RemoveApi ) );
         AddNativeApi( "api_stack" , nameof( ApiStack ) );
         AddNativeApi( "assembly"  , nameof( GetAssembly ) );
         AddNativeApi( "assemblies", nameof( GetAssemblies ) );
         AddNativeApi( "config"    , nameof( LoadConfig ) );
         AddNativeApi( "dir"       , nameof( GetDir ) );
         AddNativeApi( "log"       , nameof( DoLog ) );
         AddNativeApi( "logger"    , nameof( GetLogger ) );
         AddNativeApi( "mod_info"  , nameof( GetModInfo ) );
         AddNativeApi( "mod_list"  , nameof( ListMods ) );
         AddNativeApi( "mod_load"  , nameof( LoadMod ) );
         AddNativeApi( "mod_unload", nameof( UnloadMod ) );
         AddNativeApi( "path"      , nameof( GetPath ) );
         AddNativeApi( "stacktrace", nameof( Stacktrace ) );
         AddNativeApi( "version"   , nameof( GetVersion ) );
      } }

      public object ModAPI ( string action, object param = null ) {
         var logError = true;
         try {
            IsMultiPart( action, out string cmd, out string spec, out logError );
            if ( ! LowerAndIsEmpty( cmd, out cmd ) ) {
               if ( cmd.IndexOf( '.' ) < 0 ) {
                  switch ( cmd ) {
                     case "api_add"    : return AddApi( spec, param );
                     case "api_info"   : return InfoApi( param );
                     case "api_list"   : return ListApi( param );
                     case "api_remove" : return RemoveApi( spec );
                     case "api_stack"  : return ApiStack( spec, param );
                     case "assembly"   : return GetAssembly( param );
                     case "assemblies" : return GetAssemblies( param );
                     case "config"     : return LoadConfig( spec, param );
                     case "dir"        : return GetDir( param );
                     case "log"        : return DoLog( spec, param );
                     case "logger"     : return GetLogger( param );
                     case "mod_info"   : return GetModInfo( param );
                     case "mod_list"   : return ListMods( param );
                     case "mod_load"   : return LoadMod( param );
                     case "mod_unload" : return UnloadMod( param );
                     case "path"       : return GetPath( param );
                     case "stacktrace" : return Stacktrace( spec );
                     case "version"    : return GetVersion( param );
                  }
               } else {
                  API_Func handler;
                  var stackPushed = false;
                  lock ( ApiExtension ) ApiExtension.TryGetValue( cmd, out handler );
                  if ( handler != null ) try {
                     stackPushed = ApiStackPush( action, cmd, spec, param );
                     var result = handler( spec, param );
                     if ( logError && result is Exception err ) Warn( err );
                     return result;
                  } finally {
                     if ( stackPushed ) ApiStackPop();
                  }
               }
            }
            if ( logError ) Warn( "Unknown api action '{0}'", cmd );
            return null;
         } catch ( Exception ex ) {
            if ( logError ) Warn( ex );
            return ex;
         }
      }

      private static bool IsMultiPart ( string text, out string firstWord, out string rest, out bool logError ) {
         logError = true;
         if ( text.Length > 1 && ( text[0] == '@' || text[0] == '\v' ) ) { // Throw NRE on null, intended
            logError = false;
            text = text.Substring( 1 );
         }
         return IsMultiPart( text.Trim(), out firstWord, out rest );
      }

      private static bool IsMultiPart ( string text, out string firstWord, out string rest ) {
         var pos = -1;
         if ( text != null )
            pos = text.IndexOf( ' ' );
         if ( pos < 0 ) {
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
      #endregion

      #region Game and Mods
      private static Assembly GameAssembly;

      private Assembly GetAssembly ( object target ) => GetAssemblies( target )?.FirstOrDefault();

      private IEnumerable < Assembly > GetAssemblies ( object target ) {
         if ( LowerAndIsEmpty( target, out string id ) ) return ModAssemblies ?? Enumerable.Empty<Assembly>();
         switch ( id ) {
            case "modnix" :
               return new Assembly[]{ Assembly.GetExecutingAssembly() };

            case "loader" :
               var ppml = ModLoader.PpmlAssembly;
               var loaderList = new Assembly[ ppml == null ? 1 : 2 ];
               loaderList[0] = Assembly.GetExecutingAssembly();
               if ( ppml != null ) loaderList[1] = ppml;
               return loaderList;

            case "phoenixpointmodloader" : case "phoenix point mod loader" : case "ppml" :
               return new Assembly[]{ ModLoader.PpmlAssembly };

            case "phoenixpoint" : case "phoenix point" : case "game" :
               if ( GameAssembly == null ) // No need to lock. No conflict.
                  GameAssembly = Array.Find( AppDomain.CurrentDomain.GetAssemblies(), e => e.FullName.StartsWith( "Assembly-CSharp," ) );
               return new Assembly[]{ GameAssembly };

            default:
               var mod = ModLoader.GetModById( id );
               if ( mod == null ) return null;
               return mod.ModAssemblies ?? Enumerable.Empty<Assembly>();
         }
      }

      private Version GetVersion ( object target ) {
         if ( LowerAndIsEmpty( target, out string id ) ) lock ( Metadata ) return Metadata.Version;
         ModLoader.GetVersionById( id, out _, out Version ver );
         return ver;
      }

      private ModEntry GetMod ( object target ) {
         if ( LowerAndIsEmpty( target, out string id ) ) return this;
         return ModLoader.GetModById( id );
      }

      private string GetPath ( object target ) {
         if ( LowerAndIsEmpty( target, out string id ) ) return Path;
         switch ( id ) {
            case "mods_root" : return ModLoader.ModDirectory;
            case "loader" : case "modnix" :
               return ModLoader.LoaderPath;
            case "phoenixpoint" : case "phoenix point" : case "game" :
               return Process.GetCurrentProcess().MainModule?.FileName;
            default :
               return ModLoader.GetModById( id )?.Path;
         }
      }

      private string GetDir ( object target ) {
         var path = GetPath( target );
         if ( path == null || object.ReferenceEquals( path, ModLoader.ModDirectory ) ) return path;
         return System.IO.Path.GetDirectoryName( path );
      }

      private static IEnumerable<string> ListMods ( object target ) =>
         FilterStringList( ModLoader.EnabledMods.Where( e => ! e.IsUnloaded ).Select( e => e.Metadata.Id ), target );

      private static IEnumerable<string> FilterStringList ( IEnumerable<string> list, object param ) {
         if ( param == null ) return list;
         if ( param is string txt ) return list.Where( e => e.IndexOf( txt, StringComparison.OrdinalIgnoreCase ) >= 0 );
         if ( param is Regex reg ) return list.Where( e => reg.IsMatch( e ) );
         return null;
      }

      private bool? LoadMod ( object param ) {
         if ( param == null || ! ( param is string ) ) throw new ArgumentException( "mod_load requires mod id as parameter" );
         var mod = ModLoader.GetModById( param?.ToString() );
         if ( mod == null ) return null;
         lock ( mod ) {
            if ( ! mod.IsUnloaded ) return false;
            Info( "Loading mod {0}", mod.Metadata.Id );
            mod.IsUnloaded = false;
            ModPhases.RunPastPhaseOnMod( mod );
         }
         return true;
      }

      private bool? UnloadMod ( object param ) {
         if ( param == null || ! ( param is string ) ) throw new ArgumentException( "mod_unload requires mod id as parameter" );
         var mod = ModLoader.GetModById( param?.ToString() );
         List< ModEntry > mods = null;
         lock( ModLoader.ModsInPhase ) ModLoader.ModsInPhase.TryGetValue( "unloadmod", out mods );
         if ( mod == null || mods?.Contains( mod ) != true ) return null;
         lock ( mod ) {
            if ( mod.IsUnloaded ) return false;
            Info( "Unloading mod '{0}'", mod.Metadata.Id );
            ModPhases.RunPhaseOnMod( mod, "UnloadMod" );
            mod.IsUnloaded = true;
         }
         lock ( ApiExtension ) {
            foreach ( var entry in ApiExtOwner.ToArray() )
               if ( entry.Value.Key == mod ) {
                  RemoveApiCmd( entry.Key );
                  Log().Verbo( "Unloaded API '{0}'", entry.Key ); // TODO: Multi level logging
               }
         }
         return true;
      }
      #endregion

      #region API Stack
      private static Dictionary<Thread,Stack<object[]>> ApiCalls = new Dictionary<Thread, Stack<object[]>>();

      private bool ApiStackPush ( string action, string command, string spec, object param ) {
         var thread = Thread.CurrentThread;
         Stack<object[]> stack;
         lock ( ApiCalls ) {
            if ( ! ApiCalls.TryGetValue( thread, out stack ) )
               ApiCalls.Add( thread, stack = new Stack<object[]>() );
         }
         stack.Push( new object[]{ Metadata.Id, action, command, spec, param } );
         return true;
      }

      private static void ApiStackPop () {
         var thread = Thread.CurrentThread;
         Stack<object[]> stack;
         lock ( ApiCalls ) stack = ApiCalls[ thread ];
         stack.Pop();
         if ( stack.Count == 0 )
            lock ( ApiCalls )
               ApiCalls.Remove( thread );
      }

      private System.Collections.IEnumerable ApiStack ( string type, object param ) {
         LowerAndIsEmpty( type, out type );
         Stack<object[]> stack;
         lock ( ApiCalls ) ApiCalls.TryGetValue( param as Thread ?? Thread.CurrentThread, out stack );
         if ( stack != null )
            switch ( type ) {
               case ""    :
               case null  : return stack.ToArray();
               case "mod" : return stack.Select( e => e[0].ToString() ).ToArray();
               case "action"  : return stack.Select( e => e[1].ToString() ).ToArray();
               case "command" : return stack.Select( e => e[2].ToString() ).ToArray();
               case "spec"  : return stack.Select( e => e[3].ToString() ).ToArray();
               case "param" : return stack.Select( e => e[4] ).ToArray();
            }
         return null;
      }
      #endregion

      #region API Extension
      private bool AddApi ( string name, object param ) {
         if ( IsMultiPart( name, out name, out string type ) )
            throw new ArgumentException( $"Unknown specifier '{type}'." );
         if ( LowerAndIsEmpty( name, out string cmd ) || ! cmd.Contains( "." ) || cmd.Length < 3 || cmd[0] == '@' )
            throw new ArgumentException( $"Invalid name for api_add, need a dot and at least 3 chars, must not starts with @. Got '{cmd}'." );
         var func = param is Delegate dele ? dele as API_Func ?? WrapExtension( dele ) : null;
         if ( func == null )
            throw new ArgumentException( "api_add parameter must be compatible with Func<object, object> or Func<string,object, object>. Got " + param.ToString() );
         lock ( ApiExtension ) {
            if ( ApiExtension.ContainsKey( cmd ) )
               throw new InvalidOperationException( $"Cannot re-register api 'cmd'." );
            ApiExtension.Add( cmd, func );
            ApiExtOwner.Add( cmd, new KeyValuePair<ModEntry, MethodInfo>( this, ( param as Delegate ).Method ) );
         }
         Info( "Registered API '{0}'", cmd );
         return true;
      }

      private static API_Func WrapExtension ( Delegate func ) {
         var info = func.GetMethodInfo();
         var name = info.Name;
         if ( ! info.IsStatic ) throw new ArgumentException( "Delegate " + name + " must be static." );
         if ( info.IsAbstract ) throw new ArgumentException( "Delegate " + name + " must not be abstract." );
         var augs = info.GetParameters();
         foreach ( var aug in augs )
            if ( aug.IsOut || aug.IsIn || aug.ParameterType.IsByRef )
               throw new ArgumentException( "Delegate " + name + " contains in, out, or ref parameter." );

         var hasReturn = info.ReturnType != typeof( void );
         var returnIsBool = info.ReturnType == typeof( bool );
         var returnIsVal = hasReturn && info.ReturnType.IsValueType;

         if ( augs.Length == 0 ) {
            if ( hasReturn ) {
               if ( returnIsBool ) {
                  var d = CreateDelegate<Func<bool>>( info ); return ( _, __ ) => d();
               } else if ( ! returnIsVal ) {
                  var d = CreateDelegate<Func<object>>( info ); return ( _, __ ) => d();
               } else
                  return ( _, __ ) => func.DynamicInvoke( null );
            } else {
               var d = CreateDelegate<Action>( info ); return ( _, __ ) => { d(); return true; };
            }

         } else if ( augs.Length == 1 ) {
            if ( hasReturn ) {
               if ( returnIsBool ) {
                  var d = CreateDelegate<Func<object,bool>>( info ); return ( _, b ) => d( b );
               } else if ( ! returnIsVal ) {
                  var d = CreateDelegate<Func<object,object>>( info ); return ( _, b ) => d( b );
               } else if ( augs[0].ParameterType != typeof( object ) ) {
                  throw new ArgumentException( "Delegate " + name + " is not taking an object param." );
               } else
                  return ( _, b ) => func.DynamicInvoke( new object[] { b } );
            } else {
               var d = CreateDelegate<Action<object>>( info );
               return ( _, b ) => { d( b ); return true; };
            }

         } else if ( augs.Length == 2 ) {
            if ( hasReturn ) {
               if ( returnIsBool ) {
                  var d = CreateDelegate<Func<string,object,bool>>( info ); return ( a, b ) => d( a, b );
               } else if ( ! returnIsVal ) {
                  return CreateDelegate<Func<string,object,object>>( info );
               } else if ( augs[0].ParameterType != typeof( object ) && augs[0].ParameterType != typeof( string ) ) {
                  throw new ArgumentException( "Delegate " + name + " is not taking a string or an object as first param." );
               } else if ( augs[1].ParameterType != typeof( object ) ) {
                  throw new ArgumentException( "Delegate " + name + " is not taking an object as second param." );
               } else
                  return ( a, b ) => func.DynamicInvoke( new object[] { a, b } );
            } else {
               var d = CreateDelegate<Action<string,object>>( info );
               return ( a, b ) => { d( a, b ); return true; };
            }
         }
         throw new ArgumentException( "Delegate " + name + " has too many parameters." );
      }

      private static T CreateDelegate<T> ( MethodInfo info ) where T : class => Delegate.CreateDelegate( typeof( T ), info ) as T;

      private bool RemoveApi ( object param ) {
         if ( LowerAndIsEmpty( param, out string cmd ) )
            throw new ArgumentNullException( nameof( param ) );
         if ( IsMultiPart( cmd, out cmd, out string type ) )
            throw new ArgumentException( $"Unknown specifier '{type}'." );
         KeyValuePair<ModEntry, MethodInfo> info;
         lock ( ApiExtension ) ApiExtOwner.TryGetValue( cmd, out info );
         if ( info.Key != this )
            throw new UnauthorizedAccessException( $"Non-owner cannot api_remove '{cmd}'. Owner is '{info.Key?.Metadata?.Id ?? "null"}'." );
         RemoveApiCmd( cmd );
         Info( "Removed API '{0}'", cmd );
         return true;
      }

      private void RemoveApiCmd ( string cmd ) { lock ( ApiExtension ) {
         ApiExtension.Remove( cmd );
         ApiExtOwner.Remove( cmd );
      } }

      private static MethodInfo InfoApi ( object param ) {
         if ( param == null ) return null;
         var name = param?.ToString().Trim().ToLowerInvariant();
         if ( NativeApi.TryGetValue( name, out MethodInfo info ) ) return info;
         if ( ApiExtOwner.TryGetValue( name, out KeyValuePair<ModEntry, MethodInfo> owner ) ) return owner.Value;
         return null;
      }

      private static IEnumerable<string> ListApi ( object param ) {
         InitiateNativeApi();
         string[] list;
         lock ( ApiExtension ) list = ApiExtension.Keys.ToArray();
         return FilterStringList( NativeApi.Keys.Concat( list ), param );
      }
      #endregion

      #region Logger
      private LoggerProxy Logger; // Created when and only when an initialiser accepts a logging function

      internal LogFilter PrefixFilter => LogFilters.AddPrefix( Metadata.Id + ModLoader.LOG_DIVIDER );

      public Logger Log () {
         lock ( this ) {
            if ( Logger != null ) return Logger;
            Logger = new LoggerProxy( ModLoader.Log ){ Level = LogLevel ?? ModLoader.Settings.LogLevel };
         }
         Logger.Filters.Add( LogFilters.IgnoreDuplicateExceptions() );
         Logger.Filters.Add( PrefixFilter );
         return Logger;
      }

      private Delegate GetLogger ( object param ) {
         string txt = null;
         if ( param is Type t ) txt = t.Name;
         else if ( param is string s ) txt = s;
         else return null;
         switch ( txt ) {
            case "TraceEventType" : return (Action<TraceEventType,object,object[]>) Log().Log;
            case "SourceLevels"   : return (Action<SourceLevels,object,object[]>) Log().Log;
            case "TraceLevel"     : return (Action<TraceLevel,object,object[]>) Log().Log;
         }
         return null;
      }

      private ModMeta GetModInfo ( object param ) {
         var mod = GetMod( param );
         if ( mod == null ) return null;
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
                  Logger.Verbo( "Flushing log.{0}{1}", param == null ? "" : " Reason: ", param ?? "" );
                  Logger.Flush();
                  return true;
               }
               break;
         }
         Log().Log( lv, param );
         return true;
      }

      private StackTrace Stacktrace ( string name ) {
         var trace = new StackTrace( 2 );
         Func<string> DumpTrace = () => "Stacktrace" + trace;
         DoLog( name, DumpTrace );
         return trace;
      }

      internal void Info  ( object msg, params object[] augs ) => Log().Info ( msg, augs );
      internal void Warn  ( object msg, params object[] augs ) => Log().Warn ( msg, augs );
      internal void Error ( object msg ) => Log().Error( msg );
      #endregion

      #region Config
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
            return Activator.CreateInstance( type ); // Skip text conversion when not reading from config file

         var txt = isDefault ? GetDefaultConfigText() : GetConfigText( confFile );
         if ( param == null || type != null ) { // No param or param is a Type, need to create new instance.
            if ( type == typeof( string ) ) return txt;
            if ( txt == null ) return Activator.CreateInstance( GetConfigType( type ) );
            if ( param == null && ( txt.IndexOf( '{' ) < 0 || txt.IndexOf( '}' ) < 0 ) ) return txt;
            param = Json.Parse( txt, GetConfigType( type ) );
         } else {
            if ( txt == null || isDefault ) return param;
            if ( param is string ) return txt;
            JsonConvert.PopulateObject( txt, param, Json.JsonOptions );
         }
         return param;
      } catch ( Exception e ) { Error( e ); return null; } }

      private Task SaveConfig ( object param ) {
         if ( param == null ) return null;
         var syn = new object();
         lock ( syn ) return Task.Run( () => { lock ( syn ) {
            if ( ! ( param is string str ) )
               str = Json.Stringify( param );
            WriteConfigText( str );
         } } );
      }

      public bool DeleteConfig () {
         var confFile = CheckConfigFile();
         if ( confFile == null ) return true;
         File.Delete( confFile );
         lock ( Metadata ) Metadata.ConfigText = null;
         return ! File.Exists( confFile );
      }

      public bool HasConfig { get { lock ( Metadata ) {
         return ! IsModPack && ( Metadata.ConfigType != null || Metadata.ConfigText != null || CheckConfigFile() != null );
      } } }

      public string GetConfigFile () { try {
         return Path == null ? null : System.IO.Path.Combine( Dir, System.IO.Path.GetFileNameWithoutExtension( Path ) + ".conf" );
      } catch ( Exception ex ) { Error( ex ); return null; } }

      public string CheckConfigFile () { try {
         var confFile = GetConfigFile();
         return File.Exists( confFile ) ? confFile : null;
      } catch ( Exception ex ) { Error( ex ); return null; } }

      public string GetDefaultConfigText () { try {
         var meta = Metadata;
         lock ( meta ) {
            object config = null;
            if ( meta.DefaultConfigText != null ) return meta.DefaultConfigText;
            if ( meta.ConfigType != null && ModAssemblies != null ) try {
               foreach ( var asm in ModAssemblies ) {
                  var type = asm.GetType( meta.ConfigType );
                  if ( type == null ) continue;
                  config = Activator.CreateInstance( type );
                  break;
               }
            } catch ( Exception ex ) { Error( ex ); }
            if ( config == null ) return null;
            return meta.DefaultConfigText = Json.Stringify( config );
         }
      } catch ( Exception ex ) { Error( ex ); return null; } }

      public string GetConfigText ( string confFile = null ) { try {
         var meta = Metadata;
         lock ( meta )
            if ( meta.ConfigText != null )
               return meta.ConfigText;
         if ( confFile == null ) confFile = CheckConfigFile();
         return meta.ConfigText = confFile != null ? Tools.ReadText( confFile ) : GetDefaultConfigText();
      } catch ( Exception ex ) { Error( ex ); return null; } }

      public string CacheDefaultConfigText ( string config ) {
         if ( config == null ) return null;
         lock ( Metadata ) return Metadata.DefaultConfigText = config;
      }

      public void WriteConfigText ( string str ) { try {
         if ( string.IsNullOrWhiteSpace( str ) ) return;
         var path = GetConfigFile();
         Log().Info( $"Writing {str.Length} chars to {path}" );
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
         if ( Metadata.Version != null ) txt += " " + Json.TrimVersion( Metadata.Version );
         if ( Disabled ) txt += " (Disabled)";
         return txt;
      } }
   }

   public class ModMeta {
      public string    Id;
      public Version   Version;
      public string[]  Flags;

      public TextSet   Name;
      public string[]  Lang;
      public string    Duration;
      public TextSet   Description;
      public TextSet   Author;
      public TextSet   Url;
      public TextSet   Contact;
      public TextSet   Copyright;

      public AppVer[]  Avoids;
      public AppVer[]  Requires;
      public AppVer[]  Disables;
      public long      LoadIndex;

      public string[]  Mods;
      public DllMeta[] Dlls;
      public Dictionary<string,object>[] Actions;

      public   string  ConfigType;
      internal string  DefaultConfigText;
      internal string  ConfigText;

      internal bool HasContent => Mods != null || Dlls != null || Actions != null;

      internal ModMeta ImportFrom ( ModMeta overrider ) {
         lock ( this ) if ( overrider == null ) return this;
         lock ( overrider ) {
            CopyNonNull( overrider.Id, ref Id );
            CopyNonNull( overrider.Version, ref Version );
            CopyNonNull( overrider.Flags, ref Flags );
            CopyNonNull( overrider.Name, ref Name );
            CopyNonNull( overrider.Lang, ref Lang );
            CopyNonNull( overrider.Duration, ref Duration );
            CopyNonNull( overrider.Description, ref Description );
            CopyNonNull( overrider.Author, ref Author );
            CopyNonNull( overrider.Url, ref Url );
            CopyNonNull( overrider.Contact, ref Contact );
            CopyNonNull( overrider.Copyright, ref Copyright );
            CopyNonNull( overrider.Avoids, ref Avoids );
            CopyNonNull( overrider.Requires, ref Requires );
            CopyNonNull( overrider.Disables, ref Disables );
            CopyNonNull( overrider.LoadIndex, ref LoadIndex );
            CopyNonNull( overrider.Mods, ref Mods );
            CopyNonNull( overrider.Dlls, ref Dlls );
            CopyNonNull( overrider.Actions, ref Actions );
            CopyNonNull( overrider.ConfigType, ref ConfigType );
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
         NormStringArray( ref Flags );
         if ( Flags != null ) Flags = Flags.Select( e => e.ToLowerInvariant() ).ToArray();
         NormStringArray( ref Lang );
         Duration = NormString( Duration );
         NormTextSet( ref Description );
         NormTextSet( ref Author );
         NormTextSet( ref Url );
         NormTextSet( ref Contact );
         NormTextSet( ref Copyright );
         NormAppVer( ref Avoids );
         NormAppVer( ref Requires );
         NormAppVer( ref Disables );
         NormStringArray( ref Mods );
         NormDllMeta( ref Dlls );
         NormDictArray( ref Actions );
         ConfigType = NormString( ConfigType );
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

      private static void NormDllMeta ( ref DllMeta[] val ) {
         if ( val == null ) return;
         for ( int i = val.Length - 1 ; i >= 0 ; i-- ) {
            val[i].Path = NormString( val[i].Path );
            if ( val[i].Path == null ) val[i] = null;
         }
         if ( val.Any( e => e == null || e.Path == null ) )
            val = val.Where( e => e?.Path != null ).ToArray();
         if ( val.Length == 0 ) val = null;
      }

      internal static void NormDictArray ( ref Dictionary<string,object>[] val ) {
         if ( val == null ) return;
         for ( int i = val.Length - 1 ; i >= 0 ; i-- ) {
            var dict = val[i];
            if ( dict == null ) continue;
            foreach ( var pair in dict.ToArray() ) {
               var key = NormString( pair.Key )?.ToLowerInvariant();
               if ( pair.Key == key ) continue;
               dict.Remove( pair.Key );
               if ( key != null ) dict[ key ] = pair.Value;
            }
            if ( dict.Count == 0 ) val[i] = null;
         }
         if ( val.Any( e => e == null ) )
            val = val.Where( e => e != null ).ToArray();
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
      public string Name;
      public Version Min;
      public Version Max;
      public string Url;

      public AppVer () {}

      public AppVer ( string id, Version min = null, Version max = null ) {
         Id = id;
         Min = min;
         Max = max;
      }
   }

   public class DllMeta {
      public string Path;
      public Dictionary< string, HashSet< string > > Methods;
   }
}