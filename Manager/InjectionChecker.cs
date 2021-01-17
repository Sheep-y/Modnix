using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;

namespace Sheepy.Modnix.MainGUI {

   internal static class InjectionChecker {

      internal static bool FindPpmlInjection ( string codeDir ) =>
         ScanInjectionTarget( Path.Combine( codeDir, "Assembly-CSharp.dll" ), "PhoenixPoint.Common.Game.PhoenixGame",
            "System.Void PhoenixPointModLoader.PPModLoader::Init(", "System.Void PhoenixPointModLoader.PhoenixPointModLoader::Initialize(" );

      internal static bool FindModnix2Injection ( string codeDir ) =>
         ScanInjectionTarget( Path.Combine( codeDir, "Cinemachine.dll" ), "Cinemachine.CinemachineBrain", "System.Void Sheepy.Modnix.ModLoader::Init(" );

      private static bool ScanInjectionTarget ( string file, string typeName, params string[] hooks ) { try {
         using ( var dll = ModuleDefinition.ReadModule( file ) ) {
            var type = dll.GetType( typeName );
            if ( type == null ) return false;
            AppControl.Instance.Log( $"Scanning {typeName} of {file}" );
            return ScanTypeMethods( type, hooks );
         }
      } catch ( Exception ex ) { return AppControl.Instance.Log( ex, false ); } }

      private static bool ScanTypeMethods ( TypeDefinition type, string[] hooks ) {
         foreach ( var meth in type.Methods )
            if ( meth.Body != null && ScanInjectedCalls( meth, hooks ) )
               return true;
         foreach ( var subtype in type.NestedTypes )
            if ( ScanTypeMethods( subtype, hooks ) )
               return true;
         return false;
      }

      private static bool ScanInjectedCalls ( MethodDefinition meth, string[] hooks ) {
         foreach ( var instruction in meth.Body.Instructions ) {
            if ( ! instruction.OpCode.Equals( OpCodes.Call ) ) continue;
            var op = instruction.Operand.ToString();
            foreach ( var hook in hooks )
               if ( op.StartsWith( hook ) )
                  return true;
         }
         return false;
      }
   }
}
