using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Sheepy.Modnix {

   public static class ModResolver {
      private static Logger Log => ModLoader.Log;

      private static List<ModEntry> EnabledMods => ModLoader.EnabledMods;
      private static Dictionary<string,List<ModEntry>> ModsInPhase => ModLoader.ModsInPhase;

      #region Resolving
      private static bool ResolveModAgain;

      internal static void Resolve () {
         ModsInPhase.Clear();
         EnabledMods.Clear();
         EnabledMods.AddRange( ModLoader.AllMods.Where( e => ! e.Disabled ) );
         Log.Info( "Resolving {0} mods", EnabledMods.Count );
         ApplyUserOverride();
         EnabledMods.Sort( CompareModIndex );
         RemoveDuplicateMods();
         var loopIndex = 0;
         ResolveModAgain = true;
         while ( ResolveModAgain && loopIndex++ < 30 ) {
            ResolveModAgain = false;
            RemoveUnfulfilledMods();
            if ( ! ResolveModAgain ) RemoveRecessMods();
            if ( ! ResolveModAgain ) RemoveConflictMods();
            if ( ! ResolveModAgain ) AssignModsToPhases();
         }
         Func<string> countMod = () => { lock ( ModsInPhase )
            return ModsInPhase.Values.SelectMany( e => e ).Distinct().Count().ToString(); };
         Log.Info( "Assigned {0} mods to {1} phases", countMod, ModsInPhase.Count );
      }

      private static void ApplyUserOverride () {
         var settings = ModLoader.Settings.Mods;
         if ( settings == null ) return;
         Log.Verbo( "Check manual mod settings" );
         foreach ( var mod in EnabledMods.ToArray() ) {
            if ( ! settings.TryGetValue( mod.Key, out ModSettings modSetting ) ) continue;
            if ( modSetting.Disabled )
               DisableAndRemoveMod( mod, "manual", "mod is manually disabled." );
            else {
               if ( modSetting.LoadIndex.HasValue ) {
                  Log.Verbo( "Mod {0} LoadIndex manually set to {1}", mod.Key, modSetting.LoadIndex);
                  lock ( mod.Metadata ) mod.Metadata.LoadIndex = modSetting.LoadIndex.Value;
               }
               if ( modSetting.LogLevel.HasValue ) {
                  Log.Verbo( "Mod {0} LogLevel set to {1}", mod.Key, modSetting.LogLevel );
                  lock ( mod ) mod.LogLevel = modSetting.LogLevel.Value;
               }
            }
         }
      }

      private static void RemoveDuplicateMods () {
         Log.Verbo( "Check duplicate mods" );
         var IdList = new HashSet<string>( EnabledMods.Select( e => e.Key ) );
         foreach ( var mod in EnabledMods.ToArray() ) {
            if ( mod.Disabled ) continue; // Already removed as a dup
            var key = mod.Key;
            if ( ! IdList.Contains( key ) ) continue; // Already removed dups
            RemoveDuplicates( EnabledMods.Where( e => e.Key == key ).ToArray() );
            IdList.Remove( key );
         }
      }

      private static void RemoveDuplicates ( ModEntry[] clones ) {
         var keep = FindLatestMod( clones );
         foreach ( var mod in clones ) {
            if ( mod == keep ) continue;
            DisableAndRemoveMod( mod, "duplicate", "duplicate of {2}.", keep, mod.Path, keep.Path );
         }
      }

      private static void RemoveRecessMods () {
         Log.Verbo( "Check mod avoidances" );
         foreach ( var mod in EnabledMods.ToArray() ) {
            if ( mod.Disabled ) continue;
            var reqs = mod.Metadata.Avoids;
            if ( reqs == null ) continue;
            foreach ( var req in reqs ) {
               if ( ! ModLoader.GetVersionById( req.Id, out ModEntry target, out Version ver ) || target.Disabled ) continue;
               if ( target == mod ) {
                  mod.Log().Warn( "Mod {0} not allowed to disable itself with mod_info.Avoids.", req.Id );
                  continue;
               }
               if ( req.Min != null && req.Min > ver ) continue;
               if ( req.Max != null && req.Max < ver ) continue;
               DisableAndRemoveMod( mod, "avoid", "avoiding {0} {1}", (object) target ?? req.Id, ver );
               break;
            }
         }
      }

      private static void RemoveUnfulfilledMods () {
         Log.Verbo( "Check mod requirements" );
         var dependees = new HashSet< string >();
         foreach ( var mod in EnabledMods.ToArray() ) {
            if ( mod.Disabled ) continue;
            var reqs = mod.Metadata.Requires;
            if ( reqs == null ) continue;
            var requirements = new Dictionary<string, List<AppVer>>();
            foreach ( var req in reqs ) {
               var id = ModScanner.NormaliseModId( req.Id );
               if ( id == null ) continue;
               if ( ! requirements.ContainsKey( id ) ) requirements[ id ] = new List<AppVer>();
               requirements[ id ].Add( req );
            }
            foreach ( var reqSet in requirements ) {
               bool found = ModLoader.GetVersionById( reqSet.Key, out ModEntry target, out Version ver ), fulfill = found;
               if ( target == mod ) {
                  mod.Log().Warn( "Mod {0} not allowed to depends on itself with mod_info.Requires", reqSet.Key );
                  continue;
               }
               if ( found )
                  fulfill = reqSet.Value.Any( r => ( r.Min == null || r.Min <= ver ) && ( r.Max == null || r.Max >= ver ) );
               if ( ! fulfill ) {
                  var r = reqs.FirstOrDefault( e => ModScanner.NormaliseModId( e.Id ) == reqSet.Key );
                  DisableAndRemoveMod( mod, "require", "requirement {0} failed, found {1}", reqSet.Key, found ? (object) ver : "none", r?.Name, r?.Url );
                  break;
               } else
                  dependees.Add( reqSet.Key );
            }
         }

         foreach ( var mod in EnabledMods.ToArray() ) {
            var flags = mod.Metadata.Flags;
            if ( flags != null && flags.Any( e => e.Trim().ToLowerInvariant() == "library" ) && ! dependees.Contains( mod.Key ) )
               DisableAndRemoveMod( mod, "no_dependent", "library disabled because no mods require it" );
         }
      }

      private static void RemoveConflictMods () {
         Log.Verbo( "Check mod conflicts" );
         foreach ( var mod in EnabledMods.ToArray() ) {
            if ( mod.Disabled ) continue;
            var targets = mod.Metadata.Disables;
            if ( targets == null ) continue;
            foreach ( var req in targets ) {
               if ( ! ModLoader.GetVersionById( req.Id, out ModEntry target, out Version ver ) || target.Disabled ) continue;
               if ( target == mod ) {
                  mod.Log().Warn( "Mod {0} not allowed to disable itself with mod_info.Disables.", req.Id );
                  continue;
               }
               if ( req.Min != null && req.Min > ver ) continue;
               if ( req.Max != null && req.Max < ver ) continue;
               DisableAndRemoveMod( target, "disable", "disabled by {1} [{2},{3}]", mod, mod.Metadata.Id, req.Min, req.Max );
            }
         }
      }

      private static void AssignModsToPhases () { lock ( ModsInPhase ) {
         ModsInPhase.Clear();
         var unassigned = new List<ModEntry>( EnabledMods );
         foreach ( var mod in EnabledMods.ToArray() ) {
            var assigned = false;
            var dlls = mod.Metadata.Dlls;
            if ( dlls == null ) continue;
            foreach ( var dll in dlls ) {
               if ( dll.Methods == null ) continue;
               foreach ( var phase in dll.Methods.Keys )
                  AddModToPhase( mod, phase.ToLowerInvariant(), ref assigned );
            }
            if ( assigned ) unassigned.Remove( mod );
         }

         var hasActionHandler = ModsInPhase.ContainsKey( "actionmod" );
         foreach ( var mod in EnabledMods.ToArray() ) try {
            var assigned = false;
            ref var actions = ref mod.Metadata.Actions;
            if ( actions == null ) continue;
            var origLen = actions.Length;
            actions = ModActions.Resolve( mod, actions );
            if ( actions == null ) continue;
            if ( actions.Length < origLen ) {
               mod.Metadata.Actions = actions;
               mod.Log().Verbo( "Merged {0} default actions.", origLen - actions.Length );
               if ( actions == null ) continue;
            }
            if ( ! hasActionHandler && mod.Metadata.Dlls == null ) {
               DisableAndRemoveMod( mod, "no_actionmod", "no action handler mods." );
               continue;
            }
            foreach ( var phase in ModActions.FindPhases( actions ) )
               AddModToPhase( mod, phase, ref assigned );
            if ( assigned ) unassigned.Remove( mod );
         } catch ( Exception ex ) { mod.Log().Warn( ex ); }

         foreach ( var mod in unassigned )
            DisableAndRemoveMod( mod, "no_phase", "no matching mod phases." );
      } }

      private static void AddModToPhase ( ModEntry mod, string phase, ref bool assigned ) {
         if ( ! ModsInPhase.TryGetValue( phase, out List<ModEntry> list ) )
            ModsInPhase.Add( phase, list = new List<ModEntry>() );
         if ( ! list.Contains( mod ) ) {
            Log.Verbo( "Mod {0} added to {1}", mod.Metadata.Id, phase );
            list.Add( mod );
            assigned = true;
         }
      }

      private static void DisableAndRemoveMod ( ModEntry mod, string reason, string log, params object[] augs ) { lock ( mod ) {
         if ( mod.Disabled ) return;
         mod.Log().Info( "Mod Disabled: " + log, augs );
         mod.Disabled = true;
         mod.AddNotice( TraceEventType.Error, reason, augs );
         EnabledMods.Remove( mod );
         ResolveModAgain = true;
      } }
      #endregion

      #region Helpers
      private static ModEntry FindLatestMod ( ModEntry[] clones ) {
         var best = clones[0];
         foreach ( var mod in clones ) {
            if ( mod == best ) continue;
            if ( CompareModVersion( mod, best ) == 1 )
               best = mod;
         }
         return best;
      }

      internal static int CompareModIndex ( ModEntry x, ModEntry y ) {
         var diff = CompareAttr( x.Index, y.Index );
         if ( diff != 0 ) return diff;
         diff = CompareAttr( x.Key, y.Key );
         if ( diff != 0 ) return diff;
         return CompareModFallback( x, y );
      }

      private static int CompareModVersion ( ModEntry x, ModEntry y ) {
         var diff = CompareAttr( x.Metadata.Version, y.Metadata.Version );
         if ( diff != 0 ) return diff;
         return CompareModFallback( x, y );
      }

      private static int CompareModFallback ( ModEntry x, ModEntry y ) {
         var diff = CompareAttr( x.LastModified.GetValueOrDefault(), y.LastModified.GetValueOrDefault() );
         if ( diff != 0 ) return diff;
         if ( x.Path == null || y.Path == null ) return CompareAttr( x.Path, y.Path );
         return CompareAttr( new FileInfo( x.Path ).Length, new FileInfo( y.Path ).Length );
      }

      private static int CompareAttr < T > ( T our, T their ) where T : IComparable {
         if ( our == null && their == null ) return 0;
         if ( our == null ) return -1; // We are null while they are not
         if ( their == null ) return 1;
         return our.CompareTo( their );
      }
      #endregion
   }
}