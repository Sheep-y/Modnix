using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix {

   using ModData = IDictionary< string,object >;

   public static class ModLoader {
      private readonly static string MOD_PATH  = "My Games/Phoenix Point/Mods".FixSlash();
      public readonly static List<ModEntry> AllMods = new List<ModEntry>();
      private static bool Initialized;

      private static Logger Log;
      //private static HarmonyInstance Patcher;

      private const BindingFlags PUBLIC_STATIC_BINDING_FLAGS = Public | Static;
      private static readonly List<string> IGNORE_FILE_NAMES = new List<string>() {
         "0Harmony.dll",
         "PPModLoader.dll",
         "ModnixLoader.dll",
         "Mono.Cecil.dll",
      };

      public static string ModDirectory { get; private set; }

      public static void Init () { try {
         if ( Log != null ) {
            if ( Initialized ) return;
            Initialized = true;
            LoadMods( "default" );  // Second call loads default and mainmenu mods
            LoadMods( "mainmenu" );
            return;
         }
         Setup();
         //Patcher = HarmonyInstance.Create( typeof( ModLoader ).Namespace );
         BuildModList();
         LoadMods( "splash" );
      } catch ( Exception ex ) { Log?.Error( ex ); } }

      public static void Setup () { try { lock ( AllMods ) {
         if ( ModDirectory != null ) return;
         var LoaderInfo = Assembly.GetExecutingAssembly().GetName();
         ModDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), MOD_PATH );
         if ( Log == null ) {
            Log = new FileLogger( Path.Combine( ModDirectory, LoaderInfo.Name + ".log" ) ){ TimeFormat = "HH:mm:ss.ffff " };
            if ( ! Directory.Exists( ModDirectory ) )
               Directory.CreateDirectory( ModDirectory );
            else
               Log.Clear();
         }
         Log.Info( "{0} v{1} {2}", typeof( ModLoader ).FullName, LoaderInfo.Version, DateTime.Now.ToString( "u" ) );
      } } catch ( Exception ex ) { Log?.Error( ex ); } }

      public static void LoadMods ( string flags ) { try { lock ( AllMods ) {
         Log.Info( "Loading {0} mods", flags );
         if ( flags == "splash" ) return; // Not implemented
         foreach ( var mod in AllMods )
            foreach ( var dll in mod.Metadata.Dlls ) try {
               LoadDLL( dll.Path, dll.Method ?? "Init" );
            } catch ( Exception ex ) { Log.Error( ex ); }
         Log.Flush();
      } } catch ( Exception ex ) { Log.Error( ex ); } }

      public static void BuildModList () { try { lock ( AllMods ) {
         Log.Info( "Scanning {0} for mods", ModDirectory );
         AllMods.Clear();
         if ( Directory.Exists( ModDirectory ) )
            ScanFolderForMod( ModDirectory, null );
         Log.Info( "{0} mods found.", AllMods.Count );
      } } catch ( Exception ex ) { Log.Error( ex ); } }

      public static void ScanFolderForMod ( string path, string parentName ) {
         var dlls = Directory.EnumerateFiles( path, "*.dll", SearchOption.AllDirectories )
            .Where( e => ! IGNORE_FILE_NAMES.Contains( Path.GetFileName( e ) ) ).ToArray();
         foreach ( var dll in dlls ) {
            var info = ParseMod( dll );
            if ( info != null )
               AllMods.Add( info );
         }
      }

      public static ModEntry ParseMod ( string file ) {
         var info = FileVersionInfo.GetVersionInfo( file );
         var meta = new ModMeta();
         meta.Id = Path.GetFullPath( file ).Replace( ModDirectory, "" ).ToLowerInvariant();
         meta.Name = new TextSet(){ Default = info.FileDescription };
         meta.Version = info.FileVersion;
         meta.Description = new TextSet(){ Default = info.Comments };
         meta.Author = new TextSet() { Default = info.CompanyName };
         meta.Dlls = new DllMeta[] { new DllMeta() { Path = file } };
         return new ModEntry(){ Metadata = meta };
      }

      public static Assembly LoadDLL ( string path, string methodName = "Init", string typeName = null, object[] parameters = null, BindingFlags bFlags = PUBLIC_STATIC_BINDING_FLAGS ) {
         Log.Info( "Loading {0}", path );
         if ( ! File.Exists( path ) ) throw new FileNotFoundException();

         var fileName = Path.GetFileName( path );
         var assembly = Assembly.LoadFrom(path);
         var name = assembly.GetName();
         var version = name.Version;
         var types = new List<Type>();
         if ( methodName == null ) return assembly;
         if ( typeName == null )
            types.AddRange( assembly.GetTypes().Where( x => x.GetMethod( methodName, bFlags ) != null ) );
         else
            types.Add( assembly.GetType( typeName ) );

         if ( types.Count == 0 ) {
            Log.Error( "{0} (v{1}): Failed to find entry point: {2}.{3}", fileName, version, typeName ?? "Unnamed", methodName );
            return null;
         }

         // run each entry point
         foreach ( var type in types ) {
            var entryMethod = type.GetMethod(methodName, bFlags);
            var methodParams = entryMethod?.GetParameters();

            if ( methodParams == null )
               continue;

            if ( methodParams.Length == 0 ) {
               Log.Info( "{0} (v{1}): Calling entry point \"{2}\" in type \"{3}\"", fileName, version, entryMethod, type.FullName );
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
      }
   }

   internal static class Tools {
      internal static string FixSlash ( this string path ) => path.Replace( '/', Path.DirectorySeparatorChar );
   }
}

