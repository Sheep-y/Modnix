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

   public static class ModScanner {
      public readonly static List<ModEntry> AllMods = new List<ModEntry>();
      public readonly static List<ModEntry> EnabledMods = new List<ModEntry>();

      internal const BindingFlags INIT_METHOD_FLAGS = Public | Static | Instance;
      private static readonly List<string> IGNORE_FILE_NAMES = new List<string> {
         "0harmony",
         "mod_info",
         "modnixloader",
         "mono.cecil",
         "phoenixpointmodloader",
         "ppmodloader",
      };
      private static Logger Log => ModLoader.Log;

      private static readonly Regex IgnoreInModId = new Regex( "[^\\w.-]+", RegexOptions.Compiled );
      internal static string NormaliseModId ( string Id ) {
         if ( Id == null ) return null;
         return IgnoreInModId.Replace( Id.Trim().ToLowerInvariant(), "" );
      }

      #region Scanning
      public static void BuildModList ( ) { try { lock ( AllMods ) {
         AllMods.Clear();
         EnabledMods.Clear();
         if ( Directory.Exists( ModLoader.ModDirectory ) ) {
            ScanFolderForMods( ModLoader.ModDirectory, true );
            ResolveMods();
         }
         Log.Info( "{0} mods found, {1} enabled.", AllMods.Count, EnabledMods.Count );
      } } catch ( Exception ex ) { Log.Error( ex ); } }

      public static void ScanFolderForMods ( string path, bool isRoot ) {
         Log.Log( isRoot ? TraceEventType.Information : TraceEventType.Verbose, "Scanning for mods: {0}", path );
         var container = Path.GetFileName( path );
         if ( ! isRoot ) {
            var file = Path.Combine( path, "mod_info.js" );
            if ( File.Exists( file ) && AddMod( ParseMod( file, container ) ) ) return;
            file = Path.Combine( path, "mod_info.json" );
            if ( File.Exists( file ) && AddMod( ParseMod( file, container ) ) ) return;
         }
         var foundMod = false;
         foreach ( var target in new string[] { "*.js", "*.json", "*.dll" } ) {
            foreach ( var file in Directory.EnumerateFiles( path, target ) ) {
               var name = Path.GetFileNameWithoutExtension( file ).ToLowerInvariant();
               if ( IGNORE_FILE_NAMES.Contains( name ) ) continue;
               if ( ( isRoot || NameMatch( container, name ) ) && AddMod( ParseMod( file, container ) ) )
                  foundMod = true;
            }
            if ( ! isRoot && foundMod ) return;
         }
         foreach ( var dir in Directory.EnumerateDirectories( path ) ) {
            if ( isRoot || NameMatch( container, Path.GetFileName( dir ) ) )
               ScanFolderForMods( dir, false );
         }
      }

      private static bool AddMod ( ModEntry mod ) {
         if ( mod == null ) return false;
         AllMods.Add( mod );
         return true;
      }

      // PP prefix | non-word characters | ( .tar | browser copies | windows copies | nexus mod ids )+$
      private static readonly Regex IgnoreInFolderName = new Regex( "(^Phoenix[ -_]?P(?:oin)?t[ _-]?|\\W+|(\\.tar|\\(\\d+\\)| - Copy|-\\d+)+$)", RegexOptions.Compiled | RegexOptions.IgnoreCase );

      public static bool NameMatch ( string container, string subject ) {
         if ( container == null || subject == null ) return false;
         container = IgnoreInFolderName.Replace( container, "" ).ToLowerInvariant();
         subject = IgnoreInFolderName.Replace( subject, "" ).ToLowerInvariant();
         if ( container.Length < 3 || subject.Length < 3 ) return false;
         var len = Math.Max( 3, (int) Math.Round( Math.Min( container.Length, subject.Length ) * 2.0 / 3.0 ) );
         return container.Substring( 0, len ) == subject.Substring( 0, len );
      }

      private static ModEntry ParseMod ( string file, string container ) { try {
         ModMeta meta;
         if ( file.EndsWith( ".dll", StringComparison.OrdinalIgnoreCase ) ) {
            meta = ParseDllInfo( file );
            if ( meta == null ) return null;
            var info = new StringBuilder();
            if ( FindEmbeddedFile( file, info, "mod_info", "mod_info.js", "mod_info.json" ) ) {
               Log.Verbo( "Parsing embedded mod_info" );
               meta.ImportFrom( ParseInfoJs( info.ToString() )?.EraseModsAndDlls() );
            }
         } else {
            Log.Verbo( $"Parsing as mod_info: {file}" );
            var default_id = Path.GetFileNameWithoutExtension( file );
            if ( string.IsNullOrWhiteSpace( default_id ) || default_id.Equals( "mod_info", StringComparison.OrdinalIgnoreCase ) )
               default_id = container;
            meta = ParseInfoJs( File.ReadAllText( file, Encoding.UTF8 ).Trim(), default_id );
            if ( meta == null ) return null;
            if ( ! meta.HasContent )
               meta.Dlls = Directory.EnumerateFiles( Path.GetDirectoryName( file ), "*.dll" )
                  .Where( e => NameMatch( container, Path.GetFileNameWithoutExtension( e ) ) )
                  .Select( e => new DllMeta { Path = e } ).ToArray();
            if ( meta.Dlls != null ) {
               foreach ( var dll in meta.Dlls ) {
                  if ( dll.Methods == null || dll.Methods.Count == 0 )
                     dll.Methods = ParseEntryPoints( dll.Path, true );
               }
            }
         }
         if ( ! ValidateMod( meta ) ) {
            Log.Info( "Not a mod: {0}", file );
            return null;
         }
         Log.Info( "Found mod {0} at {1} ({2} dlls)", meta.Id, file, meta.Dlls?.Length ?? 0 );
         return new ModEntry( file, meta );
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }

      private static ModMeta ParseInfoJs ( string js, string default_id = null ) { try {
         js = js?.Trim();
         if ( js == null || js.Length <= 2 ) return null;
         // Remove ( ... ) to make parsable json
         if ( js[0] == '(' && js[js.Length-1] == ')' )
            js = js.Substring( 1, js.Length - 2 ).Trim();
         var meta = ModMetaJson.ParseMod( js ).Normalise();
         if ( meta.Id == null && default_id != null ) {
            meta.Id = default_id;
            meta.Normalise(); // Fill in Name
         }
         return meta;
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }

      private static ModMeta ParseDllInfo ( string file ) { try {
         Log.Verbo( $"Parsing as dll: {file}" );
         var methods = ParseEntryPoints( file, false );
         if ( methods == null ) return null;
         var info = FileVersionInfo.GetVersionInfo( file );
         return new ModMeta{
            Id = Path.GetFileNameWithoutExtension( file ).Trim(),
            Name = new TextSet{ Default = info.FileDescription.Trim() },
            Version = Version.Parse( info.FileVersion ),
            Description = new TextSet{ Default = info.Comments.Trim() },
            Author = new TextSet{ Default = info.CompanyName.Trim() },
            Copyright = new TextSet { Default = info.LegalCopyright.Trim() },
            Dlls = new DllMeta[] { new DllMeta{ Path = file, Methods = methods } },
         }.Normalise();
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }

      public static bool FindEmbeddedFile ( string file, StringBuilder text, params string[] names ) { try {
         var dotName = names.Select( e => '.' + e ).ToArray();
         using ( var lib = AssemblyDefinition.ReadAssembly( file ) ) {
            if ( ! lib.MainModule.HasResources ) return false;
            foreach ( var resource in lib.MainModule.Resources ) {
               if ( ! ( resource is EmbeddedResource res ) || res.ResourceType != ResourceType.Embedded ) continue;
               if ( dotName.Any( e => res.Name.EndsWith( e, StringComparison.OrdinalIgnoreCase ) ) ) {
                  if ( text != null )
                     text.Append( ModMetaJson.ReadAsText( res.GetResourceStream() ) );
                  return true;
               }
               if ( ! res.Name.EndsWith( ".resources", StringComparison.OrdinalIgnoreCase ) ) continue;
               using ( var reader = new ResourceReader( res.GetResourceStream() ) ) {
                  var data = reader.GetEnumerator();
                  while ( data.MoveNext() ) {
                     var item = data.Key.ToString();
                     if ( names.Any( e => item.EndsWith( e, StringComparison.OrdinalIgnoreCase ) ) ) {
                        if ( text != null )
                           text.Append( data.Value is Stream stream ? ModMetaJson.ReadAsText( stream ) : data.Value?.ToString() );
                        return true;
                     }
                  }
               }
            }
         }
         return false;
      } catch ( Exception ex ) { Log.Warn( ex ); return false; } }

      private static DllEntryMeta ParseEntryPoints ( string file, bool active ) {
         DllEntryMeta result = null;
         using ( var lib = AssemblyDefinition.ReadAssembly( file ) ) {
            foreach ( var type in lib.MainModule.GetTypes() ) {
               if ( type.IsNested || type.IsNotPublic || type.IsInterface || ( type.IsAbstract && ! type.IsSealed ) || type.IsEnum || type.Name.IndexOf( '<' ) >= 0 ) continue;
               var count = 0;
               foreach ( var method in type.Methods ) {
                  if ( ! method.IsPublic || method.IsConstructor || method.IsAbstract || method.ContainsGenericParameter ) continue;
                  ++count;
                  var name = method.Name;
                  if ( name.Length == 0 || name[0] == 'P' || Array.IndexOf( ModLoader.PHASES, name ) < 0 ) continue; // Skip Prefix/Postfix, then check phase
                  // if ( method.CustomAttributes.Any( e => e.AttributeType.FullName.Equals( "System.ObsoleteAttribute" ) ) ) continue;
                  if ( name == "Initialize" && ! type.Interfaces.Any( e => e.InterfaceType.FullName == "PhoenixPointModLoader.IPhoenixPointMod" ) ) {
                     Log.Info( "Ignoring {0}.Initialize because not IPhoenixPointMod", type.FullName );
                     continue;
                  }
                  if ( result == null ) result = new DllEntryMeta();
                  if ( ! result.TryGetValue( name, out var list ) )
                     result[ name ] = list = new HashSet<string>();
                  if ( ! list.Contains( type.FullName ) ) {
                     list.Add( type.FullName );
                     Log.Verbo( "Found {0}.{1}", type.FullName, name );
                  }
               }
               Log.Verbo( "Scanned {1} public methods on {0}", type.FullName, count );
            }
         }
         // Remove Init from Modnix DLLs, so that they will not be initiated twice
         if ( result != null ) {
            if ( result.Count > 1 ) // Ignore PPML+ first to prevent giving the wrong signal, because we are only partially compatible
               result.Remove( "Initialize" );
            if ( result.Count > 1 )
               result.Remove( "Init" );
         } else if ( active )
            Log.Warn( "Mod initialisers not found in {0}", file );
         return result;
      }

      private static bool ValidateMod ( ModMeta meta ) {
         if ( meta == null ) return false;
         var key = NormaliseModId( meta.Id );
         if ( string.IsNullOrWhiteSpace( key ) ) {
            Log.Warn( "Id must not be empty" );
            return false;
         }
         switch ( key ) {
            case "modnix" : case "loader" :
            case "phoenixpoint" : case "phoenix point" : case "game" :
            case "ppml" : case "ppml+" : case "phoenixpointmodloader" : case "phoenix point mod loader" :
            case "non-modnix" : case "nonmodnix" :
               Log.Warn( "{0} is a reserved mod id.", meta.Id );
               return false;
         }
         return true;
      }
      #endregion

      #region Mod Query
      private static ModEntry _GetModById ( string key ) => EnabledMods.Find( e => e.Key == key && ! e.Disabled );

      internal static ModEntry GetModById ( string id ) => _GetModById( NormaliseModId( id ) );

      internal static Version GetVersionById ( string id ) {
         if ( string.IsNullOrEmpty( id ) ) return ModLoader.LoaderVersion;
         id = NormaliseModId( id );
         switch ( id ) {
            case "modnix" : case "loader" : case "":
               return ModLoader.LoaderVersion;
            case "phoenixpoint" : case "phoenix point" : case "game" :
               return ModLoader.GameVersion;
            case "ppml" : case "ppml+" : case "phoenixpointmodloader" : case "phoenix point mod loader" :
               return ModLoader.PPML_COMPAT;
            case "non-modnix" : case "nonmodnix" :
               return null;
            default:
               return GetVersionFromMod( _GetModById( id ) );
         }
      }

      private static Version GetVersionFromMod ( ModEntry mod ) {
         if ( mod == null ) return null;
         return mod.Metadata.Version ?? new Version( 0, 0, 0, 0 );
      }

      private static ModEntry FindLatestMod ( ModEntry[] clones ) {
         ModEntry best = clones[0];
         foreach ( var mod in clones ) {
            if ( mod == best ) continue;
            if ( CompareModVersion( mod, best ) == 1 )
               best = mod;
         }
         return best;
      }

      private static int CompareModIndex ( ModEntry x, ModEntry y ) {
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

      #region Resolving
      private static void ResolveMods () {
         EnabledMods.Clear();
         EnabledMods.AddRange( AllMods.Where( e => ! e.Disabled ) );
         Log.Info( "Resolving {0} mods", EnabledMods.Count );
         ApplyUserOverride();
         EnabledMods.Sort( CompareModIndex );
         RemoveDuplicateMods();
         RemoveUnfulfilledMods();
         RemoveConflictMods();
      }

      private static void ApplyUserOverride () {
         var settings = ModLoader.Settings.Mods;
         if ( settings == null ) return;
         Log.Verbo( "Check manual mod settings" );
         foreach ( var mod in EnabledMods.ToArray() ) {
            if ( ! settings.TryGetValue( mod.Key, out ModSettings modSetting ) ) continue;
            if ( modSetting.Disabled )
               DisableAndRemoveMod( mod, "manual", "Mod {0} is manually disabled.", mod.Key );
            else if ( modSetting.LoadIndex.HasValue ) {
               Log.Verbo( "Mod {0} LoadIndex manually set to {1}", mod.Key, modSetting.LoadIndex);
               lock ( mod.Metadata ) mod.Metadata.LoadIndex = modSetting.LoadIndex.Value;
            } else if ( modSetting.LogLevel.HasValue ) {
               Log.Verbo( "Mod {0} LogLevel set to {1}", mod.Key, modSetting.LogLevel );
               lock ( mod ) mod.LogLevel = modSetting.LogLevel.Value;
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
         ModEntry keep = FindLatestMod( clones );
         foreach ( var mod in clones ) {
            if ( mod == keep ) continue;
            DisableAndRemoveMod( mod, "duplicate", "Mod {1} is a duplicate of {2}.", keep, mod.Path, keep.Path );
         }
      }

      private static void RemoveUnfulfilledMods () {
         int loopIndex = 1;
         bool NeedAnotherLoop;
         do {
            NeedAnotherLoop = false;
            Log.Verbo( "Check mod requirements, loop {0}", loopIndex );
            foreach ( var mod in EnabledMods.ToArray() ) {
               if ( mod.Disabled ) continue;
               var reqs = mod.Metadata.Requires;
               if ( reqs == null ) continue;
               foreach ( var req in reqs ) {
                  var ver = GetVersionById( req.Id );
                  var pass = ver != null;
                  if ( pass && req.Min != null && req.Min > ver ) pass = false;
                  if ( pass && req.Max != null && req.Max < ver ) pass = false;
                  if ( ! pass ) {
                     DisableAndRemoveMod( mod, "require", "Mod {4} requirement {0} [{1},{2}] failed, found {3}",
                        req.Id, req.Min, req.Max, ver, mod.Metadata.Id );
                     NeedAnotherLoop = true;
                  }
               }
            }
         } while ( NeedAnotherLoop && loopIndex++ <= 20 );
      }

      private static void RemoveConflictMods () {
         Log.Verbo( "Check mod conflicts" );
         foreach ( var mod in EnabledMods.ToArray() ) {
            if ( mod.Disabled ) continue;
            var targets = mod.Metadata.Disables;
            if ( targets == null ) continue;
            foreach ( var req in targets ) {
               var target = GetModById( req.Id );
               if ( target == null || target == mod || target.Disabled ) continue;
               var ver = GetVersionFromMod( target );
               if ( req.Min != null && req.Min > ver ) continue;
               if ( req.Max != null && req.Max < ver ) continue;
               DisableAndRemoveMod( target, "disable", "Mod {1} (v{3}) is disabled by {2} [{4},{5}]",
                  mod, target.Metadata.Id, mod.Metadata.Id, ver, req.Min, req.Max );
            }
         }
      }

      private static void DisableAndRemoveMod ( ModEntry mod, string reason, string log, params object[] augs ) { lock ( mod ) {
         if ( mod.Disabled ) return;
         Log.Warn( log, augs );
         mod.Disabled = true;
         mod.AddNotice( TraceEventType.Error, reason, augs );
         EnabledMods.Remove( mod );
      } }
      #endregion
   }
}