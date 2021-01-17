using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;

namespace Sheepy.Modnix.MainGUI {

   internal static class InjectionChecker {

      private static void Log ( object msg ) => AppControl.Instance.Log( msg );

      internal static bool FindPpmlInjection ( string codeDir ) =>
         ScanInjectionTarget( Path.Combine( codeDir, "Assembly-CSharp.dll" ), "PhoenixPoint.Common.Game.PhoenixGame",
            "System.Void PhoenixPointModLoader.PPModLoader::Init(", "System.Void PhoenixPointModLoader.PhoenixPointModLoader::Initialize(" );

      internal static bool FindModnix2Injection ( string codeDir ) =>
         ScanInjectionTarget( Path.Combine( codeDir, "Cinemachine.dll" ), "Cinemachine.CinemachineBrain", "System.Void Sheepy.Modnix.ModLoader::Init(" );

      internal static void RestoreBackup ( string codeDir ) {
         if ( FindPpmlInjection( codeDir ) ) RestoreFile( Path.Combine( codeDir, "Assembly-CSharp.dll" ) );
         if ( FindModnix2Injection( codeDir ) ) RestoreFile( Path.Combine( codeDir, "Cinemachine.dll" ) );
      }

      private static bool ScanInjectionTarget ( string file, string typeName, params string[] hooks ) { try {
         using ( var dll = ModuleDefinition.ReadModule( file ) ) {
            var type = dll.GetType( typeName );
            if ( type == null ) return false;
            Log( $"Scanning {typeName} of {file}" );
            return ScanTypeMethods( type, hooks );
         }
      } catch ( Exception ex ) { Log( ex ); return false; } }

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
               if ( op.StartsWith( hook ) ) {
                  Log( $"Found injected call to {hook})" );
                  return true;
               }
         }
         return false;
      }

      private static void RestoreFile ( string file ) { try {
         var backup = file + ".orig";
         if ( ! File.Exists( backup ) ) { Log( $"Not found: {backup}" ); return; }
         File.Copy( backup, file, true );
      } catch ( Exception ex ) { Log( ex ); } }
   }
}
