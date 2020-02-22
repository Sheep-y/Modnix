using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;

namespace Harmony {

   public static class FileLog {
      public static void Log ( string str ) {
         HarmonyLib.FileLog.Log( str );
      }
   }

   public class HarmonyInstance {
      public static bool DEBUG;
      public string Id => Me.Id;
      private HarmonyLib.Harmony Me;

      public static HarmonyInstance Create ( string id )
         => new HarmonyInstance{ Me = new HarmonyLib.Harmony( id ) };

      public IEnumerable<MethodBase> GetPatchedMethods ()
         => Me.GetPatchedMethods();

      //public Patches GetPatchInfo ( MethodBase method );

      public bool HasAnyPatches ( string harmonyID )
         => HarmonyLib.Harmony.HasAnyPatches( harmonyID );

      public DynamicMethod Patch ( MethodBase original, HarmonyMethod prefix = null, HarmonyMethod postfix = null, HarmonyMethod transpiler = null )
         => Me.Patch( original, prefix?.Me, postfix?.Me, transpiler?.Me ) as DynamicMethod;

      public void PatchAll ()
         => PatchAll( Assembly.GetExecutingAssembly() );

      public void PatchAll ( Assembly assembly ) {
         foreach ( Type type in assembly.GetTypes() ) {
            var methods = type.GetCustomAttributes( typeof(HarmonyAttribute), true )
               .Select( e => ( e as HarmonyAttribute )?.info?.SyncTo() )
               .ToList();
            if ( methods == null || methods.Count <= 0 ) return;
            DoPatch( type, HarmonyLib.HarmonyMethod.Merge( methods ) );
         }
      }

      private void DoPatch ( Type type, HarmonyLib.HarmonyMethod info ) {
         var target = FindPatchMethod( type, typeof( HarmonyTargetMethod ), "TargetMethod" );
         MethodBase subject = InvokeWithParam( target, this, info.method );
         if ( subject == null ) subject = FindOriginalMethod( info );

         var prepare = FindPatchMethod( type, typeof( HarmonyPrepare ), "Prepare" );
         if ( ! InvokeWithParam( prepare, info.method, true ) ) return;

         var pre = FindPatchMethod( type, typeof( HarmonyPrefix ), "Prefix" );
         var post = FindPatchMethod( type, typeof( HarmonyPostfix ), "Postfix" );
         var trans = FindPatchMethod( type, typeof( HarmonyTranspiler ), "Transpiler" );
         Me.Patch( subject, new HarmonyLib.HarmonyMethod( pre ), new HarmonyLib.HarmonyMethod( post ), new HarmonyLib.HarmonyMethod( trans ) );

         var cleanup = FindPatchMethod( type, typeof( HarmonyCleanup ), "Cleanup" );
         _ = InvokeWithParam( cleanup, info.method, true );
      }

      private MethodBase FindOriginalMethod ( HarmonyLib.HarmonyMethod info ) {
         switch ( info.methodType ) {
            case HarmonyLib.MethodType.Normal :
               return AccessTools.Method( info.declaringType, info.methodName, info.argumentTypes );
            case HarmonyLib.MethodType.Getter :
               return AccessTools.PropertyGetter( info.declaringType, info.methodName );
            case HarmonyLib.MethodType.Setter :
               return AccessTools.PropertySetter( info.declaringType, info.methodName );
            case HarmonyLib.MethodType.Constructor :
               return AccessTools.Constructor( info.declaringType, info.argumentTypes, false );
            case HarmonyLib.MethodType.StaticConstructor :
               return AccessTools.Constructor( info.declaringType, info.argumentTypes, true );
         }
         return null;
      }

      private const BindingFlags MethodFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

      private static MethodInfo FindPatchMethod ( Type type, Type attr, string name ) {
         var fixedName = type.GetMethod( name, MethodFlags );
         if ( fixedName != null ) return fixedName;
         foreach ( var m in type.GetMethods( MethodFlags ) )
            if ( m.GetCustomAttribute( attr ) != null )
               return m;
         return null;
      }

