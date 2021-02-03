﻿using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static System.Globalization.CultureInfo;

namespace Sheepy.Modnix.MainGUI {

   internal interface IAppGui {
      void SetInfo ( GuiInfo info, object value );
      void Prompt ( AppAction action, PromptFlag flags = PromptFlag.NONE, Exception ex = null );
      void Log ( object message );
   }

   public enum GuiInfo { NONE, VISIBILITY, MOD, MOD_LIST,
      APP_STATE, APP_VER, APP_UPDATE,
      GAME_RUNNING, GAME_PATH, GAME_VER }

   public abstract class ArchiveReader {
      protected readonly string ArchivePath;
      protected ArchiveReader ( string path ) { ArchivePath = path; }
      public abstract string[] ListFiles ();
      public abstract string[] Install ( string modFolder );
      protected void Log ( object msg ) => AppControl.Instance.Log( msg );
      public bool ShouldUseRoot ( string [] files ) => files.Any( e => e.StartsWith( "PPDefModifier", StringComparison.Ordinal ) && e != "PPDefModifier.dll" );
   }

   internal static class AppRes {
      internal const string INJECTOR = "ModnixInjector.exe";
      internal const string LOADER   = "ModnixLoader.dll";
      internal const string HARM_DLL = "0Harmony.dll";
      internal const string CECI_DLL = "Mono.Cecil.dll";
      internal const string JSON_DLL = "Newtonsoft.Json.dll";
   }

   public partial class AppControl : Application {
      public static AppControl Instance { get; private set; }

      // Use slash for all paths, and use .FixSlash() to correct to platform slash.
      internal readonly static string MOD_PATH = "My Games/Phoenix Point/Mods".FixSlash();
      internal readonly static string DLL_PATH = "PhoenixPointWin64_Data/Managed".FixSlash();
      internal const string LIVE_NAME  = "Modnix";
      internal const string APP_EXT  = ".exe";
      internal const string GAME_EXE = "PhoenixPointWin64.exe";
      internal const string GAME_DLL = "Assembly-CSharp.dll";
      internal const string DOOR_DLL = "winhttp.dll";
      internal const string DOOR_CNF = "doorstop_config.ini";
      internal const string JBA_DLL  = "JetBrains.Annotations.dll";
      internal const string PAST     = "PhoenixPointModLoaderInjector.exe";
      internal const string PAST_BK  = "PhoenixPointModLoaderInjector.exe.orig";
      internal const string PAST_DL1 = "PPModLoader.dll";
      internal const string PAST_DL2 = "PhoenixPointModLoader.dll";
      internal const string PAST_MOD = "Mods";
      internal const string MOD_LOG  = "ModnixLoader.log";
      internal const string GAME_LOG = "Console.log";
      internal const string EPIC_DIR = ".egstore";
      internal const string GOGG_DLL = "Galaxy64.dll";

