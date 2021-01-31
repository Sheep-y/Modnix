using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using static Sheepy.Modnix.Tools;

namespace Sheepy.Modnix {
   using ActionDef = Dictionary<string,object>;
   using IAction = IDictionary<string,object>;

   public static class ModActions {

      internal const string DEFAULT_PHASE = "Gamemod";
      private const string DEFAULT_PHASE_LOWER = "gamemod";

      private static DllMeta[] ActionHandlers;
      private static Dictionary<DllMeta,ModEntry> ActionMods;

      private static bool InitActionHandlers () { lock ( ModLoader.ModsInPhase ) {
         if ( ActionHandlers == null ) {
            if ( ! ModLoader.ModsInPhase.TryGetValue( "actionmod", out List<ModEntry> mods ) )
               return false;
            var Handlers = new List<DllMeta>();
            ActionMods = new Dictionary<DllMeta, ModEntry>();
            foreach ( var mod in mods )
               foreach ( var dll in mod.Metadata.Dlls )
                  if ( dll.Methods.ContainsKey( "ActionMod" ) ) {
                     Handlers.Add( dll );
                     ActionMods.Add( dll, mod );
                  }
            ActionHandlers = Handlers.ToArray();
            if ( Handlers.Count == 0 )
               ModLoader.Log.Error( "No action handlers found, please install mods to support actions.  Actions will not be processed." );
         }
         return ActionHandlers.Length > 0;
      } }

      internal static void RunActions ( ModEntry mod, string phase ) { try {
         var actions = FilterActions( mod.Metadata.Actions, phase.ToLowerInvariant() );

         var log = mod.Log();
         log.Verbo( "Running {0} actions", actions.Length );
         if ( ! InitActionHandlers() ) return;

         var modPrefix = mod.PrefixFilter;
         foreach ( var act in actions ) {
            foreach ( var dll in ActionHandlers ) {
               var handler = ActionMods[ dll ];
               if ( handler != mod ) handler.Log().Filters.Add( modPrefix );
               var result = RunActionHandler( mod, dll, act );
               if ( handler != mod ) handler.Log().Filters.Remove( modPrefix );
               if ( result is bool flag && flag )
                  goto NextAction;
               if ( result is Exception )
                  return;
            }
            Func<string> stringify = () => Json.Stringify( act );
            LogActionError( log, act, "Unhandled action: {0}", stringify );
NextAction:;
         }
      } catch ( Exception ex ) { mod.Log().Error( ex ); } }

      private static IAction[] FilterActions ( IAction[] actions, string phase ) {
         phase = phase.ToLowerInvariant();
         var result = actions.Where( e => InList( e.SafeGet( "phase" )?.ToString() ?? DEFAULT_PHASE_LOWER, phase ) ).ToArray();
         return result.Length > 0 ? result : null;
      }

      private static void LogActionError ( Logger log, IAction act, object err, params object[] args ) {
         var directives = act.GetText( "onerror", "log" );
         if ( InList( directives, "log" ) || InList( directives, "error" ) ) log.Error( err, args );
         else if ( InList( directives, "warn" ) ) log.Warn( err, args );
         else if ( InList( directives, "info" ) ) log.Info( err, args );
         else if ( InList( directives, "verbo" ) ) log.Verbo( err, args );
         else if ( InList( directives, "silent" ) ) return;
         else log.Error( err, args );
      }

