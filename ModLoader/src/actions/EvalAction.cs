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

      private Logger Log => Mod.CreateLogger();

      public EvalAction ( ModEntry mod ) => this.Mod = mod;

      internal static void Run ( ModEntry mod, ModAction[] actions ) {
         if ( actions == null || actions.Length == 0 ) return;
         new EvalAction( mod ).Run( actions );
      }

      private static readonly string[] Assemblies = new string[]{ "0Harmony.dll", "Newtonsoft.Json.dll" };
      private static StringBuilder Usings;
      private static CompilerParameters Params;

      private static void PrepareReferences () {
         lock ( Assemblies ) {
            if ( Params != null ) return;
            ModLoader.Log.Info( "Initiating runtime compiler" );
            HashSet<string> names = new HashSet<string>();

            Params = new CompilerParameters();
            Params.CompilerOptions = "-t:library -p:x64 -o+";
            Params.GenerateInMemory = true;
            var refs = Params.ReferencedAssemblies;
            refs.Add( ModLoader.LoaderPath );
            foreach ( var f in Directory.EnumerateFiles( Path.GetDirectoryName( ModLoader.LoaderPath ) ) ) {
               var name = Path.GetFileName( f );
               if ( name.StartsWith( "System." ) || name.StartsWith( "Unity." ) || name.StartsWith( "UnityEngine." ) || Assemblies.Contains( name ) ) {
                  refs.Add( f );
                  names.Add( name );
               }
            }
            ModLoader.Log.Verbo( "Added references {0}", names.ToArray() );
            names.Clear();

            int capacity = 0;
            foreach ( var asm in AppDomain.CurrentDomain.GetAssemblies() ) try {
               Type[] types;
               if ( asm.IsDynamic || ! refs.Contains( asm.Location ) ) continue;
               ModLoader.Log.Verbo( asm.Location );
               try {
                  types = asm.GetTypes();
               } catch ( ReflectionTypeLoadException rtlex ) { // Happens on System.dll
                  types = rtlex.Types;
               }
               foreach ( var t in types ) {
                  if ( t?.IsPublic != true ) continue;
                  names.Add( t.Namespace );
               }
            } catch ( Exception ex ) { ModLoader.Log.Warn( ex ); }

            Usings = new StringBuilder( names.Sum( e => e.Length + 7 ) );
            foreach ( var name in names )
               Usings.Append( "using " ).Append( name ).Append( ';' );
            ModLoader.Log.Verbo( Usings );
         }
      }

      private void Run ( ModAction[] actions ) {
         var mainFunc = Compile( BuildActionCode( actions ) );
         if ( mainFunc == null ) return;
         Log.Verbo( "Calling compiled actions", actions );
         mainFunc.Invoke( null, new object[]{ Mod } );
      }

      private string BuildActionCode ( ModAction[] actions ) {
         PrepareReferences();
         Log.Verbo( "Buliding {0} eval actions", actions.Length );
         StringBuilder code = new StringBuilder( 2048 ), main = new StringBuilder( 256 );
         code.Append( "static class " ).Append( Mod.Key ).Append( "{" );
         code.Append( "static Sheepy.Modnix.ModEntry Mod;" );
         code.Append( "static object Api(string a,object p=null)=>Mod.ModAPI(a,p);" );
         int id = 0;
         foreach ( var act in actions ) {
            if ( act?.Eval == null ) continue; // TODO: Add random brackets to prevent jailbreak
            code.Append( "static void Action" ).Append( id ).Append( "(){" ).Append( act.Eval ).Append( ";}" );
            main.Append( "Action" ).Append( id++ ).Append( "();" );
         }
         code.Append( "public static void Eval(Sheepy.Modnix.ModEntry mod){" );
         code.Append( "Api(\"log\",\"Evaluating\");" );
         code.Append( main ).Append( "}}" );
         return new StringBuilder( Usings.Length + code.Length ).Append( Usings ).Append( code ).ToString();
      }

      private MethodInfo Compile ( string code ) {
         Log.Verbo( "Compiling {0} chars", code.Length );
         var result = new CSharpCodeProvider().CompileAssemblyFromSource( Params, code );
         if ( result.Errors.Count > 0 ) {
            Log.Error( "Cannot compile {0}", code );
            foreach ( var line in result.Errors )
               Log.Error( line );
            return null;
         }
         return result.CompiledAssembly.GetType( "global::" + Mod.Key ).GetMethod( "Eval" );
      }
   }
}
