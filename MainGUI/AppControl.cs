using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix.MainGUI {
   internal class AppControl {

      internal readonly static string MOD_PATH = @"My Games\Phoenix Point\Mods";
      internal readonly static string DLL_PATH = @"PhoenixPointWin64_Data\Managed";
      internal readonly static string SETUP_TO =  "Modnix.exe";
      internal readonly static string INJECTOR =  "ModnixInjector.exe";
      internal readonly static string LOADER   =  "ModnixLoader.dll";
      internal readonly static string PAST     =  "PhoenixPointModLoaderInjector.exe";
      internal readonly static string PAST_BK  =  "PhoenixPointModLoaderInjector.exe.orig";
      internal readonly static string GAME_EXE =  "PhoenixPointWin64.exe";
      internal readonly static string GAME_DLL =  "Assembly-CSharp.dll";

      // Game and install files are considered corrupted and thus non exists if smaller than this size
      private readonly static long MIN_FILE_SIZE = 1024 * 10;

      private readonly static string[] GAME_PATHS =
         new string[]{ ".", @"C:\Program Files\Epic Games\PhoenixPoint" };

      private readonly MainWindow GUI;
      private readonly object SynRoot = new object();
      private GameInstallation currentGame;

      internal AppControl ( MainWindow _GUI ) => GUI = _GUI;

      #region Check Status
      internal void CheckStatusAsync () {
         Log( "Queuing status check" );
         Task.Run( (Action) CheckStatus );
      }

      internal AssemblyName Myself;
      internal string ModFolder;

      /// 1. If injector is in correct place = call injector to detect status
      /// 2. If injector is not in place, but dummy exists
      ///   Check Phoenix Point. Found = can setup. Not found = Error.
      /// 3. If dummy not exists = Error, re-download
      private void CheckStatus () { lock ( SynRoot ) { try {
         Log( "Checking status" );
         Myself = Assembly.GetExecutingAssembly().GetName();
         Log( "Assembly: " + Myself.CodeBase );
         Log( "Working Dir: " + Directory.GetCurrentDirectory() );
         ModFolder = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), MOD_PATH );
         Log( "Mod Dir: " + ModFolder );
         GUI.SetAppVer( CheckAppVer() );
         if ( FoundGame( out string gamePath ) ) {
            currentGame = new GameInstallation( this, gamePath );
            GUI.SetGamePath( gamePath );
            CheckInjectionStatus();
         } else {
            GUI.SetAppState( "no_game" );
         }
      } catch ( Exception ex ) {
         Log( ex );
      } } }

      private bool FoundRunningGame () {
         return Process.GetProcessesByName( Path.GetFileNameWithoutExtension( GAME_EXE ) ).Length > 0;
      }

      private void CheckInjectionStatus () {
         string status = null;
         if ( CheckInjected() ) {
            status = currentGame.Status; // status should be either modnix or both
            if ( status == "both" ) status = "ppml"; // Make GUI shows ppml, and thus require setup to remove ppml
            GUI.SetGameVer( CheckGameVer() );
         } else {
            status = "setup";
         }
         if ( FoundRunningGame() ) status = "running";
         GUI.SetAppState( status );
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
         string ver = Assembly.GetExecutingAssembly().GetName().Version.ToString();
         Log( "Version: " + ver );
         return ver;
      } catch ( Exception ex ) {
         return Log( ex, "error" );
      } }

      internal string CheckGameVer () { try {
         Log( "Detecting game version." );
         string ver = currentGame.RunInjector( "/g" );
         Log( "Game Version: " + ver );
         return ver;
      } catch ( Exception ex ) {
         return Log( ex, "error" );
      } }

      /// Try to detect game path
      internal bool FoundGame ( out string gamePath ) { gamePath = null; try {
         foreach ( string path in GAME_PATHS ) {
            string exe = Path.Combine( path, GAME_EXE ), dll = Path.Combine( path, DLL_PATH, GAME_DLL );
            if ( File.Exists( exe ) && new FileInfo( exe ).Length > MIN_FILE_SIZE &&
                 File.Exists( dll ) && new FileInfo( dll ).Length > MIN_FILE_SIZE ) {
               gamePath = Path.GetFullPath( path );
               return Log( $"Found game at " + gamePath, true );
            }
            Log( $"Game not found at {path}" );
         }
         return false;
      } catch ( IOException ex ) { return Log( ex, false ); } }
      #endregion

      #region Setup / Restore
      internal void DoSetupAsync () {
         Log( "Queuing setup" );
         Task.Run( (Action) DoSetup );
      }

      private void DoSetup () { lock ( SynRoot ) { try {
         string prompt = "setup_ok";
         // Copy exe to mod folder
         string MyPath = Uri.UnescapeDataString( new UriBuilder( Myself.CodeBase ).Path );
         string SetupExe = Path.Combine( ModFolder, SETUP_TO );
         Log( $"Setup from {MyPath}" );
         if ( CopySelf( MyPath, SetupExe ) )
            prompt += ",self_copy";
         // Copy hook files
         currentGame.WriteCodeFile( "0Harmony.dll", SetupPackage._0Harmony );
         currentGame.WriteCodeFile( "Mono.Cecil.dll", SetupPackage.Mono_Cecil );
         currentGame.WriteCodeFile( LOADER, SetupPackage.ModnixLoader );
         currentGame.WriteCodeFile( INJECTOR, SetupPackage.ModnixInjector );
         currentGame.RunInjector( "/y" );
         CheckStatus();
         if ( HasPPML() && currentGame.RenameCodeFile( PAST, PAST_BK ) )
            prompt += ",ppml";
         GUI.Prompt( prompt );
      } catch ( Exception ex ) {
         try { CheckStatus(); } catch ( Exception ) {}
         Log( ex );
         GUI.Prompt( "error", ex );
      } } }

      internal bool CopySelf ( string me, string there ) { try {
         if ( me == there ) return false;
         Log( $"Copying self to {there}" );
         Directory.CreateDirectory( ModFolder );
         File.Copy( me, there );
         return File.Exists( there );
      } catch ( Exception ex ) {
         return Log( ex, false );
      } }
      
      internal void DoRestoreAsync () {
         Log( "Queuing restore" );
         Task.Run( (Action) DoRestore );
      }

      private void DoRestore () { lock ( SynRoot ) { try {
         currentGame.RunInjector( "/y /r" );
         CheckStatus();
         if ( currentGame.Status == "none" ) {
            currentGame.DeleteCodeFile( INJECTOR );
            currentGame.DeleteCodeFile( LOADER );
            GUI.Prompt( "restore_ok" );
         }
      } catch ( Exception ex ) {
         Log( ex );
         GUI.Prompt( "error", ex );
      } } }

      private bool HasPPML () { try {
         return File.Exists( Path.Combine( currentGame.CodeDir, PAST ) );
      } catch ( Exception) {
         return false;
      } }
      #endregion

      #region Helpers
      internal string RunAndWait ( string path, string exe, string param = null ) {
         Log( $"Running at {path} : {exe} {param}" );
         try {
            using ( Process p = new Process() ) {
               p.StartInfo.UseShellExecute = false;
               p.StartInfo.RedirectStandardOutput = true;
               p.StartInfo.FileName = exe;
               p.StartInfo.Arguments = param;
               p.StartInfo.WorkingDirectory = path;
               p.StartInfo.CreateNoWindow = true;
               p.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
               p.Start();

               string output = p.StandardOutput.ReadToEnd()?.Trim();
               Log( $"Standard out: {output}" );
               p.WaitForExit( 1000 );
               return output;
            }
         } catch ( Exception ex ) {
            return Log( ex, String.Empty );
         }
      }

      internal void Log ( object message ) => GUI.Log( message.ToString() );

      internal T Log<T> ( object message, T result ) {
         Log( message.ToString() );
         return result;
      }
      #endregion
   }

   internal class GameInstallation {
      internal GameInstallation ( AppControl app, string gameDir ) {
         App = app;
         GameDir  = gameDir;
         CodeDir  = Path.Combine( gameDir, AppControl.DLL_PATH );
         Injector = Path.Combine( CodeDir, AppControl.INJECTOR );
         Loader   = Path.Combine( CodeDir, AppControl.LOADER   );
      }

      internal readonly AppControl App;
      internal readonly string GameDir;
      internal readonly string CodeDir;
      internal readonly string Injector;
      internal readonly string Loader;

      internal string Status; // Injection status

      internal string RunInjector ( string param ) {
         return App.RunAndWait( CodeDir, Injector, param );
      }

      internal void WriteCodeFile ( string file, byte[] content ) {
         if ( content == null ) throw new ArgumentNullException( "content" );
         string target = Path.Combine( CodeDir, file );
         App.Log( $"Writing {content.Length} bytes to {target}" );
         File.WriteAllBytes( target, content );
      }

      internal bool DeleteCodeFile ( string file ) { try {
      string subject = Path.Combine( CodeDir, file );
      App.Log( $"Deleting {subject}" );
         File.Delete( subject );
         return ! File.Exists( subject );
      } catch ( Exception ex ) {
         return App.Log( ex, false );
      } }

      internal bool RenameCodeFile ( string file, string toName ) { try {
      string subject = Path.Combine( CodeDir, file   );
      string target  = Path.Combine( CodeDir, toName );
      App.Log( $"Renaming {subject} to {toName}" );
         File.Move( subject, target );
         return File.Exists( target );
      } catch ( Exception ex ) {
         return App.Log( ex, false );
      } }
   }
}
