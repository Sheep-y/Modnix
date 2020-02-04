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
   public class AppControl {

      public readonly static string DLL_PATH = @"PhoenixPointWin64_Data\Managed";
      public readonly static string INJECTOR =  "ModnixInjector.exe";
      public readonly static string LOADER   =  "ModnixLoader.dll";
      public readonly static string LEGACY   =  "PhoenixPointModLoaderInjector.exe";
      public readonly static string GAME_EXE =  "PhoenixPointWin64.exe";
      public readonly static string GAME_DLL =  "Assembly-CSharp.dll";

      // Game and install files are considered corrupted and thus non exists if smaller than this size
      private readonly static long MIN_FILE_SIZE = 1024 * 10;

      private readonly static string[] GAME_PATHS =
         new string[]{ ".", @"C:\Program Files\Epic Games\PhoenixPoint" };

      private readonly MainWindow GUI;
      private readonly object SynRoot = new object();
      private GameInstallation currentGame;

      public AppControl ( MainWindow _GUI ) => GUI = _GUI;

      #region Check Status
      public void CheckStatusAsync () {
         Log( "Queuing status check" );
         Task.Run( (Action) CheckStatus );
      }

      private AssemblyName Myself;

      /// 1. If injector is in correct place = call injector to detect status
      /// 2. If injector is not in place, but dummy exists
      ///   Check Phoenix Point. Found = can setup. Not found = Error.
      /// 3. If dummy not exists = Error, re-download
      private void CheckStatus () { lock ( SynRoot ) { try {
         Log( "Checking status" );
         Myself = Assembly.GetExecutingAssembly().GetName();
         Log( "Assembly: " + Myself.CodeBase );
         Log( "Working Dir: " + Directory.GetCurrentDirectory() );
         GUI.SetAppVer( CheckAppVer() );
         bool found = FoundGame( out string gamePath );
         if ( found ) {
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
         Process[] clones = Process.GetProcessesByName( Path.GetFileNameWithoutExtension( GAME_EXE ) );
         return clones.Length > 0;
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

      public string InjectorPath ( string gamePath ) => Path.Combine( gamePath, DLL_PATH, INJECTOR );
      public string LoaderPath   ( string gamePath ) => Path.Combine( gamePath, DLL_PATH, LOADER   );

      /// Check that mod injector and mod loader is in place
      public bool InjectorInPlace () { try {
         if ( ! File.Exists( currentGame.Injector ) ) return Log( $"Missing injector: {currentGame.Injector}", false );
         if ( ! File.Exists( currentGame.Loader   ) ) return Log( $"Missing loader: {currentGame.Loader}", false );
         return Log( $"Injector and loader found in {currentGame.CodeDir}", true );
      } catch ( IOException ex ) { return Log( ex, false ); } }

      /// Return true if injectors are in place and injected.
      public bool CheckInjected () {
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

      public string CheckAppVer () { try {
         string ver = Assembly.GetExecutingAssembly().GetName().Version.ToString();
         Log( "Version: " + ver );
         return ver;
      } catch ( Exception ex ) {
         return Log( ex, "error" );
      } }

      public string CheckGameVer () { try {
         Log( "Detecting game version." );
         string ver = currentGame.RunInjector( "/g" );
         Log( "Game Version: " + ver );
         return ver;
      } catch ( Exception ex ) {
         return Log( ex, "error" );
      } }

      /// Try to detect game path
      public bool FoundGame ( out string gamePath ) { gamePath = null; try {
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
      public void DoSetupAsync () {
         Log( "Queuing setup" );
         Task.Run( (Action) DoSetup );
      }

      private void DoSetup () { lock ( SynRoot ) { try {
         currentGame.WriteCodeFile( "0Harmony.dll", SetupPackage._0Harmony );
         currentGame.WriteCodeFile( "Mono.Cecil.dll", SetupPackage.Mono_Cecil );
         currentGame.WriteCodeFile( LOADER, SetupPackage.ModnixLoader );
         currentGame.WriteCodeFile( INJECTOR, SetupPackage.ModnixInjector );
         currentGame.RunInjector( "/y" );
         CheckStatus();
         if ( currentGame.Status == "modnix" )
            GUI.SetupSuccess( HasPPML() );
      } catch ( Exception ex ) {
         try { CheckStatus(); } catch ( Exception ) {}
         Log( ex );
         GUI.SetAppState( ex.GetType().ToString() );
      } } }

      public void DeletePPMLAsync () {
         Log( "Queuing delete PPML" );
         Task.Run( (Action) DeletePPML );
      }

      private void DeletePPML () { lock ( SynRoot ) { try {
         currentGame.DeleteCodeFile( LEGACY );
      } catch ( Exception ex ) {
         Log( ex );
      } } }
      
      public void DoRestoreAsync () {
         Log( "Queuing restore" );
         Task.Run( (Action) DoRestore );
      }

      private void DoRestore () { lock ( SynRoot ) { try {
         currentGame.RunInjector( "/y /r" );
         CheckStatus();
         if ( currentGame.Status == "none" ) {
            currentGame.DeleteCodeFile( INJECTOR );
            currentGame.DeleteCodeFile( LOADER );
            GUI.RestoreSuccess();
         }
      } catch ( Exception ex ) {
         Log( ex );
      } } }

      private bool HasPPML () { try {
         return File.Exists( Path.Combine( currentGame.CodeDir, LEGACY ) );
      } catch ( Exception) {
         return false;
      } }
      #endregion

      #region Helpers
      public string RunAndWait ( string path, string exe, string param = null ) {
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
            Log( ex );
            return String.Empty;
         }
      }

      public void Log ( object message ) => GUI.Log( message.ToString() );

      public T Log<T> ( object message, T result ) {
         Log( message.ToString() );
         return result;
      }
      #endregion
   }

   public class GameInstallation {
      public GameInstallation ( AppControl app, string gameDir ) {
         App = app;
         GameDir  = gameDir;
         CodeDir  = Path.Combine( gameDir, AppControl.DLL_PATH );
         Injector = Path.Combine( CodeDir, AppControl.INJECTOR );
         Loader   = Path.Combine( CodeDir, AppControl.LOADER   );
      }

      public readonly AppControl App;
      public readonly string GameDir;
      public readonly string CodeDir;
      public readonly string Injector;
      public readonly string Loader;

      public string Status { get; internal set; } // Injection status

      public string RunInjector ( string param ) {
         return App.RunAndWait( CodeDir, Injector, param );
      }

      public void WriteCodeFile ( string file, byte[] content ) {
         if ( content == null ) throw new ArgumentNullException( "content" );
         string target = Path.Combine( CodeDir, file );
         App.Log( $"Writing {content.Length} bytes to {target}" );
         File.WriteAllBytes( target, content );
      }

      public void DeleteCodeFile ( string file ) {
         string target = Path.Combine( CodeDir, file );
         App.Log( $"Deleting {target}" );
         try {
            File.Delete( target );
         } catch ( Exception ex ) {
            App.Log( ex );
         }
      }
   }
}
