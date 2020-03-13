using Harmony;
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
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix {

   public static class ModLoader {
      private readonly static string MOD_PATH  = "My Games/Phoenix Point/Mods".FixSlash();
      public const string CONF_FILE = "Modnix.conf";
      internal static readonly string[] PHASES = new string[]{ "SplashMod", "Init", "Initialize", "MainMod" };

      internal static Logger Log;
      private static LoaderSettings _Settings;
      public static LoaderSettings Settings { get { lock( MOD_PATH ) { return _Settings; } } set { lock( MOD_PATH ) { _Settings = value; } } }

      public static Version LoaderVersion, GameVersion;
      internal readonly static Version PPML_COMPAT = new Version( 0, 1 );

      public static string ModDirectory { get; private set; }

      #region Initialisation
      private static bool RunMainPhaseOnInit;
      private static object harmony; // Type is not HarmonyInstance to avoid hard crash when harmony is missing 
      private static Assembly GameAssembly;

      public static void Init () { try {
         if ( Log == null ) { // First run
            RunMainPhaseOnInit = true;
            Setup();
            ModScanner.BuildModList();
            LoadMods( "SplashMod" );
            PatchMenuCrt();
         } if ( RunMainPhaseOnInit ) { // Subsequence runs
            MainPhase();
            return;
         }
      } catch ( Exception ex ) {
         if ( Log == null )
            Console.WriteLine( ex );
         else
            Log.Error( ex );
      } }

      private static void PatchMenuCrt () { try {
         var patcher = HarmonyInstance.Create( typeof( ModLoader ).Namespace );
         patcher.Patch(
            GetGameAssembly().GetType( "PhoenixPoint.Common.Game.PhoenixGame" ).GetMethod( "MenuCrt", NonPublic | Instance ),
            postfix: new HarmonyMethod( typeof( ModLoader ).GetMethod( nameof( MainPhase ), NonPublic | Static ) )
         );
         harmony = patcher;
         RunMainPhaseOnInit = false; // Disable fallback
      } catch ( Exception ex ) { Log.Error( ex ); } }

      private static void UnpatchMenuCrt () { try {
         (harmony as HarmonyInstance)?.UnpatchAll( typeof( ModLoader ).Namespace );
         harmony = null;
      } catch ( Exception ex ) { Log.Error( ex ); } }

      private static void MainPhase () {
         RunMainPhaseOnInit = false;
         LoadMods( "Init" ); // PPML v0.1
         LoadMods( "Initialize" ); // PPML v0.2
         LoadMods( "MainMod" ); // Modnix
         if ( harmony != null ) UnpatchMenuCrt();
      }

      private static Assembly GetGameAssembly () {
         if ( GameAssembly != null ) return GameAssembly;
         foreach ( var e in AppDomain.CurrentDomain.GetAssemblies() )
            if ( e.FullName.StartsWith( "Assembly-CSharp, ", StringComparison.OrdinalIgnoreCase ) )
               return GameAssembly = e;
         return null;
      }

      public static bool NeedSetup { get { lock( MOD_PATH ) {
         return ModDirectory == null;
      } } }

      public static void Setup () { try { lock( MOD_PATH ) {
         if ( ModDirectory != null ) return;
         AppDomain.CurrentDomain.AssemblyResolve += ModLoaderResolve;
         ModDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), MOD_PATH );
         if ( Log == null ) {
            if ( !Directory.Exists( ModDirectory ) )
               Directory.CreateDirectory( ModDirectory );
            SetLog( new FileLogger( Path.Combine( ModDirectory, Assembly.GetExecutingAssembly().GetName().Name + ".log" ) ) { TimeFormat = "HH:mm:ss.ffff " }, true );
            LogGameVersion();
         }
         var corlib = new Uri( typeof( string ).Assembly.CodeBase ).LocalPath;
         Log.Verbo( ".Net/{0}; mscorlib/{1} {2}", Environment.Version, FileVersionInfo.GetVersionInfo( corlib ).FileVersion, corlib );
         LoadSettings();
      } } catch ( Exception ex ) { Log?.Error( ex ); } }

      // Dynamically load embedded dll
      private static Assembly ModLoaderResolve  ( object domain, ResolveEventArgs dll ) { try {
         Log.Trace( "Resolving {0}", dll.Name );
         AppDomain app = domain as AppDomain ?? AppDomain.CurrentDomain;
         if ( dll.Name.StartsWith( "PhoenixPointModLoader, Version=0.2.0.0, ", StringComparison.OrdinalIgnoreCase ) ) {
            Log.Verbo( "Loading embedded PPML v0.2" );
            return app.Load( Properties.Resources.PPML_0_2 );
         }
         if ( dll.Name.StartsWith( "System." ) && dll.Name.Contains( ',' ) ) { // Generic system library lookup
            string file = dll.Name.Substring( 0, dll.Name.IndexOf( ',' ) ) + ".dll";
            string target = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.Windows ), "Microsoft.NET/Framework/v4.0.30319", file );
            if ( File.Exists( target ) ) {
               Log.Info( "Loading {0}", target );
               return Assembly.LoadFrom( target );
            }
         }
         return null;
      } catch ( Exception ex ) { Log?.Error( ex ); return null; } }

      private static void LoadSettings () {
         var confFile = Path.Combine( ModDirectory, CONF_FILE );
         if ( File.Exists( confFile ) ) try {
            Log.Info( $"Loading {confFile}" );
            Settings = JsonConvert.DeserializeObject<LoaderSettings>( confFile );
         } catch ( Exception ex ) { Log.Error( ex ); }
         if ( Settings == null ) {
            Log.Info( $"Using default settings, because cannot find or parse {confFile}" );
            Settings = new LoaderSettings();
         }
      }

      public static void SaveSettings () {
         var confFile = Path.Combine( ModDirectory, CONF_FILE );
         if ( Settings.SettingVersion == null )
            Settings.SettingVersion = LoaderVersion.ToString();
         var json = JsonConvert.SerializeObject( Settings );
         if ( string.IsNullOrWhiteSpace( json ) ) return;
         if ( ! Directory.Exists( ModDirectory ) )
            Directory.CreateDirectory( ModDirectory );
         File.WriteAllText( confFile, json, Encoding.UTF8 );
      }

      public static void SetLog ( Logger logger, bool clear = false ) { lock (  MOD_PATH ) {
         if ( Log != null ) throw new InvalidOperationException();
         Log = logger ?? throw new NullReferenceException( nameof( logger ) );
         logger.Filters.Clear();
         logger.Filters.Add( LogFilters.FormatParams );
         logger.Filters.Add( LogFilters.ResolveLazy );
         //logger.Level = SourceLevels.All;
         if ( clear ) Log.Clear();
         LoaderVersion = Assembly.GetExecutingAssembly().GetName().Version;
         Log.Info( "{0}/{1}; {2}", typeof( ModLoader ).FullName, LoaderVersion, DateTime.Now.ToString( "u" ) );
         ModMetaJson.JsonLogger.Masters.Clear();
         ModMetaJson.JsonLogger.Masters.Add( Log );
      } }

      public static void LogGameVersion () { try { lock ( MOD_PATH ) {
         var game = GetGameAssembly();
         if ( game == null ) return;
         var ver = game.GetType( "Base.Build.RuntimeBuildInfo" ).GetProperty( "Version" ).GetValue( null )?.ToString();
         Log.Info( "{0}/{1}", Path.GetFileNameWithoutExtension( game.CodeBase ), ver );
         GameVersion = Version.Parse( ver );
      } } catch ( Exception ex ) { Log?.Error( ex ); } }
      #endregion

      #region Mod settings
      public static string GetSettingFile ( string path ) {
         if ( path == null ) return null;
         var name = Path.GetFileNameWithoutExtension( path );
         if ( name.Equals( "mod_info", StringComparison.OrdinalIgnoreCase ) )
            name = "mod_init";
         return Path.Combine( Path.GetDirectoryName( path ), name + ".conf" );
      }

      public static string CheckSettingFile ( string path ) {
         var confFile = GetSettingFile( path );
         if ( confFile == null || ! File.Exists( confFile ) )
            confFile = Path.Combine( Path.GetDirectoryName( path ), "mod_init.conf" );
         return File.Exists( confFile ) ? confFile : null;
      }

      public static string ReadSettingText ( ModEntry mod ) { try {
         var confFile = CheckSettingFile( mod.Path );
         if ( confFile != null )
            return File.ReadAllText( confFile, Encoding.UTF8 );
         var meta = mod.Metadata;
         lock ( meta ) {
            if ( meta.EmbeddedSettings != null )
               return meta.EmbeddedSettings;
            if ( meta.DefaultSettings != null )
               return meta.EmbeddedSettings = ModMetaJson.Stringify( meta.DefaultSettings );
         }
         return null;
      } catch ( Exception ex ) { Log?.Error( ex ); return null; } }
      #endregion

      #region Loading Mods
      public static void LoadMods ( string phase ) { try {
         Log.Info( "PHASE {0}", phase );
         foreach ( var mod in ModScanner.EnabledMods ) {
            if ( mod.Metadata.Dlls == null ) continue;
            foreach ( var dll in mod.Metadata.Dlls ) {
               if ( dll.Methods == null ) continue;
               if ( ! dll.Methods.TryGetValue( phase, out var entries ) ) continue;
               var lib = LoadDll( dll.Path );
               if ( lib == null ) continue;
               foreach ( var type in entries )
                  CallInit( mod, lib, dll.Path, type, phase );
            }
         }
         Log.Flush();
      } catch ( Exception ex ) { Log.Error( ex ); } }

      public static Assembly LoadDll ( string path ) { try {
         Log.Info( "Loading {0}", path );
         return Assembly.LoadFrom( path );
      } catch ( Exception ex ) { Log.Error( ex ); return null; } }

      private static Action<object> LoggerA ( Logger log ) => ( msg ) =>
         log.Log( msg is Exception ? SourceLevels.Error : SourceLevels.Information, msg, null );
      private static Action<object,object[]> LoggerB ( Logger log ) => ( msg, augs ) =>
         log.Log( msg is Exception ? SourceLevels.Error : SourceLevels.Information, msg, augs );
      private static Action<SourceLevels,object> LoggerC ( Logger log ) => ( lv, msg ) => log.Log( lv, msg, null );
      private static Action<SourceLevels,object,object[]> LoggerD ( Logger log ) => ( lv, msg, augs ) => log.Log( lv, msg, augs );

      public static void CallInit ( ModEntry mod, Assembly dll, string path, string typeName, string methodName ) { try {
         var type = dll.GetType( typeName );
         if ( type == null ) {
            Log.Error( "Cannot find type {1} in {0}", dll.Location, typeName );
            return;
         }

         var func = type.GetMethods( ModScanner.INIT_METHOD_FLAGS )?.FirstOrDefault( e => e.Name.Equals( methodName ) );
         if ( func == null ) {
            Log.Error( "Cannot find {1}.{2} in {0}", dll.Location, typeName, methodName );
            return;
         }
         var augs = new List<object>();
         foreach ( var aug in func.GetParameters() )
            augs.Add( ParamValue( aug,  mod, path, type ) );
         Func<string> augTxt = () => string.Join( ", ", augs.Select( e => e?.GetType()?.Name ?? "null" ) );
         Log.Info( "Calling {1}.{2}({3}) in {0}", mod.Path, typeName, methodName, augTxt );
         object target = null;
         if ( ! func.IsStatic ) {
            if ( mod.Instance == null )
               mod.Instance = Activator.CreateInstance( type );
            target = mod.Instance;
         }
         func.Invoke( target, augs.ToArray() );
      } catch ( Exception ex ) { Log.Error( ex ); } }

      private static object ParamValue ( ParameterInfo aug, ModEntry mod, string path, Type type ) {
         var pType = aug.ParameterType;
         var pName = aug.Name;
         var isLog =  pName.IndexOf( "log", StringComparison.OrdinalIgnoreCase ) >= 0;
         // Paths
         if ( pType == typeof( string ) && pName.Equals( "ModsRoot", StringComparison.OrdinalIgnoreCase ) )
            return ModDirectory;
         if ( pType == typeof( string ) && pName.Equals( "ModPath", StringComparison.OrdinalIgnoreCase ) )
            return mod.Path;
         if ( pType == typeof( string ) && pName.Equals( "AssemblyPath", StringComparison.OrdinalIgnoreCase ) )
            return path;
         // Version checkers
         if ( pType == typeof( Func<string,Version> ) )
            return (Func<string,Version>) ModScanner.GetVersionById;
         if ( pType == typeof( Assembly ) )
            return Assembly.GetExecutingAssembly();
         // Loggers
         if ( pType == typeof( Action<object> ) && isLog )
            return LoggerA( CreateLogger( mod ) );
         if ( pType == typeof( Action<object,object[]> ) && isLog )
            return LoggerB( CreateLogger( mod ) );
         if ( pType == typeof( Action<SourceLevels,object> ) && isLog )
            return LoggerC( CreateLogger( mod ) );
         if ( pType == typeof( Action<SourceLevels,object,object[]> ) && isLog )
            return LoggerD( CreateLogger( mod ) );
         // Mod info
         if ( pType == typeof( Func<string,ModEntry> ) )
            return (Func<string,ModEntry>) ModScanner.GetModById;
         if ( pType == typeof( ModMeta ) )
            return mod.Metadata;
         if ( pType == typeof( ModEntry ) )
            return mod;
         // Settings
         if ( IsSetting( pName ) ) {
            if ( pType == typeof( string ) )
               return ReadSettingText( mod );
            if ( pType == typeof( JObject ) || IsSetting( pType.Name ) ) try {
               return JsonConvert.DeserializeObject( ReadSettingText( mod ), pType, ModMetaJson.JsonOptions );
            } catch ( Exception e ) { Log.Warn( e ); }
         }
         return DefaultParamValue( aug, mod, path, type );
      }

      private static bool IsSetting ( string name ) =>
         name.IndexOf( "setting", StringComparison.OrdinalIgnoreCase ) >= 0 ||
         name.IndexOf( "conf"   , StringComparison.OrdinalIgnoreCase ) >= 0;

      private static object DefaultParamValue ( ParameterInfo aug, ModEntry mod, string path, Type type ) {
         if ( aug.HasDefaultValue )
            return aug.RawDefaultValue;
         var pType = aug.ParameterType;
         if ( pType.IsValueType )
            return Activator.CreateInstance( pType );
         return null;
      }

      private static Logger CreateLogger ( ModEntry mod ) { lock ( mod ) {
         if ( mod.Logger == null ) {
            var logger = mod.Logger = new LoggerProxy( Log );
            var filters = logger.Filters;
            filters.Add( LogFilters.IgnoreDuplicateExceptions );
            filters.Add( LogFilters.AutoMultiParam );
            filters.Add( LogFilters.AddPrefix( mod.Metadata.Id + "┊" ) );
         }
         return mod.Logger;
      } }
      #endregion
   }

   internal static class Tools {
      internal static string FixSlash ( this string path ) => path.Replace( '/', Path.DirectorySeparatorChar );
   }
}