using Harmony;
using Sheepy.Logging;
using System;
using System.Reflection;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix {

   public static class GamePatcher {
      private static Logger Log => ModLoader.Log;

      private static object Patcher; // Type is not HarmonyInstance to avoid hard crash when harmony is missing

      internal static bool PatchPhases () { try {
         Log.Info( "Patching Phase entry points" );
         var patcher = HarmonyInstance.Create( typeof( ModLoader ).Namespace );
         patcher.Patch( GameMethod( "PhoenixPoint.Common.Game.PhoenixGame", "MenuCrt" ), postfix: ToHarmony( nameof( MainPhase ) ) );
         Log.Verbo( "Patched PhoenixGame.MenuCrt" );
         patcher.Patch( GameMethod( "Base.View.GameView", "OnLevelStateChanged" ), postfix: ToHarmony( nameof( AfterState ) ) );
         Log.Verbo( "Patched GameView.OnLevelStateChanged" );
         Patcher = patcher;
         return true;
      } catch ( Exception ex ) {
         Log.Error( ex );
         return false;
      } }

      private static void MainPhase () {
         ModPhases.RunPhase( "Init" ); // PPML v0.1
         ModPhases.RunPhase( "Initialize" ); // PPML v0.2
         ModPhases.RunPhase( "MainMod" ); // Modnix 1 & 2
      }

      private static void AfterState ( object __instance, int prevState, int newState ) {
         switch ( newState ) {
            case 0 : TriggerPhase( __instance, "Mod" ); break; // Anything => NotLoaded, will be called once during load and once during unload!
            case 5 : TriggerPhase( __instance, "OnShow" ); break; // Anything => Playing
            default :
               if ( prevState == 5 ) TriggerPhase( __instance, "OnHide" ); break; // Playing => Anything
         }
      }

      private static void TriggerPhase ( object instance, string trigger ) {
         var typeName = instance.GetType().Name;
         Log.Trace( "OnLevelStateChanged {0} {1}", typeName, trigger );
         var isOnHide = trigger == "OnHide";
         switch ( typeName ) {
            case "HomeScreenView" :
               ModPhases.RunPhase( "Home" + trigger );
               break;
            case "GeoscapeView" :
               if ( isOnHide ) ModPhases.RunPhase( "Game" + trigger );
               ModPhases.RunPhase( "Geoscape" + trigger );
               if ( isOnHide ) ModPhases.RunPhase( "GameOnHide" );
               break;
            case "TacticalView" :
               if ( isOnHide ) ModPhases.RunPhase( "Game" + trigger );
               ModPhases.RunPhase( "Tactical" + trigger );
               if ( isOnHide ) ModPhases.RunPhase( "GameOnHide" );
               break;
         }
      }

      private static Assembly _GameAssembly;

      internal static Assembly GameAssembly { get {
         if ( _GameAssembly != null ) return _GameAssembly;
         foreach ( var e in AppDomain.CurrentDomain.GetAssemblies() )
            if ( e.FullName.StartsWith( "Assembly-CSharp, ", StringComparison.OrdinalIgnoreCase ) )
               return _GameAssembly = e;
         return null;
      } }

      private static MethodInfo MyMethod ( string method ) => typeof( GamePatcher ).GetMethod( method, NonPublic | Static );

      private static MethodInfo GameMethod ( string type, string method ) => GameAssembly.GetType( type ).GetMethod( method, Public | NonPublic | Static | Instance );

      private static HarmonyMethod ToHarmony( string method ) => new HarmonyMethod( MyMethod( method ) );
   }
}