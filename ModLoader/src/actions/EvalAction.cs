using Microsoft.CSharp;
using Sheepy.Logging;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix.Actions {
   internal class EvalAction {

      private readonly ModEntry Mod;
      private readonly string Phase;

      private Logger Log => Mod.CreateLogger();

      public EvalAction ( ModEntry mod, string phase ) {
         Mod = mod;
         Phase = phase;
      }

      internal static void Run ( ModEntry mod, string phase, ModAction[] actions ) {
         if ( actions == null || actions.Length == 0 ) return;
         new EvalAction( mod, phase ).Run( actions );
      }

      private static readonly string[] Assemblies = new string[]{ "mscorlib.", "Assembly-CSharp.", "Cinemachine.", "0Harmony.", "Newtonsoft.Json." };
      private static StringBuilder Usings;

      private static void PrepareReferences () { lock ( Assemblies ) {
         if ( Usings != null ) return;
         ModLoader.Log.Info( "Building namespaces" );
         var CodePath = Path.GetDirectoryName( ModLoader.LoaderPath );

         HashSet<string> names = new HashSet<string>();
         foreach ( var asm in AppDomain.CurrentDomain.GetAssemblies() ) try {
            Type[] types;
            if ( asm.IsDynamic || ! CodePath.Equals( Path.GetDirectoryName( asm.Location ) ) ) continue;
            var name = asm.FullName;
            ModLoader.Log.Trace( name );
            if ( name.StartsWith( "System" ) || name.StartsWith( "Unity." ) || name.StartsWith( "UnityEngine." ) ||
                  Assemblies.Any( e => name.StartsWith( e ) ) ) {
               try {
                  types = asm.GetTypes();
               } catch ( ReflectionTypeLoadException rtlex ) { // Happens on System.dll
                  types = rtlex.Types;
               }
               foreach ( var t in types ) {
                  if ( t?.IsPublic != true ) continue;
                  if ( ! string.IsNullOrEmpty( t.Namespace ) )
                     names.Add( t.Namespace );
               }
            }
         } catch ( Exception ex ) { ModLoader.Log.Warn( ex ); }
         names.Remove( "System.Xml.Xsl.Runtime" );

         Usings = new StringBuilder( names.Sum( e => e.Length + 7 ) );
         foreach ( var name in names )
            Usings.Append( "using " ).Append( name ).Append( ';' );
         ModLoader.Log.Verbo( Usings );
      } }

      private void Run ( ModAction[] actions ) {
         var mainFunc = Compile( BuildActionCode( actions ) );
         if ( mainFunc == null ) return;
         Log.Verbo( "Calling compiled actions", actions );
         mainFunc.Invoke( null, new object[]{ Mod } );
      }

      private string BuildActionCode ( ModAction[] actions ) {
         PrepareReferences();
         Log.Verbo( "Building {0} eval actions", actions.Length );
         StringBuilder code = new StringBuilder( 8192 ), main = new StringBuilder( 512 );
         code.Append( "\nnamespace " ).Append( Mod.Key.Replace( '.', '_' ) ).Append( "{" );
         code.Append( "static class " ).Append( Phase ).Append( "{" );
         code.Append( "static Sheepy.Modnix.ModEntry Mod;" );
         code.Append( "static object Api(string a,object p=null){return Mod.ModAPI(a,p);}" );
         int id = 1;
         foreach ( var act in actions ) {
            if ( act?.Eval == null ) continue; // TODO: Add random brackets to prevent jailbreak
            code.Append( "static void Action" ).Append( id ).Append( "(){" ).Append( act.Eval ).Append( ";}" );
            main.Append( "Action" ).Append( id++ ).Append( "();" );
         }
         code.Append( "public static void Eval(Sheepy.Modnix.ModEntry mod){Mod=mod;" );
         code.Append( "Api(\"log\",\"Evaluating " ).Append( Phase ).Append( "\");" );
         code.Append( main ).Append( "}}}" );
         return new StringBuilder( Usings.Length + code.Length ).Append( Usings ).Append( code ).ToString();
      }

      private MethodInfo Compile ( string code ) {
         var output = Path.Combine( Path.GetDirectoryName( Mod.Path ), Mod.Key + "." + Phase + ".cs" );
         File.WriteAllText( output, code, Encoding.UTF8 );
         Log.Info( "{0} chars written to {1}", code.Length, output );
         return null;
      }
   }
}