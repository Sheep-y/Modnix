using Mono.Cecil;
using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Sheepy.Modnix {

   public static class ModLoader {
      public readonly static List<ModEntry> AllMods = new List<ModEntry>();
      public readonly static List<ModEntry> EnabledMods = new List<ModEntry>();
      public readonly static Dictionary<string,List<ModEntry>> ModsInPhase = new Dictionary<string, List<ModEntry>>();

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
      public static string DnFrameworkDir => Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.Windows ), "Microsoft.NET/Framework/v4.0.30319".FixSlash() );

      public static void Main () {
         AppDomain.CurrentDomain.AssemblyLoad += ModLoaderAsmLoaded;
      }

      private static bool IsGameLoaded => GamePatcher.GameAssembly != null;
      private static bool IsRuntimeLoaded => GamePatcher.FindAssembly( "System.Runtime" ) != null;

      private static void ModLoaderAsmLoaded ( object sender, AssemblyLoadEventArgs args ) {
         var asmName = args.LoadedAssembly.FullName;
         if ( asmName.StartsWith( "Assembly-CSharp,", StringComparison.OrdinalIgnoreCase ) ) {
            if ( ! IsRuntimeLoaded ) Init();
         } else if ( asmName.StartsWith( "System.Runtime,", StringComparison.OrdinalIgnoreCase ) ) {
            if ( IsGameLoaded ) LogGameVersion();
            else Init();
         }
         if ( IsGameLoaded && IsRuntimeLoaded )
            AppDomain.CurrentDomain.AssemblyLoad -= ModLoaderAsmLoaded;
      }

      public static void Init () { try {
         if ( Log != null ) return;
         Setup();
         ModScanner.BuildModList();
         ModPhases.RunPhase( "SplashMod" );
         if ( ! GamePatcher.PatchPhases() )
            Log.Log( SourceLevels.Critical, "Cannot patch game with Harmony. Non-SplashMods may not be loaded." );
      } catch ( Exception ex ) {
         if ( Log == null )
            Console.WriteLine( ex );
         else
            Log.Error( ex );
      } }

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
            SetLog( new FileLogger( Path.Combine( ModDirectory, Assembly.GetExecutingAssembly().GetName().Name + ".log" ) ) { TimeFormat = "HH:mm:ss.fff " }, true );
            LogGameVersion();
         }
         var corlib = new Uri( typeof( string ).Assembly.CodeBase ).LocalPath;
         LoadSettings();
         var os = new OperatingSystem( Environment.OSVersion.Platform, Environment.OSVersion.Version );
         Log.Verbo( "{0}/{1}; .Net/{2}; mscorlib/{3} {4}", os.Platform, os.Version, Environment.Version, FileVersionInfo.GetVersionInfo( corlib ).FileVersion, corlib );
      } } catch ( Exception ex ) { Log?.Error( ex ); } }

      internal static volatile Assembly PpmlAssembly;

      // Dynamically load embedded dll
      private static Assembly ModLoaderResolve  ( object domain, ResolveEventArgs dll ) { try {
         var name = dll.Name;
         if ( name.StartsWith( "Microsoft.VisualStudio.", StringComparison.OrdinalIgnoreCase ) ) return null;
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

      #region Game Version
      public static void LogGameVersion () { try { lock ( MOD_PATH ) {
         var game = GamePatcher.GameAssembly;
         if ( game == null ) return;
         string ver = null;
         if ( ! IsRuntimeLoaded ) {
            ver = ParseVersionWithCecil( game.Location );
         } else {
            var type = game.GetType( "Base.Build.RuntimeBuildInfo" );
            ver = ( type?.GetProperty( "BuildVersion" ).GetValue( null ) ?? type?.GetProperty( "Version" )?.GetValue( null ) ).ToString();
         }
         if ( ver == null ) return;
         Log.Info( "{0}/{1}", Path.GetFileNameWithoutExtension( game.CodeBase ), ver );
         GameVersion = Version.Parse( ver );
      } } catch ( Exception ex ) { Log?.Error( ex ); } }

      internal static string ParseVersionWithCecil ( string gameDllPath ) {
         using ( var dll = ModuleDefinition.ReadModule( gameDllPath ) ) {
            foreach ( var type in dll.Types ) {
               if ( type.FullName == "Base.Build.RuntimeBuildInfo" ) {
                  var method = type.Methods.FirstOrDefault( e => e.Name == "get_BuildVersion" );
                  if ( method?.HasBody == true ) return FindGameVersion2( method );
                  method = type.Methods.FirstOrDefault( e => e.Name == "get_Version" );
                  if ( method?.HasBody == true ) return FindGameVersion1( method );
                  return null;
               }
            }
         }
         return null;
      }

      // 1.0.58929 (2020-06-17) and below
      private static string FindGameVersion1 ( MethodDefinition method ) { try {
         var version = new int[2];
         var ldcCount = 0;
         foreach ( var code in method.Body.Instructions ) {
            var op = code.OpCode.ToString();
            if ( ! op.StartsWith( "ldc.i4" ) ) continue;
            if ( ldcCount >= 2 ) { Log.Warn( "GameVer1: opcode {0} >= 2", ldcCount ); return null; }

            var ver = 0;
            if ( code.Operand is int num ) ver = num;
            else if ( code.Operand is sbyte num2 ) ver = num2;
            else if ( code.OpCode.Code.Equals( Mono.Cecil.Cil.Code.Ldc_I4_M1 ) ) ver = -1;
            else ver = int.Parse( op.Substring( 7 ) );

            version[ ldcCount ] = ver;
            ++ldcCount;
         }
         if ( ldcCount < 2 ) { Log.Warn( "GameVer1: opcode {0} < 2.", ldcCount ); return null; }
         return version[0].ToString() + '.' + version[1];
      } catch ( Exception ex ) { Log.Error( ex ); return null; } }

      // Danforth 1.5.2 (2020-07-07) and up
      private static string FindGameVersion2 ( MethodDefinition method ) { try {
         var version = new int[3];
         var ldcCount = 0;
         foreach ( var code in method.Body.Instructions ) {
            var op = code.OpCode.ToString();
            if ( ! op.StartsWith( "ldc.i4" ) ) continue;
            if ( ldcCount > 7 ) { Log.Warn( "GameVer1: opcode {0} > 7", ldcCount ); return null; }

            var ver = 0;
            if ( code.Operand is int num ) ver = num;
            else if ( code.Operand is sbyte num2 ) ver = num2;
            else if ( code.OpCode.Code.Equals( Mono.Cecil.Cil.Code.Ldc_I4_M1 ) ) ver = -1;
            else ver = int.Parse( op.Substring( 7 ) );
            switch ( ldcCount ) {
               case 2 : version[ 0 ] = ver; break;
               case 4 : version[ 1 ] = ver; break;
               case 6 : version[ 2 ] = ver; break;
            }
            ++ldcCount;
         }
         if ( ldcCount <= 7 ) { Log.Warn( "GameVer1: opcode {0} <= 7", ldcCount ); return null; }
         return version[0].ToString() + '.' + version[1] + '.' + version[2];
      } catch ( Exception ex ) { Log.Error( ex ); return null; } }
      #endregion

      #region Mod Query
      private static ModEntry _GetModById ( string key ) => ModLoader.EnabledMods.Find( e => e.Key == key && ! e.Disabled );

      internal static ModEntry GetModById ( string id ) => _GetModById( ModScanner.NormaliseModId( id ) );

      internal static bool GetVersionById ( string id, out ModEntry mod, out Version version ) {
         mod = null;
         version = null;
         if ( string.IsNullOrEmpty( id ) ) return false;
         id = ModScanner.NormaliseModId( id );
         switch ( id ) {
            case "modnix" : case "loader" : case "":
               version = ModLoader.LoaderVersion;
               return true;
            case "phoenixpoint" : case "phoenix point" : case "game" :
               version = ModLoader.GameVersion;
               return true;
            case "ppml" : case "ppml+" : case "phoenixpointmodloader" : case "phoenix point mod loader" :
               version = ModLoader.PPML_COMPAT;
               return true;
            case "non-modnix" : case "nonmodnix" :
               return false;
            default:
               mod = _GetModById( id );
               version = GetVersionFromMod( mod );
               return mod != null;
         }
      }

      private static Version GetVersionFromMod ( ModEntry mod ) {
         if ( mod == null ) return null;
         return mod.Metadata.Version ?? new Version( 0, 0, 0, 0 );
      }
      #endregion
   }
}