using Mono.Cecil;
using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix {
   using DllEntryMeta = Dictionary< string, HashSet< string > >;

   public static class ModLoader {
      private readonly static string MOD_PATH  = "My Games/Phoenix Point/Mods".FixSlash();
      public readonly static List<ModEntry> AllMods = new List<ModEntry>();
      public readonly static List<ModEntry> EnabledMods = new List<ModEntry>();

      private static Logger Log;
      //private static HarmonyInstance Patcher;
      private static bool Initialized;
      public static Version LoaderVersion, GameVersion;

      private const BindingFlags ALL_BINDING_FLAGS = Public | NonPublic | Static | Instance;
      private static readonly List<string> IGNORE_FILE_NAMES = new List<string> {
         "0harmony",
         "phoenixpointmodloader",
         "ppmodloader",
         "modnixloader",
         "mono.cecil",
      };
      private readonly static Version PPML_COMPAT = new Version( 0, 1 );

      public static string ModDirectory { get; private set; }

      private static readonly string[] PHASES = new string[]{ "SplashMod", "Init", "Initialize", "MainMod" };

      public static void Init () { try {
         if ( Log != null ) {
            if ( Initialized ) return;
            Initialized = true;
            LoadMods( "Init" ); // PPML v0.1
            LoadMods( "Initialize" ); // PPML v0.2
            LoadMods( "MainMod" ); // Modnix
            return;
         }
         Setup();
         //Patcher = HarmonyInstance.Create( typeof( ModLoader ).Namespace );
         BuildModList();
         LoadMods( "SplashMod" );
      } catch ( Exception ex ) { Log?.Error( ex ); } }

      public static bool NeedSetup => ModDirectory == null;

      public static void Setup () { try { lock ( AllMods ) {
         if ( ModDirectory != null ) return;
         // Dynamically load embedded dll
         AppDomain.CurrentDomain.AssemblyResolve += ( domain, dll ) => { try {
            Log.Trace( "Resolving {0}", dll.Name );
            AppDomain app = domain as AppDomain ?? AppDomain.CurrentDomain;
            if ( dll.Name.StartsWith( "PhoenixPointModLoader, Version=0.2.0.0, ", StringComparison.InvariantCultureIgnoreCase ) ) {
               Log.Verbo( "Loading embedded PPML v0.2" );
               return app.Load( Properties.Resources.PPML_0_2 );
            }
            if ( dll.Name.StartsWith( "System.Numerics, Version=4.0.0.0, " ) ) {
               string path = Path.GetDirectoryName( new Uri( Assembly.GetExecutingAssembly().CodeBase ).LocalPath );
               string target = Path.Combine( path, "System.Numerics.dll" );
               if ( File.Exists( target ) ) {
                  Log.Trace( "Loading {0}", target );
                  return Assembly.LoadFrom( target );
               }
            }
            return null;
         } catch ( Exception ) { return null; } };
         ModDirectory = Path.Combine( Environment.GetFolderPath( Environment.SpecialFolder.MyDocuments ), MOD_PATH );
         if ( Log == null ) {
            if ( ! Directory.Exists( ModDirectory ) )
               Directory.CreateDirectory( ModDirectory );
            SetLog( new FileLogger( Path.Combine( ModDirectory, Assembly.GetExecutingAssembly().GetName().Name + ".log" ) ){ TimeFormat = "HH:mm:ss.ffff " }, true );
            LogGameVersion();
         }
         string corlib = new Uri( typeof( string ).Assembly.CodeBase ).LocalPath;
         Log.Verbo( ".Net/{0}; mscorlib/{1} {2}", Environment.Version, FileVersionInfo.GetVersionInfo( corlib ).FileVersion, corlib );
      } } catch ( Exception ex ) { Log?.Error( ex ); } }

      public static void SetLog ( Logger logger, bool clear = false ) {
         if ( logger == null ) throw new NullReferenceException( nameof( logger ) );
         if ( Log != null ) throw new InvalidOperationException();
         LoaderVersion = Assembly.GetExecutingAssembly().GetName().Version;
         Log = logger;
         logger.Filters.Clear();
         logger.Filters.Add( LogFilters.FormatParams );
         logger.Filters.Add( LogFilters.ResolveLazy );
         logger.Level = SourceLevels.All;
         if ( clear ) Log.Clear();
         Log.Info( "{0}/{1}; {2}", typeof( ModLoader ).FullName, LoaderVersion, DateTime.Now.ToString( "u" ) );
         ModMetaJson.JsonLogger.Masters.Clear();
         ModMetaJson.JsonLogger.Masters.Add( Log );
      }

      public static void LogGameVersion () { try {
         foreach ( var e in AppDomain.CurrentDomain.GetAssemblies() ) {
            if ( ! e.FullName.StartsWith( "Assembly-CSharp, ", StringComparison.InvariantCultureIgnoreCase ) ) continue;
            var ver = e.GetType( "Base.Build.RuntimeBuildInfo" ).GetProperty( "Version" ).GetValue( null )?.ToString();
            Log.Info( "{0}/{1}", Path.GetFileNameWithoutExtension( e.CodeBase ), ver );
            GameVersion = Version.Parse( ver );
            return;
         }
      } catch ( Exception ex ) { Log?.Error( ex ); } }

      public static void LoadMods ( string phase ) { try {
         Log.Info( "Calling {0} mods", phase );
         foreach ( var mod in EnabledMods ) {
            if ( mod.Metadata.Dlls == null ) continue;
            foreach ( var dll in mod.Metadata.Dlls ) {
               if ( dll.Methods == null ) continue;
               if ( ! dll.Methods.TryGetValue( phase, out var entries ) ) continue;
               var lib = LoadDll( dll.Path );
               if ( lib == null ) continue;
               foreach ( var type in entries )
                  CallInit( mod, lib, type, phase );
            }
         }
         Log.Flush();
      } catch ( Exception ex ) { Log.Error( ex ); } }

      #region Parsing
      public static void BuildModList () { try { lock ( AllMods ) {
         AllMods.Clear();
         EnabledMods.Clear();
         if ( Directory.Exists( ModDirectory ) ) {
            ScanFolderForMods( ModDirectory, true );
            ResolveMods();
         }
         Log.Info( "{0} mods found, {1} enabled.", AllMods.Count, EnabledMods.Count );
      } } catch ( Exception ex ) { Log.Error( ex ); } }

      public static void ScanFolderForMods ( string path, bool isRoot ) {
         Log.Log( isRoot ? SourceLevels.Information : SourceLevels.Verbose, "Scanning for mods: {0}", path );
         var container = Path.GetFileName( path );
         var foundMod = false;
         foreach ( var dll in Directory.EnumerateFiles( path, "*.dll" ) ) {
            var name = Path.GetFileNameWithoutExtension( dll ).ToLowerInvariant();
            if ( IGNORE_FILE_NAMES.Contains( name ) ) continue;
            if ( isRoot || NameMatch( container, name ) ) {
               var info = ParseMod( dll, container );
               if ( info != null ) {
                  AllMods.Add( info );
                  foundMod = true;
               }
            }
         }
         if ( ! isRoot && foundMod ) return;
         foreach ( var dir in Directory.EnumerateDirectories( path ) ) {
            if ( isRoot || NameMatch( container, Path.GetFileName( dir ) ) )
               ScanFolderForMods( dir, false );
         }
      }

      private static readonly Regex DropFromName = new Regex( "\\W+", RegexOptions.Compiled );

      private static bool NameMatch ( string container, string subject ) {
         if ( container == null || subject == null ) return false;
         container = DropFromName.Replace( container, "" ).ToLowerInvariant();
         subject = DropFromName.Replace( subject, "" ).ToLowerInvariant();
         if ( container.Length < 3 || subject.Length < 3 ) return false;
         int len = Math.Max( 3, (int) Math.Round( Math.Min( container.Length, subject.Length ) * 2.0 / 3.0 ) );
         return container.Substring( 0, len ) == subject.Substring( 0, len );
      }

      public static ModEntry ParseMod ( string file, string container ) { try {
         ModMeta meta;
         if ( file.EndsWith( ".dll", StringComparison.InvariantCultureIgnoreCase ) ) {
            meta = ParseDllInfo( file );
            var info = FindEmbeddedModInfo( file );
            if ( info != null ) {
               Log.Verbo( "Parsing embedded mod_info" );
               meta.ImportFrom( ParseInfoJs( info )?.EraseModsAndDlls() );
            }
         } else {
            Log.Verbo( $"Parsing as mod_info: {file}" );
            meta = ParseInfoJs( File.ReadAllText( file, Encoding.UTF8 ).Trim() );
            if ( ! meta.HasContent ) {
               var dlls = Directory.EnumerateFiles( Path.GetDirectoryName( file ), "*.dll" );
               if ( dlls.Count() > 1 ) dlls = dlls.Where( e => NameMatch( container, Path.GetFileNameWithoutExtension( e ) ) ).ToArray();
               if ( dlls.Count() == 1 ) {
                  var autodll = dlls.First();
                  Log.Verbo( "Dll not specified; automatically using {0}", autodll );
                  meta.Dlls = new DllMeta[] { new DllMeta{ Path = autodll, Methods = ParseEntryPoints( autodll ) } };
               }
            }
         }
         meta = ValidateMod( meta );
         if ( meta == null ) {
            Log.Warn( "Not a mod: {0}", file );
            return null;
         }
         Log.Info( "Found mod {0} at {1} ({2} dlls)", meta.Id, file, meta.Dlls?.Length ?? 0 );
         return new ModEntry{ Path = file, Metadata = meta };
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }

      private static ModMeta ValidateMod ( ModMeta meta ) {
         if ( meta == null ) return null;
         switch ( meta.Id ) {
            case "modnix" : case "":
            case "phoenixpoint" : case "phoenix point" :
            case "ppml" : case "phoenixpointmodloader" : case "phoenix point mod loader" :
            case "non-modnix" : case "nonmodnix" :
               Log.Warn( "{0} is a reserved mod id.", meta.Id );
               return null;
         }
         return meta;
      }

      private static ModMeta ParseInfoJs ( string js ) { try {
         js = js?.Trim();
         if ( js == null || js.Length <= 2 ) return null;
         // Remove ( ... ) to make parsable json
         if ( js[0] == '(' && js[js.Length-1] == ')' )
            js = js.Substring( 1, js.Length - 2 ).Trim();
         return ModMetaJson.ParseMod( js ).Normalise();
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }

      private static ModMeta ParseDllInfo ( string file ) { try {
         Log.Verbo( $"Parsing as dll: {file}" );
         var info = FileVersionInfo.GetVersionInfo( file );
         var meta = new ModMeta{
            Id = Path.GetFileNameWithoutExtension( file ).ToLowerInvariant().Trim(),
            Name = new TextSet{ Default = info.FileDescription.Trim() },
            Version = Version.Parse( info.FileVersion ),
            Description = new TextSet{ Default = info.Comments.Trim() },
            Author = new TextSet{ Default = info.CompanyName.Trim() },
            Copyright = new TextSet { Default = info.LegalCopyright.Trim() },
            Dlls = new DllMeta[] { new DllMeta{ Path = file, Methods = ParseEntryPoints( file ) } },
         };
         if ( meta.Dlls[0].Methods == null ) return null;
         return meta.Normalise();
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }

      private static string FindEmbeddedModInfo ( string file ) {
         using ( var lib = AssemblyDefinition.ReadAssembly( file ) ) {
            if ( ! lib.MainModule.HasResources ) return null;
            var res = lib.MainModule?.Resources.FirstOrDefault() as EmbeddedResource;
            if ( res == null || res.ResourceType != ResourceType.Embedded ) return null;
            using ( var reader = new ResourceReader( res.GetResourceStream() ) ) {
               var data = reader.GetEnumerator();
               while ( data.MoveNext() ) {
                  if ( data.Key.ToString().ToLowerInvariant() == "mod_info" )
                     return data.Value?.ToString();
               }
            }
         }
         return null;
      }

      private static DllEntryMeta ParseEntryPoints ( string file ) {
         DllEntryMeta result = null;
         using ( var lib = AssemblyDefinition.ReadAssembly( file ) ) {
            foreach ( var type in lib.MainModule.GetTypes() ) {
               foreach ( var method in type.Methods ) {
                  string name = method.Name;
                  if ( Array.IndexOf( PHASES, name ) < 0 ) continue;
                  if ( name == "Initialize" && ! type.Interfaces.Any( e => e.InterfaceType.FullName == "PhoenixPointModLoader.IPhoenixPointMod" ) ) {
                     Log.Warn( "Ignoring {0}.Initialize because not IPhoenixPointMod", type.FullName );
                     continue;
                  }
                  if ( result == null ) result = new DllEntryMeta();
                  if ( ! result.TryGetValue( name, out var list ) )
                     result[ name ] = list = new HashSet<string>();
                  if ( list.Contains( type.FullName ) ) {
                     Log.Warn( "Found overloaded {0}.{1}, removing all.", type.FullName, name );
                     list.Remove( type.FullName );
                     goto NextType;
                  } else {
                     list.Add( type.FullName );
                     Log.Verbo( "Found {0}.{1}", type.FullName, name );
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
      #endregion

      #region Resolving
      private static bool NeedMoreResolve;

      private static void ResolveMods () {
         NeedMoreResolve = true;
         for ( int i = 0 ; i < 100 && NeedMoreResolve ; i++ ) {
            NeedMoreResolve = false;
            EnabledMods.Clear();
            EnabledMods.AddRange( AllMods.Where( e => ! e.Disabled ) );
            Log.Info( "Resolving {0} mods, iteration {1}", EnabledMods.Count, i );
            CheckModRequirements();
         }
      }

      private static Version GetVersionById ( string id ) {
         if ( string.IsNullOrEmpty( id ) ) return LoaderVersion;
         id = id.Trim().ToLowerInvariant();
         switch ( id ) {
            case "modnix" : case "":
               return LoaderVersion;
            case "phoenixpoint" : case "phoenix point" :
               return GameVersion;
            case "ppml" : case "phoenixpointmodloader" : case "phoenix point mod loader" :
               return PPML_COMPAT;
            case "non-modnix" : case "nonmodnix" :
               return null;
         }
         var target = EnabledMods.Find( e => e.Metadata.Id.ToLowerInvariant() == id );
         if ( target != null ) return target.Metadata.Version ?? new Version( 0, 0 );
         return null;
      }

      private static void CheckModRequirements () {
         foreach ( var mod in EnabledMods.ToArray() ) {
            var reqs = mod.Metadata.Requires;
            if ( reqs == null ) continue;
            foreach ( var req in reqs ) {
               Version ver = GetVersionById( req.Id );
               var pass = ver != null;
               if ( pass && req.Min != null && req.Min > ver ) pass = false;
               if ( pass && req.Max != null && req.Max < ver ) pass = false;
               if ( ! pass ) {
                  Log.Info( "Mod [{0}] requirement {1} [{2}-{3}] failed, found {4}", mod.Metadata.Id, req.Id, req.Min, req.Max, ver );
                  mod.Disabled = true;
                  mod.AddNotice( SourceLevels.Error, "requires", req.Id, req.Min, req.Max, ver );
                  EnabledMods.Remove( mod );
                  NeedMoreResolve = true;
               }
            }
         }
      }
      #endregion

      #region Loading
      public static Assembly LoadDll ( string path ) { try {
         Log.Info( "Loading {0}", path );
         return Assembly.LoadFrom( path );
      } catch ( Exception ex ) { Log.Error( ex ); return null; } }

      private static Action<object> LoggerA ( Logger log ) => ( msg ) =>
         log.Log( msg is Exception ? SourceLevels.Error : SourceLevels.Information, msg, null );
      private static Action<object,object[]> LoggerB ( Logger log ) => ( msg, augs ) =>
         log.Log( msg is Exception ? SourceLevels.Error : SourceLevels.Information, msg, augs );
      private static Action<SourceLevels,object> LoggerC ( Logger log ) => ( lv, msg ) => log.Log( lv, msg, null );
      private static Action<SourceLevels,object,object[]> LoggerD ( Logger log ) => ( lv, msg, augs ) => log.Log( lv, msg, augs );

      public static void CallInit ( ModEntry mod, Assembly dll, string typeName, string methodName ) { try {
         Type type = dll.GetType( typeName );
         if ( type == null ) {
            Log.Error( "Cannot find type {1} in {0}", dll.Location, typeName );
            return;
         }

         MethodInfo func = type.GetMethod( methodName, ALL_BINDING_FLAGS );
         if ( func == null ) {
            Log.Error( "Cannot find {1}.{2} in {0}", dll.Location, typeName, methodName );
            return;
         }
         List<object> augs = new List<object>();
         foreach ( var aug in func.GetParameters() ) {
            var pType = aug.ParameterType;
            // Version checkers
            if ( pType == typeof( Func<string,Version> ) )
               augs.Add( (Func<string,Version>) GetVersionById );
            else if ( pType == typeof( Assembly ) )
               augs.Add( Assembly.GetExecutingAssembly() );
            // Loggers
            else if ( pType == typeof( Action<object> ) )
               augs.Add( LoggerA( CreateLogger( mod ) ) );
            else if ( pType == typeof( Action<object,object[]> ) )
               augs.Add( LoggerB( CreateLogger( mod ) ) );
            else if ( pType == typeof( Action<SourceLevels,object> ) )
               augs.Add( LoggerC( CreateLogger( mod ) ) );
            else if ( pType == typeof( Action<SourceLevels,object,object[]> ) )
               augs.Add( LoggerD( CreateLogger( mod ) ) );
            // Mod info
            else if ( pType == typeof( ModMeta ) )
               augs.Add( mod.Metadata );
            else if ( pType == typeof( ModEntry ) )
               augs.Add( mod );
            // Defaults
            else if ( pType.IsValueType )
               augs.Add( Activator.CreateInstance( pType ) );
            else
               augs.Add( null );
         }
         Func<string> augTxt = () => string.Join( ", ", augs.Select( e => e?.GetType()?.Name ?? "null" ) );
         Log.Info( "Calling {1}.{2}({3}) in {0}", mod.Path, typeName, methodName, augTxt );
         var target = func.IsStatic ? null : Activator.CreateInstance( type );
         func.Invoke( target, augs.ToArray() );
      } catch ( Exception ex ) { Log.Error( ex ); } }

      private static Logger CreateLogger ( ModEntry mod ) { lock ( mod ) {
         if ( mod.Logger == null ) {
            var logger = mod.Logger = new LoggerProxy( Log );
            var filters = logger.Filters;
            filters.Add( LogFilters.IgnoreDuplicateExceptions );
            filters.Add( LogFilters.AutoMultiParam );
            filters.Add( LogFilters.AddPrefix( mod.Metadata.Id + "┊" ) );
         }
         return mod.Logger;
      } }
      #endregion
   }

   internal static class Tools {
      internal static string FixSlash ( this string path ) => path.Replace( '/', Path.DirectorySeparatorChar );
   }
}