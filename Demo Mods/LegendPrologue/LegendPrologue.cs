using System;
using System.Reflection;
using Harmony;
using PhoenixPoint.Common.Levels.Params;
using PhoenixPoint.Home.View.ViewControllers;
using PhoenixPoint.Home.View.ViewModules;
using PhoenixPoint.Home.View.ViewStates;
using static System.Reflection.BindingFlags;

namespace Sheepy.PhoenixPt.LegendPrologue {
   public class Mod {
      public static void Init () => new Mod().MainMod(); // PPML compatibility

      public void MainMod ( Func< string, object, object > api = null ) { // Modnix entry point
         Mod.api = api; // For logging errors
         harmony = HarmonyInstance.Create( harmonyId );
         harmony.Patch( Method( typeof( UIModuleGameSettings ), "MainOptions_OnElementSelected" )  , postfix: Method( nameof( AfterOptionSelected_ShowTickbox ) ) );
         harmony.Patch( Method( typeof( UIStateNewGeoscapeGameSettings ), "OnSettingsBackClicked" ), postfix: Method( nameof( AfterBack_ClearFlag ) ) );
         harmony.Patch( Method( typeof( UIStateNewGeoscapeGameSettings ), "CreateSceneBinding" ), Method( nameof( BeforeSceneBind_CheckFlag ) ) );
      }

      public static void DisarmMod () {
         harmony.UnpatchAll( harmonyId ); // Remove all patches!
         harmony = null; // And release resources.
         api = null;
      }

      private static GameOptionViewController TutorialBox; // The prologue tick box

      private static void AfterOptionSelected_ShowTickbox ( UIModuleGameSettings __instance ) { LogError( () => {
         TutorialBox = __instance?.SecondaryOptions?.Elements?[ 0 ]; // Find tick box
         TutorialBox?.gameObject.SetActive( true ); // And always show it
      } ); }

      private static void AfterBack_ClearFlag () => TutorialBox = null; // Cleanup when back from new game screen

      private static void BeforeSceneBind_CheckFlag ( GeoscapeGameParams gameParams ) { LogError( () => {
         if ( TutorialBox?.IsSelected == true ) gameParams.TutorialEnabled = true; // Apply tickbox always
         TutorialBox = null;
      } ); }

      // fields and utilities
      private static Func< string, object, object > api;
      private static HarmonyInstance harmony = HarmonyInstance.Create( harmonyId );
      private static string harmonyId => typeof( Mod ).Namespace;

      private static HarmonyMethod Method ( string name ) => new HarmonyMethod( Method( typeof( Mod ), name ) );
      private static MethodInfo Method ( Type type, string name ) => type.GetMethod( name, Public | NonPublic | Instance | Static );

      private static void LogError ( Action action ) { // Call an action and, if error happens, try to log it.
         try {
            action();
         } catch ( Exception ex ) {
            api?.Invoke( "log error", ex );
         }
      }
   }
}