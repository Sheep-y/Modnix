using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix {
   public class HarmonyMigration {
      private readonly static Harmony Patcher = new Harmony( typeof( HarmonyMigration ).Namespace );

      public static void PatchHarmony ( Assembly v1 ) {
         lock ( Patcher ) {
            //if ( Harmony.HasAnyPatches( Patcher.Id ) ) return;
            //Assembly v1 = Assembly.Load( Properties.Resources._0Harmony_v1 );
            ModLoader.Log.Info( "Patching {0}", v1.GetName() );
            var type = v1.GetType( "Harmony.HarmonyInstance" );
            Apply( type.GetMethod( "Create", AnyPublic ), postfix: nameof( Create ) );
            Apply( type.GetMethod( "GetPatchedMethods", AnyPublic ), nameof( GetPatchedMethods ) );
            Apply( type.GetMethod( "GetPatchInfo", AnyPublic ), nameof( GetPatchInfo ) );
            Apply( type.GetMethod( "HasAnyPatches", AnyPublic ), nameof( HasAnyPatches ) );
            Apply( type.GetMethod( "Patch", AnyPublic ), nameof( Patch ) );
            Apply( type.GetMethod( "PatchAll", AnyPublic, null, new Type[]{ typeof( Assembly ) }, null ), nameof( PatchAll ) );
            Apply( type.GetMethod( "UnpatchAll", AnyPublic ), nameof( UnpatchAll ) );
            Apply( type.GetMethod( "VersionInfo", AnyPublic ), nameof( VersionInfo ) );
            //var unpatches = type.GetMethods( AnyPublic ).Where( e => e.Name == "Unpatch" )
         }
      }

      #region Patch helpers
      private const BindingFlags AnyPublic = Public | Instance | Static;

      private static void Apply ( MethodBase method, string prefix = null, string postfix = null ) {
         ModLoader.Log.Info( "Patching {0}.{1}", method.DeclaringType, method.Name );
         var pre  = prefix  == null ? null : new HarmonyMethod( typeof( HarmonyMigration ).GetMethod( prefix , NonPublic | Static ) );
         var post = postfix == null ? null : new HarmonyMethod( typeof( HarmonyMigration ).GetMethod( postfix, NonPublic | Static ) );
         Patcher.Patch( method, pre, post );
      }
      #endregion

      #region Patch implementations
      private static Dictionary<string, WeakReference<Harmony>> CreatedInstance = new Dictionary<string, WeakReference<Harmony>>();

      private static string InstanceId ( object __instance ) {
         if ( __instance == null ) return null;
         return __instance.GetType().GetField( "id", Instance | NonPublic )?.GetValue( __instance )?.ToString();
      }

      private static Harmony MapInstance ( object from ) {
         string id = InstanceId( from );
         if ( id == null ) return null;
         if ( ! CreatedInstance.TryGetValue( id, out var reference ) || ! reference.TryGetTarget( out var result ) )
            CreatedInstance[ id ] = reference = new WeakReference<Harmony>( result = new Harmony( id ) );
         return result;
      }

      private static HarmonyMethod MapMethod ( object from ) {
         if ( from == null ) return null;
         var method = from.GetType().GetField( "method" ).GetValue( from );
         if ( method == null ) return null;
         return new HarmonyMethod( (MethodInfo) method );
      }
      #endregion

      private static void Create ( string id ) {
         lock ( CreatedInstance ) {
            CreatedInstance[ id ] = new WeakReference<Harmony>( new Harmony( id ) );
         }
      }

      private static bool GetPatchedMethods ( object __instance, ref IEnumerable<MethodBase> __result ) {
         __result = null;
         return false;
      }

      private static bool GetPatchInfo ( object __instance, MethodBase method, ref object __result ) {
         __result = null;
         return false;
      }

      private static bool HasAnyPatches ( string harmonyID, ref bool __result ) {
         __result = Harmony.HasAnyPatches( harmonyID );
         return false;
      }

      private static bool Patch ( object __instance, MethodBase original, object prefix, object postfix, object transpiler, DynamicMethod __result ) {
         ModLoader.Log.Info( "Patching {0}.{1}", original.DeclaringType, original.Name );
         __result = MapInstance( __instance ).Patch( original, MapMethod( prefix ), MapMethod( postfix ), MapMethod( transpiler ) ) as DynamicMethod;
         return false;
      }

      private static bool PatchAll ( Assembly assembly ) {
         return false;
      }

      private static bool Unpatch3 ( object __instance, MethodBase original, int type, string harmonyID ) {
         MapInstance( __instance ).Unpatch( original, (HarmonyPatchType) type, harmonyID );
         return false;
      }

      private static bool Unpatch2 ( object __instance, MethodBase original, MethodInfo patch ) {
         MapInstance( __instance ).Unpatch( original, patch );
         return false;
      }

      private static bool UnpatchAll ( object __instance, string harmonyID = null ) {
         MapInstance( __instance ).UnpatchAll( harmonyID );
         return false;
      }

      private static bool VersionInfo ( object __instance, out Version currentVersion, ref Dictionary<string, Version> __result ) {
         currentVersion = null;
         __result = null;
         return false;
      }
   }
}
