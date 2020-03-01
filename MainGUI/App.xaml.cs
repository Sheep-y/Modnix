using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using static System.Globalization.CultureInfo;

namespace Sheepy.Modnix.MainGUI {

   internal interface IAppGui {
      void SetInfo ( GuiInfo info, object value );
      void Prompt ( string v, Exception ex = null );
      void Log ( object message );
   }

   public enum GuiInfo { NONE, VISIBILITY, MOD_LIST,
      APP_STATE, APP_VER, APP_UPDATE,
      GAME_RUNNING, GAME_PATH, GAME_VER }

   public partial class AppControl : Application {
      public static AppControl Instance { get; private set; }

      // Use slash for all paths, and use .FixSlash() to correct to platform slash.
      internal readonly static string MOD_PATH = "My Games/Phoenix Point/Mods".FixSlash();
      internal readonly static string DLL_PATH = "PhoenixPointWin64_Data/Managed".FixSlash();
      internal const string LIVE_NAME  = "Modnix";
      internal const string APP_EXT  = ".exe";
      internal const string GAME_EXE = "PhoenixPointWin64.exe";
      internal const string GAME_DLL = "Assembly-CSharp.dll";

      internal const string INJECTOR = "ModnixInjector.exe";
      internal const string LOADER   = "ModnixLoader.dll";
      internal const string PAST     = "PhoenixPointModLoaderInjector.exe";
      internal const string PAST_BK  = "PhoenixPointModLoaderInjector.exe.orig";
      internal const string PAST_DL1 = "PPModLoader.dll";
      internal const string PAST_DL2 = "PhoenixPointModLoader.dll";
      internal const string PAST_MOD = "Mods";
      internal const string HARM_DLL = "0Harmony.dll";
      internal const string CECI_DLL = "Mono.Cecil.dll";
      internal const string MOD_LOG  = "ModnixLoader.log";

      internal const string EPIC_DIR = ".egstore";

      private readonly static string[] GAME_PATHS =
         new string[]{
            "C:/Program Files/Epic Games/PhoenixPoint".FixSlash(),
            ".." ,
            "." ,
            "D:/Program Files/Epic Games/PhoenixPoint".FixSlash(),
            "E:/Program Files/Epic Games/PhoenixPoint".FixSlash(),
            };

