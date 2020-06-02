using Harmony;
using Sheepy.Logging;
using System;
using System.Diagnostics;
using System.Reflection;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix {

   public static class GamePatcher {
      private static Logger Log => ModLoader.Log;

      internal static bool PatchPhases () { try {
         Log.Info( "Patching Phase entry points" );
         var patcher = HarmonyInstance.Create( typeof( ModLoader ).Namespace );
         patcher.Patch( GameMethod( "PhoenixPoint.Common.Game.PhoenixGame", "MenuCrt" ), postfix: ToHarmony( nameof( MainPhase ) ) );
         Log.Verbo( "Patched PhoenixGame.MenuCrt" );
         patcher.Patch( GameMethod( "PhoenixPoint.Common.Levels.MenuLevelController", "OnLevelStateChanged" ), postfix: ToHarmony( nameof( AfterHomeState ) ) );
         patcher.Patch( GameMethod( "PhoenixPoint.Geoscape.Levels.GeoLevelController", "OnLevelStateChanged" ), postfix: ToHarmony( nameof( AfterGeoState ) ) );
         patcher.Patch( GameMethod( "PhoenixPoint.Tactical.Levels.TacticalLevelController", "OnLevelStateChanged" ), postfix: ToHarmony( nameof( AfterTacState ) ) );
         patcher.Patch( GameMethod( "PhoenixPoint.Common.UI.LoadingTipsController", "HideTip" ), postfix: ToHarmony( nameof( AfterHideTip ) ) );
         //patcher.Patch( GameMethod( "Base.Core.Game", "QuitGame" ), postfix: ToHarmony( nameof( BeforeQuit ) ) );
         //patcher.Patch( GameMethod( "Base.Platforms.Platform", "Abort" ), postfix: ToHarmony( nameof( BeforeQuit ) ) );
         Log.Verbo( "Patched OnLevelStateChanged and HideTip" );
         foreach ( var e in AppDomain.CurrentDomain.GetAssemblies() )
            if ( e.FullName.StartsWith( "UnityEngine.CoreModule,", StringComparison.OrdinalIgnoreCase ) ) try {
               patcher.Patch( e.GetType( "UnityEngine.Application" ).GetMethod( "Quit", new Type[] { } ), ToHarmony( nameof( BeforeQuit ) ) );
            } catch ( Exception ex ) { Log.Warn( ex ); }
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

      private static void AfterHomeState ( int prevState, int newState ) => StateChanged( "Home", prevState, newState );

      private static void AfterGeoState  ( int prevState, int state ) => StateChanged( "Geoscape", prevState, state );

      private static void AfterTacState  ( int prevState, int state ) => StateChanged( "Tactical", prevState, state );

      private static void AfterHideTip () {
         if ( ! FireGeoscapeOnShow ) return;
         ModPhases.RunPhase( "GameOnShow" );
         ModPhases.RunPhase( "GeoscapeOnShow" );
         FireGeoscapeOnShow = false;
      }

      // Mainly call GeoscapeOnHide and GameOnHide when quiting from Geoscape, which seems to not trigger OnLevelStateChanged.
      // May also be called if an exception bubbles to the main game loop.
      private static void BeforeQuit  () {
         Log.Info( "Game Quit" );
         if ( ModPhases.LastPhase?.EndsWith( "OnShow", StringComparison.Ordinal ) != true ) return;
         ModPhases.RunPhase( ModPhases.LastPhase.Replace( "OnShow", "OnHide" ) );
         if ( ModPhases.LastPhase != "HomeOnHide" ) ModPhases.RunPhase( "GameOnHide" );
         Log.Flush();
      }

      private static bool FireGeoscapeOnShow;
         
      private static void StateChanged ( string level, int prevState, int newState ) {
         //Log.Trace( "{0} StateChanged {1} => {2}", level, prevState, newState );
         switch ( prevState ) {
            case -1 : // Uninitialized => NotLoaded
               if ( level != "Home" ) ModPhases.RunPhase( "GameMod" );
               ModPhases.RunPhase( level + "Mod" );
               return;
            case 5 : // Playing => Loaded
               if ( ModPhases.LastPhase?.EndsWith( "OnHide", StringComparison.Ordinal ) == true ) return;
               ModPhases.RunPhase( level + "OnHide" );
               if ( level != "Home" ) ModPhases.RunPhase( "GameOnHide" );
               return;
            case 2 : // Loaded => Playing, OR Loaded => Unloading
               if ( newState != 5 ) return;
               if ( level == "Geoscape" )
                  FireGeoscapeOnShow = true;
               else {
                  if ( level != "Home" ) ModPhases.RunPhase( "GameOnShow" );
                  ModPhases.RunPhase( level + "OnShow" );
               }
               return;
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

      private static MethodInfo GameMethod ( string type, string method ) => GameAssembly.GetType( type ).GetMethod( method, Public | NonPublic | Static | Instance );

      private static HarmonyMethod ToHarmony( string method ) => new HarmonyMethod( typeof( GamePatcher ).GetMethod( method, NonPublic | Static ) );
   }
}