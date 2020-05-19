using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Sheepy.Modnix {

   public static class ModPhases {
      internal static readonly HashSet<string> PHASES = new HashSet<string>(
         new string[]{ "SplashMod", "Init", "Initialize", "MainMod", "UnloadMod", // Do not start phases with P.
         "HomeMod", "HomeOnShow", "GameMod", "GameOnShow", ModActions.ACTION_METHOD, // P are fast skipped as Prefix/Postfix
         "TacticalMod", "TacticalOnShow", "GeoscapeMod", "GeoscapeOnShow" } );

      public const char LOG_DIVIDER = '┊';

      private static readonly HashSet<string> LoadedPhases = new HashSet<string>();

      public static void RunPhase ( string phase ) { try {
         // Make sure Init, Initialize, and *Mod phases are not repeated
         if ( phase.StartsWith( "Init" ) || phase.EndsWith( "Mod" ) ) lock ( LoadedPhases ) {
            if ( LoadedPhases.Contains( phase ) ) return;
            LoadedPhases.Add( phase );
            // UnloadMod and ActionMod should not go through here!
            if ( phase == "UnloadMod" || phase == ModActions.ACTION_METHOD ) {
               Log.Error( new ArgumentException( phase ) );
               return;
            }
         }
         if ( ! ModScanner.ModsInPhase.TryGetValue( phase.ToLowerInvariant(), out List<ModEntry> list ) ) {
            Log.Verbo( "Phase {0} skipped, no mods.", phase );
            return;
         }
         Log.Info( "PHASE {0}", phase );
         foreach ( var mod in list ) {
            lock ( mod.Metadata ) ;
            if ( mod.Metadata.Dlls != null )
               foreach ( var dll in mod.Metadata.Dlls )
                  RunPhaseOnDll( mod, dll, phase );
            if ( mod.Metadata.Actions != null )
               ModActions.RunActions( mod, phase );
         }
         Log.Verbo( "Phase {0} ended", phase );
         Log.Flush();
      } catch ( Exception ex ) { Log.Error( ex ); } }

      public static void RunPhaseOnDll ( ModEntry mod, DllMeta dll, string phase ) { try {
         if ( dll.Methods == null ) return;
         if ( ! dll.Methods.TryGetValue( phase, out var entries ) ) return;
         var lib = LoadDll( mod, dll.Path );
         if ( lib == null ) return;
         foreach ( var type in entries )
            if ( CallInit( mod, lib, type, phase, ( e ) => ParamValue( e, mod ) ) is Exception err )
               Log.Error( err );
      } catch ( Exception ex ) { mod.Error( ex ); } }

      public static Assembly LoadDll ( ModEntry mod, string path ) { try {
         if ( mod.ModAssemblies != null ) {
            foreach ( var a in mod.ModAssemblies )
               if ( ! a.IsDynamic && a.Location == path )
                  return a;
         }
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
         log.Log( ModActions.ACTION_METHOD.Equals( methodName ) ? SourceLevels.Verbose : SourceLevels.Information,
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