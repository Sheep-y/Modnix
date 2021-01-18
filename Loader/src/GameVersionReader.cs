using Mono.Cecil;
using Sheepy.Logging;
using System;
using System.Linq;

namespace Sheepy.Modnix {

   public static class GameVersionReader {
      private static Logger Log => ModLoader.Log;

      public static string ParseVersionWithCecil ( string gameDllPath ) {
         using ( var dll = ModuleDefinition.ReadModule( gameDllPath ) ) {
            foreach ( var type in dll.Types ) {
               if ( type.FullName == "Base.Build.RuntimeBuildInfo" ) {
                  var method = type.Methods.FirstOrDefault( e => e.Name == "get_BuildVersion" );
                  if ( method?.HasBody == true ) return FindGameVersion2( method );
                  method = type.Methods.FirstOrDefault( e => e.Name == "get_Version" );
                  if ( method?.HasBody == true ) return FindGameVersion1( method );
                  return null;
               }
            }
         }
         return null;
      }

      // 1.0.58929 (2020-06-17) and below
      private static string FindGameVersion1 ( MethodDefinition method ) { try {
         var version = new int[2];
         var ldcCount = 0;
         foreach ( var code in method.Body.Instructions ) {
            var op = code.OpCode.ToString();
            if ( ! op.StartsWith( "ldc.i4" ) ) continue;
            version[ ldcCount ] = ParseI4Param( code );
            ++ldcCount;
         }
         if ( ldcCount != 2 ) { Log.Warn( "GameVer1: opcode {0} <> 2.", ldcCount ); return null; }
         return version[0].ToString() + '.' + version[1];
      } catch ( Exception ex ) { Log.Error( ex ); return null; } }

      // Danforth 1.5.2 (2020-07-07) and up
      private static string FindGameVersion2 ( MethodDefinition method ) { try {
         var version = new int[3];
         var ldcCount = 0;
         foreach ( var code in method.Body.Instructions ) {
            var op = code.OpCode.ToString();
            if ( ! op.StartsWith( "ldc.i4" ) ) continue;
            var ver = ParseI4Param( code );
            switch ( ldcCount ) {
               case 2 : version[ 0 ] = ver; break;
               case 4 : version[ 1 ] = ver; break;
               case 6 : version[ 2 ] = ver; break;
            }
            ++ldcCount;
         }
         if ( ldcCount != 8 ) { Log.Warn( "GameVer1: opcode {0} <> 8", ldcCount ); return null; }
         return version[0].ToString() + '.' + version[1] + '.' + version[2];
      } catch ( Exception ex ) { Log.Error( ex ); return null; } }

      private static int ParseI4Param ( Mono.Cecil.Cil.Instruction code ) {
         if ( code.Operand is int num ) return num;
         if ( code.Operand is sbyte num2 ) return num2;
         if ( code.OpCode.Code.Equals( Mono.Cecil.Cil.Code.Ldc_I4_M1 ) ) return -1;
         return int.Parse( code.OpCode.ToString().Substring( 7 ) );
      }
   }
}