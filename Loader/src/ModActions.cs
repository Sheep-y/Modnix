using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Sheepy.Modnix {
   using ActionDef = Dictionary<string,object>;

   public static class ModActions {

      internal const string DEFAULT_PHASE = "gamemod";
      private const string DEFAULT_PHASE_LOWER = "gamemod";

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
         var actions = FilterActions( mod.Metadata.Actions, phase.ToLowerInvariant() );

         var log = mod.Log();
         log.Verbo( "Running {0} actions", actions.Length );
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
               if ( result is Exception ex ) return;
            }
         }
         foreach ( var dll in ActionHandlers )
            ActionMods[ dll ].Log().Filters.Remove( modPrefix );
      } catch ( Exception ex ) { mod.Log().Error( ex ); } }

      private static ActionDef[] FilterActions ( ActionDef[] actions, string phase ) {
         phase = phase.ToLowerInvariant();
         var result = actions.Where( e => InList( e["phase"]?.ToString(), phase ) ).ToArray();
         return result.Length > 0 ? result : null;
      }

      private static void LogActionError ( Logger log, ActionDef act, Exception err ) {
         var directives = act.GetText( "onerror", "log" );
         if ( InList( directives, "log" ) || InList( directives, "error" ) ) log.Error( err );
         else if ( InList( directives, "warn" ) ) log.Warn( err );
         else if ( InList( directives, "info" ) ) log.Info( err );
         else if ( InList( directives, "verbo" ) ) log.Verbo( err );
      }

      internal static HashSet< string > FindPhases ( ActionDef[] actions ) {
         var found = new HashSet< string >();
         var hasDefault = false;
         foreach ( var act in actions ) {
            act.TryGetValue( "phase", out object phaseObj );
            var txt = phaseObj?.ToString()?.ToLowerInvariant();
            if ( ! string.IsNullOrWhiteSpace( txt ) ) {
               foreach ( var p in txt.Split( ',' ) )
                  if ( ! string.IsNullOrWhiteSpace( p ) )
                     found.Add( p.Trim() );
            } else
               hasDefault = true;
         }
         found.IntersectWith( ModPhases.PHASES_LOWER );
         if ( hasDefault ) found.Add( DEFAULT_PHASE_LOWER );
         return found.Count > 0 ? found : null;
      }

      private static readonly Dictionary< string, HashSet< string > > StrLists = new Dictionary<string, HashSet<string>>();

      private static bool InList ( string list, string val ) {
         if ( string.IsNullOrWhiteSpace( list ) ) return false;
         list = list.Trim().ToLowerInvariant();
         HashSet<string> parsed;
         lock ( StrLists ) {
            if ( ! StrLists.TryGetValue( list, out parsed ) ) {
               parsed = new HashSet<string>( list.Split( new char[]{ ',' }, StringSplitOptions.RemoveEmptyEntries ).Select( e => e.Trim() ) );
               if ( parsed.Count == 0 ) parsed = null;
               StrLists.Add( list, parsed );
            }
         }
         return parsed?.Contains( val ) == true;
      }

      internal static ActionDef[] Resolve ( ActionDef[] list ) {
         ActionDef defValues = null;
         var actions = new List<ActionDef>();
         foreach ( var a in list ) {
            if ( string.Equals( a.GetText( "action" ), "default", StringComparison.InvariantCultureIgnoreCase ) )
               MergeDefAction( ref defValues, a );
            else
               actions.Add( AddDefAction( a, defValues ) );
         }
         return actions.Count > 0 ? actions.ToArray() : null;
      }

      private static object RunActionHandler ( ModEntry mod, DllMeta dll, ActionDef act ) { try {
         var lib = ModPhases.LoadDll( mod, dll.Path );
         if ( lib == null ) return false;
         var handler = ActionMods[ dll ];
         object GetParamValue ( ParameterInfo pi ) => ParamValue( act, pi, mod, handler );

         object result;
         foreach ( var type in dll.Methods[ "ActionMod" ] ) {
            result = ModPhases.CallInit( mod, lib, type, "ActionMod", GetParamValue );
            if ( result is bool success ) {
               if ( success ) return true;
            } else if ( result is Exception ex ) {
               LogActionError( handler.Log(), act, ex );
               if ( InList( act.GetText( "onerror" ), "continue" ) ) continue;
               if ( InList( act.GetText( "onerror" ), "skip" ) ) return null;
               mod.Log().Info( "Aborting Actions. Set OnError to Continue or Skip to not abort." );
               return ex;
            } else if ( result != null )
               handler.Log().Error( "Unexpected ActionMod result: {0}", result.GetType() );
         }
         return null;
      } catch ( Exception ex ) { mod.Error( ex ); return null; } }

      private static object ParamValue ( ActionDef act, ParameterInfo arg, ModEntry actionMod, ModEntry handler ) {
         if ( arg.ParameterType == typeof( ActionDef ) )
            return act;
         if ( arg.ParameterType == typeof( string ) )
            return actionMod.Metadata.Id;
         return ModPhases.ParamValue( arg, handler );
      }

      private static string GetText ( this ActionDef act, string key, string fallback = null ) {
         if ( act.TryGetValue( key, out object o ) && o is string txt ) return txt;
         return fallback;
      }

      private static void MergeDefAction ( ref ActionDef defValues, ActionDef a ) {
         if ( defValues == null ) defValues = new ActionDef();
         foreach ( var e in a ) {
            if ( "action".Equals( e.Key ) ) continue;
            defValues[ e.Key ] = e.Value;
         }
      }

      private static ActionDef AddDefAction ( ActionDef a, ActionDef defValues ) {
         a = new ActionDef( a );
         if ( defValues == null ) return a;
         foreach ( var e in defValues )
            if ( ! a.ContainsKey( e.Key ) )
               a.Add( e.Key, e.Value );
         if ( ! a.ContainsKey( "phase" ) ) a.Add( "phase", DEFAULT_PHASE_LOWER );
         else a[ "phase" ] = a[ "phase" ].ToString().Trim().ToLowerInvariant();
         return a;
      }
   }

}