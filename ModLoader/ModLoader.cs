using Harmony;
using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Sheepy.Modnix {

   public static class ModLoader {
      private static Logger Log;

      private const BindingFlags PUBLIC_STATIC_BINDING_FLAGS = BindingFlags.Public | BindingFlags.Static;
      private static readonly List<string> IGNORE_FILE_NAMES = new List<string>() {
         "0Harmony.dll",
         "ModnixLoader.dll"
      };

      public static string ModDirectory { get; private set; }

      public static void Init () {
         var LoaderVersion = Assembly.GetExecutingAssembly().GetName().Version;
         var manifestDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                ?? throw new InvalidOperationException("Manifest path is invalid.");

         // this should be (wherever Phoenix Point is Installed)\PhoenixPoint\PhoenixPointWin64_Data\Managed
         ModDirectory = Path.GetFullPath( Path.Combine( manifestDirectory, Path.Combine( @"..\..\Mods" ) ) );
         Log = new FileLogger( Path.Combine( ModDirectory, "ModnixLoader.log" ) );
         Log.TimeFormat = "HH:mm:ss.ffff ";

         if ( !Directory.Exists( ModDirectory ) )
            Directory.CreateDirectory( ModDirectory );

         // create log file, overwriting if it's already there
         Log.Clear();
         Log.Info( "{0} --v{1} -- {2}", typeof( ModLoader ).FullName, LoaderVersion, DateTime.Now );

         // ReSharper disable once UnusedVariable
         var harmony = HarmonyInstance.Create( typeof( ModLoader ).Namespace );

         // get all dll paths
         var dllPaths = Directory.GetFiles( ModDirectory ).Where( x => Path.GetExtension(x).ToLower() == ".dll" ).ToArray();

         if ( dllPaths.Length == 0 ) {
            Log.Error( @"No .DLLs loaded. DLLs must be placed in the root of the folder \PhoenixPoint\Mods\." );
            return;
         }

         // load the DLLs
         foreach ( var dllPath in dllPaths ) {
            if ( !IGNORE_FILE_NAMES.Contains( Path.GetFileName( dllPath ) ) )
               LoadDLL( dllPath );
         }
      }

      public static Assembly LoadDLL ( string path, string methodName = "Init", string typeName = null, object[] parameters = null, BindingFlags bFlags = PUBLIC_STATIC_BINDING_FLAGS ) {
         var fileName = Path.GetFileName(path);

         if ( !File.Exists( path ) ) {
            Log.Error( "Failed to load {0} at path {1}, because it doesn't exist at that path.", fileName, path );
            return null;
         }

         try {
            var assembly = Assembly.LoadFrom(path);
            var name = assembly.GetName();
            var version = name.Version;
            var types = new List<Type>();

            // if methodName is null, don't try to run an entry point
            if ( methodName == null )
               return assembly;

            // find the type/s with our entry point/s
            if ( typeName == null )
               types.AddRange( assembly.GetTypes().Where( x => x.GetMethod( methodName, bFlags ) != null ) );
            else
               types.Add( assembly.GetType( typeName ) );

            if ( types.Count == 0 ) {
               Log.Error( "{0} (v{1}): Failed to find specified entry point: {2}.{3}", fileName, version, typeName ?? "Unnamed", methodName );
               return null;
            }

            // run each entry point
            foreach ( var type in types ) {
               var entryMethod = type.GetMethod(methodName, bFlags);
               var methodParams = entryMethod?.GetParameters();

               if ( methodParams == null )
                  continue;

               if ( methodParams.Length == 0 ) {
                  Log.Info( "{0} (v{1}): Found and called entry point \"{2}\" in type \"{3}\"", fileName, version, entryMethod, type.FullName );
                  entryMethod.Invoke( null, null );
                  continue;
               }

               // match up the passed in params with the method's params, if they match, call the method
               if ( parameters != null && methodParams.Length == parameters.Length
                   && !methodParams.Where( ( info, i ) => parameters[ i ]?.GetType() != info.ParameterType ).Any() ) {
                  Log.Info( "{0} (v{1}): Found and called entry point \"{2}\" in type \"{3}\"", fileName, version, entryMethod, type.FullName );
                  entryMethod.Invoke( null, parameters );
                  continue;
               }

               // failed to call entry method of parameter mismatch
               // diagnosing problems of this type is pretty hard
               Log.Error( "{0} (v{1}): Provided params don't match {2}.{3}", fileName, version, type.FullName, entryMethod.Name );
               Log.Error( "\tPassed in Params:" );
               if ( parameters != null ) {
                  foreach ( var parameter in parameters )
                     Log.Error( "\t\t{0}", parameter.GetType() );
               } else {
                  Log.Error( "\t\t'parameters' is null" );
               }

               if ( methodParams.Length != 0 ) {
                  Log.Error( "\tMethod Params:" );
                  foreach ( var prm in methodParams )
                     Log.Error( "\t\t{0}", prm.ParameterType );
               }
            }

            return assembly;
         } catch ( Exception e ) {
            Log.Error( "{0}: While loading a dll, an exception occured:", fileName );
            Log.Error( e );
            return null;
         }
      }
   }
}

