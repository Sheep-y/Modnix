using Microsoft.CSharp;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix.Compiler {

   static class CompilerApp {

      private const int RC_OK = 0;
      private const int RC_ERROR = 1;
      private const int RC_NOT_FOUND = 2;
      private const int RC_SCAN_FAIL = 3;
      private const int RC_COMPILE_FAIL = 4;
      private const int RC_WRITE_FAIL = 5;

      static void Main ( string[] args ) {
         if ( args.Length == 0 ) ExitWithHelp();
         switch ( args[0].Trim().ToLowerInvariant() ) {
            case "-h": case "/h": case "--help": case "/help": case "?": case "/?":
               ExitWithHelp();
               break;
            case "-v": case "/v": case "--version": case "-version": case "/version":
               Exit( MyVersion.ToString() );
               break;
            default:
               Compile( args[0] );
               break;
         }
      }

      private static Version MyVersion => Assembly.GetExecutingAssembly().GetName().Version;
      private static string  MyPath => Assembly.GetExecutingAssembly().Location;
      private static string  MyFilename => Path.GetFileName( MyPath );

      private static void ExitWithHelp () {
         Console.WriteLine(  "Modnix Compiler " + MyVersion );
         Console.WriteLine(  "Compile generated mod eval code to dll" );
         Console.WriteLine( $"Usage: {MyFilename} [option] [input file]" );
         Console.WriteLine(  "Options:" );
         Console.WriteLine(  " -h Show this help" );
         Console.WriteLine(  " -v Show version and exit" );
         Exit();
      }

      private static void Compile ( string file ) { try {
         if ( ! File.Exists( file ) ) Exit( "File not found: " + file, RC_NOT_FOUND );
         string code = ReadFile( file );
         if ( code.Length == 0 ) Exit( "Empty file: " + file, RC_NOT_FOUND );
         var output = Path.Combine( Path.GetDirectoryName( file ), Path.GetFileNameWithoutExtension( file ) + ".dll" );
         PrepareCompiler( output );
         CompileAssembly( code );
      } catch ( Exception ex ) { Exit( ex.ToString(), RC_ERROR ); } }

      private static readonly string[] Assemblies = new string[]{ "0Harmony.dll", "Newtonsoft.Json.dll", "ModnixLoader.dll" };
      private static CompilerParameters Params;

      private static void PrepareCompiler ( string output ) {
         if ( Params != null ) return;

         Params = new CompilerParameters();
         Params.CompilerOptions = "-t:library -o+";
         var refs = Params.ReferencedAssemblies;

         var dotNetDir = Path.GetDirectoryName( typeof( string ).Assembly.Location );
         foreach ( var f in new string[] { "mscorlib.dll", "Microsoft.CSharp.dll" } )
            refs.Add( Path.Combine( dotNetDir, f ) );

         try {
            foreach ( var f in Directory.EnumerateFiles( "." ) ) {
               var name = Path.GetFileName( f );
               if ( name.StartsWith( "Unity." ) || name.StartsWith( "UnityEngine." ) || Assemblies.Contains( name ) ) {
                  refs.Add( f );
               } else if ( name.StartsWith( "System." ) ) {
                  var mirror = Path.Combine( dotNetDir, name );
                  if ( File.Exists( mirror ) )
                     refs.Add( mirror );
               }
            }
         } catch ( Exception ex ) {
            Console.WriteLine( ex );
            Exit( RC_SCAN_FAIL );
         }

         Params.OutputAssembly = output;
         try {
            if ( File.Exists( output ) )
               File.Delete( output );
         } catch ( SystemException ex ) {
            Console.WriteLine( ex.GetType() + " " + ex.Message );
            Exit( RC_WRITE_FAIL );
         }
      }

      private static void CompileAssembly ( string code ) { try {
         var result = new CSharpCodeProvider().CompileAssemblyFromSource( Params, code );
         if ( result.Errors.Count > 0 ) {
            foreach ( var line in result.Errors )
               Console.WriteLine( line );
            Exit( RC_COMPILE_FAIL );
         }
         foreach ( var f in result.TempFiles ) try {
            File.Delete( f.ToString() );
         } catch ( SystemException ex ) {
            Console.WriteLine( ex.GetType() + " " + ex.Message );
         }
         Console.WriteLine( result.PathToAssembly );
         Exit();
      } catch ( Exception ex ) {
         Console.WriteLine( ex );
         Exit( RC_COMPILE_FAIL );
      } }

      #region Helpers
      private static StreamReader Read ( string file ) =>
         new StreamReader( new FileStream( file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete ), Encoding.UTF8, true );

      private static string ReadFile ( string file ) { using ( var reader = Read( file ) ) return reader.ReadToEnd(); }

      private static void Exit ( int code = 0 ) => Environment.Exit( code );

      private static void Exit ( string message, int code = RC_OK ) {
         Console.WriteLine( message );
         Exit( code );
      }
      #endregion
   }
}
