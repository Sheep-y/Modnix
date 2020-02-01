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
      private static string LOADER   = "ModnixLoader.dll";

      private readonly MainWindow GUI;
      private readonly object SynRoot = new object();

      private bool Checking;

      public AppControl ( MainWindow _GUI ) => GUI = _GUI;

      public void CheckAppStateAsync () {
         Task.Run( (Action) CheckAppState );
      }

      /// 1. If injector is in correct place = call injector to detect status
      /// 2. If injector is not in place, but dummy exists
      ///   Check Phoenix Point. Found = can setup. Not found = Error.
      /// 3. If dummy not exists = Error, re-download
      private void CheckAppState () {
         lock ( SynRoot ) {
            if ( Checking ) return;
            Checking = true;
         }
         Log( "Checking states" );
         try {
            GUI.SetAppVer( Assembly.GetExecutingAssembly().GetName().Version.ToString() );
            GUI.SetAppState( CheckInjected( out bool hasInjector ) );
            if ( hasInjector ) {

            } else {

            }

         } finally {
            lock ( SynRoot ) {
               Checking = false;
            }
         }
      }

      public readonly string InjectorPath = Path.Combine( DLL_PATH, INJECTOR );
      public readonly string LoaderPath   = Path.Combine( DLL_PATH, LOADER   );

      public bool InjectorInPlace () { try {
         if ( ! File.Exists( InjectorPath ) ) return Log( $"Injector not found: {InjectorPath}", false );
         if ( ! File.Exists( LoaderPath ) ) return Log( $"Loader not found: {LoaderPath}", false );
         return Log( $"Injector and loader found in {DLL_PATH}", true );
      } catch ( IOException ex ) {
         return Log( ex, false );
      } }

      public string CheckInjected ( out bool hasInjector ) {
         hasInjector = false;
         try {
            if ( ! InjectorInPlace() ) return "missing";
            Log( "Detecting injection status." );
            string state = RunAndWait( DLL_PATH, InjectorPath, "/d" ).Trim();
            if ( state == "ppml" || state == "modnix" ) {
               hasInjector = true;
               return state;
            }
            return Log( $"Unknown result: {state}", "none" );
         } catch ( Exception ex ) {
            return Log( ex, "error" );
         }
      }

      private string RunAndWait ( string path, string exe, string param = null ) {
         Log( $"Running at {path} : {exe} {param}" );
         
         Process p = new Process();
         p.StartInfo.UseShellExecute = false;
         p.StartInfo.RedirectStandardOutput = true;
         p.StartInfo.FileName = exe;
         p.StartInfo.Arguments = param;
         p.StartInfo.WorkingDirectory = path;
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
   }
}
