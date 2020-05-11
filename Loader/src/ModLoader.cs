using Harmony;
using Sheepy.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix {

   public static class ModLoader {
      private readonly static string MOD_PATH  = "My Games/Phoenix Point/Mods".FixSlash();

      internal static Logger Log;
      public const char LOG_DIVIDER = '┊';

      public const string CONF_FILE = "Modnix.conf";
      private static LoaderSettings _Settings;
      public static LoaderSettings Settings { get { lock( MOD_PATH ) { return _Settings; } } set { lock( MOD_PATH ) { _Settings = value; } } }

      public static Version LoaderVersion, GameVersion;
      internal readonly static Version PPML_COMPAT = new Version( 0, 2 );

      public static string ModDirectory { get; private set; }
      public static string LoaderPath => Assembly.GetExecutingAssembly().Location;
      public static string DnFrameworkDir => Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.Windows ), "Microsoft.NET/Framework/v4.0.30319" );

      private static bool RunMainPhaseOnInit;
      private static object harmony; // Type is not HarmonyInstance to avoid hard crash when harmony is missing
      private static Assembly GameAssembly;

      public static void Init () { try {
         if ( Log == null ) { // First run
            RunMainPhaseOnInit = true;
            Setup();
            ModScanner.BuildModList();
            ModPhases.LoadMods( "SplashMod" );
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
         var patcher = harmony as HarmonyInstance;
         if ( patcher == null) harmony = patcher = HarmonyInstance.Create( typeof( ModLoader ).Namespace );
         patcher.Patch(
            GetGameAssembly().GetType( "PhoenixPoint.Common.Game.PhoenixGame" ).GetMethod( "MenuCrt", NonPublic | Instance ),
            postfix: new HarmonyMethod( typeof( ModLoader ).GetMethod( nameof( MainPhase ), NonPublic | Static ) )
         );
         RunMainPhaseOnInit = false; // Disable fallback
      } catch ( Exception ex ) { Log.Error( ex ); } }

      private static void UnpatchMenuCrt () { try {
         (harmony as HarmonyInstance)?.UnpatchAll( typeof( ModLoader ).Namespace );
         harmony = null;
      } catch ( Exception ex ) { Log.Error( ex ); } }

      private static void MainPhase () {
         RunMainPhaseOnInit = false;
         ModPhases.LoadMods( "Init" ); // PPML v0.1
         ModPhases.LoadMods( "Initialize" ); // PPML v0.2
         ModPhases.LoadMods( "MainMod" ); // Modnix
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
         AppDomain.CurrentDomain.UnhandledException += ModLoaderException;
         ModDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), MOD_PATH );
         if ( Log == null ) {
            if ( ! Directory.Exists( ModDirectory ) )
               Directory.CreateDirectory( ModDirectory );
            SetLog( new FileLogger( Path.Combine( ModDirectory, Assembly.GetExecutingAssembly().GetName().Name + ".log" ) ) { TimeFormat = "HH:mm:ss.ffff " }, true );
            LogGameVersion();
         }
         var corlib = new Uri( typeof( string ).Assembly.CodeBase ).LocalPath;
         LoadSettings();
         Log.Verbo( ".Net/{0}; mscorlib/{1} {2}", Environment.Version, FileVersionInfo.GetVersionInfo( corlib ).FileVersion, corlib );
      } } catch ( Exception ex ) { Log?.Error( ex ); } }

      internal static volatile Assembly PpmlAssembly;

      // Dynamically load embedded dll
      private static Assembly ModLoaderResolve  ( object domain, ResolveEventArgs dll ) { try {
         var name = dll.Name;
         var app = domain as AppDomain ?? AppDomain.CurrentDomain;
         if ( name.StartsWith( "PhoenixPointModLoader,", StringComparison.OrdinalIgnoreCase ) ) {
            Log.Info( "Loading embedded PPML" );
            return PpmlAssembly = app.Load( GetResourceBytes( "PPML_0_2.dll" ) );
         }
         if ( name.StartsWith( "System." ) && dll.Name.Contains( ',' ) ) { // Generic system library lookup
            var file = dll.Name.Substring( 0, dll.Name.IndexOf( ',' ) ) + ".dll";
            var target = Path.Combine( DnFrameworkDir, file );
            if ( File.Exists( target ) ) {
               Log.Info( "Loading {0}", target );
               return Assembly.LoadFrom( target );
            }
         } else {
            Log.Warn( "Cannot resolve {0}", name );
            Log.Flush(); // The app may crash right after the failure, so flush the log now
         }
         return null;
      } catch ( Exception ex ) { Log?.Error( ex ); return null; } }

      private static void ModLoaderException ( object sender, UnhandledExceptionEventArgs e ) {
         Log.Log( SourceLevels.Critical, e );
         Log.Flush();
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
            Settings = Json.Parse<LoaderSettings>( Tools.ReadText( confFile ) );
         } catch ( Exception ex ) { Log.Error( ex ); }
         if ( Settings == null ) {
            Log.Info( $"Using default settings, because cannot find or parse {confFile}" );
            Settings = new LoaderSettings();
         }
         SetLogLevel( Settings.LogLevel );
      }

      public static void SaveSettings () {
         var confFile = Path.Combine( ModDirectory, CONF_FILE );
         var json = Json.Stringify( Settings );
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
         Json.JsonLogger.Masters.Clear();
         Json.JsonLogger.Masters.Add( Log );
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
   }
}