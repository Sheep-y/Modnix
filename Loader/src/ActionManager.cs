using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix.Actions {
   using ActionDef = Dictionary<string,object>;

   public static class ActionManager {

      internal static void RunAction ( ModEntry mod, string phase ) { try {
         ActionDef[] all;
         lock ( mod.Metadata ) all = mod.Metadata.Actions;
         if ( all == null ) return;
         var lowPhase = phase.ToLowerInvariant();

         var actions = FilterActions( all, lowPhase );
         if ( actions == null ) return;

         mod.CreateLogger().Info( "Running {0} actions", actions.Count );
         foreach ( var a in actions ) {
            //EvalAction.Run( mod, a );
         }

      } catch ( Exception ex ) { mod.CreateLogger().Error( ex ); } }

      public static List<ActionDef> FilterActions ( ActionDef[] list, string phase ) {
         phase = phase.ToLowerInvariant();
         ActionDef defValues = null;
         var actions = new List<ActionDef>();
         foreach ( var a in list ) {
            if ( "default".Equals( GetActionField( a, defValues, "action" ) ) ) {
               MergeDefAction( ref defValues, a );
               continue;
            }
            if ( ( GetActionField( a, defValues, "phase" ) ?? "mainmod" ).IndexOf( phase ) >= 0 ) // TODO: Change to gamemod
               actions.Add( AddDefAction( a, defValues ) );
         }
         return actions.Count > 0 ? actions : null;
      }

      private static string TrimAndLower ( string key ) => key?.Trim().ToLowerInvariant();

      private static string GetActionField ( ActionDef action, ActionDef def, string key ) =>
         action.TryGetValue( key, out object val ) || def?.TryGetValue( key, out val ) == true
         ? TrimAndLower( val as string ) : null;

      private static void MergeDefAction ( ref ActionDef defValues, ActionDef a ) {
         if ( defValues == null ) defValues = new ActionDef();
         foreach ( var e in a ) {
            if ( "action".Equals( e.Key ) && "default".Equals( TrimAndLower( e.Value as string ) ) ) continue;
            defValues.Add( e.Key, e.Value );
         }
      }

      private static ActionDef AddDefAction ( ActionDef a, ActionDef defValues ) {
         a = new ActionDef( a );
         if ( defValues == null ) return a;
         foreach ( var e in defValues )
            if ( ! a.ContainsKey( e.Key ) )
               a.Add( e.Key, e.Value );
         return a;
      }
   }

}