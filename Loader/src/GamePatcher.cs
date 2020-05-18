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
      private static Assembly GameAssembly;

      internal static bool PatchPhases () { try {
         var patcher = HarmonyInstance.Create( typeof( ModLoader ).Namespace );
         patcher.Patch(
            GetGameAssembly().GetType( "PhoenixPoint.Common.Game.PhoenixGame" ).GetMethod( "MenuCrt", NonPublic | Instance ),
            postfix: new HarmonyMethod( typeof( ModLoader ).GetMethod( nameof( MainPhase ), NonPublic | Static ) )
         );
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

      internal static Assembly GetGameAssembly () {
         if ( GameAssembly != null ) return GameAssembly;
         foreach ( var e in AppDomain.CurrentDomain.GetAssemblies() )
            if ( e.FullName.StartsWith( "Assembly-CSharp, ", StringComparison.OrdinalIgnoreCase ) )
               return GameAssembly = e;
         return null;
      }
   }
}