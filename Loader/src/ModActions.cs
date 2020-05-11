using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Sheepy.Modnix.Actions {
   using ActionDef = Dictionary<string,object>;

   public static class ModActions {

      internal const string ACTION_METHOD = "ActionMod";

      private static List<DllMeta> ActionHandlers;
      private static Dictionary<DllMeta,ModEntry> ActionMods;

      private static bool InitActionHandlers () { lock ( ACTION_METHOD ) {
         if ( ActionHandlers == null ) {
            ActionHandlers = new List<DllMeta>();
            ActionMods = new Dictionary<DllMeta, ModEntry>();
            foreach ( var mod in ModScanner.EnabledMods )
               if ( mod.Metadata.Dlls != null )
                  foreach ( var dll in mod.Metadata.Dlls )
                     if ( dll.Methods.ContainsKey( ACTION_METHOD ) ) {
                        ActionHandlers.Add( dll );
                        ActionMods.Add( dll, mod );
                     }
            if ( ActionHandlers.Count == 0 )
               ModLoader.Log.Error( "No action handlers found, please install mods to support actions.  Actions will not be processed." );
         }
         return ActionHandlers.Count > 0;
      } }

      internal static void RunActions ( ModEntry mod, string phase ) { try {
         ActionDef[] all = mod.Metadata.Actions;

         Logger log = mod.CreateLogger();
         log.Verbo( "Scanning {0} actions", all.Length );
         var actions = FilterActions( all, phase.ToLowerInvariant() );
         if ( actions == null ) return;
         if ( ! InitActionHandlers() ) return;

         log.Info( "Running {0} actions", actions.Count );
         foreach ( var act in actions ) {
            foreach ( var dll in ActionHandlers ) {
               var result = RunActionHandler( mod, dll, phase, act );
               if ( result is bool success && success ) continue;
               if ( result is Exception ex ) {
                  act.TryGetValue( "onerror", out object onerror );
                  var handle = TrimAndLower( onerror ) ?? "log";
                  if ( handle.IndexOf( "log" ) >= 0 ) log.Error( ex );
                  else if ( handle.IndexOf( "err" ) >= 0 ) log.Error( ex );
                  else if ( handle.IndexOf( "warn" ) >= 0 ) log.Warn( ex );
                  else if ( handle.IndexOf( "info" ) >= 0 ) log.Info( ex );
                  else if ( handle.IndexOf( "verbo" ) >= 0 ) log.Verbo( ex );
                  if ( handle.IndexOf( "stop" ) >= 0 ) {
                     log.Info( "Aborting because OnError == Stop" );
                     return;
                  }
               }
            }
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

      public static object RunActionHandler ( ModEntry mod, DllMeta dll, string phase, ActionDef act ) { try {
         var lib = ModPhases.LoadDll( mod, dll.Path );
         if ( lib == null ) return false;
         var handler = ActionMods[ dll ];
         foreach ( var type in dll.Methods[ ACTION_METHOD ] ) {
            var result = ModPhases.CallInit( mod, lib, type, ACTION_METHOD, ( e ) => ParamValue( act, e, mod, handler ) );
            if ( result is bool success && success ) return true;
            if ( result is Exception ex ) return ex;
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