      internal string ModFolder = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), MOD_PATH );
      internal string ModGuiExe;
      internal string MyPath;

      internal IAppGui GUI;
      private GameInstallation currentGame;
      private readonly object SynRoot = new object();

      #region Startup
      private AssemblyName Myself;

      internal bool paramSkipProcessCheck;
      private int  paramIgnorePid;

      internal void ApplicationStartup ( object sender, StartupEventArgs e ) { try { lock ( SynRoot ) {
         Instance = this;
         Log( $"Startup time {DateTime.Now.ToString( "yyyy-MM-dd HH:mm:ss.ffff", InvariantCulture )}" );
         Init( e?.Args );
         if ( ! paramSkipProcessCheck ) {
            Log( $"Running startup checks" );
            MigrateSettings();
            if ( FoundRunningModnix() ) {
               Shutdown();
               return;
            }
            if ( ! IsSelfInstalled() ) {
               if ( FoundInstalledModnix() ) {
                  GUI = new SetupWindow( "launch" );
               } else if ( ShouldRunSetup() ) {
                  GUI = new SetupWindow( "setup" );
               }
            }
         }
         Log( $"Launching main window" );
         if ( GUI == null )
            GUI = new MainWindow();
         Log( null ); // Send startup log to GUI
         GUI.SetInfo( GuiInfo.VISIBILITY, "true" );
      } } catch ( Exception ex ) {
         try {
            Console.WriteLine( ex );
            File.WriteAllText( LIVE_NAME + " Startup Error.log", startup_log + ex.ToString() );
         } catch ( Exception ) { }
         if ( GUI != null ) {
            GUI.SetInfo( GuiInfo.VISIBILITY, "true" );
            Log( ex );
         } else {
            Shutdown();
         }
      } }

      private void Init ( string[] args ) {
         // Dynamically load embedded dll
         AppDomain.CurrentDomain.AssemblyResolve += ( domain, dll ) => {
            Log( $"Modnix resolving {dll.Name}" );
            AppDomain app = domain as AppDomain ?? AppDomain.CurrentDomain;
            if ( dll.Name.StartsWith( "ModnixLoader,", StringComparison.InvariantCultureIgnoreCase ) )
               return app.Load( MainGUI.Properties.Resources.ModnixLoader );
            if ( dll.Name.StartsWith( "Mono.Cecil,", StringComparison.InvariantCultureIgnoreCase ) )
               return app.Load( MainGUI.Properties.Resources.Mono_Cecil );
            if ( dll.Name.StartsWith( "Newtonsoft.Json,", StringComparison.InvariantCultureIgnoreCase ) )
               return app.Load( MainGUI.Properties.Resources.Newtonsoft_Json );
            return null;
         };

         // Build important paths and self information
         ModGuiExe = Path.Combine( ModFolder, LIVE_NAME + APP_EXT );
         Myself = Assembly.GetExecutingAssembly().GetName();
         MyPath = Uri.UnescapeDataString( new UriBuilder( Myself.CodeBase ).Path ).FixSlash();
         Log( "Assembly: " + MyPath );
         Log( "Working Dir: " + Directory.GetCurrentDirectory() );
         Log( "Mod Dir: " + ModFolder );

         // Parse command line arguments
         if ( args != null )
            ProcessParams( args );
      }

      private void MigrateSettings () {
         // Migrate settings from old version
         try {
            var settings = MainGUI.Properties.Settings.Default;
            if ( ! settings.Settings_Migrated ) {
               Log( $"Migrating settings from {settings.Settings_Version}" );
               settings.Upgrade();
               settings.Settings_Migrated = true;
               settings.Settings_Version = Myself.Version.ToString();
               settings.Save();
            Log( "Settings saved." );
            }
            // v0.6 had no default value for Last_Update_Check which may throw NRE on access
            try {
               if ( settings.Last_Update_Check == null ) throw new NullReferenceException();
            } catch ( NullReferenceException ) {
               Log( "Filling Last_Update_Check default" );
               settings.Last_Update_Check = DateTime.Parse( "2000-01-01T12:00", InvariantCulture );
               settings.Save();
            }
         } catch ( Exception ex ) { Log( ex ); }
      }

      /// Parse command line arguments.
      /// -i --ignore-pid (id)    Ignore given pid in running process check
      /// -s --skip-launch-check  Skip checking running process, modnix installation, and setting migration
      /// -reset --reset          Clear and reset App settings
      private void ProcessParams ( string[] args ) {
         if ( args == null || args.Length <= 0 ) return;
         List<string> param = args.ToList();

         if ( ParamIndex( param, "reset", "reset" ) >= 0 ) {
            Log( "Resetting app settings." );
            var settings = MainGUI.Properties.Settings.Default;
            settings.Reset();
            settings.Settings_Migrated = true;
            settings.Save();
            Log( "Settings saved." );
         }

         int pid = ParamIndex( param, "i", "ignore-pid" );
         if ( pid >= 0 && param.Count > pid+1 )
            _ = int.TryParse( param[ pid + 1 ], out paramIgnorePid );

         /// -o --open-mod-dir        Open mod folder on launch, once used as part of setup
         //if ( ParamIndex( param, "o", "open-mod-dir" ) >= 0 )
         //   Process.Start( "explorer.exe", $"/select, \"{ModGuiExe}\"" );

         paramSkipProcessCheck = ParamIndex( param, "s", "skip-launch-check" ) >= 0;
      }

      private static int ParamIndex ( List<String> args, string quick, string full ) {
         int win1 = args.IndexOf(  "/" + quick );
         int win2 = args.IndexOf(  "/" + full  );
         int lin1 = args.IndexOf(  "-" + quick );
         int lin2 = args.IndexOf( "--" + full  );
         return Math.Max( Math.Max( win1, win2 ), Math.Max( lin1, lin2 ) );
      }

      private bool FoundRunningModnix () { try {
         // Find running instances
         int myId = Process.GetCurrentProcess().Id;
         Process running = Process.GetProcesses()
               .Where( e => e.ProcessName == LIVE_NAME ||
                       e.ProcessName.StartsWith( LIVE_NAME+"Setup", StringComparison.InvariantCultureIgnoreCase ) ||
                       e.ProcessName.StartsWith( LIVE_NAME+"Install", StringComparison.InvariantCultureIgnoreCase ) )
               .Where( e => e.Id != myId && ( paramIgnorePid == 0 || e.Id != paramIgnorePid ) ).FirstOrDefault();
         if ( running == null ) return false;
         // Bring to foreground
         IntPtr handle = running.MainWindowHandle;
         if ( handle == IntPtr.Zero ) return false;
         Log( $"Another instance (pid {running.Id}) found. Self-closing." );
         return NativeMethods.SetForegroundWindow( handle );
      } catch ( Exception ex ) { return Log( ex, false ); } }

      internal void LaunchInstalledModnix () { try {
         Process.Start( ModGuiExe, "/i " + Process.GetCurrentProcess().Id );
      } catch ( Exception ex ) { Log( ex ); } }

      private bool IsSelfInstalled () {
         // Modnix may be launched from My Games or from PhoenixPoint symbolic link, so we're just checking the tail.
         return MyPath.EndsWith( Path.Combine( PAST_MOD, LIVE_NAME + APP_EXT ), StringComparison.InvariantCultureIgnoreCase );
         // Alternatively, use Win32 api to find real path: https://stackoverflow.com/questions/2302416/
      }

      private bool FoundInstalledModnix () { try {
         if ( ! File.Exists( ModGuiExe ) ) return false;
         ModGuiExe = new FileInfo( ModGuiExe ).FullName; // Normalise path - e.g. My Documents to Documents
         if ( MyPath == ModGuiExe ) return false;

         Log( $"Found {ModGuiExe}" );
         var ver = Version.Parse( FileVersionInfo.GetVersionInfo( ModGuiExe ).ProductVersion );
         Log( $"Their version: {ver}" );
         if ( ver >= Myself.Version ) {
            if ( GUI != null )
               GUI.SetInfo( GuiInfo.APP_VER, ver.ToString() );
            return true;
         }
         return false;
      } catch ( Exception ex ) { return Log( ex, false ); } }

      private bool ShouldRunSetup () { try {
         if ( ! MyPath.Contains( "/Mods/" ) && ! MyPath.Contains( "\\Mods\\" ) ) return true;
         if ( Path.GetFileName( MyPath ).ToLowerInvariant().Contains( "setup" ) ) return true;
         if ( Path.GetFileName( MyPath ).ToLowerInvariant().Contains( "install" ) ) return true;
         Log( $"No need to run setup." );
         return false;
      } catch ( Exception ex ) { return Log( ex, false ); } }
      #endregion

      #region Check Status
      private ModLoaderBridge bridge;

      internal void CheckStatusAsync () {
         Log( "Queuing status check" );
         Task.Run( (Action) CheckStatus );
      }

      private void CheckStatus () { try { lock ( SynRoot ) {
         Log( "Checking status" );
         GUI.SetInfo( GuiInfo.APP_VER, CheckAppVer() );
         if ( FoundGame( out string gamePath ) ) {
            Log( $"Found game at {gamePath}" );
            currentGame = new GameInstallation( gamePath );
            GUI.SetInfo( GuiInfo.GAME_PATH, gamePath );
            CheckInjectionStatus();
            GetModList();
         } else {
            GUI.SetInfo( GuiInfo.APP_STATE, "no_game" );
         }
      } } catch ( Exception ex ) { Log( ex ); } }

      private void CheckInjectionStatus () {
         GUI.SetInfo( GuiInfo.GAME_RUNNING, IsGameRunning() );
         if ( CheckInjected() ) {
            GUI.SetInfo( GuiInfo.APP_STATE, currentGame.Status );
            GUI.SetInfo( GuiInfo.GAME_VER, CheckGameVer() );
         } else {
            GUI.SetInfo( GuiInfo.APP_STATE, "setup" ); // TODO: Return "none"
         }
      }

      public static bool IsGameRunning () {
         return Process.GetProcessesByName( Path.GetFileNameWithoutExtension( GAME_EXE ) ).Length > 0;
      }

      internal string InjectorPath ( string gamePath ) => Path.Combine( gamePath, DLL_PATH, INJECTOR );
      internal string LoaderPath   ( string gamePath ) => Path.Combine( gamePath, DLL_PATH, LOADER   );

      /// Check that mod injector and mod loader is in place
      internal bool InjectorInPlace () { try {
         if ( ! File.Exists( currentGame.Injector ) ) return Log( $"Missing injector: {currentGame.Injector}", false );
         if ( ! File.Exists( currentGame.Loader   ) ) return Log( $"Missing loader: {currentGame.Loader}", false );
         return Log( $"Injector and loader found in {currentGame.CodeDir}", true );
      } catch ( IOException ex ) { return Log( ex, false ); } }

      /// Return true if injectors are in place and injected.
      internal bool CheckInjected () {
         try {
            if ( ! InjectorInPlace() ) return false;
            Log( "Detecting injection status." );
            var result = currentGame.Status = currentGame.RunInjector( "/d" );
            return result == "modnix" || result == "both";
         } catch ( Exception ex ) {
            currentGame.Status  = "error";
            return Log( ex, false );
         }
      }

      internal string CheckAppVer () { try {
         string version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
         return new Regex( "(\\.0){1,2}$" ).Replace( version, "" );
      } catch ( Exception ex ) { return Log( ex, "error" ); } }

      internal string CheckGameVer () { try {
         Log( "Detecting game version." );
         try {
            string logFile = Path.Combine( ModFolder, MOD_LOG );
            if ( File.Exists( logFile ) && 
                 File.GetLastWriteTime( logFile ) > File.GetLastWriteTime( Path.Combine( currentGame.CodeDir, GAME_DLL ) ) ) {
               Log( $"Parsing {logFile}" );
               var line = File.ReadLines( logFile ).ElementAtOrDefault( 1 );
               var match = Regex.Match( line ?? "", "Assembly-CSharp/([^ ;]+)", RegexOptions.IgnoreCase );
               if ( match.Success )
                  return match.Value.Substring( 16 );
            }
         } catch ( Exception ex ) { Log( ex ); }

         return currentGame.RunInjector( "/g" );
      } catch ( Exception ex ) { return Log( ex, "error" ); } }

      /// Try to detect game path
      private bool FoundGame ( out string gamePath ) { gamePath = null; try {
         gamePath = SearchRegistry();
         if ( gamePath != null ) return true;
         gamePath = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.ProgramFiles ), "Epic Games", "PhoenixPoint" );
         if ( IsGamePath( gamePath ) ) return true;
         gamePath = SearchDrives();
         if ( gamePath != null ) return true;
         return false;
      } catch ( IOException ex ) { gamePath = null; return Log( ex, false ); } }

      private string SearchRegistry () { try {
         using ( RegistryKey key = Registry.CurrentUser.OpenSubKey( "System\\GameConfigStore\\Children\\acd774ad-4030-4091-8b74-e50749daefd8" ) ) {
            if ( key == null ) return null;
            var val = key.GetValue( "MatchedExeFullPath" )?.ToString();
            if ( val == null || ! File.Exists( val ) ) return null;
            val= Path.GetDirectoryName( val );
            if ( IsGamePath( val ) ) return val;
         }
         return null;
      } catch ( Exception ex ) { return Log< string >( ex, null ); } }

      private string SearchDrives () { try {
         foreach ( var drive in DriveInfo.GetDrives() ) try {
            if ( drive.DriveType != DriveType.Fixed ) continue;
            if ( ! drive.IsReady ) continue;
            string path = Path.Combine( drive.Name, "Program Files", "Epic Games", "PhoenixPoint" );
            if ( IsGamePath( path ) ) return path;
         } catch ( Exception ) { }
         return null;
      } catch ( Exception ex ) { return Log< string >( ex, null ); } }

      private bool IsGamePath ( string path ) { try {
         string exe = Path.Combine( path, GAME_EXE ), dll = Path.Combine( path, DLL_PATH, GAME_DLL );
         Log( $"Checking {path}" );
         return File.Exists( exe ) && File.Exists( dll );
      } catch ( Exception ex ) { return Log( ex, false ); } }
      #endregion

      internal void LaunchGame ( string type ) {
         // Non-Async
         try {
            if ( type == "online" ) {
               if ( currentGame.GameType == "epic" ) {
                  Log( "Launching through epic game launcher" );
                  Process.Start( "com.epicgames.launcher://apps/Iris?action=launch" );
                  return;
               }
            } else {
               string exe = Path.Combine( currentGame.GameDir, GAME_EXE );
               Log( $"Launching {exe}" );
               Process.Start( exe );
               return;
            }
            Log( $"Unsupported launch type. Requested {type}. Game is {currentGame.GameType}." );
            GUI.Prompt( "error" );
         } catch ( Exception ex ) {
            GUI.Prompt( "error", ex );
         }
      }

      #region Setup / Restore
      internal void DoSetupAsync () {
         Log( "Queuing setup" );
         Task.Run( (Action) DoSetup );
      }

      private void DoSetup () { try { lock ( SynRoot ) {
         string prompt = "setup_ok";
         // Copy exe to mod folder
         if ( CopySelf( MyPath, ModGuiExe ) )
            prompt += ",self_copy";
         // Copy hook files
         currentGame.WriteCodeFile( HARM_DLL, MainGUI.Properties.Resources._0Harmony   );
         currentGame.WriteCodeFile( CECI_DLL, MainGUI.Properties.Resources.Mono_Cecil   );
         currentGame.WriteCodeFile( LOADER  , MainGUI.Properties.Resources.ModnixLoader  );
         currentGame.WriteCodeFile( INJECTOR, MainGUI.Properties.Resources.ModnixInjector );
         currentGame.RunInjector( "/y" );
         CheckInjectionStatus();
         if ( currentGame.Status == "modnix" ) {
            // Migrate mods
            if ( MigrateLegacy() )
               prompt += ",mod_moved";
            // Disable PPML
            if ( HasLegacy() && currentGame.RenameCodeFile( PAST, PAST_BK ) )
               prompt += ",ppml";
            // Cleanup - accident prevention. Old dlls at game base may override dlls in the managed folder.
            foreach ( var file in new string[] { PAST, PAST_DL1, PAST_DL2, INJECTOR, LOADER, HARM_DLL, CECI_DLL } )
               currentGame.DeleteRootFile( file );
            GUI.Prompt( prompt );
         } else
            GUI.Prompt( "error" );
      } } catch ( Exception ex ) {
         try { CheckStatus(); } catch ( Exception ) {}
         Log( ex );
         GUI.Prompt( "error", ex );
      } }

      internal bool CopySelf ( string me, string there ) { try {
         if ( me == there ) return false;
         Log( $"Copying {MyPath} to {there}" );
         if ( File.Exists( there ) )
            File.Delete( there );
         else
            Directory.CreateDirectory( ModFolder );
         File.Copy( me, there );
         return File.Exists( there );
      } catch ( Exception ex ) { return Log( ex, false ); } }

      private bool MigrateLegacy () { try {
         string OldPath = Path.Combine( currentGame.GameDir, PAST_MOD );
         string NewPath = ModFolder;
         if ( ! Directory.Exists( OldPath ) ) {
            CreateShortcut();
            return false;
         }
         // Delete a few files that should not be in the mods folder
         var modDir = new GameInstallation( OldPath );
         modDir.DeleteRootFile( LOADER );
         modDir.DeleteRootFile( INJECTOR );
         modDir.DeleteRootFile( HARM_DLL );
         modDir.DeleteRootFile( PAST );
         modDir.DeleteRootFile( PAST_DL1 );
         modDir.DeleteRootFile( PAST_DL2 );
         if ( IsSameDir( OldPath, ModFolder ) ) {
            Log( $"{OldPath} seems to be symbolic link, skipping migration." );
            return false;
         }
         bool ModsMoved = false;
         Log( $"Migrating {OldPath} to {NewPath}" );
         // Move mods
         foreach ( var file in Directory.EnumerateFiles( OldPath ) ) try {
            string to = Path.Combine( NewPath, Path.GetFileName( file ) );
            Log( $"{file} => {to}" );
            File.Move( file, to );
            ModsMoved = true;
         } catch ( Exception ex ) { Log( ex ); }
         foreach ( var dir in Directory.EnumerateDirectories( OldPath ) ) try {
            string to = Path.Combine( NewPath, dir.Replace( OldPath + Path.DirectorySeparatorChar, "" ) );
            Log( $"{dir} => {to}" );
            Directory.Move( dir, to );
            ModsMoved = true;
         } catch ( Exception ex ) { Log( ex ); }
         // Remove Mods folder if empty
         if ( ! Directory.EnumerateFiles( OldPath ).Any() ) try {
            Log( $"Deleting empty {OldPath}" );
            try {
               Directory.Delete( OldPath, false );
               CreateShortcut();
            } catch ( Exception ex ) { Log( ex ); }
         } catch ( Exception ex ) { Log( ex ); }
         return ModsMoved;
      } catch ( Exception ex ) { return Log( ex, false ); } }

      private static bool IsSameDir ( string path1, string path2 ) {
         if ( ! Directory.Exists( path1 ) || ! Directory.Exists( path2 ) ) return false;
         // Very stupid, I know, but I don't want to go the very long win32 route...
         foreach ( var e in Directory.EnumerateFiles( path1 ) )
            if ( ! File.Exists( Path.Combine( path2, Path.GetFileName( e ) ) ) )
               return false;
         foreach ( var e in Directory.EnumerateFiles( path2 ) )
            if ( ! File.Exists( Path.Combine( path1, Path.GetFileName( e ) ) ) )
               return false;
         foreach ( var e in Directory.EnumerateDirectories( path1 ) )
            if ( ! Directory.Exists( Path.Combine( path2, Path.GetFileName( e ) ) ) )
               return false;
         foreach ( var e in Directory.EnumerateDirectories( path2 ) )
            if ( ! Directory.Exists( Path.Combine( path1, Path.GetFileName( e ) ) ) )
               return false;
         return true;
      }

      private bool HasLegacy () { try {
         return File.Exists( Path.Combine( currentGame.CodeDir, PAST ) );
      } catch ( Exception ex ) { return Log( ex, false ); } }
      

      internal void CreateShortcut () {
         string name = Path.Combine( currentGame.GameDir, PAST_MOD );
         Log( "Creating Mods shortcut to support legacy mods." );
         RunAndWait( currentGame.GameDir, "cmd", $"/c mklink /d \"{name}\" \"{ModFolder}\"", true );
      }

      internal void DoRestoreAsync () {
         Log( "Queuing restore" );
         Task.Run( (Action) DoRestore );
      }

      private void DoRestore () { try { lock ( SynRoot ) { 
         currentGame.RunInjector( "/y /r" );
         CheckInjectionStatus();
         if ( currentGame.Status == "none" ) {
            currentGame.DeleteCodeFile( INJECTOR );
            currentGame.DeleteCodeFile( LOADER );
            GUI.Prompt( "restore_ok" );
         } else
            GUI.Prompt( "error" );
      } } catch ( Exception ex ) {
         Log( ex );
         GUI.Prompt( "error", ex );
      } }

      internal void CheckUpdateAsync () {
         Log( "Queuing update check" );
         Task.Run( (Action) CheckUpdate );
      }

      private Updater updater;

      private void CheckUpdate () { try {
         lock( SynRoot ) {
            if ( updater == null )
               updater = new Updater();
         }
         GUI.SetInfo( GuiInfo.APP_UPDATE, updater.FindUpdate( Myself.Version ) );
      } catch ( Exception ex ) { Log( ex ); } }
      #endregion

      #region mods
      public void GetModList () { try {
         if ( bridge == null ) bridge = new ModLoaderBridge();
         GUI.SetInfo( GuiInfo.MOD_LIST, bridge.LoadModList() );
      } catch ( IOException ex ) { Log( ex ); } }

      internal void DoModActionAsync ( ModActionType action, ModInfo mod ) {
         Log( $"Queuing mod {action}" );
         Task.Run( () => DoModAction( action, mod ) );
      }

      private void DoModAction ( ModActionType action, ModInfo mod ) { try { lock ( SynRoot) {
         if ( mod == null ) return;
         switch ( action ) {
            case ModActionType.DELETE : 
               bridge.Delete( mod );
               GetModList();
               return;
            default :
               Log( $"Unknown command {action}" );
               return;
         }
      } } catch ( IOException ex ) {
         Log( ex );
         GUI.Prompt( "error", ex );
         GetModList();
      } }
      #endregion

      #region Helpers
      internal static void Explore ( string filename ) {
         Process.Start( "explorer.exe", $"/select, \"{filename}\"" );
      }

      internal string RunAndWait ( string path, string exe, string param = null, bool asAdmin = false ) {
         Log( $"Running{( asAdmin ? " as admin" : "" )} at {path} : {exe} {param}" );
         try {
            using ( Process p = new Process() ) {
               p.StartInfo.UseShellExecute = asAdmin;
               p.StartInfo.RedirectStandardOutput = ! asAdmin;
               p.StartInfo.FileName = exe;
               p.StartInfo.Arguments = param;
               p.StartInfo.WorkingDirectory = path;
               p.StartInfo.CreateNoWindow = true;
               p.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
               if ( asAdmin ) p.StartInfo.Verb = "runas";
               if ( ! p.Start() ) Log( "Process reused." );

               string output = "";
               if ( ! asAdmin ) {
                  output = p.StandardOutput.ReadToEnd()?.Trim();
                  Log( $"Standard out: {output}" );
               }
               p.WaitForExit( 30_000 );
               return output;
            }
         } catch ( Exception ex ) {
            return Log( ex, String.Empty );
         }
      }

      private string startup_log = "Startup log:\n";

      internal void Log ( object message ) {
         if ( GUI != null ) {
            if ( startup_log != null ) {
               GUI.Log( startup_log.Trim() );
               startup_log = null;
               if ( message == null ) return;
            }
            GUI.Log( message?.ToString() );
         } else {
            startup_log += message + "\n";
            Console.WriteLine( message );
         }
      }

      internal T Log<T> ( object message, T result ) {
         Log( message.ToString() );
         return result;
      }
      #endregion
   }

   internal class GameInstallation {
      internal GameInstallation ( string gameDir ) {
         GameDir  = gameDir;
         CodeDir  = Path.Combine( gameDir, AppControl.DLL_PATH );
         Injector = Path.Combine( CodeDir, AppControl.INJECTOR );
         Loader   = Path.Combine( CodeDir, AppControl.LOADER   );
      }

      internal readonly AppControl App = AppControl.Instance;
      internal readonly string GameDir;
      internal readonly string CodeDir;
      internal readonly string Injector;
      internal readonly string Loader;

      internal string Status; // Injection status

      internal string GameType { get {
         if ( Directory.Exists( Path.Combine( GameDir, AppControl.EPIC_DIR ) ) )
            return "epic";
         return "offline";
      } }

      internal string RunInjector ( string param ) {
         return App.RunAndWait( CodeDir, Injector, param );
      }

      internal void WriteCodeFile ( string file, byte[] content ) {
         if ( content == null ) throw new ArgumentNullException( nameof( content ) );
         string target = Path.Combine( CodeDir, file );
         App.Log( $"Writing {content.Length} bytes to {target}" );
         File.WriteAllBytes( target, content );
      }

      internal bool DeleteRootFile ( string file ) { try {
         string subject = Path.Combine( GameDir, file );
         if ( ! File.Exists( subject ) ) return false;
         App.Log( $"Deleting {subject}" );
         File.Delete( subject );
         return ! File.Exists( subject );
      } catch ( Exception ex ) { return App.Log( ex, false ); } }

      internal bool DeleteCodeFile ( string file ) { try {
         string subject = Path.Combine( CodeDir, file );
         if ( ! File.Exists( subject ) ) return false;
         App.Log( $"Deleting {subject}" );
         File.Delete( subject );
         return ! File.Exists( subject );
      } catch ( Exception ex ) { return App.Log( ex, false ); } }

      internal bool RenameCodeFile ( string file, string toName ) { try {
         string subject = Path.Combine( CodeDir, file   );
         string target  = Path.Combine( CodeDir, toName );
         App.Log( $"Renaming {subject} to {toName}" );
         File.Move( subject, target );
         return File.Exists( target );
      } catch ( Exception ex ) { return App.Log( ex, false ); } }
   }

   internal static class ExtCls {
      internal static string FixSlash ( this string path ) => path.Replace( '/', Path.DirectorySeparatorChar );
   }
}