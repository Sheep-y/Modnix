using System;
using System.Collections.Generic;

namespace Harmony {
   [AttributeUsage(AttributeTargets.Method)]
   public sealed class HarmonyTargetMethod : Attribute {}

   [AttributeUsage(AttributeTargets.Method)]
   public sealed class HarmonyPrepare : Attribute {}

   [AttributeUsage(AttributeTargets.Method)]
   public sealed class HarmonyPrefix : Attribute {}

   [AttributeUsage(AttributeTargets.Method)]
   public sealed class HarmonyPostfix : Attribute {}

   [AttributeUsage(AttributeTargets.Method)]
   public sealed class HarmonyTranspiler : Attribute {}

   [AttributeUsage(AttributeTargets.Method)]
   public sealed class HarmonyCleanup : Attribute {}

   public class HarmonyAttribute : Attribute {
      public HarmonyMethod info = new HarmonyMethod();
   }

   [AttributeUsage( AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true )]
   public sealed class HarmonyPatch : HarmonyAttribute {
      public HarmonyPatch () { }

      public HarmonyPatch ( Type declaringType ) => info.declaringType = declaringType;

      public HarmonyPatch ( Type declaringType, Type[] argumentTypes ) {
         info.declaringType = declaringType;
         info.argumentTypes = argumentTypes;
      }

      public HarmonyPatch ( Type declaringType, string methodName ) {
         info.declaringType = declaringType;
         info.methodName = methodName;
      }

      public HarmonyPatch ( Type declaringType, string methodName, params Type[] argumentTypes ) {
         info.declaringType = declaringType;
         info.methodName = methodName;
         info.argumentTypes = argumentTypes;
      }

      public HarmonyPatch ( Type declaringType, string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations ) {
         info.declaringType = declaringType;
         info.methodName = methodName;
         ParseSpecialArguments( argumentTypes, argumentVariations );
      }

      public HarmonyPatch ( Type declaringType, MethodType methodType ) {
         info.declaringType = declaringType;
         info.methodType = new MethodType?( methodType );
      }

      public HarmonyPatch ( Type declaringType, MethodType methodType, params Type[] argumentTypes ) {
         info.declaringType = declaringType;
         info.methodType = new MethodType?( methodType );
         info.argumentTypes = argumentTypes;
      }

      public HarmonyPatch ( Type declaringType, MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations ) {
         info.declaringType = declaringType;
         info.methodType = new MethodType?( methodType );
         ParseSpecialArguments( argumentTypes, argumentVariations );
      }

      public HarmonyPatch ( Type declaringType, string propertyName, MethodType methodType ) {
         info.declaringType = declaringType;
         info.methodName = propertyName;
         info.methodType = new MethodType?( methodType );
      }

      public HarmonyPatch ( string methodName ) => info.methodName = methodName;

      public HarmonyPatch ( string methodName, params Type[] argumentTypes ) {
         info.methodName = methodName;
         info.argumentTypes = argumentTypes;
      }

      public HarmonyPatch ( string methodName, Type[] argumentTypes, ArgumentType[] argumentVariations ) {
         info.methodName = methodName;
         ParseSpecialArguments( argumentTypes, argumentVariations );
      }

      public HarmonyPatch ( string propertyName, MethodType methodType ) {
         info.methodName = propertyName;
         info.methodType = new MethodType?( methodType );
      }

      public HarmonyPatch ( MethodType methodType ) => info.methodType = new MethodType?( methodType );

      public HarmonyPatch ( MethodType methodType, params Type[] argumentTypes ) {
         info.methodType = new MethodType?( methodType );
         info.argumentTypes = argumentTypes;
      }

      public HarmonyPatch ( MethodType methodType, Type[] argumentTypes, ArgumentType[] argumentVariations ) {
         info.methodType = new MethodType?( methodType );
         ParseSpecialArguments( argumentTypes, argumentVariations );
      }

      public HarmonyPatch ( Type[] argumentTypes ) => info.argumentTypes = argumentTypes;

      public HarmonyPatch ( Type[] argumentTypes, ArgumentType[] argumentVariations )
         => ParseSpecialArguments( argumentTypes, argumentVariations );

      /*
      [Obsolete( "This attribute will be removed in the next major version. Use HarmonyPatch together with MethodType.Getter or MethodType.Setter instead" )]
      public HarmonyPatch ( string propertyName, PropertyMethod type ) {
         info.methodName = propertyName;
         info.methodType = new MethodType?( ( type == PropertyMethod.Getter ) ? MethodType.Getter : MethodType.Setter );
      }
      */

      private void ParseSpecialArguments ( Type[] argumentTypes, ArgumentType[] argumentVariations ) {
         if ( argumentVariations == null || argumentVariations.Length == 0 ) {
            info.argumentTypes = argumentTypes;
         } else {
            if ( argumentTypes.Length < argumentVariations.Length )
               throw new ArgumentException();
            List<Type> list = new List<Type>();
            for ( int i = 0 ; i < argumentTypes.Length ; i++ ) {
               Type type = argumentTypes[i];
               ArgumentType argumentType = argumentVariations[i];
               if ( argumentType - ArgumentType.Ref > 1 ) {
                  if ( argumentType == ArgumentType.Pointer )
                     type = type.MakePointerType();
               } else
                  type = type.MakeByRefType();
               list.Add( type );
            }
            info.argumentTypes = list.ToArray();
         }
      }
   }
}
