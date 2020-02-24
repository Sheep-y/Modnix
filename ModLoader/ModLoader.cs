using Mono.Cecil;
using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix {
   using DllEntryMeta = Dictionary< string, HashSet< string > >;

   using ModData = IDictionary< string,object >;

   public static class ModLoader {
      private readonly static string MOD_PATH  = "My Games/Phoenix Point/Mods".FixSlash();
      public readonly static List<ModEntry> AllMods = new List<ModEntry>();
      private static bool Initialized;

      internal static Logger Log;
      //private static HarmonyInstance Patcher;

      private const BindingFlags PUBLIC_STATIC_BINDING_FLAGS = Public | Static;
      private static readonly List<string> IGNORE_FILE_NAMES = new List<string> {
         "0Harmony",
         "PhoenixPointModLoader",
         "PPModLoader",
         "ModnixLoader",
         "Mono.Cecil",
      };

      public static string ModDirectory { get; private set; }

      private static readonly string[] PHASES = new string[]{ "SplashMod", "Init", "MainMod" };

      public static void Init () { try {
         if ( Log != null ) {
            if ( Initialized ) return;
            Initialized = true;
            LoadMods( "Init" );  // Second call loads default and mainmenu mods
            LoadMods( "MainMod" );
            return;
         }
         Setup();
         //Patcher = HarmonyInstance.Create( typeof( ModLoader ).Namespace );
         BuildModList();
         LoadMods( "SplashMod" );
      } catch ( Exception ex ) { Log?.Error( ex ); } }

      public static bool NeedSetup => Log == null;

      public static void SetLog ( Logger logger, bool clear = false ) {
         if ( logger == null ) throw new NullReferenceException( nameof( logger ) );
         if ( Log != null ) throw new InvalidOperationException();
         Log = logger;
         if ( clear ) Log.Clear();
         Log.Info( "{0} v{1} {2}", typeof( ModLoader ).FullName, Assembly.GetExecutingAssembly().GetName().Version, DateTime.Now.ToString( "u" ) );
         ModMetaJson.JsonLogger.Masters.Clear();
         ModMetaJson.JsonLogger.Masters.Add( Log );
      }

      public static void Setup ( AppDomain domain = null ) { try { lock ( AllMods ) {
         if ( ModDirectory != null ) return;
         ModDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), MOD_PATH );
         var LoaderInfo = Assembly.GetExecutingAssembly().GetName();
         if ( Log == null ) {
            if ( ! Directory.Exists( ModDirectory ) )
               Directory.CreateDirectory( ModDirectory );
            SetLog( new FileLogger( Path.Combine( ModDirectory, LoaderInfo.Name + ".log" ) ){ TimeFormat = "HH:mm:ss.ffff " }, true );
         }
      } } catch ( Exception ex ) { Log?.Error( ex ); } }

      public static void LoadMods ( string phase ) { try { lock ( AllMods ) {
         Log.Info( "Calling {0} mods", phase );
         foreach ( var mod in AllMods ) {
            if ( mod.Metadata.Dlls == null ) continue;
            foreach ( var dll in mod.Metadata.Dlls ) {
               if ( dll.Methods == null ) continue;
               if ( ! dll.Methods.TryGetValue( phase, out var entries ) ) continue;
               var lib = LoadDll( dll.Path );
               if ( lib == null ) continue;
               foreach ( var type in entries )
                  CallInit( lib, type, phase );
            }
         }
         Log.Flush();
      } } catch ( Exception ex ) { Log.Error( ex ); } }

      public static void BuildModList () { try { lock ( AllMods ) {
         AllMods.Clear();
         if ( Directory.Exists( ModDirectory ) )
            ScanFolderForMod( ModDirectory, true );
         Log.Info( "{0} mods found.", AllMods.Count );
      } } catch ( Exception ex ) { Log.Error( ex ); } }

      public static void ScanFolderForMod ( string path, bool isRoot ) {
         Log.Info( "Scanning for mods: {0}", path );
         var container = Path.GetFileName( path );
         var foundMod = false;
         foreach ( var dll in Directory.EnumerateFiles( path, "*.dll" ) ) {
            var name = Path.GetFileNameWithoutExtension( dll );
            if ( IGNORE_FILE_NAMES.Contains( name ) ) continue;
            if ( isRoot || NameMatch( container, name ) ) {
               var info = ParseMod( dll );
               if ( info != null ) {
                  AllMods.Add( info );
                  foundMod = true;
               }
            }
         }
         if ( ! isRoot && foundMod ) return;
         foreach ( var dir in Directory.EnumerateDirectories( path ) ) {
            if ( isRoot || NameMatch( container, Path.GetFileName( dir ) ) )
               ScanFolderForMod( dir, false );
         }
      }

      private static readonly Regex DropFromName = new Regex( "\\W+", RegexOptions.Compiled );

      private static bool NameMatch ( string container, string subject ) {
         if ( container == null || subject == null ) return false;
         container = DropFromName.Replace( container, "" );
         subject = DropFromName.Replace( subject, "" );
         if ( container.Length < 3 || subject.Length < 3 ) return false;
         int len = Math.Max( 3, (int) Math.Round( Math.Min( container.Length, subject.Length ) * 2.0 / 3.0 ) );
         return container.Substring( 0, len ) == subject.Substring( 0, len );
      }

      public static ModEntry ParseMod ( string file ) { try {
         Log.Info( $"Parsing as a mod: {file}" );
         ModMeta meta;
         if ( file.EndsWith( ".dll", StringComparison.InvariantCultureIgnoreCase ) ) {
            var info = FileVersionInfo.GetVersionInfo( file );
            meta = new ModMeta{
               Id = Path.GetFullPath( file ).Replace( ModDirectory, "" ).ToLowerInvariant(),
               Name = new TextSet{ Default = info.FileDescription },
               Version = info.FileVersion,
               Description = new TextSet{ Default = info.Comments },
               Author = new TextSet{ Default = info.CompanyName },
               Copyright = new TextSet { Default = info.LegalCopyright },
               Dlls = new DllMeta[] { new DllMeta{ Path = file, Methods = ParseEntryPoints( file ) } },
            };
            if ( meta.Dlls[0].Methods == null ) return null;
         } else {
            string js = File.ReadAllText( file, Encoding.UTF8 ).Trim();
            if ( js.StartsWith( "(", StringComparison.InvariantCulture ) &&
                 js.EndsWith( ")", StringComparison.InvariantCulture ) )
               js = js.Substring( 1, js.Length - 1 );
            meta = ModMetaJson.ParseMod( js );
         }
         return new ModEntry{ Metadata = meta };
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }

      private static DllEntryMeta ParseEntryPoints ( string file ) {
         DllEntryMeta result = null;
         using ( var lib = AssemblyDefinition.ReadAssembly( file ) ) {
            foreach ( var type in lib.MainModule.GetTypes() ) {
               foreach ( var method in type.Methods ) {
                  string name = method.Name;
                  if ( Array.IndexOf( PHASES, name ) >= 0 ) {
                     if ( result == null ) result = new DllEntryMeta();
                     if ( ! result.TryGetValue( name, out var list ) )
                        result[ name ] = list = new HashSet<string>();
                     if ( list.Contains( type.FullName ) ) {
                        Log.Warn( "Found overloaded {0}.{1}, removing all.", type.FullName, name );
                        list.Remove( type.FullName );
                        goto NextType;
                     } else {
                        list.Add( type.FullName );
                        Log.Info( "Found {0}.{1}", type.FullName, name );
                     }
                  }
               }
               NextType:;
            }
         }
         // Remove Init from Modnix DLLs, so that they will not be initiated twice
         if ( result != null )
            if ( result.Count > 1 )
               result.Remove( "Init" );
            else if ( result.Count <= 0 )
               return null;
         return result;
      }

      public static Assembly LoadDll ( string path ) { try {
         Log.Info( "Loading {0}", path );
         return Assembly.LoadFrom( path );
      } catch ( Exception ex ) { Log.Error( ex ); return null; } }

      private static void LoggerA ( object msg ) => LoggerB( msg, null );
      private static void LoggerB ( object msg, object[] augs ) =>
         Log.Log( msg is Exception ? SourceLevels.Error : SourceLevels.Information, msg, augs );
      private static void LoggerC ( SourceLevels lv, object msg ) => Log.Log( lv, msg );
      private static void LoggerD ( SourceLevels lv, object msg, object[] augs ) => Log.Log( lv, msg, augs );

      public static void CallInit ( Assembly dll, string typeName, string methodName ) { try {
         Type type = dll.GetType( typeName );
         if ( type == null ) {
            Log.Error( "Cannot find type {1} in {0}", typeName, dll.Location );
            return;
         }

         MethodInfo func = type.GetMethod( methodName, PUBLIC_STATIC_BINDING_FLAGS );
         List<object> augs = new List<object>();
         foreach ( var aug in func.GetParameters() ) {
            var pType = aug.ParameterType;
            // Mod Loaders
            if ( pType == typeof( Assembly ) )
               augs.Add( Assembly.GetExecutingAssembly() );
            // Loggers
            else if ( pType == typeof( Action<object> ) )
               augs.Add( (Action<object>) LoggerA );
            else if ( pType == typeof( Action<object,object[]> ) )
               augs.Add( (Action<object,object[]>) LoggerB );
            else if ( pType == typeof( Action<SourceLevels,object> ) )
               augs.Add( (Action<SourceLevels,object>) LoggerC );
            else if ( pType == typeof( Action<SourceLevels,object,object[]> ) )
               augs.Add( (Action<SourceLevels,object,object[]>) LoggerD );
            // Defaults
            else if ( pType.IsValueType )
               augs.Add( Activator.CreateInstance( pType ) );
            else
               augs.Add( null );
         }
         Log.Info( "Calling {0}.{1} with {2} parameters", typeName, methodName, augs.Count );
         func.Invoke( null, augs.ToArray() );
      } catch ( Exception ex ) { Log.Error( ex ); } }
   }

   internal static class Tools {
      internal static string FixSlash ( this string path ) => path.Replace( '/', Path.DirectorySeparatorChar );
   }
}

