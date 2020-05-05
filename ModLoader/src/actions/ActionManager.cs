using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix.Actions {

   internal class ActionManager {

      internal static void RunAction ( ModEntry mod, string phase ) {
         ModAction[] actions;
         lock ( mod.Metadata ) actions = mod.Metadata.Actions;
         if ( actions == null ) return;
         mod.CreateLogger().Info( "Running {0} actions" );

         List<ModAction> evals = null;
         foreach ( var a in actions ) try {
            if ( Array.IndexOf( a.Phase, phase ) < 0 ) continue;
            if ( a.Eval != null ) {
               if ( evals == null ) evals = new List<ModAction>();
               evals.Add( a );
            } else
               RunAction( a );
         } catch ( Exception ex ) { mod.CreateLogger().Error( ex ); }

         if ( evals != null ) try {
            EvalAction.Run( mod, evals.ToArray() );
         } catch ( Exception ex ) { mod.CreateLogger().Error( ex ); }
      }

      internal static void RunAction ( ModAction action ) {
         throw new NotImplementedException();
      }
   }

}