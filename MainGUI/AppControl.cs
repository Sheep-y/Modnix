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

      private static string DLL_PATH = "PhoenixPointWin64_Data/Managed";
      private static string INJECTOR = "ModnixInjector.exe";

      private readonly MainWindow GUI;
      private readonly object SynRoot = new object();

      private bool Checking;

      public AppControl ( MainWindow _GUI ) => GUI = _GUI;

      public void CheckAppStateAsync () {
         Task.Run( (Action) CheckAppState );
      }

      private void CheckAppState () {
         lock ( SynRoot ) {
            if ( Checking ) return;
            Checking = true;
         }
         Log( "Checking states" );
         try {
            GUI.SetAppVer( Assembly.GetExecutingAssembly().GetName().Version.ToString() );
            GUI.SetAppState( CheckInjected() );

         } finally {
            lock ( SynRoot ) {
               Checking = false;
            }
         }
      }

      public string CheckInjected () {
         string injector = Path.Combine( DLL_PATH, INJECTOR );
         try {
            if ( ! File.Exists( injector ) ) return "missing";

            Log( "Injector found. Detecting injection." );
            // Start the child process.
            Process p = new Process();
            // Redirect the output stream of the child process.
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = injector;
            p.StartInfo.Arguments = "/d";
            p.StartInfo.WorkingDirectory = DLL_PATH;
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            if ( output == "ppml" || output == "modnix" )
               return output;
            return "none";
         } catch ( Exception ex ) {
            Log( ex );
            return "none";
         }
      }

      private void Log ( object message ) => GUI.Log( message.ToString() );
   }

   public enum AppState {
      UNKNOWN = 0, // Should not happen

      CHECKING = 1,    // App state is being checked
      NEED_SETUP = 2,  // Not setup
      INJECTED = 4,    // Already injected
      NEED_UPDATE = 6, // Already injected, but needs update
      HAS_UPDATE = 8,  // Has update available on GitHub

      ERROR = 65536,
      ERR_MISSING = ERROR + 1, // Some files are missing
   } 
}