      private string[] UNSAFE_DLL = new string[] { AppRes.LOADER, AppRes.INJECTOR, AppRes.CECI_DLL, AppRes.HARM_DLL, JBA_DLL, PAST, PAST_DL1, PAST_DL2, DOOR_DLL, DOOR_CNF };
      internal readonly string ModFolder = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), MOD_PATH );

      private string _ModGuiExe;
      internal string ModGuiExe { get => Get( ref _ModGuiExe ); private set => SetOnce( ref _ModGuiExe, value ); }

      private string _MyPath;
      internal string MyPath { get => Get( ref _MyPath ); private set => SetOnce( ref _MyPath, value ); }

      private IAppGui _GUI;
      internal IAppGui GUI { get => Get( ref _GUI ); private set => SetOnce( ref _GUI, value ); }

      private GameInstallation _CurrentGame;
      internal GameInstallation CurrentGame { get => Get( ref _CurrentGame ); set => Set( ref _CurrentGame, value ); }

      #region Startup
      private AssemblyName _Myself;
      private AssemblyName Myself { get => Get( ref _Myself ); set => SetOnce( ref _Myself, value ); }

      private bool _ParamSkipStartupCheck;
      internal bool ParamSkipStartupCheck { get => Get( ref _ParamSkipStartupCheck ); set => Set( ref _ParamSkipStartupCheck, value ); }

      private int _ParamIgnorePid;
      private int ParamIgnorePid { get => Get( ref _ParamIgnorePid ); set => Set( ref _ParamIgnorePid, value ); }

      internal void ApplicationStartup ( object sender, StartupEventArgs e ) { try {
         Log( $"Startup time {DateTime.Now.ToString( "yyyy-MM-dd HH:mm:ss.ffff", InvariantCulture )}" );
         Instance = this;
         ModBridge = new ModLoaderBridge();
         Init( e?.Args );
         if ( ! ParamSkipStartupCheck ) {
            Log( $"Running startup checks" );
            if ( FoundRunningModnix() ) {
               Shutdown();
               return;
            }
            if ( IsInstaller ) {
               if ( FoundInstalledModnix() )
                  GUI = new SetupWindow( "launch" );
               else
                  GUI = new SetupWindow( "setup" );
            } else if ( CheckRuntimeConfigAndRestart() && LaunchManagerIgnoreSelf( MyPath ) ) {
               Log( "Restarting after created .Net config" );
               Shutdown();
               return;
            }
         }
         Log( $"Launching main window" );
         if ( GUI == null )
            GUI = new MainWindow();
         LoadSettings();
         GUI.SetInfo( GuiInfo.VISIBILITY, "true" );
      } catch ( Exception ex ) {
         Console.WriteLine( ex );
         try {
            File.WriteAllText( LIVE_NAME + " Startup Error.log", StartupLog + ex.ToString() );
         } catch ( SystemException ) { }
         if ( GUI != null ) {
            GUI.SetInfo( GuiInfo.VISIBILITY, "true" );
            Log( ex );
         } else {
            Shutdown();
         }
      } }

      internal void ApplicationCleanup ( object sender, ExitEventArgs  e ) { try {
         SevenZipArchiveReader.Cleanup();
      } catch ( Exception ex ) { Console.WriteLine( ex ); } }

      private void Init ( string[] args ) {
         AssemblyLoader.Log = Log;
         // Dynamically load embedded dll
         AppDomain.CurrentDomain.AssemblyResolve += AssemblyLoader.AssemblyResolve;

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

      private void LoadSettings () {
         // Make sure settings is loaded before other processes
         if ( Settings.SettingVersion < 20200403 )
            Settings.SettingVersion = 20200403; // Update version but no need to save immediately.
      }

      internal LoaderSettings Settings => ModBridge.GetSettings();

      internal void SaveSettings () { try {
         ModBridge.SaveSettings();
      } catch ( Exception ex ) {
         GUI.Prompt( AppAction.NONE, PromptFlag.ERROR,
            new ApplicationException( "Cannot save settings. Check Anti-virus and make sure Mods folder is writable.", ex ) );
      } }

      internal void SetLogLevel ( SourceLevels level ) {
         var settings = Settings;
         if ( settings.LogLevel == level ) return;
         ModLoader.SetLogLevel( level );
         settings.LogLevel = level;
         SaveSettings();
      }

      // Parse command line arguments.
      // -i --ignore-pid (id)    Ignore given pid in running process check
      // -s --skip-launch-check  Skip checking running process, modnix installation, and setting migration
      // -reset --reset          Clear and reset App settings
      private void ProcessParams ( string[] args ) {
         if ( args == null || args.Length == 0 ) return;
         var param = args.ToList();

         if ( ParamIndex( param, "reset", "reset" ) >= 0 ) {
            try {
               var file = Path.Combine( ModFolder, ModLoader.CONF_FILE );
               Log( $"Deleting {file}" );
               File.Delete( file );
            } catch ( IOException ex ) { Log( ex ); }
         }

         int pid = ParamIndex( param, "i", "ignore-pid" );
         if ( pid >= 0 && param.Count > pid+1 && int.TryParse( param[ pid + 1 ], out int id ) )
            ParamIgnorePid = id;

         /// -o --open-mod-dir        Open mod folder on launch, once used as part of setup
         //if ( ParamIndex( param, "o", "open-mod-dir" ) >= 0 )
         //   Process.Start( "explorer.exe", $"/select, \"{ModGuiExe}\"" );

         ParamSkipStartupCheck = ParamIndex( param, "s", "skip-launch-check" ) >= 0;
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
         Process running = Array.Find( Process.GetProcesses(), e => ( e.ProcessName == LIVE_NAME ||
                       e.ProcessName.StartsWith( LIVE_NAME+"Setup", StringComparison.OrdinalIgnoreCase ) ||
                       e.ProcessName.StartsWith( LIVE_NAME+"Install", StringComparison.OrdinalIgnoreCase ) ) &&
                       e.Id != myId && ( ParamIgnorePid == 0 || e.Id != ParamIgnorePid ) );
         if ( running == null ) return false;
         // Bring to foreground
         IntPtr handle = running.MainWindowHandle;
         if ( handle == IntPtr.Zero ) return false;
         Log( $"Another instance (pid {running.Id}) found. Self-closing." );
         return NativeMethods.SetForegroundWindow( handle );
      } catch ( Exception ex ) { return Log( ex, false ); } }


      internal bool LaunchManagerIgnoreSelf ( string target ) { try {
         Process.Start( target, "/i " + Process.GetCurrentProcess().Id );
         return true;
      } catch ( Exception ex ) { Log( ex ); return false; } }

      private bool FoundInstalledModnix () { try {
         if ( ! File.Exists( ModGuiExe ) ) return false;
         Set( ref _ModGuiExe, new FileInfo( ModGuiExe ).FullName ); // Normalise path - e.g. My Documents to Documents
         if ( MyPath == ModGuiExe ) return false;

         Log( $"Found {ModGuiExe}" );
         var ver = Version.Parse( FileVersionInfo.GetVersionInfo( ModGuiExe ).ProductVersion );
         Log( $"Their version: {ver}" );
         if ( ver > Myself.Version ) {
            GUI?.SetInfo( GuiInfo.APP_VER, ver.ToString() );
            return true;
         } else if ( ver == Myself.Version )
            return new FileInfo( ModGuiExe ).Length == new FileInfo( Assembly.GetExecutingAssembly().Location ).Length;
         return false;
      } catch ( Exception ex ) { return Log( ex, false ); } }

      private bool IsInstaller =>
         Path.GetFileName( MyPath ).IndexOf( "Installer", StringComparison.OrdinalIgnoreCase ) >= 0;
      #endregion

      #region Check Status
      private ModLoaderBridge _ModBridge;
      internal ModLoaderBridge ModBridge { get => Get( ref _ModBridge ); set => Set( ref _ModBridge, value ); }

      internal Task CheckStatusTask ( bool listMods ) {
         Log( "Queuing status check" );
         if ( listMods )
            Task.Run( GetModList );
         return Task.Run( CheckStatus );
      }

      private void CheckStatus () { try {
         Log( $"Checking app and game status" );
         GUI.SetInfo( GuiInfo.APP_VER, CheckAppVer() );
         if ( FoundGame( out string gamePath ) ) {
            Log( $"Found game at {gamePath}" );
            if ( gamePath != Settings.GamePath ) {
               Settings.GamePath = gamePath;
               SaveSettings();
            }
            CurrentGame = new GameInstallation( gamePath );
            GUI.SetInfo( GuiInfo.GAME_PATH, gamePath );
            CheckInjectionStatus( true );
         } else {
            GUI.SetInfo( GuiInfo.APP_STATE, "no_game" );
         }
      } catch ( Exception ex ) { Log( ex ); } }

      private void CheckInjectionStatus ( bool CheckVersion = false ) {
         GUI.SetInfo( GuiInfo.GAME_RUNNING, IsGameRunning() );
         if ( InjectorInPlace() ) {
            if ( CheckVersion )
               Task.Run( () => {
                  var ver = CheckGameVer();
                  var gameVer = ver == "1.0" ? new Version( 1, 0, 999999 ) : Version.Parse( ver );
                  ModLoader.GameVersion = gameVer;
                  GUI.SetInfo( GuiInfo.GAME_VER, ver );
               } );
            if ( CheckInjected() ) {
               GUI.SetInfo( GuiInfo.APP_STATE, CurrentGame.Status );
               return;
            }
         }
         GUI.SetInfo( GuiInfo.APP_STATE, "none" );
      }

      public static bool IsGameRunning () {
         return Process.GetProcessesByName( Path.GetFileNameWithoutExtension( GAME_EXE ) ).Length > 0;
      }

      // Check that mod injector and mod loader is in place
      internal bool InjectorInPlace () { try {
         if ( ! File.Exists( CurrentGame.Injector ) ) return Log( $"Missing injector: {CurrentGame.Injector}", false );
         if ( ! File.Exists( CurrentGame.Loader   ) ) return Log( $"Missing loader: {CurrentGame.Loader}", false );
         return Log( $"Injector and loader found in {CurrentGame.CodeDir}", true );
      } catch ( IOException ex ) { return Log( ex, false ); } }

      // Return true if injectors are in place and injected.
      private bool CheckInjected () {
         try {
            Log( "Detecting injection status." );
            var result = CurrentGame.Status = CurrentGame.RunInjector( "/d" );
            return result == "modnix" || result == "both";
         } catch ( Exception ex ) {
            CurrentGame.Status = "error";
            return Log( ex, false );
         }
      }

      internal string CheckAppVer () { try {
         var version = Assembly.GetExecutingAssembly().GetName().Version.ToString();
         return ModMetaJson.RegxVerTrim.Replace( version, "" );
      } catch ( Exception ex ) { return Log( ex, "error" ); } }

      internal string CheckGameVer () { try {
         Log( "Detecting game version." );
         try {
            var logFile = Path.Combine( ModFolder, MOD_LOG );
            if ( File.Exists( logFile ) &&
                 File.GetLastWriteTime( logFile ) > File.GetLastWriteTime( Path.Combine( CurrentGame.CodeDir, GAME_DLL ) ) ) {
               Log( $"Parsing {logFile}" );
               var line = Utils.ReadLine( logFile, 1 );
               var match = Regex.Match( line ?? "", "Assembly-CSharp/([^ ;]+)", RegexOptions.IgnoreCase );
               if ( match.Success )
                  return match.Value.Substring( 16 );
            }
         } catch ( Exception ex ) { Log( ex ); }

         return CurrentGame.RunInjector( "/g" );
      } catch ( Exception ex ) { return Log( ex, "error" ); } }

      // Try to detect game path
      private bool FoundGame ( out string gamePath ) { try {
         if ( IsGamePath( gamePath = Settings.GamePath ) ) return true;
         foreach ( var path in new string[] { ".", "..", Path.Combine( "..", ".." ) } )
            if ( IsGamePath( gamePath = path ) ) return true;
         gamePath = SearchRegistry();
         if ( gamePath != null ) return true;
         gamePath = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.ProgramFiles ), "Epic Games", "PhoenixPoint" );
         if ( IsGamePath( gamePath ) ) return true;
         gamePath = SearchDrives();
         return gamePath != null;
      } catch ( IOException ex ) { gamePath = null; return Log( ex, false ); } }

      private string SearchRegistry () { try {
         Log( "Checking Steam registry" );
         using ( RegistryKey steam = Registry.LocalMachine.OpenSubKey( "SOFTWARE\\WOW6432Node\\Valve\\Steam" ) ) {
            var path = Path.Combine( steam?.GetValue( "InstallPath" )?.ToString(), "steamapps", "common", "Phoenix Point" );
            if ( IsGamePath( path ) ) return path;
         }
         Log( "Checking Steam App Uninstall registry" );
         using ( RegistryKey steamPP = Registry.LocalMachine.OpenSubKey( "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 839770" ) ) {
            var path = steamPP?.GetValue( "InstallLocation" )?.ToString();
            if ( IsGamePath( path ) ) return path;
         }
         Log( "Scanning Windows Game Bar registry" );
         using ( RegistryKey gamebar = Registry.CurrentUser.OpenSubKey( "System\\GameConfigStore\\Children" ) ) {
            foreach ( var game in gamebar.GetSubKeyNames() ) {
               using ( RegistryKey key = gamebar.OpenSubKey( game ) ) {
                  if ( key == null ) continue;
                  var val = key.GetValue( "MatchedExeFullPath" )?.ToString();
                  if ( val == null || val.IndexOf( GAME_EXE, StringComparison.OrdinalIgnoreCase ) < 0 || ! File.Exists( val ) ) continue;
                  val = Path.GetDirectoryName( val );
                  if ( IsGamePath( val ) ) return val;
               }
            }
         }
         return null;
      } catch ( Exception ex ) { return Log< string >( ex, null ); } }

      private string SearchDrives () { try {
         foreach ( var drive in DriveInfo.GetDrives() ) try {
            if ( drive.DriveType != DriveType.Fixed ) continue;
            if ( ! drive.IsReady ) continue;
            var path = Path.Combine( drive.Name, "Program Files (x86)", "Steam", "steamapps", "common", "Phoenix Point" );
            if ( IsGamePath( path ) ) return path;
            path = Path.Combine( drive.Name, "Program Files", "Epic Games", "PhoenixPoint" );
            if ( IsGamePath( path ) ) return path;
         } catch ( SystemException ) { }
         return null;
      } catch ( Exception ex ) { return Log< string >( ex, null ); } }

      internal bool IsGamePath ( string path ) { try {
         if ( string.IsNullOrWhiteSpace( path ) || ! Directory.Exists( path ) ) return false;
         string exe = Path.Combine( path, GAME_EXE ), dll = Path.Combine( path, DLL_PATH, GAME_DLL );
         Log( $"Detecting game at {path}" );
         return File.Exists( exe ) && File.Exists( dll );
      } catch ( Exception ex ) { return Log( ex, false ); } }
      #endregion

      internal void LaunchGame ( string type ) { try {
         CurrentGame.DeleteRootFile( GAME_LOG );
         if ( type == "online" ) {
            if ( CurrentGame.GameType == "epic" ) {
               Log( "Launching through Epic Games" );
               Process.Start( Settings.EgsCommand ?? "com.epicgames.launcher://apps/Iris?action=launch", Settings.EgsParameter );
               return;
            } else if ( CurrentGame.GameType == "gog" ) {
               Log( "Launching through Gog Galaxy" );
               string launcher = Settings.GogExe;
               var param = ( Settings.GogParameter ?? "/gameId=1795581746 /command=runGame /path=\"%GAME_PATH%\"" )
                     .Replace( "%GAME_PATH%", Path.Combine( CurrentGame.GameDir, GAME_EXE ).Replace( "\"", "\"\"" ) );
               if ( string.IsNullOrWhiteSpace( launcher ) )
                     using ( RegistryKey reg = Registry.LocalMachine.OpenSubKey( "SOFTWARE\\Wow6432Node\\GOG.com\\GalaxyClient\\paths" ) )
                        launcher = Path.Combine( reg?.GetValue( "client" )?.ToString(), "GalaxyClient.exe" );
               if ( ! File.Exists( launcher ) )
                  launcher = "C:/Program Files (x86)/GOG Galaxy/GalaxyClient.exe".FixSlash();
               if ( File.Exists( launcher ) )
                  Process.Start( launcher, param );
               else
                  MessageBox.Show( "Not found: " + launcher, "Error", MessageBoxButton.OK, MessageBoxImage.Error );
               return;
            } else {
               Log( "Launching through Steam" );
               Process.Start( Settings.SteamCommand ?? "steam://rungameid/839770" );
               return;
            }
         } else {
            var exe = Path.Combine( CurrentGame.GameDir, GAME_EXE );
            Log( $"Launching {exe}" );
            using ( Process p = new Process() ) {
               p.StartInfo.UseShellExecute = false;
               p.StartInfo.FileName = exe;
               p.StartInfo.WorkingDirectory = CurrentGame.GameDir;
               p.StartInfo.Arguments = Settings.OfflineParameter;
               p.Start();
            }
            return;
         }
         throw new InvalidOperationException( $"Game is {CurrentGame.GameType}. Cannot launch as {type}." );
      } catch ( Exception ex ) {
         GUI.Prompt( AppAction.LAUNCH_GAME, PromptFlag.ERROR, ex );
      } }

      #region Setup / Restore
      internal void SetGamePath ( string path ) {
         Log( $"Setting game path to {path}" );
         Settings.GamePath = path;
         CurrentGame = new GameInstallation( path );
      }

      internal Task DoSetupTask () {
         Log( "Queuing setup" );
         return Task.Run( DoSetup );
      }

      private void DoSetup () { try {
         Log( $"Running setup" );
         var flags = PromptFlag.NONE;
         // Copy exe to mod folder
         if ( CopySelf( MyPath, ModGuiExe ) )
            flags |= PromptFlag.SETUP_SELF_COPY;
         // Copy hook files
         CurrentGame.WriteCodeFile( AppRes.HARM_DLL, AssemblyLoader.GetResourceStream( AppRes.HARM_DLL ) );
         CurrentGame.WriteCodeFile( AppRes.CECI_DLL, AssemblyLoader.GetResourceStream( AppRes.CECI_DLL ) );
         CurrentGame.WriteCodeFile( AppRes.LOADER  , AssemblyLoader.GetResourceStream( AppRes.LOADER   ) );
         CurrentGame.WriteCodeFile( AppRes.INJECTOR, AssemblyLoader.GetResourceStream( AppRes.INJECTOR ) );
         CurrentGame.RunInjector( "/y" );
         CheckInjectionStatus( true );
         if ( CurrentGame.Status == "modnix" ) {
            CreateRuntimeConfig( ModGuiExe );
            SaveSettings();
            // Migrate mods
            if ( MigrateLegacy() )
               flags |= PromptFlag.SETUP_MOD_MOVED;
            // Disable PPML
            if ( HasLegacy() && CurrentGame.RenameCodeFile( PAST, PAST_BK ) )
               flags |= PromptFlag.SETUP_PPML;
            // Cleanup - accident prevention. Old dlls at game base may override dlls in the managed folder.
            foreach ( var file in UNSAFE_DLL )
               CurrentGame.DeleteRootFile( file );
            //CurrentGame.DeleteCodeFile( JBA_DLL );
            GUI.Prompt( AppAction.SETUP, flags );
         } else
            throw new ApplicationException( "Modnix injection failed" );
      } catch ( Exception ex ) {
         GUI.Prompt( AppAction.SETUP, PromptFlag.ERROR, ex );
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

      private string RuntimeConfigPath ( string exePath ) => Path.Combine( Path.GetDirectoryName( exePath ), Path.GetFileNameWithoutExtension( exePath ) ) + ".config";

      private bool CheckRuntimeConfigAndRestart () {
         string confPath = RuntimeConfigPath( MyPath );
         if ( File.Exists( confPath ) ) return false;
         if ( CreateRuntimeConfig( MyPath ) != null ) return false;
         return File.Exists( confPath );
      }

      internal Exception CreateRuntimeConfig ( string exePath ) { try {
         var confPath = RuntimeConfigPath( exePath );
         Log( "Creating .Net config at " + confPath );
         File.WriteAllText( confPath, "<?xml version=\"1.0\" encoding=\"utf-8\"?><configuration><runtime><loadFromRemoteSources enabled=\"true\"/></runtime></configuration>" );
         return null;
      } catch ( Exception ex ) { Log( ex ); return ex; } }

      private bool MigrateLegacy () { try {
         var OldPath = Path.Combine( CurrentGame.GameDir, PAST_MOD );
         var NewPath = ModFolder;
         if ( ! Directory.Exists( OldPath ) ) {
            CreateShortcut();
            return false;
         }
         // Delete a few files that should not be in the mods folder
         var modDir = new GameInstallation( OldPath );
         foreach ( var dll in UNSAFE_DLL )
            modDir.DeleteRootFile( dll );
         if ( IsSameDir( OldPath, ModFolder ) ) {
            Log( $"{OldPath} seems to be symbolic link, skipping migration." );
            return false;
         }
         var ModsMoved = false;
         Log( $"Migrating {OldPath} to {NewPath}" );
         // Move mods
         foreach ( var file in Directory.EnumerateFiles( OldPath ) ) try {
            var to = Path.Combine( NewPath, Path.GetFileName( file ) );
            Log( $"{file} => {to}" );
            File.Move( file, to );
            ModsMoved = true;
         } catch ( Exception ex ) { Log( ex ); }
         foreach ( var dir in Directory.EnumerateDirectories( OldPath ) ) try {
            var to = Path.Combine( NewPath, dir.Replace( OldPath + Path.DirectorySeparatorChar, "" ) );
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
         return File.Exists( Path.Combine( CurrentGame.CodeDir, PAST ) );
      } catch ( Exception ex ) { return Log( ex, false ); } }


      internal void CreateShortcut () {
         var name = Path.Combine( CurrentGame.GameDir, PAST_MOD );
         Log( "Creating Mods shortcut to support legacy mods." );
         RunAndWait( CurrentGame.GameDir, "cmd", $"/c mklink /d \"{name}\" \"{ModFolder}\"", true );
      }

      internal Task DoRestoreTask () {
         Log( "Queuing restore" );
         return Task.Run( DoRestore );
      }

      private void DoRestore () { try {
         Log( $"Running restore" );
         CurrentGame.RunInjector( "/y /r" );
         CheckInjectionStatus();
         if ( CurrentGame.Status == "none" ) {
            CurrentGame.DeleteCodeFile( AppRes.INJECTOR );
            CurrentGame.DeleteCodeFile( AppRes.LOADER );
            GUI.Prompt( AppAction.REVERT );
         } else
            throw new ApplicationException( "Modnix revert failed" );
      } catch ( Exception ex ) {
         GUI.Prompt( AppAction.REVERT, PromptFlag.ERROR, ex );
      } }

      internal Task<GithubRelease> CheckUpdateTask () {
         Log( "Queuing update check" );
         return Task.Run( CheckUpdate );
      }

      private Updater _UpdateChecker;
      private Updater UpdateChecker { get => Singleton( ref _UpdateChecker ); }

      private GithubRelease CheckUpdate() => UpdateChecker.FindUpdate( Myself.Version );
      #endregion

      #region Mods
      internal string LoaderLog => Path.Combine( ModFolder, "ModnixLoader.log" );
      internal string ConsoleLog => CurrentGame == null ? null : Path.Combine( CurrentGame.GameDir, "Console.log" );

      private bool IsLoadingModList;
      private HashSet< string > ModWithError = new HashSet<string>();
      private HashSet< string > ModWithWarning = new HashSet<string>();
      private HashSet< string > ModWithConfWarn = new HashSet<string>();
      private DateTime LoaderLogLastModified;
      private readonly Regex RegexModCaptureLine = new Regex( "^(?>[\\d:\\.]+) (EROR|WARN) ([^┊]+)┊", RegexOptions.Compiled );

      public void GetModList () { try {
         lock ( ModWithError ) {
            if ( IsLoadingModList ) return;
            IsLoadingModList = true;
         }
         try {
            Log( "Rebuilding mod list" );
            ModInfo[] list = null;
            Task.WaitAll( new Task[] { // Scan mods and loader logs in parallel.
               Task.Run( () => {
                  var result = ModBridge.LoadModList();
                  lock ( SynGetSet ) list = result;
               } ),
               Task.Run( CheckLogForError ),
            } );
            lock ( SynGetSet ) if ( list == null ) return;
            lock ( ModWithError ) if ( ModWithError.Count > 0 || ModWithWarning.Count > 0 || ModWithConfWarn.Count > 0 ) {
               Log( "Adding warnings to mods with runtime notices." );
               foreach ( var mod in list ) {
                  if ( ! mod.Is( ModQuery.ENABLED ) ) continue;
                  if ( ModWithError.Contains( mod.Id ) ) ModLoaderBridge.AddLoaderLogNotice( mod, "runtime_error" );
                  else if ( ModWithWarning.Contains( mod.Id ) ) ModLoaderBridge.AddLoaderLogNotice( mod, "runtime_warning" );
                  else if ( ModWithConfWarn.Contains( mod.Id ) ) ModLoaderBridge.AddLoaderLogNotice( mod, "config_mismatch" );
               }
            }
            GUI.SetInfo( GuiInfo.MOD_LIST, list );
         } finally {
            lock ( ModWithError ) IsLoadingModList = false;
         }
      } catch ( SystemException ex ) { Log( ex ); } }

      private void CheckLogForError () { try {
         void ClearLogs () { lock ( ModWithError ) {
            ModWithError.Clear();
            ModWithWarning.Clear();
            ModWithConfWarn.Clear();
         } }
         var file = LoaderLog;
         if ( ! File.Exists( file ) ) {
            ClearLogs();
            return;
         }
         DateTime mTime = new FileInfo( file ).LastWriteTime;
         lock ( ModWithError ) {
            if ( mTime == LoaderLogLastModified ) return;
            Log( $"Parsing {file} for errors, last updated {mTime}." );
            ClearLogs();
            using ( var reader = new StreamReader( file ) ) {
               string line;
               while ( ( line = reader.ReadLine()?.Trim() ) != null ) {
                  if ( line.Length == 0 ) continue;
                  var match = RegexModCaptureLine.Match( line );
                  if ( ! match.Success ) continue;
                  string type = match.Groups[ 1 ].Value, id = match.Groups[ 2 ].Value;
                  if ( type.Equals( "EROR" ) )
                     ModWithError.Add( id );
                  else if ( line.EndsWith( "┊Default config mismatch.", StringComparison.Ordinal ) )
                     ModWithConfWarn.Add( id );
                  else
                     ModWithWarning.Add( id );
               }
            }
            Log( $"Log parsed. Error {ModWithError.Count}, Warn {ModWithWarning.Count}, Config {ModWithConfWarn.Count}." );
            LoaderLogLastModified = mTime;
         }
      } catch ( SystemException ex ) { Log( ex ); } }

      internal Task ModActionTask ( AppAction action, IEnumerable<ModInfo> mods ) {
         Log( $"Queuing {action} of {mods.Count()} mods" );
         return Task.WhenAll(
            mods.Select( mod => Task.Run( () => DoModAction( action, mod ) )
         ) );
      }

      private void DoModAction ( AppAction action, ModInfo mod ) {
         Log( $"Running {action} of {mod}" );
         if ( mod == null ) return;
         switch ( action ) {
            case AppAction.DEL_MOD :
               ModBridge.DeleteMod( mod );
               return;
            case AppAction.SAVE_CONFIG :
               mod.Do( AppAction.SAVE_CONFIG );
               return;
            case AppAction.DELETE_CONFIG :
               mod.Do( AppAction.DELETE_CONFIG );
               return;
            default :
               Log( $"Unknown command {action}" );
               return;
         }
      }

      internal Task<string[][]> AddModTask ( string[] files ) {
         Log( $"Queuing add mod of {string.Join( "\n", files )}" );
         return Task.WhenAll( files.Select( file => Task.Run( () => AddMod( file ) ) ) );
      }

      private static Regex IgnoreInAddMod = new Regex( "(-\\d{6,}|\\.(dll|js|json))+$", RegexOptions.Compiled );

      private string[] AddMod ( string file ) {
         var ext = Path.GetExtension( file ).ToLowerInvariant();
         var modname = IgnoreInAddMod.Replace( Path.GetFileNameWithoutExtension( file ), "" );
         if ( string.IsNullOrWhiteSpace( modname ) ) modname = Path.GetFileNameWithoutExtension( file );
         var folder = Path.Combine( ModFolder, modname );
         if ( ext.Equals( ".js" ) || ext.Equals( ".json" ) || ext.Equals( ".dll" ) ) {
            Log( $"Adding {file} as a single file mod" );
            Directory.CreateDirectory( folder );
            var destination = Path.Combine( folder, Path.GetFileName( file ) );
            Log( destination );
            File.Copy( file, destination, true );
            return new string[] { destination };
         } else {
            Log( $"Adding {file} as a packed mod" );
            var reader = ext.Equals( ".zip" ) ? (ArchiveReader) new ZipArchiveReader( file ) : new SevenZipArchiveReader( file );
            string[] files = reader.ListFiles();
            if ( files.Length == 0 ) return new string[0];
            return reader.Install( reader.ShouldUseRoot( files ) ? ModFolder : folder );
         }
      }

      internal static string LangIdToName ( string id ) {
         switch ( id ) {
            case "*"  : return "All Languages";
            case "--" :
            case "-"  : return "Language Independent";
            case "en" : return "English";
            case "de" : return "Deutsch";
            case "es" : return "Español";
            case "fr" : return "français";
            case "it" : return "Italiano";
            case "pl" : return "polski";
            case "ru" : return "русский";
            case "zh" : return "中文";
            default   : return id;
         }
      }
      #endregion

      #region Helpers
      private static readonly object SynGetSet = new object();

      private static T SetOnce < T > ( ref T field, T val ) where T : class {
         lock ( SynGetSet ) {
            if ( field != null ) throw new InvalidOperationException();
            field = val;
         }
         return val;
      }

      private static T Set < T > ( ref T field, T val ) {
         lock ( SynGetSet ) return field = val;
      }

      private static T Get < T > ( ref T field ) {
         lock ( SynGetSet ) return field;
      }

      private static T Singleton < T > ( ref T field ) where T : class, new() {
         lock ( SynGetSet ) return field ?? ( field = new T() );
      }

      private static T Singleton < T > ( ref T field, Func<T> creator ) where T : class {
         lock ( SynGetSet ) return field ?? ( field = creator() );
      }

      internal static void Explore ( string filename ) {
         Process.Start( "explorer.exe", $"/select, \"{filename}\"" );
      }

      internal string RunAndWait ( string path, string exe, string param = null, bool asAdmin = false, bool suppressLog = false ) {
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
                  if ( ! suppressLog )
                     Log( $"Standard out: {output}" );
               }
               p.WaitForExit( 30_000 );
               return output;
            }
         } catch ( Exception ex ) {
            return Log( ex, String.Empty );
         }
      }

      private string _StartupLog = "Startup log:\n";
      private string StartupLog { get => Get( ref _StartupLog ); set => Set( ref _StartupLog, value ); }

      // Double as a memory barrier because of its GUI read
      internal void Log ( object message ) {
         var id = Thread.CurrentThread.ManagedThreadId;
         var txt = ( id <= 1 ? "" : $"Thread{Thread.CurrentThread.ManagedThreadId}┊" ) + message?.ToString();
         if ( GUI != null ) {
            lock ( SynGetSet ) {
               if ( StartupLog != null ) {
                  GUI.Log( StartupLog.Trim() );
                  StartupLog = null;
               }
            }
            GUI.Log( txt );
         } else {
            lock ( SynGetSet ) StartupLog += txt + "\n";
            Console.WriteLine( txt );
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
         Injector = Path.Combine( CodeDir, AppRes.INJECTOR );
         Loader   = Path.Combine( CodeDir, AppRes.LOADER );
      }

      internal readonly AppControl App = AppControl.Instance;
      internal readonly string GameDir;
      internal readonly string CodeDir;
      internal readonly string Injector;
      internal readonly string Loader;

      internal string _Status; // Injection status
      internal string Status  {
         get { lock( this ) { return _Status; } }
         set { lock( this ) { _Status = value; } } }

      internal string GameType { get {
         if ( Directory.Exists( Path.Combine( GameDir, AppControl.EPIC_DIR ) ) )
            return "epic";
         else if ( File.Exists( Path.Combine( GameDir, AppControl.GOGG_DLL ) ) )
            return "gog";
         return "unknown";
      } }

      internal string RunInjector ( string param ) {
         return App.RunAndWait( CodeDir, Injector, param );
      }

      internal void WriteCodeFile ( string file, byte[] content ) {
         if ( content == null ) throw new ArgumentNullException( nameof( content ) );
         var target = Path.Combine( CodeDir, file );
         App.Log( $"Writing {content.Length} bytes to {target}" );
         File.WriteAllBytes( target, content );
      }

      internal void WriteCodeFile ( string file, Stream source ) {
         if ( source == null ) throw new ArgumentNullException( nameof( source ) );
         var target = Path.Combine( CodeDir, file );
         App.Log( $"Writing to {target}" );
         using ( var writer = new FileStream( target, FileMode.Create ) ) {
            source.CopyTo( writer );
         }
      }

      internal bool DeleteRootFile ( string file ) { try {
         var subject = Path.Combine( GameDir, file );
         if ( ! File.Exists( subject ) ) return false;
         App.Log( $"Deleting {subject}" );
         File.Delete( subject );
         return ! File.Exists( subject );
      } catch ( Exception ex ) { return App.Log( ex, false ); } }

      internal bool DeleteCodeFile ( string file ) { try {
         var subject = Path.Combine( CodeDir, file );
         if ( ! File.Exists( subject ) ) return false;
         App.Log( $"Deleting {subject}" );
         File.Delete( subject );
         return ! File.Exists( subject );
      } catch ( Exception ex ) { return App.Log( ex, false ); } }

      internal bool RenameCodeFile ( string file, string toName ) { try {
         var subject = Path.Combine( CodeDir, file   );
         var target  = Path.Combine( CodeDir, toName );
         DeleteCodeFile( toName );
         App.Log( $"Renaming {subject} to {toName}" );
         File.Move( subject, target );
         return File.Exists( target );
      } catch ( Exception ex ) { return App.Log( ex, false ); } }
   }

   internal static class AssemblyLoader {
      internal static Action<object> Log;

      internal static Assembly AssemblyResolve ( object domain, ResolveEventArgs dll ) {
         if ( dll.Name.StartsWith( "Microsoft.VisualStudio.", StringComparison.OrdinalIgnoreCase ) ) return null;
         Log?.Invoke( $"Modnix resolving {dll.Name}" );
         var app = domain as AppDomain ?? AppDomain.CurrentDomain;
         if ( dll.Name.StartsWith( "ModnixLoader,", StringComparison.OrdinalIgnoreCase ) )
            return app.Load( GetResourceBytes( AppRes.LOADER ) );
         if ( dll.Name.StartsWith( "Mono.Cecil,", StringComparison.OrdinalIgnoreCase ) )
            return app.Load( GetResourceBytes( AppRes.CECI_DLL ) );
         if ( dll.Name.StartsWith( "Newtonsoft.Json,", StringComparison.OrdinalIgnoreCase ) )
            return app.Load( GetResourceBytes( AppRes.JSON_DLL ) );
         return null;
      }

      internal static Stream GetResourceStream ( string path ) {
         path = ".Resources." + path + ".gz";
         var me = Assembly.GetExecutingAssembly();
         var fullname = Array.Find( me.GetManifestResourceNames(), e => e.EndsWith( path, StringComparison.Ordinal ) );
         return new GZipStream( me.GetManifestResourceStream( fullname ), CompressionMode.Decompress );
      }

      internal static byte[] GetResourceBytes ( string path ) {
         var mem = new MemoryStream();
         using ( var stream = GetResourceStream( path ) ) {
            stream.CopyTo( mem );
            Log?.Invoke( string.Format( "Mapped {0} to memory, {1:n0} bytes.", path, mem.Length ) );
         }
         return mem.ToArray();
      }
   }

   internal static class Utils {
      private static StreamReader Read ( string file ) =>
         new StreamReader( new FileStream( file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete ), Encoding.UTF8, true );

      internal static string ReadFile ( string file ) { using ( var reader = Read( file ) ) return reader.ReadToEnd(); }
      internal static string ReadLine ( string file, int skipLine = 0 ) {
         using ( var reader = Read( file ) ) {
            while ( skipLine-- > 0 ) reader.ReadLine();
            return reader.ReadLine();
         }
      }
   }

   internal static class ExtCls {
      internal static string FixSlash ( this string path ) => path.Replace( '/', Path.DirectorySeparatorChar );
   }
}