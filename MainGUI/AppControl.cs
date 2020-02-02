using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix.MainGUI {
   public class AppControl {

      private readonly static string DLL_PATH = @"PhoenixPointWin64_Data\Managed";
      private readonly static string INJECTOR =  "ModnixInjector.exe";
      private readonly static string LOADER   =  "ModnixLoader.dll";
      private readonly static string GAME_EXE =  "PhoenixPointWin64.exe";
      private readonly static string GAME_DLL =  "Assembly-CSharp.dll";

      // Game and install files are considered corrupted and thus non exists if smaller than this size
      private readonly static long MIN_FILE_SIZE = 1024 * 10;

      private readonly static string[] GAME_PATHS =
         new string[]{ ".", @"C:\Program Files\Epic Games\PhoenixPoint" };
      private readonly static string[] PACKAGES  =
         new string[]{ "ModnixInjector.ex_", "ModnixLoader.dll", "0Harmony.dll", "Mono.Cecil.dll" };

      private readonly MainWindow GUI;
      private readonly object SynRoot = new object();

      private bool Checking;

      public AppControl ( MainWindow _GUI ) => GUI = _GUI;

      public void CheckStatusAsync () {
         Task.Run( (Action) CheckStatus );
      }

      /// 1. If injector is in correct place = call injector to detect status
      /// 2. If injector is not in place, but dummy exists
      ///   Check Phoenix Point. Found = can setup. Not found = Error.
      /// 3. If dummy not exists = Error, re-download
      private void CheckStatus () {
         lock ( SynRoot ) {
            if ( Checking ) return;
            Checking = true;
         }
         Log( "Checking status" );
         try {
            Log( "Assembly: " + Assembly.GetExecutingAssembly().GetName().CodeBase );
            Log( "Working Dir: " + Directory.GetCurrentDirectory() );
            GUI.SetAppVer( CheckAppVer() );
            bool found = FoundGame( out string gamePath );
            if ( found )
               GUI.SetGamePath( gamePath );

            if ( CheckInjected( out string injectState ) ) {
               GUI.SetAppState( injectState );
               GUI.SetGameVer( CheckGameVer() );
            } else {
               if ( PackagesInPlace() && found ) {
                  GUI.SetAppState( "setup" );
               } else {
                  if ( found )
                     GUI.SetAppState( "missing" );
                  else
                     GUI.SetAppState( "no_game" );
               }
            }

         } finally {
            lock ( SynRoot ) {
               Checking = false;
            }
         }
      }

      public readonly string InjectorPath = Path.Combine( DLL_PATH, INJECTOR );
      public readonly string LoaderPath   = Path.Combine( DLL_PATH, LOADER   );

      /// Check that mod injector and mod loader is in place
      public bool InjectorInPlace () { try {
         if ( ! File.Exists( InjectorPath ) ) return Log( $"Missing injector: {InjectorPath}", false );
         if ( ! File.Exists( LoaderPath ) ) return Log( $"Missing loader: {LoaderPath}", false );
         return Log( $"Injector and loader found in {DLL_PATH}", true );
      } catch ( IOException ex ) { return Log( ex, false ); } }

      /// Return true if injectors are in place and injected.
      /// injectState may be null, "ppml", "modnix", or "error".
      public bool CheckInjected ( out string injectState ) {
         injectState = null;
         try {
            if ( ! InjectorInPlace() ) return false;
            Log( "Detecting injection status." );
            string state = RunAndWait( DLL_PATH, InjectorPath, "/d" ).Trim();
            if ( state == "ppml" || state == "modnix" ) {
               injectState = state;
               return true;
            }
            return Log( $"Unknown result: {state}", false );
         } catch ( Exception ex ) {
            injectState = "error";
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
         string ver = RunAndWait( DLL_PATH, InjectorPath, "/g" ).Trim();
         Log( "Game Version: " + ver );
         return ver;
      } catch ( Exception ex ) {
         return Log( ex, "error" );
      } }

      /// Check that all install package files are in place
      public bool PackagesInPlace () { try {
         foreach ( string file in PACKAGES ) {
            if ( File.Exists( file ) && new FileInfo( file ).Length > MIN_FILE_SIZE ) continue;
            return Log( $"Missing install file: " + file, false );
         }
         return Log( $"All {PACKAGES.Length} install files found", true );
      } catch ( IOException ex ) { return Log( ex, false ); } }

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

      #region Helpers
      private string RunAndWait ( string path, string exe, string param = null ) {
         Log( $"Running at {path} : {exe} {param}" );
         
         Process p = new Process();
         p.StartInfo.UseShellExecute = false;
         p.StartInfo.RedirectStandardOutput = true;
         p.StartInfo.FileName = exe;
         p.StartInfo.Arguments = param;
         p.StartInfo.WorkingDirectory = path;
         p.StartInfo.CreateNoWindow = true;
         p.StartInfo.WindowStyle = ProcessWindowStyle.Minimized;
         p.Start();

         string output = p.StandardOutput.ReadToEnd();
         p.WaitForExit();
         return output;
      }

      private void Log ( object message ) => GUI.Log( message.ToString() );

      private T Log<T> ( object message, T result ) {
         Log( message.ToString() );
         return result;
      }
      #endregion
   }
}
