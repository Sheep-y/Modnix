using Harmony;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Sheepy.Modnix {

   using static Logger;
   public static class ModLoader {
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
         LogPath = Path.Combine( ModDirectory, "ModnixLoader.log" );

         if ( !Directory.Exists( ModDirectory ) )
            Directory.CreateDirectory( ModDirectory );

         // create log file, overwriting if it's already there
         using ( var logWriter = File.CreateText( LogPath ) ) {
            logWriter.WriteLine( $"{typeof( ModLoader ).FullName} -- v{LoaderVersion} -- {DateTime.Now}" );
         }

         // ReSharper disable once UnusedVariable
         var harmony = HarmonyInstance.Create( typeof( ModLoader ).Namespace );

         // get all dll paths
         var dllPaths = Directory.GetFiles(ModDirectory).Where(x => Path.GetExtension(x).ToLower() == ".dll").ToArray();

         if ( dllPaths.Length == 0 ) {
            Log( @"No .DLLs loaded. DLLs must be placed in the root of the folder \PhoenixPoint\Mods\." );
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
            LogWithDate( $"Failed to load {fileName} at path {path}, because it doesn't exist at that path." );
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
               LogWithDate( $"{fileName} (v{version}): Failed to find specified entry point: {typeName ?? "NotSpecified"}.{methodName}" );
               return null;
            }

            // run each entry point
            foreach ( var type in types ) {
               var entryMethod = type.GetMethod(methodName, bFlags);
               var methodParams = entryMethod?.GetParameters();

               if ( methodParams == null )
                  continue;

               if ( methodParams.Length == 0 ) {
                  LogWithDate( $"{fileName} (v{version}): Found and called entry point \"{entryMethod}\" in type \"{type.FullName}\"" );
                  entryMethod.Invoke( null, null );
                  continue;
               }

               // match up the passed in params with the method's params, if they match, call the method
               if ( parameters != null && methodParams.Length == parameters.Length
                   && !methodParams.Where( ( info, i ) => parameters[ i ]?.GetType() != info.ParameterType ).Any() ) {
                  LogWithDate( $"{fileName} (v{version}): Found and called entry point \"{entryMethod}\" in type \"{type.FullName}\"" );
                  entryMethod.Invoke( null, parameters );
                  continue;
               }

               // failed to call entry method of parameter mismatch
               // diagnosing problems of this type is pretty hard
               LogWithDate( $"{fileName} (v{version}): Provided params don't match {type.Name}.{entryMethod.Name}" );
               Log( "\tPassed in Params:" );
               if ( parameters != null ) {
                  foreach ( var parameter in parameters )
                     Log( $"\t\t{parameter.GetType()}" );
               } else {
                  Log( "\t\t'parameters' is null" );
               }

               if ( methodParams.Length != 0 ) {
                  Log( "\tMethod Params:" );
                  foreach ( var prm in methodParams )
                     Log( $"\t\t{prm.ParameterType}" );
               }
            }

            return assembly;
         } catch ( Exception e ) {
            LogException( $"{fileName}: While loading a dll, an exception occured", e );
            return null;
         }
      }
   }
}

