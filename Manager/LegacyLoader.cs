using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;

namespace Sheepy.Modnix.MainGUI {

   internal static class LegacyLoader {

      private const string PAST     = "PhoenixPointModLoaderInjector.exe";
      private const string PAST_DL1 = "PPModLoader.dll";
      private const string PAST_DL2 = "PhoenixPointModLoader.dll";
      private const string JBA_DLL  = "JetBrains.Annotations.dll";
      private const string INJECTOR = "ModnixInjector.exe";

      internal static string[] LEGACY_CODE = new string[] { AppRes.DOOR_DLL, AppRes.LOADER, AppRes.CECI_DLL, AppRes.HARM_DLL, AppControl.DOOR_CNF, INJECTOR, JBA_DLL, PAST, PAST_DL1, PAST_DL2 };
      internal static string[] UNSAFE_ROOT = new string[] { INJECTOR, JBA_DLL, PAST, PAST_DL1, PAST_DL2 };

      private static void Log ( object msg ) => AppControl.Instance.Log( msg );

      internal static bool FoundAndRenamePpmlInjector ( string codeDir, Func<string,string,bool> renamer ) { try {
         return File.Exists( Path.Combine( codeDir, PAST ) ) && renamer( PAST, PAST + ".orig" );
      } catch ( Exception ex ) { Log( ex ); return false; } }

      internal static bool FindLegacyInjection ( string codeDir ) => FindModnix2Injection( codeDir ) || FindPpmlInjection( codeDir );

      private static bool FindPpmlInjection ( string codeDir ) =>
         ScanInjectionTarget( Path.Combine( codeDir, "Assembly-CSharp.dll" ), "PhoenixPoint.Common.Game.PhoenixGame",
            "System.Void PhoenixPointModLoader.PPModLoader::Init(", "System.Void PhoenixPointModLoader.PhoenixPointModLoader::Initialize(" );

      private static bool FindModnix2Injection ( string codeDir ) =>
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
