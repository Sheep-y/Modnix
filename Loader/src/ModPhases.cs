using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Sheepy.Modnix {

   internal static class ModPhases {
      internal static readonly HashSet<string> PHASES = new HashSet<string>(
         new string[]{ "SplashMod", "ActionMod", "UnloadMod", // Do not start phases with P.
            "Init", "Initialize", "MainMod",                  // P are fast skipped as Prefix/Postfix
            "HomeMod", "HomeOnShow", "HomeOnHide",
            "GameMod", "GameOnShow", 
            "GeoscapeMod", "GeoscapeOnShow", "GeoscapeOnHide",
            "TacticalMod", "TacticalOnShow", "TacticalOnHide" } );

      internal static readonly string[] PHASES_LOWER = PHASES.Select( e => e.ToLowerInvariant() ).ToArray();

      public const char LOG_DIVIDER = '┊';

      private static readonly List<string> LoadedPhases = new List<string>();

      internal static volatile string LastPhase;

      public static void RunPhase ( string phase ) { try {
         // UnloadMod and ActionMod should not go through here!
         if ( phase == "UnloadMod" || phase == "ActionMod" ) throw new ArgumentException( phase );
         lock ( LoadedPhases ) {
            if ( LoadedPhases.Contains( phase ) ) {
               // Make sure Init, Initialize, and *Mod phases are not repeated
               if ( phase.StartsWith( "Init" ) || phase.EndsWith( "Mod" ) ) return;
            } else
               LoadedPhases.Add( phase );
         }
         LastPhase = phase;
         if ( ! ModLoader.ModsInPhase.TryGetValue( phase.ToLowerInvariant(), out List<ModEntry> list ) ) {
            Log.Verbo( "Phase {0} skipped, no mods.", phase );
            return;
         }
         Log.Info( "PHASE {0}", phase );
         foreach ( var mod in list ) RunPhaseOnMod( mod, phase );
         Log.Verbo( "Phase {0} ended", phase );
         Log.Flush();
      } catch ( Exception ex ) { Log.Error( ex ); } }

      internal static void RunPastPhaseOnMod ( ModEntry mod ) { try { lock ( LoadedPhases ) {
         foreach ( var phase in LoadedPhases )
            RunPhaseOnMod( mod, phase );
      } } catch ( Exception ex ) { mod.Error( ex ); } }

      internal static void RunPhaseOnMod ( ModEntry mod, string phase ) { try {
         lock ( mod.Metadata ) if ( mod.IsUnloaded ) return;
         if ( mod.Metadata.Dlls != null )
            foreach ( var dll in mod.Metadata.Dlls )
               RunPhaseOnDll( mod, dll, phase );
         if ( mod.Metadata.Actions != null )
            ModActions.RunActions( mod, phase );
      } catch ( Exception ex ) { mod.Error( ex ); } }

      private static void RunPhaseOnDll ( ModEntry mod, DllMeta dll, string phase ) { try {
         if ( dll.Methods == null ) return;
         if ( ! dll.Methods.TryGetValue( phase, out var entries ) ) return;
         var lib = LoadDll( mod, dll.Path );
         if ( lib == null ) return;
         foreach ( var type in entries )
            if ( CallInit( mod, lib, type, phase, ( e ) => ParamValue( e, mod ) ) is Exception err )
               Log.Error( err );
      } catch ( Exception ex ) { mod.Error( ex ); } }

      internal static Assembly LoadDll ( ModEntry mod, string path ) { try {
         if ( mod.ModAssemblies != null ) {
            foreach ( var a in mod.ModAssemblies )
               if ( ! a.IsDynamic && a.Location == path )
                  return a;
         }
         /*if ( ! Tools.IsSafePath( path ) ) { // TODO: Make auto-scanned dll use relative path
            mod.Log().Error( "Invalid or unsafe path: {0}", path );
            return null;
         }*/
         mod.Log().Info( "Loading {0}", path );
         var asm = Assembly.LoadFrom( path );
         if ( asm == null ) return null;
         if ( mod.ModAssemblies == null )
            mod.ModAssemblies = new List<Assembly>();
         if ( ! mod.ModAssemblies.Contains( asm ) )
            mod.ModAssemblies.Add( asm );
         return asm;
      } catch ( Exception ex ) { mod.Error( ex ); return null; } }

      private readonly static Dictionary<Type,WeakReference<object>> ModInstances = new Dictionary<Type,WeakReference<object>>();

      public static object CallInit ( ModEntry mod, Assembly dll, string typeName, string methodName, Func< ParameterInfo, object > paramGetter ) { try {
         Logger log = mod.Log();

         var type = dll.GetType( typeName );
         if ( type == null ) {
            log.Error( "Cannot find type {1} in {0}", dll.Location, typeName );
            return null;
         }

         var func = type.GetMethods( ModScanner.INIT_METHOD_FLAGS )?.FirstOrDefault( e => e.Name.Equals( methodName ) );
         if ( func == null ) {
            log.Error( "Cannot find {1}.{2} in {0}", dll.Location, typeName, methodName );
            return null;
         }

         var args = func.GetParameters().Select( paramGetter );
         Func<string> argTxt = () => string.Join( ", ", args.Select( e => e?.GetType()?.Name ?? "null" ) );
         log.Log( methodName == "ActionMod" ? SourceLevels.Verbose : SourceLevels.Information,
            "Calling {1}.{2}({3}) in {0}", dll.Location, typeName, methodName, argTxt );
         object target = null;
         if ( ! func.IsStatic ) lock ( ModInstances ) {
            if ( ! ModInstances.TryGetValue( type, out WeakReference<object> wref ) || ! wref.TryGetTarget( out target ) )
               ModInstances[ type ] = new WeakReference<object>( target = Activator.CreateInstance( type ) );
         }
         var result = func.Invoke( target, args.ToArray() );
         log.Trace( "Done calling {1}.{2}", typeName, methodName );
         return result;
      } catch ( Exception ex ) { return ex; } }

      internal static object ParamValue ( ParameterInfo arg, ModEntry mod ) {
         if ( arg.ParameterType == typeof( Func<string,object,object> ) )
            return (Func<string,object,object>) mod.ModAPI;
         return DefaultParamValue( arg );
      }

      private static object DefaultParamValue ( ParameterInfo aug ) {
         if ( aug.HasDefaultValue )
            return aug.RawDefaultValue;
         var pType = aug.ParameterType;
         if ( pType.IsValueType )
            return Activator.CreateInstance( pType );
         return null;
      }

      private static Logger Log => ModLoader.Log;
   }
}