using Harmony;
using Sheepy.Logging;
using Sheepy.Modnix.Actions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix {

   public static class ModPhases {
      internal static readonly HashSet<string> PHASES = new HashSet<string>(
         new string[]{ "SplashMod", "Init", "Initialize", "MainMod", // Do not start phases with P.
         "HomeMod", "HomeOnShow", "GameMod", "GameOnShow", "RunModActions", // P are fast skipped as Prefix/Postfix
         "TacticalMod", "TacticalOnShow", "GeoscapeMod", "GeoscapeOnShow" } );

      public const char LOG_DIVIDER = '┊';

      public static void LoadMods ( string phase ) { try {
         Log.Info( "PHASE {0}", phase );
         foreach ( var mod in ModScanner.EnabledMods ) {
            if ( mod.Metadata.Dlls != null )
               foreach ( var dll in mod.Metadata.Dlls )
                  RunPhaseOnDll( mod, dll, phase );
            ActionManager.RunAction( mod, phase );
         }
         Log.Verbo( "Phase {0} ended", phase );
         Log.Flush();
      } catch ( Exception ex ) { Log.Error( ex ); } }

      public static void RunPhaseOnDll ( ModEntry mod, DllMeta dll, string phase ) { try {
         if ( dll.Methods == null ) return;
         if ( ! dll.Methods.TryGetValue( phase, out var entries ) ) return;
         var lib = LoadDll( mod, dll.Path );
         if ( lib == null ) return;
         if ( mod.ModAssemblies == null )
            mod.ModAssemblies = new List<Assembly>();
         if ( ! mod.ModAssemblies.Contains( lib ) )
            mod.ModAssemblies.Add( lib );
         foreach ( var type in entries )
            CallInit( mod, lib, type, phase );
      } catch ( Exception ex ) { mod.Error( ex ); } }

      public static Assembly LoadDll ( ModEntry mod, string path ) { try {
         Log.Info( "Loading {0}", path );
         return Assembly.LoadFrom( path );
      } catch ( Exception ex ) { mod.Error( ex ); return null; } }

      private readonly static Dictionary<Type,WeakReference<object>> ModInstances = new Dictionary<Type,WeakReference<object>>();

      public static void CallInit ( ModEntry mod, Assembly dll, string typeName, string methodName ) { try {
         var type = dll.GetType( typeName );
         if ( type == null ) {
            Log.Error( "Cannot find type {1} in {0}", dll.Location, typeName );
            return;
         }

         var func = type.GetMethods( ModScanner.INIT_METHOD_FLAGS )?.FirstOrDefault( e => e.Name.Equals( methodName ) );
         if ( func == null ) {
            Log.Error( "Cannot find {1}.{2} in {0}", dll.Location, typeName, methodName );
            return;
         }

         var augs = new List<object>();
         foreach ( var aug in func.GetParameters() )
            augs.Add( ParamValue( aug, mod ) );
         Func<string> augTxt = () => string.Join( ", ", augs.Select( e => e?.GetType()?.Name ?? "null" ) );
         Log.Info( "Calling {1}.{2}({3}) in {0}", mod.Path, typeName, methodName, augTxt );
         object target = null;
         if ( ! func.IsStatic ) lock ( ModInstances ) {
            if ( ! ModInstances.TryGetValue( type, out WeakReference<object> wref ) || ! wref.TryGetTarget( out target ) )
               ModInstances[ type ] = new WeakReference<object>( target = Activator.CreateInstance( type ) );
         }
         func.Invoke( target, augs.ToArray() );
         Log.Verbo( "Done calling {0}", mod.Path );
      } catch ( Exception ex ) { mod.Error( ex ); } }

      private static object ParamValue ( ParameterInfo aug, ModEntry mod ) {
         var pType = aug.ParameterType;
         var pName = aug.Name;
         var isLog =  pName.IndexOf( "log", StringComparison.OrdinalIgnoreCase ) >= 0;
         // API
         if ( pType == typeof( Func<string,object,object> ) )
            return (Func<string,object,object>) mod.ModAPI;
         return DefaultParamValue( aug );
      }

      private static bool IsSetting ( string name ) =>
         name.IndexOf( "setting", StringComparison.OrdinalIgnoreCase ) >= 0 ||
         name.IndexOf( "conf"   , StringComparison.OrdinalIgnoreCase ) >= 0;

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