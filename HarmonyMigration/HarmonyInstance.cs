using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace Harmony {
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
         => Me.PatchAll();

      public void PatchAll ( Assembly assembly )
         => Me.PatchAll( assembly );

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
         => Me = new HarmonyLib.HarmonyMethod();

      public HarmonyMethod ( MethodInfo method )
         => Me = new HarmonyLib.HarmonyMethod( method );

      public HarmonyMethod ( Type type, string name, Type[] parameters = null )
         => Me = new HarmonyLib.HarmonyMethod( type, name, parameters );

      public static List<string> HarmonyFields ()
         => HarmonyLib.HarmonyMethod.HarmonyFields();

      public static HarmonyMethod Merge ( List<HarmonyMethod> attributes ) {
         var result = HarmonyLib.HarmonyMethod.Merge( attributes.Select( e => e?.Me ).Where( e => e != null ).ToList() );
         if ( result == null ) return null;
         return new HarmonyMethod{ Me = result };
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

   internal static class HarmonyMigrationExt {
      internal static HarmonyLib.HarmonyPatchType ToV2 ( this HarmonyPatchType from ) {
         switch ( from ) {
            case HarmonyPatchType.Prefix : return HarmonyLib.HarmonyPatchType.Prefix;
            case HarmonyPatchType.Postfix : return HarmonyLib.HarmonyPatchType.Postfix;
            case HarmonyPatchType.Transpiler : return HarmonyLib.HarmonyPatchType.Transpiler;
         }
         return HarmonyLib.HarmonyPatchType.All;
      }
   }
}
