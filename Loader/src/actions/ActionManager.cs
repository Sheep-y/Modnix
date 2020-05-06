using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix.Actions {

   internal class ActionManager {

      internal static void RunAction ( ModEntry mod, string phase ) { try {
         ModAction[] all;
         lock ( mod.Metadata ) all = mod.Metadata.Actions;
         if ( all == null ) return;

         var actions =  new List<ModAction>();
         foreach ( var a in all ) {
            if ( Array.IndexOf( a.Phase, phase ) < 0 ) continue;
            actions.Add( a );
         }
         if ( actions.Count == 0 ) return;

         mod.CreateLogger().Info( "Running {0} actions", actions.Count );
         foreach ( var a in actions ) {
            EvalAction.Run( mod, a );
         }

      } catch ( Exception ex ) { mod.CreateLogger().Error( ex ); } }
   }

}