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
      internal static readonly string[] PHASES = new string[]{ "SplashMod", "Init", "Initialize", "MainMod" };

      internal static Logger Log;
      public const char LOG_DIVIDER = '┊';

      public const string CONF_FILE = "Modnix.conf";
      private static LoaderSettings _Settings;
      public static LoaderSettings Settings { get { lock( MOD_PATH ) { return _Settings; } } set { lock( MOD_PATH ) { _Settings = value; } } }

      public static Version LoaderVersion, GameVersion;
      internal readonly static Version PPML_COMPAT = new Version( 0, 3 );
      private static bool PpmlInitialised;

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
         } else if ( RunMainPhaseOnInit ) // Subsequence runs
            MainPhase();
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
         LoadMods( "Initialize" ); // PPML v0.2+
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
            if ( ! Directory.Exists( ModDirectory ) )
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
         var name = dll.Name;
         Log.Trace( "Resolving {0}", name );
         var app = domain as AppDomain ?? AppDomain.CurrentDomain;
         if ( name.StartsWith( "PhoenixPointModLoader,", StringComparison.OrdinalIgnoreCase ) )
            return app.Load( GetResourceBytes( "PPML_0_3.dll" ) );
         if ( name.StartsWith( "SimpleInjector,", StringComparison.OrdinalIgnoreCase ) )
            return app.Load( GetResourceBytes( "SimpleInjector.dll" ) );
         if ( name.StartsWith( "System." ) && dll.Name.Contains( ',' ) ) { // Generic system library lookup
            var file = dll.Name.Substring( 0, dll.Name.IndexOf( ',' ) ) + ".dll";
            var target = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.Windows ), "Microsoft.NET/Framework/v4.0.30319", file );
            if ( File.Exists( target ) ) {
               Log.Info( "Loading {0}", target );
               return Assembly.LoadFrom( target );
            }
         }
         return null;
      } catch ( Exception ex ) { Log?.Error( ex ); return null; } }

      private static void LoadPpmlPlus () {
         var asm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault( e => e.FullName.StartsWith( "PhoenixPointModLoader," ) );
         if ( asm == null ) return;
         lock ( asm ) {
            if ( PpmlInitialised ) return;
            PpmlInitialised = true;
         }
         Log.Info( "Initialising PPML+." );
         var init = asm.GetType( "PhoenixPointModLoader.PhoenixPointModLoader" )?.GetMethod( "Initialize", Static | Public );
         if ( init == null ) Log.Warn( "Cannot find PhoenixPointModLoader.Initialize, PPML+ may not be initialized properly." );
         try {
            init.Invoke( null, new object[]{} );
         } catch ( Exception ex ) { Log.Error( ex ); }
      }

      private static Stream GetResourceStream ( string path ) {
         path = ".Resources." + path;
         var me = Assembly.GetExecutingAssembly();
         var fullname = Array.Find( me.GetManifestResourceNames(), e => e.EndsWith( path, StringComparison.Ordinal ) );
         return me.GetManifestResourceStream( fullname );
      }

      private static byte[] GetResourceBytes ( string path ) {
         var mem = new MemoryStream();
         using ( var stream = GetResourceStream( path ) ) {
            stream.CopyTo( mem );
            Log.Verbo( "Mapped {0} to memory, {1:n0} bytes.", path, mem.Length );
         }
         return mem.ToArray();
      }

      private static void LoadSettings () {
         var confFile = Path.Combine( ModDirectory, CONF_FILE );
         if ( File.Exists( confFile ) ) try {
            Log.Info( $"Loading {confFile}" );
            Settings = ModMetaJson.Parse<LoaderSettings>( File.ReadAllText( confFile ) );
         } catch ( Exception ex ) { Log.Error( ex ); }
         if ( Settings == null ) {
            Log.Info( $"Using default settings, because cannot find or parse {confFile}" );
            Settings = new LoaderSettings();
         }
         SetLogLevel( Settings.LogLevel );
      }

      public static void SaveSettings () {
         var confFile = Path.Combine( ModDirectory, CONF_FILE );
         var json = ModMetaJson.Stringify( Settings );
         if ( string.IsNullOrWhiteSpace( json ) ) return;
         if ( ! Directory.Exists( ModDirectory ) )
            Directory.CreateDirectory( ModDirectory );
         File.WriteAllText( confFile, json, Encoding.UTF8 );
      }

      public static void SetLog ( Logger logger, bool clear = false ) { lock ( MOD_PATH ) {
         if ( Log != null ) throw new InvalidOperationException();
         Log = logger ?? throw new NullReferenceException( nameof( logger ) );
         logger.Filters.Clear();
         Log.Filters.Add( LogFilters.IgnoreDuplicateExceptions( "(same stacktrace as before)" ) );
         Log.Filters.Add( LogFilters.AutoMultiParam );
         Log.Filters.Add( LogFilters.FormatParams );
         Log.Filters.Add( LogFilters.ResolveLazy );
         if ( clear ) Log.Clear();
         LoaderVersion = Assembly.GetExecutingAssembly().GetName().Version;
         Log.Info( "{0}/{1}; {2}", typeof( ModLoader ).FullName, LoaderVersion, DateTime.Now.ToString( "u" ) );
         ModMetaJson.JsonLogger.Masters.Clear();
         ModMetaJson.JsonLogger.Masters.Add( Log );
      } }

      public static void SetLogLevel ( SourceLevels level ) { lock ( MOD_PATH ) {
         if ( Log == null ) throw new InvalidOperationException( "Log not set" );
         Log.Level = level;
      } }

      public static void LogGameVersion () { try { lock ( MOD_PATH ) {
         var game = GetGameAssembly();
         if ( game == null ) return;
         var ver = game.GetType( "Base.Build.RuntimeBuildInfo" ).GetProperty( "Version" ).GetValue( null )?.ToString();
         Log.Info( "{0}/{1}", Path.GetFileNameWithoutExtension( game.CodeBase ), ver );
         GameVersion = Version.Parse( ver );
      } } catch ( Exception ex ) { Log?.Error( ex ); } }
      #endregion

      #region Loading Mods
      public static void LoadMods ( string phase ) { try {
         Log.Info( "PHASE {0}", phase );
         foreach ( var mod in ModScanner.EnabledMods ) {
            if ( mod.Metadata.Dlls == null ) continue;
            foreach ( var dll in mod.Metadata.Dlls ) {
               if ( dll.Methods == null ) continue;
               if ( ! dll.Methods.TryGetValue( phase, out var entries ) ) continue;
               var lib = LoadDll( mod, dll.Path );
               if ( lib == null ) continue;
               if ( mod.ModAssemblies == null )
                  mod.ModAssemblies = new List<Assembly>();
               if ( ! mod.ModAssemblies.Contains( lib ) )
                  mod.ModAssemblies.Add( lib );
               foreach ( var type in entries )
                  CallInit( mod, lib, type, phase );
            }
         }
         Log.Flush();
      } catch ( Exception ex ) { Log.Error( ex ); } }

      public static Assembly LoadDll ( ModEntry mod, string path ) { try {
         Log.Info( "Loading {0}", path );
         return Assembly.LoadFrom( path );
      } catch ( Exception ex ) { mod.Error( ex ); return null; } }

      private readonly static Dictionary<Type,WeakReference<object>> ModInstances = new Dictionary<Type,WeakReference<object>>();

      public static void CallInit ( ModEntry mod, Assembly dll, string typeName, string methodName ) { try {
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

         if ( "Initialize".Equals( methodName ) ) LoadPpmlPlus();

         var augs = new List<object>();
         foreach ( var aug in func.GetParameters() )
            augs.Add( ParamValue( aug, mod ) );
         Func<string> augTxt = () => string.Join( ", ", augs.Select( e => e?.GetType()?.Name ?? "null" ) );
         Log.Info( "Calling {1}.{2}({3}) in {0}", mod.Path, typeName, methodName, augTxt );
         object target = null;
         if ( ! func.IsStatic ) lock ( ModInstances ) {
            if ( ! ModInstances.TryGetValue( type, out WeakReference<object> wref ) || ! wref.TryGetTarget( out target ) )
               ModInstances[ type ] = new WeakReference<object>( target = Activator.CreateInstance( type ) );
         }
         func.Invoke( target, augs.ToArray() );
         Log.Verbo( "Done calling {0}", mod.Path );
      } catch ( Exception ex ) { mod.Error( ex ); } }

      private static object ParamValue ( ParameterInfo aug, ModEntry mod ) {
         var pType = aug.ParameterType;
         var pName = aug.Name;
         var isLog =  pName.IndexOf( "log", StringComparison.OrdinalIgnoreCase ) >= 0;
         // API
         if ( pType == typeof( Func<string,object,object> ) )
            return (Func<string,object,object>) mod.ModAPI;
         // Legacy logger and config
         if ( pType == typeof( Action<SourceLevels,object,object[]> ) && isLog ) {
            Log.Warn( "Mod {0} uses a legacy log parameter, which will be removed in next major Modnix relese.", mod.Metadata.Id );
            return mod.ModAPI( "logger", typeof( SourceLevels ) );
         }
         if ( IsSetting( pName ) && ( pType == typeof( string ) || pType == typeof( JObject ) || IsSetting( pType.Name ) ) ) {
            Log.Warn( "Mod {0} uses a legacy config parameter, which will be removed in next major Modnix relese.", mod.Metadata.Id );
            return mod.ModAPI( "config", pType );
         }
         return DefaultParamValue( aug );
      }

      private static bool IsSetting ( string name ) =>
         name.IndexOf( "setting", StringComparison.OrdinalIgnoreCase ) >= 0 ||
         name.IndexOf( "conf"   , StringComparison.OrdinalIgnoreCase ) >= 0;

      private static object DefaultParamValue ( ParameterInfo aug ) {
         if ( aug.HasDefaultValue )
            return aug.RawDefaultValue;
         var pType = aug.ParameterType;
         if ( pType.IsValueType )
            return Activator.CreateInstance( pType );
         return null;
      }
      #endregion
   }

   internal static class Tools {
      internal static string FixSlash ( this string path ) => path.Replace( '/', Path.DirectorySeparatorChar );
   }
}