      internal static HashSet< string > FindPhases ( IAction[] actions ) {
         var found = new HashSet< string >();
         var hasDefault = false;
         foreach ( var act in actions ) {
            var txt = act.SafeGet( "phase" )?.ToString()?.ToLowerInvariant();
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

      internal static IAction[] Resolve ( ModEntry mod, IAction[] list ) { try {
         IAction defValues = null;
         var defCount = 0;
         AddToModActionFiles( mod, mod.Path );
         var result = PreprocessActions( mod, list, mod.Dir, ref defValues, ref defCount, 0 );
         if ( defCount > 0 ) mod.Log().Verbo( "Merged {0} default actions.", defCount );
         return result;
      } catch ( Exception ex ) {
         mod.Log().Error( ex );
         return new IAction[0];
      } }

      private static IAction[] PreprocessActions ( ModEntry mod, IAction[] list, string basedir, ref IAction defValues, ref int defCount, int level ) {
         var actions = new List< IAction >();
         foreach ( var a in list ) {
            if ( a.GetText( "include" ) is string file )
               actions.AddRange( LoadInclude( mod, basedir, file, ref defValues, ref defCount, level + 1 ) );
            else if ( string.Equals( a.GetText( "action" ), "default", StringComparison.InvariantCultureIgnoreCase ) ) {
               MergeDefAction( ref defValues, a );
               defCount++;
            } else
               actions.Add( AddDefAction( a, defValues ) );
         }
         return actions.Count > 0 ? actions.ToArray() : null;
      }

      private static IAction[] LoadInclude ( ModEntry mod, string basedir, string path, ref IAction defValues, ref int defCount, int level ) {
         if ( level > 9 ) throw new ApplicationException( "Action includes too deep: " + path );
         if ( ! IsSafePath( path ) ) {
            mod.Log().Error( "Invalid path: {0}", path );
            return new IAction[0];
         }
         string fullpath = Path.Combine( basedir, path );
         try {
            AddToModActionFiles( mod, fullpath );
            var actions = Json.Parse< IAction[] >( ReadText( fullpath ) );
            ModMeta.NormDictArray( ref actions );
            return PreprocessActions( mod, actions, Path.GetDirectoryName( fullpath ), ref defValues, ref defCount, level );
         } catch ( Exception ex ) {
            mod.ActionFiles?.Remove( fullpath );
            if ( mod.ActionFiles?.Count == 0 ) mod.ActionFiles = null;
            throw new ApplicationException( "Error when including " + path, ex );
         }
      }

      private static void AddToModActionFiles ( ModEntry mod, string file ) {
         if ( mod.ActionFiles == null ) mod.ActionFiles = new List< string >{ file };
         else mod.ActionFiles.Add( file );
      }

      private static object RunActionHandler ( ModEntry mod, DllMeta dll, IAction act ) { try {
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
               if ( InList( act.GetText( "onerror" ), "skip" ) ) return true;
               mod.Log().Info( "Aborting Actions. Set OnError to \"Log,Continue\" or \"Log,Skip\" to ignore the error." );
               return ex;
            } else if ( result != null )
               handler.Log().Error( "Unexpected ActionMod result: {0}", result.GetType() );
         }
         return null;
      } catch ( Exception ex ) { mod.Error( ex ); return null; } }

      #region Helpers
      private static object ParamValue ( IAction act, ParameterInfo arg, ModEntry actionMod, ModEntry handler ) {
         if ( arg.ParameterType == typeof( IAction ) || arg.ParameterType == typeof( ActionDef ) )
            return act;
         if ( arg.ParameterType == typeof( string ) )
            return actionMod.Metadata.Id;
         return ModPhases.ParamValue( arg, handler );
      }

      private static string GetText ( this IAction act, string key, string fallback = null ) {
         return act.SafeGet( key ) is string txt ? txt : fallback;
      }

      private static void MergeDefAction ( ref IAction defValues, IAction a ) {
         if ( defValues == null ) defValues = new ActionDef();
         foreach ( var e in a ) {
            if ( "action".Equals( e.Key ) ) continue;
            defValues[ e.Key ] = e.Value;
         }
      }

      private static IAction AddDefAction ( IAction a, IAction defValues ) {
         a = new ActionDef( a );
         if ( defValues != null ) {
            foreach ( var e in defValues )
               if ( ! a.ContainsKey( e.Key ) )
                  a.Add( e.Key, e.Value );
         }
         if ( ! a.ContainsKey( "phase" ) ) a.Add( "phase", DEFAULT_PHASE_LOWER );
         else a[ "phase" ] = a[ "phase" ].ToString().Trim().ToLowerInvariant();
         return a;
      }
      #endregion
   }
}