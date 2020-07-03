using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sheepy.Modnix {
   using ActionDef = Dictionary<string,object>;

   public static class ModActions {

      internal const string DEFAULT_PHASE = "gamemod";

      private static List<DllMeta> ActionHandlers;
      private static Dictionary<DllMeta,ModEntry> ActionMods;

      private static bool InitActionHandlers () { lock ( DEFAULT_PHASE ) {
         if ( ActionHandlers == null ) {
            if ( ! ModLoader.ModsInPhase.TryGetValue( "actionmod", out List<ModEntry> mods ) )
               return false;
            ActionHandlers = new List<DllMeta>();
            ActionMods = new Dictionary<DllMeta, ModEntry>();
            foreach ( var mod in mods )
               foreach ( var dll in mod.Metadata.Dlls )
                  if ( dll.Methods.ContainsKey( "ActionMod" ) ) {
                     ActionHandlers.Add( dll );
                     ActionMods.Add( dll, mod );
                  }
            if ( ActionHandlers.Count == 0 )
               ModLoader.Log.Error( "No action handlers found, please install mods to support actions.  Actions will not be processed." );
         }
         return ActionHandlers.Count > 0;
      } }

      internal static void RunActions ( ModEntry mod, string phase ) { try {
         phase = phase.ToLowerInvariant();
         var actions = mod.Metadata.Actions;
         if ( actions == null ) return;

         var log = mod.Log();
         log.Verbo( "Scanning {0} actions", actions.Length );
         if ( ! InitActionHandlers() ) return;

         var modPrefix = mod.PrefixFilter;
         foreach ( var dll in ActionHandlers ) {
            var handler = ActionMods[ dll ];
            if ( handler != mod )
               handler.Log().Filters.Add( modPrefix );
         }
         foreach ( var act in actions ) {
            foreach ( var dll in ActionHandlers ) {
               var result = RunActionHandler( mod, dll, act );
               if ( result is Exception ex && AbortOnError( log, act, ex ) )
                  return;
            }
         }
         foreach ( var dll in ActionHandlers )
            ActionMods[ dll ].Log().Filters.Remove( modPrefix );
      } catch ( Exception ex ) { mod.Log().Error( ex ); } }

      internal static bool AbortOnError ( Logger log, ActionDef act, Exception err ) {
         act.TryGetValue( "onerror", out object onerror );
         var handle = TrimAndLower( onerror ) ?? "log";
         if ( handle.IndexOf( "log" ) >= 0 ) log.Error( err );
         else if ( handle.IndexOf( "err" ) >= 0 ) log.Error( err );
         else if ( handle.IndexOf( "warn" ) >= 0 ) log.Warn( err );
         else if ( handle.IndexOf( "info" ) >= 0 ) log.Info( err );
         else if ( handle.IndexOf( "verbo" ) >= 0 ) log.Verbo( err );
         if ( handle.IndexOf( "stop" ) >= 0 ) {
            log.Info( "Aborting because OnError == Stop ({0})", handle );
            return true;
         }
         return false;
      }

      internal static bool PhaseMatch ( string actPhase, string phase ) {
         if ( actPhase == null ) return DEFAULT_PHASE.Equals( phase );
         return actPhase.IndexOf( phase ) >= 0;
      }

      internal static ActionDef[] MergeDefaultActions ( ActionDef[] list, out int defaultCount ) {
         defaultCount = 0;
         ActionDef defValues = null;
         var actions = new List<ActionDef>();
         foreach ( var a in list ) {
            if ( "default".Equals( GetActionField( a, defValues, "action" ) ) ) {
               MergeDefAction( ref defValues, a );
               defaultCount++;
               continue;
            } else
               actions.Add( AddDefAction( a, defValues ) );
         }
         return actions.Count > 0 ? actions.ToArray() : null;
      }

      public static object RunActionHandler ( ModEntry mod, DllMeta dll, ActionDef act ) { try {
         var lib = ModPhases.LoadDll( mod, dll.Path );
         if ( lib == null ) return false;
         var handler = ActionMods[ dll ];
         foreach ( var type in dll.Methods[ "ActionMod" ] ) {
            var result = ModPhases.CallInit( mod, lib, type, "ActionMod", ( e ) => ParamValue( act, e, mod, handler ) );
            if ( result is bool success ) {
               if ( success ) return true;
            } else if ( result is Exception ex )
               return ex;
            else if ( result != null )
               handler.Log().Info( "Unexpected ActionMod result: {0}", result.GetType() );
         }
         return null;
      } catch ( Exception ex ) { mod.Error( ex ); return null; } }

      internal static object ParamValue ( ActionDef act, ParameterInfo arg, ModEntry actionMod, ModEntry handler ) {
         if ( arg.ParameterType == typeof( ActionDef ) )
            return act;
         if ( arg.ParameterType == typeof( string ) )
            return actionMod.Metadata.Id;
         return ModPhases.ParamValue( arg, handler );
      }

      private static string TrimAndLower ( object key ) => key?.ToString().Trim().ToLowerInvariant();

      private static string GetActionField ( ActionDef action, ActionDef def, string key ) =>
         action.TryGetValue( key, out object val ) || def?.TryGetValue( key, out val ) == true
         ? TrimAndLower( val as string ) : null;

      private static void MergeDefAction ( ref ActionDef defValues, ActionDef a ) {
         if ( defValues == null ) defValues = new ActionDef();
         foreach ( var e in a ) {
            if ( "action".Equals( e.Key ) ) continue;
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