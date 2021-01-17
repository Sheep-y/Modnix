using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix.MainGUI {
   [Flags]
   internal enum InjectionState { NONE, MODNIX , PPML, BOTH = MODNIX | PPML }

   internal class InjectionChecker {
      internal InjectionState CheckInjectionOf ( string target ) {
         using ( var dll = ModuleDefinition.ReadModule( target ) ) {
            foreach ( var typeName in new string[]{ "PhoenixPoint.Common.Game.PhoenixGame", "Cinemachine.CinemachineBrain" } ) {
               var type = dll.GetType( typeName );
               if ( type == null ) continue;
               var result = CheckInjection( type );
               if ( result != InjectionState.NONE ) return result;
            }
         }
         return InjectionState.NONE;
      }

      private InjectionState CheckInjection ( TypeDefinition typeDefinition ) {
         // Check standard methods, then in places like IEnumerator generated methods (Nested)
         var result = typeDefinition.Methods.Select( CheckInjection ).FirstOrDefault( e => e != InjectionState.NONE );
         if ( result != InjectionState.NONE ) return result;
         return typeDefinition.NestedTypes.Select( CheckInjection ).FirstOrDefault( e => e != InjectionState.NONE );
      }

      private static readonly string ModnixInjectCheck = $"System.Void Sheepy.Modnix.ModLoader::Init(";
      private static readonly string PPML01InjectCheck = $"System.Void PhoenixPointModLoader.PPModLoader::Init(";
      private static readonly string PPML02InjectCheck = $"System.Void PhoenixPointModLoader.PhoenixPointModLoader::Initialize(";

      private static InjectionState CheckInjection ( MethodDefinition methodDefinition ) {
         if ( methodDefinition.Body == null )
            return InjectionState.NONE;
         foreach ( var instruction in methodDefinition.Body.Instructions ) {
            if ( ! instruction.OpCode.Equals( OpCodes.Call ) ) continue;
            var op = instruction.Operand.ToString();
            if ( op.StartsWith( ModnixInjectCheck ) )
               return InjectionState.MODNIX;
            else if ( op.StartsWith( PPML01InjectCheck ) || op.StartsWith( PPML02InjectCheck ) )
               return InjectionState.PPML;
         }
         return InjectionState.NONE;
      }
   }
}
