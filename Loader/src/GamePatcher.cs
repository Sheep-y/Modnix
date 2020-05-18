using Harmony;
using Sheepy.Logging;
using System;
using System.Linq;
using System.Reflection;
using System.Text;
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
         patcher.Patch( GameMethod( "Base.View.GameView", "OnLevelStateChanged" ), ToHarmony( nameof( BeforeState ) ), ToHarmony( nameof( AfterState ) ) );
         Log.Verbo( "Patched GameView.OnLevelStateChanged" );
         Patcher = patcher;
         return true;
      } catch ( Exception ex ) {
         Log.Error( ex );
         return false;
      } }

      private static void MainPhase () {
         ModPhases.LoadMods( "Init" ); // PPML v0.1
         ModPhases.LoadMods( "Initialize" ); // PPML v0.2
         ModPhases.LoadMods( "MainMod" ); // Modnix
      }

      private static void BeforeState ( object __instance, object level, int prevState, int newState ) {
         Log.Info( "{0}.{1} Before {2} => {3}", level?.GetType(), __instance?.GetType(), prevState, newState );
      }

      private static void AfterState ( object __instance, object level, int prevState, int newState ) {
         Log.Info( "{0}.{1} After {2} => {3}", level?.GetType(), __instance?.GetType(), prevState, newState );
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