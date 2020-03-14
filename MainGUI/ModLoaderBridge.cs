using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace Sheepy.Modnix.MainGUI {
   // This class was created to separate ModLoader classes from the main program.
   internal class ModLoaderBridge {
      private readonly AppControl App = AppControl.Instance;
      private bool Loading;

      internal void CheckSetup () { lock ( App ) {
         if ( ModLoader.NeedSetup ) {
            App.Log( "Initiating ModLoader" );
            var logger = new GuiLogger( App );
            ModLoader.SetLog( logger );
            logger.Filters.Add( LogFilters.AddPrefix( "Loader┊" ) );
            App.Log( "Setup ModLoader" );
            ModLoader.Setup();
         }
      } }

      internal LoaderSettings GetSettings () {
         CheckSetup();
         return ModLoader.Settings;
      }

      internal void SaveSettings () {
         CheckSetup();
         ModLoader.SaveSettings();
      }

      internal object LoadModList () {
         lock ( App ) {
            if ( Loading ) return null;
            Loading = true;
         }
         CheckSetup();
         App.Log( "Building mod list" );
         ModScanner.BuildModList();
         lock ( App ) {
            Loading = false;
            return ModScanner.AllMods.Select( e => new GridModItem( e ) );
         }
      }

      internal void DeleteMod ( ModInfo mod ) {
         string path = Path.GetDirectoryName( mod.Path );
         if ( path == ModLoader.ModDirectory ) {
            App.Log( $"Deleting {mod.Path}" );
            File.Delete( mod.Path );
            return;
         }
         RecurDelete( path );
         RemoveEmptyFolders( path );
      }

      private void RecurDelete ( string path ) {
         foreach ( var file in Directory.EnumerateFiles( path ) ) {
            if ( Path.GetExtension( file ).Equals( ".conf", StringComparison.OrdinalIgnoreCase ) ) continue;
            App.Log( $"Deleting {file}" );
            File.Delete( file );
         }
         foreach ( var dir in Directory.EnumerateDirectories( path ) )
            RecurDelete( dir );
         if ( ! Directory.EnumerateFileSystemEntries( path ).Any() ) {
            App.Log( $"Deleting {path}" );
            Directory.Delete( path );
         }
      }

      internal void DeleteConfig ( ModInfo mod ) {
         var file = ModLoader.CheckSettingFile( mod.Path );
         if ( file != null )
            File.Delete( file );
      }

      internal void ResetConfig ( ModInfo mod ) {
         DeleteConfig( mod );
         var str = ModLoader.ReadSettingText( ( mod as GridModItem ).Mod );
         if ( str != null )
            File.WriteAllText( ModLoader.GetSettingFile( mod.Path ), str );
      }

      private void RemoveEmptyFolders ( string path ) {
         path = Path.GetDirectoryName( path );
         while ( path != ModLoader.ModDirectory && ! Directory.EnumerateFileSystemEntries( path ).Any() ) {
            App.Log( $"Deleting empty {path}" );
            Directory.Delete( path );
            path = Path.GetDirectoryName( path );
         }
      }
   }

   internal class GridModItem : ModInfo {
      internal readonly ModEntry Mod;
      internal GridModItem ( ModEntry mod ) => Mod = mod ?? throw new ArgumentNullException( nameof( mod ) );
      public override string Name => Mod.Metadata.Name?.ToString( "en" );
      public override string Version => Mod.Metadata.Version?.ToString();
      public override string Author => Mod.Metadata.Author?.ToString( "en" );
      public override string Status { get { lock ( Mod ) return Mod.Disabled ? "Disabled" : "Enabled"; } }

      public override bool Is ( ModQuery prop ) { lock ( Mod ) {
         switch ( prop ) {
            case ModQuery.IS_FOLDER :
               var path = System.IO.Path.GetDirectoryName( Path );
               return path != AppControl.Instance.ModFolder && Directory.EnumerateFileSystemEntries( path ).Count() > 1;
            case ModQuery.IS_CHILD :
               return Mod.Parent != null;
            case ModQuery.HAS_CONFIG :
               return Mod.HasConfig;
            case ModQuery.HAS_CONFIG_FILE :
               return ModLoader.CheckSettingFile( Mod.Path ) != null;
            default:
               return false;
         }
      } }

      public override void BuildDesc ( FlowDocument doc ) { lock ( Mod ) {
         doc.Replace(
            BuildBlock( BuildBasicDesc ),
            BuildBlock( BuildProvidedDesc ),
            BuildBlock( BuildLinks ),
            BuildBlock( BuildContacts ),
            BuildBlock( BuildFileList ),
            BuildCopyright()
         );
      } }

      public override void BuildSummary ( FlowDocument doc ) { lock ( Mod ) {
         var body = doc.Blocks.FirstBlock as Paragraph;
         if ( body == null ) return;
         body.Inlines.Add( new Bold( new Run( Mod.Metadata.Name.ToString( "en" ) ) ) );
         if ( Mod.HasConfig )
            body.Inlines.Add( ModLoader.CheckSettingFile( Mod.Path ) != null
                  ? "\t[has config file]" : "\t[can create config]" );
         body.Inlines.Add( "\r" );
      } }

      private void BuildBasicDesc ( ModMeta meta, InlineCollection list ) {
         list.Add( new Bold( new Run( meta.Name.ToString( "en" ) ) ) );
         if ( meta.Version != null ) list.Add( $"\tVer {Version}" );
         list.Add( $"\rType\t{Type}" );
         if ( meta.Author != null ) list.Add( $"\rAuthor\t{Author}" );
      }

      private void BuildProvidedDesc ( ModMeta meta, InlineCollection list ) {
         string desc = meta.Description?.ToString( "en" );
         if ( string.IsNullOrWhiteSpace( desc ) ) return;
         list.Add( desc );
      }

      private static void BuildLinks ( ModMeta meta, InlineCollection list ) {
         if ( meta.Url == null ) return;
         list.Add( "Link(s)" );
         BuildDict( meta.Url, list );
      }

      private void BuildContacts ( ModMeta meta, InlineCollection list ) {
         if ( meta.Contact == null ) return;
         list.Add( "Contact(s)" );
         BuildDict( meta.Contact, list );
      }

      private void BuildFileList ( ModMeta meta, InlineCollection list ) {
         Func< string, string > fileName = System.IO.Path.GetFileName;
         list.Add( $"Path\r{System.IO.Path.GetDirectoryName(Path)}\r\r" );
         list.Add( "File(s)" );
         var self = fileName( Path );
         var selfRun = new Run( "\r" + self );
         list.Add( selfRun );
         if ( Mod.HasConfig ) {
            var config = ModLoader.CheckSettingFile( Mod.Path );
            list.Add( config != null ? $"\r{fileName(config)} [Config]" : "\r(Can create config file)" );
         }
         foreach ( var e in meta.Dlls ) {
            var path = fileName( e.Path );
            var txt = "\r" + path + " [" + string.Join( ", ", e.Methods.Keys ) + "]";
            if ( path == self ) selfRun.Text = txt;
            else list.Add( txt );
         }
      }

      private Block BuildCopyright () {
         var txt = $"Modnix Id: {Mod.Metadata.Id}";
         var copy = Mod.Metadata.Copyright?.ToString( "en" );
         if ( ! string.IsNullOrWhiteSpace( copy ) ) {
            if ( ! copy.StartsWith( "Copyright", StringComparison.OrdinalIgnoreCase ) )
               copy = "Copyright: " + copy;
            txt += "\r" + copy;
         }
         return new Paragraph( new Run( txt ) );
      }

      private Block BuildBlock ( Action<ModMeta,InlineCollection> builder ) {
         var block = new Paragraph();
         builder( Mod.Metadata, block.Inlines );
         return block.Inlines.Count == 0 ? null : block;
      }

      private static void BuildDict ( TextSet data, InlineCollection list ) {
         if ( data.Dict == null ) {
            list.Add( data.Default );
            return;
         }
         foreach ( var e in data.Dict ) {
            string name = e.Key, link = e.Value;
            if ( string.IsNullOrWhiteSpace( name ) || string.IsNullOrWhiteSpace( link ) ) continue;
            list.Add( "\r" + name + "\t" ); 
            list.Add( new Hyperlink( new Run( link ) ){ NavigateUri = new Uri( link ) } );
         }
      }

      public override string Path => Mod.Path;

      public override string Type { get { lock ( Mod ) {
         var dlls = Mod.Metadata.Dlls;
         if ( dlls == null ) return "???";
         if ( dlls.Any( e => e?.Methods?.ContainsKey( "Init" ) ?? false ) ) return "PPML";
         if ( dlls.Any( e => e?.Methods?.ContainsKey( "Initialize" ) ?? false ) ) return "PPML+";
         return "DLL";
      } } }

      public override string ToString () { lock ( Mod ) return Mod.ToString(); }
   }

   internal class GuiLogger : Logger {
      private readonly AppControl App;
      public GuiLogger ( AppControl app ) => App = app;
      public override void Clear () { }
      public override void Flush () { }
      protected override void _Log ( LogEntry entry ) => App.Log( EntryToString( entry ) );
   }

   internal class ZipArchiveReader : ArchiveReader {
      public ZipArchiveReader ( string path ) : base( path ) {}

      public override void Install ( string modFolder ) {
         Action<string> log = AppControl.Instance.Log;
         var destination = modFolder + Path.DirectorySeparatorChar;
         log( $"Extracting {ArchivePath} to {destination}" );
         using ( ZipArchive archive = ZipFile.OpenRead( ArchivePath ) ) {
            foreach ( ZipArchiveEntry entry in archive.Entries ) {
               var name = entry.FullName;
               // Use regular expression if it gets any longer...
               if ( name.Length == 0 || name[0] == '/' || name[0] == '\\' || name.Contains( "..\\" ) || name.Contains( "../" ) ) continue;
               if ( name.EndsWith( "/", StringComparison.Ordinal ) || name.EndsWith( "\\", StringComparison.Ordinal ) ) continue;
               if ( name.EndsWith( ".cs", StringComparison.OrdinalIgnoreCase ) || name.EndsWith( ".csproj", StringComparison.OrdinalIgnoreCase ) ) continue;
               if ( entry.Length <= 0 ) continue;
               var path = Path.Combine( modFolder, name );
               log( path );
               Directory.CreateDirectory( Path.GetDirectoryName( path ) );
               entry.ExtractToFile( path, true );
            }
         }
      }
   }

   internal class SevenZipArchiveReader : ArchiveReader {
      private const string EXE = "7za.exe";

      public SevenZipArchiveReader ( string path ) : base( path ) {}

      public override void Install ( string modFolder ) {
         Action<string> log = AppControl.Instance.Log;
         var destination = modFolder + Path.DirectorySeparatorChar;
         string dir = Path.GetTempPath(), exe = Path.Combine( dir, EXE );
         if ( ! File.Exists( exe ) ) using ( var writer = new FileStream( exe, FileMode.Create ) ) {
            log( $"Creating {exe}" );
            AppControl.GetResource( EXE ).CopyTo( writer );
         }
         Directory.CreateDirectory( destination );
         AppControl.Instance.RunAndWait( destination, exe, $"x -y -bd \"{ArchivePath}\" -xr!*.cs -xr!*.csprog" );
      }

      public static void Cleanup () { try {
         var exe = Path.Combine( Path.GetTempPath(), EXE );
         if ( File.Exists( exe ) )
            File.Delete( exe );
      } catch ( SystemException ) { } }
   }
}