      private static T InvokeWithParam<T> ( MethodInfo target, object param, T defVal ) {
         if ( target == null ) return defVal;
         var args = target.GetParameters().Length;
         if ( args == 0 ) return (T) target.Invoke( null, new object[] { } );
         if ( args == 1 ) return (T) target.Invoke( null, new object[] { param } );
         return defVal;
      }

      public void Unpatch ( MethodBase original, HarmonyPatchType type, string harmonyID = null )
         => Me.Unpatch( original, type.ToV2(), harmonyID );

      public void Unpatch ( MethodBase original, MethodInfo patch )
         => Me.Unpatch( original, patch );

      public void UnpatchAll ( string harmonyID = null )
         => Me.UnpatchAll( harmonyID );

      public Dictionary<string, Version> VersionInfo ( out Version currentVersion )
         => HarmonyLib.Harmony.VersionInfo( out currentVersion );
   }

   public class HarmonyMethod {
      public MethodInfo method;
      public Type declaringType;
      public string methodName;
      public MethodType? methodType;
      public Type[] argumentTypes;
      public int prioritiy;
      public string[] before;
      public string[] after;
      internal HarmonyLib.HarmonyMethod Me;

      public HarmonyMethod ()
         => SyncFrom( new HarmonyLib.HarmonyMethod() );

      public HarmonyMethod ( MethodInfo method )
         => SyncFrom( new HarmonyLib.HarmonyMethod( method ) );

      public HarmonyMethod ( Type type, string name, Type[] parameters = null )
         => SyncFrom( new HarmonyLib.HarmonyMethod( type, name, parameters ) );

      public static List<string> HarmonyFields ()
         => HarmonyLib.HarmonyMethod.HarmonyFields();

      public static HarmonyMethod Merge ( List<HarmonyMethod> attributes ) {
         var result = HarmonyLib.HarmonyMethod.Merge( attributes.Select( e => e?.SyncTo() ).Where( e => e != null ).ToList() );
         if ( result == null ) return null;
         return new HarmonyMethod().SyncFrom( result );
      }

      internal HarmonyMethod SyncFrom ( HarmonyLib.HarmonyMethod me ) {
         if ( me == null ) me = Me;
         else Me = me;
         declaringType = me.declaringType;
         method = me.method;
         methodName = me.methodName;
         methodType = me.methodType?.ToV1();
         argumentTypes = me.argumentTypes;
         prioritiy = me.priority;
         before = me.before;
         after = me.after;
         return this;
      }

      internal HarmonyLib.HarmonyMethod SyncTo () {
         Me.declaringType = declaringType;
         Me.method = method;
         Me.methodName = methodName;
         Me.methodType = methodType?.ToV2();
         Me.argumentTypes = argumentTypes;
         Me.priority = prioritiy;
         Me.before = before;
         Me.after = after;
         return Me;
      }

      public override string ToString () => Me.ToString();
   }

   public enum HarmonyPatchType {
      All = 0,
      Prefix = 1,
      Postfix = 2,
      Transpiler = 3
   }
   
   public enum MethodType {
      Normal = 0,
      Getter = 1,
      Setter = 2,
      Constructor = 3,
      StaticConstructor = 4
   }
   
   public enum ArgumentType {
      Normal = 0,
      Ref = 1,
      Out = 2,
      Pointer = 3
   }

   public static class HarmonyMethodExtensions {
      public static List<HarmonyMethod> GetHarmonyMethods ( this Type type ) {
         return ( from HarmonyAttribute hattr in
                     from attr in type.GetCustomAttributes( true )
                     where attr is HarmonyAttribute
                     select attr
                  select hattr.info ).ToList();
      }
      internal static HarmonyLib.HarmonyPatchType ToV2 ( this HarmonyPatchType from ) {
         return (HarmonyLib.HarmonyPatchType) (int) from;
      }
      internal static HarmonyLib.MethodType ToV2 ( this MethodType from ) {
         return (HarmonyLib.MethodType) (int) from;
      }
      internal static MethodType ToV1 ( this HarmonyLib.MethodType from ) {
         return (MethodType) (int) from;
      }
   }
}