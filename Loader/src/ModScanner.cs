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
      internal const BindingFlags INIT_METHOD_FLAGS = Public | Static | Instance;
      private static readonly List<string> IGNORE_FILE_NAMES = new List<string> {
         "0harmony",
         "jetbrains.annotations",
         "mod_info",
         "modnixloader",
         "mono.cecil",
         "newtonsoft.json",
         "phoenixpointmodloader",
         "ppmodloader",
      };
      private static Logger Log => ModLoader.Log;

      private static readonly Regex IgnoreInModId = new Regex( "[^\\w.-]+", RegexOptions.Compiled );
      internal static string NormaliseModId ( string Id ) {
         if ( Id == null ) return null;
         return IgnoreInModId.Replace( Id.Trim().ToLowerInvariant(), "" );
      }

      public static void BuildModList ( ) { try { lock ( ModLoader.AllMods ) {
         ModLoader.AllMods.Clear();
         var dir = ModLoader.ModDirectory;
         if ( Directory.Exists( dir ) ) {
            ScanFolderForMods( dir, true );
            ModResolver.Resolve();
         } else
            Log.Error( "Mods not scanned.  Not found: {0}", dir );
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
         ModLoader.AllMods.Add( mod );
         if ( mod.IsModPack ) {
            if ( ModResolver.GetSettings( mod )?.Disabled == true ) return true;
            foreach ( var modPath in mod.Metadata.Mods ) try {
               if ( ! Tools.IsSafePath( modPath ) ) {
                  mod.Log().Error( "Invalid path: {0}", modPath );
                  continue;
               }
               var path = Path.Combine( mod.Dir, modPath );
               if ( ! File.Exists( path ) ) {
                  mod.Log().Error( $"Not found: {0}", path );
                  continue;
               }
               var submod = ParseMod( path, Path.GetFileName( path ) );
               if ( submod == null ) continue;
               submod.AddNotice( TraceEventType.Information, "parent", mod );
               mod.AddNotice( TraceEventType.Information, "submod", submod );
               AddMod( submod );
            } catch ( Exception ex ) { Log.Log( ex ); }
         }
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
         var default_id = Path.GetFileNameWithoutExtension( file ).Trim();
         if ( string.IsNullOrEmpty( default_id ) ) return null;
         if ( file.EndsWith( ".dll", StringComparison.OrdinalIgnoreCase ) ) {
            meta = ParseDllInfo( file );
            if ( meta == null ) return null;
            var info = new StringBuilder();
            if ( FindEmbeddedFile( file, info, "mod_info", "mod_info.js", "mod_info.json" ) != null ) {
               Log.Verbo( "Parsing embedded mod_info" );
               meta.ImportFrom( ParseInfoJs( info.ToString() )?.EraseModsAndDlls() );
            }
         } else {
            Log.Verbo( $"Parsing as mod_info: {file}" );
            if ( "mod_info".Equals( default_id, StringComparison.OrdinalIgnoreCase ) )
               default_id = container;
            meta = ParseInfoJs( Tools.ReadText( file ).Trim(), default_id );
            if ( meta == null ) return null;
            ScanDLLs( meta, Path.GetDirectoryName( file ), container );
         }
         if ( ! ValidateMod( meta ) ) {
            Log.Info( "Not a mod: {0}", file );
            return null;
         }
         Log.Info( "Found mod {0} at {1} ({2} actions, {3} dlls)", meta.Id, file, meta.Actions?.Length ?? 0, meta.Dlls?.Length ?? 0 );
         return new ModEntry( file, meta );
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }

      private static void ScanDLLs ( ModMeta meta, string dir, string container ) {
         if ( ! meta.HasContent )
            meta.Dlls = Directory.EnumerateFiles( dir, "*.dll" )
               .Where( e => {
                  var name = Path.GetFileNameWithoutExtension( e ).ToLowerInvariant();
                  return NameMatch( container, name ) && ! IGNORE_FILE_NAMES.Contains( name );
               } )
               .Select( e => new DllMeta { Path = e } ).ToArray();
         if ( meta.Dlls != null ) {
            foreach ( var dll in meta.Dlls )
               if ( dll.Methods == null || dll.Methods.Count == 0 )
                  dll.Methods = ParseEntryPoints( dll.Path, true );
         }
      }

      public static string TrimBrackets ( string js ) {
         js = js?.Trim();
         if ( js == null || js.Length <= 2 ) return null;
         // Remove ( ... ) to make parsable json
         return js[0] == '(' && js[js.Length-1] == ')' ? js.Substring( 1, js.Length - 2 ).Trim() : js;
      }

      private static ModMeta ParseInfoJs ( string js, string default_id = null ) { try {
         js = TrimBrackets( js );
         if ( js == null ) return null;
         var meta = Json.ParseMod( js ).Normalise();
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

      public static string FindEmbeddedFile ( string file, StringBuilder text, params string[] names ) { try {
         var dotName = names.Select( e => '.' + e ).ToArray();
         using ( var lib = AssemblyDefinition.ReadAssembly( file ) ) {
            if ( ! lib.MainModule.HasResources ) return null;
            foreach ( var resource in lib.MainModule.Resources ) {
               if ( ! ( resource is EmbeddedResource res ) || res.ResourceType != ResourceType.Embedded ) continue;
               if ( dotName.Any( e => res.Name.EndsWith( e, StringComparison.OrdinalIgnoreCase ) ) ) {
                  text?.Append( Tools.ReadText( res.GetResourceStream() ) );
                  return res.Name;
               }
               if ( ! res.Name.EndsWith( ".resources", StringComparison.OrdinalIgnoreCase ) ) continue;
               using ( var reader = new ResourceReader( res.GetResourceStream() ) ) {
                  var data = reader.GetEnumerator();
                  while ( data.MoveNext() ) {
                     var item = data.Key.ToString();
                     if ( names.Any( e => item.EndsWith( e, StringComparison.OrdinalIgnoreCase ) ) ) {
                        text?.Append( data.Value is Stream stream ? Tools.ReadText( stream ) : data.Value?.ToString() );
                        return item;
                     }
                  }
               }
            }
         }
         return null;
      } catch ( Exception ex ) { Log.Warn( ex ); return null; } }

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
                  if ( name.Length == 0 || name[0] == 'P' || ! ModPhases.PHASES.Contains( name ) ) continue; // Skip Prefix/Postfix, then check phase
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
         // Remove legacy Init from Modnix DLLs, so that the mod will not be initiated twice
         if ( result != null ) {
            // Count non-legacy initialisers
            var hasSplash = result.ContainsKey( "SplashMod" );
            var initCount = result.Keys.Count( e => e != "DisarmMod" && e != "ActionMod" );
            if ( initCount > 1 ) // Ignore PPML+ first to prevent giving the wrong signal, since we don't support console commands.
               initCount = TryRemoveInit( file, result, "Initialize", initCount );
            if ( initCount > 1 )
               initCount = TryRemoveInit( file, result, "Init", initCount );
            if ( hasSplash ) initCount--; // Keep MainMod if SplashMod is the only other method left
            if ( initCount > 1 )
               initCount = TryRemoveInit( file, result, "MainMod", initCount );
         } else if ( active )
            Log.Warn( "Mod initialisers not found in {0}", file );
         return result;
      }

      private static int TryRemoveInit ( string file, DllEntryMeta entries, string phase, int count ) {
         if ( ! entries.ContainsKey( phase ) ) return count;
         entries.Remove( phase );
         Log.Verbo( "Ignoring legacy initialiser {0} on {1}", phase, file );
         return count - 1;
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
         if ( meta.Mods != null && ( meta.Dlls != null || meta.Actions != null ) ) {
            Log.Warn( "Mod Pack cannot directly owns dlls and actions. Only mods allowed." );
            return false;
         }
         return true;
      }
   }
}