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

      private readonly ModEntry mod;

      private Logger Log => mod.CreateLogger();

      public EvalAction ( ModEntry mod ) => this.mod = mod;

      internal static void Run ( ModEntry mod, ModAction[] actions ) => new EvalAction( mod ).Run( actions );

      private static readonly string[] Assemblies = new string[]{ "0Harmony.dll", "Newtonsoft.Json.dll", };
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
            foreach ( var asm in AppDomain.CurrentDomain.GetAssemblies() ) {
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
                  capacity += 7 + t.Namespace.Length;
               }
            }

            Usings = new StringBuilder( capacity );
            foreach ( var name in names )
               Usings.Append( "using " ).Append( name ).Append( ';' );
            ModLoader.Log.Verbo( Usings );
         }
      }

      private void Run ( ModAction[] actions ) {
         PrepareReferences();
         Log.Verbo( "Compiling {0} eval actions", actions.Length );
      }
   }
}
