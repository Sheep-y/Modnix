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

   public class ModScanner {
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

      internal static string NormaliseModId ( string Id ) => Id.Trim().ToLowerInvariant();

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
         Log.Log( isRoot ? SourceLevels.Information : SourceLevels.Verbose, "Scanning for mods: {0}", path );
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

       private static readonly Regex DropFromName = new Regex( "\\W+", RegexOptions.Compiled );

      private static bool NameMatch ( string container, string subject ) {
         if ( container == null || subject == null ) return false;
         container = DropFromName.Replace( container, "" ).ToLowerInvariant();
         subject = DropFromName.Replace( subject, "" ).ToLowerInvariant();
         if ( container.Length < 3 || subject.Length < 3 ) return false;
         var len = Math.Max( 3, (int) Math.Round( Math.Min( container.Length, subject.Length ) * 2.0 / 3.0 ) );
         return container.Substring( 0, len ) == subject.Substring( 0, len );
      }

      public static ModEntry ParseMod ( string file, string container ) { try {
         ModMeta meta;
         if ( file.EndsWith( ".dll", StringComparison.InvariantCultureIgnoreCase ) ) {
            meta = ParseDllInfo( file );
            if ( meta == null ) return null;
            var info = FindEmbeddedModInfo( file );
            if ( info != null ) {
               Log.Verbo( "Parsing embedded mod_info" );
               meta.ImportFrom( ParseInfoJs( info )?.EraseModsAndDlls() );
            }
         } else {
            Log.Verbo( $"Parsing as mod_info: {file}" );
            var default_id = Path.GetFileNameWithoutExtension( file );
            if ( default_id.ToLowerInvariant() == "mod_info" ) default_id = container;
            meta = ParseInfoJs( File.ReadAllText( file, Encoding.UTF8 ).Trim(), default_id );
            if ( meta == null ) return null;
            if ( ! meta.HasContent )
               meta.Dlls = Directory.EnumerateFiles( Path.GetDirectoryName( file ), "*.dll" )
                  .Where( e => NameMatch( container, Path.GetFileNameWithoutExtension( e ) ) )
                  .Select( e => new DllMeta { Path = e } ).ToArray();
            if ( meta.Dlls != null ) {
               foreach ( var dll in meta.Dlls ) {
                  if ( dll.Methods == null || dll.Methods.Count == 0 )
                     dll.Methods = ParseEntryPoints( dll.Path );
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
            meta.Normalise(); // Fill in Name if null
         }
         return meta;
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }

      private static ModMeta ParseDllInfo ( string file ) { try {
         Log.Verbo( $"Parsing as dll: {file}" );
         var methods = ParseEntryPoints( file );
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

      private static string FindEmbeddedModInfo ( string file ) { try {
         using ( var lib = AssemblyDefinition.ReadAssembly( file ) ) {
            if ( ! lib.MainModule.HasResources ) return null;
            var res = lib.MainModule?.Resources.FirstOrDefault() as EmbeddedResource;
            if ( res == null || res.ResourceType != ResourceType.Embedded ) return null;
            if ( res.Name.ToLowerInvariant().Contains( ".mod_info.js" ) ) {
               return Encoding.UTF8.GetString( res.GetResourceData() );
            } else if ( res.Name.EndsWith( ".resources", StringComparison.InvariantCultureIgnoreCase ) ) {
               using ( var reader = new ResourceReader( res.GetResourceStream() ) ) {
                  var data = reader.GetEnumerator();
                  while ( data.MoveNext() ) {
                     if ( data.Key.ToString().ToLowerInvariant() == "mod_info" )
                        return data.Value?.ToString();
                  }
               }
            }
         }
         return null;
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }

      private static DllEntryMeta ParseEntryPoints ( string file ) {
         DllEntryMeta result = null;
         using ( var lib = AssemblyDefinition.ReadAssembly( file ) ) {
            foreach ( var type in lib.MainModule.GetTypes() ) {
               HashSet<string> ignored = null;
               foreach ( var method in type.Methods ) {
                  var name = method.Name;
                  if ( Array.IndexOf( ModLoader.PHASES, name ) < 0 ) continue;
                  if ( ignored != null && ignored.Contains( name ) ) continue;
                  if ( name == "Initialize" && ! type.Interfaces.Any( e => e.InterfaceType.FullName == "PhoenixPointModLoader.IPhoenixPointMod" ) ) {
                     Log.Verbo( "Ignoring {0}.Initialize because not IPhoenixPointMod", type.FullName );
                     continue;
                  }
                  if ( result == null ) result = new DllEntryMeta();
                  if ( ! result.TryGetValue( name, out var list ) )
                     result[ name ] = list = new HashSet<string>();
                  if ( list.Contains( type.FullName ) ) {
                     Log.Warn( "Ignoring all overloaded {0}.{1}", type.FullName, name );
                     if ( ignored == null ) ignored = new HashSet<string>();
                     ignored.Add( name );
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
         if ( result != null ) {
            if ( result.Count > 1 )
               result.Remove( "Initialize" );
            if ( result.Count > 1 )
               result.Remove( "Init" );
            else if ( result.Count <= 0 ) {
               Log.Warn( "Mod initialisers not found in {0}", file );
               return null;
            }
         }
         return result;
      }

      private static bool ValidateMod ( ModMeta meta ) {
         if ( meta == null ) return false;
         if ( string.IsNullOrWhiteSpace( meta.Id ) ) {
            Log.Warn( "Id must not be empty" );
            return false;
         }
         switch ( meta.Id.ToLowerInvariant() ) {
            case "modnix" :
            case "phoenixpoint" : case "phoenix point" :
            case "ppml" : case "ppml+" : case "phoenixpointmodloader" : case "phoenix point mod loader" :
            case "non-modnix" : case "nonmodnix" :
               Log.Warn( "{0} is a reserved mod id.", meta.Id );
               return false;
            default:
               return true;
         }
      }
      #endregion

      #region Resolving
      private static void ResolveMods () {
         EnabledMods.Clear();
         EnabledMods.AddRange( AllMods.Where( e => ! e.Disabled ) );
         Log.Info( "Resolving {0} mods", EnabledMods.Count );
         RemoveDuplicateMods();
         RemoveUnfulfilledMods();
         RemoveConflictMods();
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

      private static ModEntry FindLatestMod ( ModEntry[] clones ) {
         ModEntry best = clones[0];
         foreach ( var mod in clones ) {
            if ( mod == best ) continue;
            if ( TestAndSwap( mod.Metadata.Version, best.Metadata.Version, mod, ref best ) ) continue;
            FileInfo myInfo = new FileInfo( mod.Path ), bestInfo = new FileInfo( best.Path );
            if ( TestAndSwap( myInfo.LastWriteTime, bestInfo.LastWriteTime, mod, ref best ) ) continue;
            if ( TestAndSwap( myInfo.Length, bestInfo.Length, mod, ref best ) ) continue;
         }
         return best;
      }

      private static bool TestAndSwap < T > ( T our, T their, ModEntry me, ref ModEntry best ) where T : IComparable {
         if ( our == null && me == null ) return false;
         if ( our == null ) return true; // We are null while they are not
         if ( their != null ) {
            var compare = our.CompareTo( their );
            if ( compare == 0 ) return false; // We are same
            if ( compare < 0 ) return true; // We are smaller
         } // Either we are bigger, or we are non-null while they are
         best = me;
         return true;
      }

      internal static ModEntry GetModById ( string key ) {
         key = NormaliseModId( key );
         return EnabledMods.Find( e => e.Key == key && ! e.Disabled );
      }

      internal static Version GetVersionById ( string key ) {
         if ( string.IsNullOrEmpty( key ) ) return ModLoader.LoaderVersion;
         key = NormaliseModId( key );
         switch ( key ) {
            case "modnix" : case "":
               return ModLoader.LoaderVersion;
            case "phoenixpoint" : case "phoenix point" :
               return ModLoader.GameVersion;
            case "ppml" : case "ppml+" : case "phoenixpointmodloader" : case "phoenix point mod loader" :
               return ModLoader.PPML_COMPAT;
            case "non-modnix" : case "nonmodnix" :
               return null;
            default:
               return GetVersionFromMod( GetModById( key ) );
         }
      }

      private static Version GetVersionFromMod ( ModEntry mod ) {
         if ( mod == null ) return null;
         return mod.Metadata.Version ?? new Version( 0, 0 );
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
                     DisableAndRemoveMod( mod, "requires", "Mod {0} requirement {1} [{2}-{3}] failed, found {4}",
                        mod.Metadata.Id, req.Id, req.Min, req.Max, ver );
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
            var targets = mod.Metadata.Conflicts;
            if ( targets == null ) continue;
            foreach ( var req in targets ) {
               var target = GetModById( req.Id );
               if ( target == null || target == mod || target.Disabled ) continue;
               var ver = GetVersionFromMod( target );
               if ( req.Min != null && req.Min > ver ) continue;
               if ( req.Max != null && req.Max < ver ) continue;
               DisableAndRemoveMod( target, "conflict", "Mod {1} (v{3}) is marked as conflicting with {2} [{4}-{5}]",
                  mod, target.Metadata.Id, mod.Metadata.Id, ver, req.Min, req.Max );
            }
         }
      }

      private static void DisableAndRemoveMod ( ModEntry mod, string reason, string log, params object[] augs ) {
         if ( mod.Disabled ) return;
         Log.Warn( log, augs );
         mod.Disabled = true;
         mod.AddNotice( SourceLevels.Error, reason, augs );
         EnabledMods.Remove( mod );
      }
      #endregion
   }
}