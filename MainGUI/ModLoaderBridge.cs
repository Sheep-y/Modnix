using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace Sheepy.Modnix.MainGUI {
   // This class was created to separate ModLoader classes from the main program.
   internal class ModLoaderBridge {
      private readonly AppControl App = AppControl.Instance;

      internal void CheckSetup () { lock ( this ) {
         if ( ModLoader.NeedSetup ) {
            App.Log( "Initiating ModLoader" );
            var logger = new GuiLogger();
            ModLoader.SetLog( logger );
            logger.Filters.Add( LogFilters.AddPrefix( "Loader┊" ) );
            App.Log( "Setup ModLoader" );
            ModLoader.Setup();
         }
      } }

      internal LoaderSettings GetSettings () {
         CheckSetup();
         lock ( ModLoader.Settings ) return ModLoader.Settings;
      }

      internal void SaveSettings () {
         CheckSetup();
         App.Log( "Saving Modnix settings" );
         ModLoader.SaveSettings();
      }

      internal IEnumerable<ModInfo> LoadModList () {
         CheckSetup();
         App.Log( "Building mod list" );
         ModScanner.BuildModList();
         return Task.WhenAll( ModScanner.AllMods.Select( ConvertModTask ).ToArray() ).Result;
      }

      private static Task< GridModItem > ConvertModTask ( ModEntry mod ) => Task.Run( () => ToGridItem( mod ) );
      internal static readonly string[] ReadmeFiles  = new string[]{ "readme.rtf", "read.txt", "readme.md", "readme", "read.me" };
      internal static readonly string[] ChangeFiles  = new string[]{ "changelog.rtf", "changelog.txt", "changelog.md", "history.rtf", "history.txt", "history.md", "changelog", "change.log" };
      internal static readonly string[] LicenseFiles = new string[]{ "license.rtf", "license.txt", "license.md", "license", "unlicense" };

      private static GridModItem ToGridItem ( ModEntry mod ) { try {
         var modPath = mod.Path;
         if ( string.IsNullOrWhiteSpace( modPath ) ) return new GridModItem( mod );
         var doc = new Dictionary< ModDoc, string >();
         var dir = Path.GetDirectoryName( modPath );
         AppControl.Instance.Log( "Scanning docs in " + dir );
         foreach ( var file in Directory.EnumerateFiles( dir ) ) { // TODO: Rewrite with string array lookup
            var name = Path.GetFileName( file ).ToLowerInvariant();
            if ( ReadmeFiles.Contains( name ) ) doc.Add( ModDoc.README, file );
            else if ( ChangeFiles.Contains( name ) ) doc.Add( ModDoc.CHANGELOG, file );
            else if ( LicenseFiles.Contains( name ) ) doc.Add( ModDoc.LICENSE, file );
         }
         if ( modPath.EndsWith( ".dll", StringComparison.OrdinalIgnoreCase ) ) {
            AppControl.Instance.Log( "Scanning embedded docs in " + modPath );
            if ( ! doc.ContainsKey( ModDoc.README ) && ( ModScanner.FindEmbeddedFile( modPath, null, ReadmeFiles ) ) )
               doc.Add( ModDoc.README, "embedded" );
            if ( ! doc.ContainsKey( ModDoc.CHANGELOG ) && ( ModScanner.FindEmbeddedFile( modPath, null, ChangeFiles ) ) )
               doc.Add( ModDoc.CHANGELOG, "embedded" );
            if ( ! doc.ContainsKey( ModDoc.LICENSE ) && ( ModScanner.FindEmbeddedFile( modPath, null, LicenseFiles ) ) )
               doc.Add( ModDoc.LICENSE, "embedded" );
         }
         return new GridModItem( mod ) { Docs = doc.Count == 0 ? null : doc };
      } catch ( Exception ex ) {
         AppControl.Instance.Log( ex );
         return new GridModItem( mod );
      } }

      private static ModEntry Mod ( ModInfo mod ) => ( mod as GridModItem )?.Mod;

      internal void AddLoaderLogNotice ( ModInfo mod, bool isError ) {
         var m = Mod( mod );
         if ( isError )
            m.AddNotice( TraceEventType.Warning, "runtime_error" );
         else
            m.AddNotice( TraceEventType.Information, "runtime_warning" );
      }
      
      internal void DeleteMod ( ModInfo mod ) {
         var path = Path.GetDirectoryName( mod.Path );
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
         var file = Mod( mod ).CheckConfigFile();
         if ( file != null ) {
            App.Log( $"Deleting {file}" );
            File.Delete( file );
         }
      }

      internal void ResetConfig ( ModInfo mod ) {
         Mod( mod ).WriteConfigText( Mod( mod ).GetDefaultConfigText() );
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
      internal Dictionary< ModDoc, string > Docs;
      internal GridModItem ( ModEntry mod ) => Mod = mod ?? throw new ArgumentNullException( nameof( mod ) );
      public override string Id => Mod.Metadata.Id;
      public override string Name => Mod.Metadata.Name?.ToString( "en" );
      public override string Version => Mod.Metadata.Version?.ToString();
      public override string Author => Mod.Metadata.Author?.ToString( "en" );
      public override string Status { get { lock ( Mod ) return Mod.Disabled ? "Off" : "On"; } }
      public override DateTime Installed { get { lock ( Mod ) return new FileInfo( Mod.Path ).LastAccessTime; } }

      public override bool Is ( ModQuery prop ) { lock ( Mod ) {
         switch ( prop ) {
            case ModQuery.ENABLED :
               return ! Mod.Disabled;
            case ModQuery.IS_FOLDER :
               var path = System.IO.Path.GetDirectoryName( Path );
               return path != AppControl.Instance.ModFolder && Directory.EnumerateFileSystemEntries( path ).Count() > 1;
            case ModQuery.IS_CHILD :
               return Mod.Parent != null;
            case ModQuery.HAS_WARNING :
               return Mod.GetNotices().Any( e => e.Level == TraceEventType.Warning || e.Level == TraceEventType.Error || e.Level == TraceEventType.Critical );
            case ModQuery.HAS_CONFIG :
               return Mod.HasConfig;
            case ModQuery.HAS_CONFIG_FILE :
               return Mod.CheckConfigFile() != null;
            case ModQuery.HAS_README :
               return Docs?.ContainsKey( ModDoc.README ) == true;
            case ModQuery.HAS_CHANGELOG :
               return Docs?.ContainsKey( ModDoc.CHANGELOG ) == true;
            case ModQuery.HAS_LICENSE :
               return Docs?.ContainsKey( ModDoc.LICENSE ) == true;
            default:
               return false;
         }
      } }

      public override void BuildDocument ( ModDoc type, FlowDocument doc ) {
         switch ( type ) {
            case ModDoc.SUMMARY : BuildSummary( doc ); break;
            case ModDoc.INFO : BuildDesc( doc ); break;
            case ModDoc.CONFIG : BuildConfig( doc ); break;
            case ModDoc.README : BuildSupportDoc( type, doc, ModLoaderBridge.ReadmeFiles ); break;
            case ModDoc.CHANGELOG : BuildSupportDoc( type, doc, ModLoaderBridge.ChangeFiles ); break;
            case ModDoc.LICENSE : BuildSupportDoc( type, doc, ModLoaderBridge.LicenseFiles ); break;
            default: doc.Replace( new Paragraph( new Run( $"Unknown doc type {type}" ) ) ); break;
         }
      }

      private void BuildSummary ( FlowDocument doc ) { lock ( Mod ) {
         var body = doc.Blocks.FirstBlock as Paragraph;
         if ( body == null ) return;
         var name = new Run( Mod.Metadata.Name.ToString( "en" ) );
         body.Inlines.Add( Mod.Disabled ? (Inline) name : new Bold( name ) );
         if ( Mod.HasConfig )
            body.Inlines.Add( Mod.CheckConfigFile() != null ? "\t[has config file]" : "\t[can create config]" );
         body.Inlines.Add( "\r" );
      } }

      private void BuildDesc ( FlowDocument doc ) { lock ( Mod ) {
         doc.Replace(
            BuildBlock( BuildBasicDesc ),
            BuildBlock( BuildProvidedDesc ),
            BuildBlock( BuildLinks ),
            BuildBlock( BuildContacts ),
            BuildBlock( BuildFileList ),
            BuildCopyright()
         );
      } }
      

      private void BuildConfig ( FlowDocument doc ) { lock ( Mod ) {
         doc.TextRange().Text = WpfHelper.Lf2Cr( Mod.GetConfigText() );
      } }

      private void BuildSupportDoc ( ModDoc type, FlowDocument doc, string[] fileList ) { try {
         string text = null;
         if ( Docs.TryGetValue( type, out string file ) && ! "embedded".Equals( file, StringComparison.Ordinal ) ) {
            AppControl.Instance.Log( $"Reading {type} {file}" );
            text = File.ReadAllText( file );
         } else {
            AppControl.Instance.Log( $"Reading embedded {type} from {Path}" );
            var buf = new StringBuilder();
            if ( ! ModScanner.FindEmbeddedFile( Path, buf, fileList ) ) return;
            text = buf.ToString();
         }
         if ( string.IsNullOrEmpty( text ) ) {
            doc.TextRange().Text = "(No Data)";
            return;
         }
         if ( text.StartsWith( "{\\rtf", StringComparison.Ordinal ) ) try {
            using ( var mem = new MemoryStream( Encoding.ASCII.GetBytes( text ) ) )
               doc.TextRange().Load( mem, System.Windows.DataFormats.Rtf );
            return;
         } catch ( ArgumentException ex ) { AppControl.Instance.Log( ex ); }
         doc.TextRange().Text = WpfHelper.Lf2Cr( text );
      } catch ( SystemException ex ) { doc.TextRange().Text = ex.ToString(); } }

      private void BuildBasicDesc ( ModMeta meta, InlineCollection list ) {
         /* Experimental image code. Online image does not work, sadly.
         BitmapImage img = new BitmapImage();
         img.BeginInit();
         //bi.UriSource = new Uri( "https://www.nexusmods.com/Contents/mods/3094/images/headers/17.jpg", UriKind.Absolute );
         img.UriSource = new Uri( "pack://application:,,,/Resources/img/book.png", UriKind.Absolute );
         img.EndInit();
         list.Add( new Image(){ Source = img, Stretch = Stretch.UniformToFill } );
         */
         list.Add( new Bold( new Run( meta.Name.ToString( "en" ) ) ) );
         if ( meta.Version != null ) list.Add( $" \tVer {ModMetaJson.RegxVerTrim.Replace( Version.ToString(), "" )}" );
         list.Add( $" \t{Type} mod" );
         if ( Is( ModQuery.HAS_CONFIG ) ) list.Add( ", can config" );
         if ( meta.Lang != null ) {
            string lang;
            if ( meta.Lang.Contains( "*" ) )
               lang = AppControl.LangIdToName( "*" );
            else if ( meta.Lang.Any( e => e == "-" || e == "--" ) )
               lang = AppControl.LangIdToName( "-" );
            else
               lang = string.Join( ", ", meta.Lang.Select( AppControl.LangIdToName ) );
            list.Add( $"\rLanguages\t{lang}" );
         }
         if ( meta.Author != null ) list.Add( $"\rAuthor\t\t{Author}" );
         switch ( meta.Duration ) {
            case "temp"    : list.Add( "\rMod claims to be temporary and not break saves." ); break;
            //case "instant" : list.Add( "\rMod claims to be instantaneous and not break saves." ); break;
            case "newgame" : list.Add( "\rMod claims to affect new game and not break saves." ); break;
            case "dlc"     : list.Add( "\rMod claims to not affect existing campaigns." ); break;
            case "perm"    : list.Add( "\rSaves made with this mod on may become dependent on this mod." ); break;
            default: break;
         }
         foreach ( var notice in Mod.GetNotices() ) {
            var txt = new Run();
            switch ( notice.Message ) {
               case "duplicate" :
                  txt.Text = string.Format( "\rDisabled: Using {0}", notice.Args[0]?.ToString() ); break;
               case "require"  :
                  txt.Text = string.Format( "\rDisabled: Missing requirement {0}", notice.Args[0]?.ToString() ); break;
               case "disable"  :
                  txt.Text = string.Format( "\rDisabled by {0}", notice.Args[0]?.ToString() ); break;
               case "runtime_error" :
                  txt.Text = "\rRuntime error(s) detected on last run, may be not safe to use."; break;
               case "runtime_warning" :
                  txt.Text = "\rRuntime warning(s) detected on last run."; break;
               default:
                  txt.Text = "\r" + notice.Message.ToString(); break;
            }
            switch ( notice.Level ) {
               case TraceEventType.Critical :
               case TraceEventType.Error    :
                  txt.Foreground = Brushes.Red; break;
               case TraceEventType.Warning  :
                  txt.Foreground = Brushes.OrangeRed; break;
               default :
                  txt.Foreground = Brushes.DarkBlue; break;
            }
            list.Add( txt );
         }
      }

      private void BuildProvidedDesc ( ModMeta meta, InlineCollection list ) {
         var desc = meta.Description?.ToString( "en" );
         if ( string.IsNullOrWhiteSpace( desc ) ) return;
         list.Add( desc );
      }

      private void BuildLinks ( ModMeta meta, InlineCollection list ) {
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
            var config = Mod.CheckConfigFile();
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
      public override void Clear () { }
      public override void Flush () { }
      protected override void _Log ( LogEntry entry ) => AppControl.Instance.Log( EntryToString( entry ) );
   }

   internal class ZipArchiveReader : ArchiveReader {
      public ZipArchiveReader ( string path ) : base( path ) {}

      public override string[] ListFiles () {
         using ( ZipArchive archive = ZipFile.OpenRead( ArchivePath ) ) {
            return archive.Entries.Select( e => e.FullName ).ToArray();
         }
      }

      private static Regex MalformPaths = new Regex( "(?:^[/\\\\]|\\.\\.[/\\\\])", RegexOptions.Compiled );
      private static Regex IgnoreFiles = new Regex( "(?:\\.(?:conf|cs|csproj)|[/\\\\])$", RegexOptions.Compiled | RegexOptions.IgnoreCase );

      public override string[] Install ( string modFolder ) {
         var destination = modFolder + Path.DirectorySeparatorChar;
         var result = new List<string>();
         Log( $"Extracting {ArchivePath} to {destination}" );
         using ( ZipArchive archive = ZipFile.OpenRead( ArchivePath ) ) {
            foreach ( ZipArchiveEntry entry in archive.Entries ) {
               var name = entry.FullName;
               if ( entry.Length <= 0 || MalformPaths.IsMatch( name ) || IgnoreFiles.IsMatch( name ) ) continue;
               var path = Path.Combine( modFolder, name );
               Log( path );
               Directory.CreateDirectory( Path.GetDirectoryName( path ) );
               entry.ExtractToFile( path, true );
               result.Add( path );
            }
         }
         return result.ToArray();
      }
   }

   internal class SevenZipArchiveReader : ArchiveReader {
      private const string EXE = "7za.exe";

      public SevenZipArchiveReader ( string path ) : base( path ) {}

      private string Create7z () {
         string dir = Path.GetTempPath(), exe = Path.Combine( dir, EXE );
         lock ( EXE ) {
            if ( ! File.Exists( exe ) ) using ( var writer = new FileStream( exe, FileMode.Create ) ) {
               Log( $"Creating {exe}" );
               AppControl.GetResource( EXE ).CopyTo( writer );
            }
         }
         return exe;
      }

      private static Regex RemoveSize = new Regex( "^\\d+\\s+\\d+\\s+", RegexOptions.Compiled );

      public override string[] ListFiles () {
         var exe = Create7z();
         var stdout = AppControl.Instance.RunAndWait( Path.GetDirectoryName( ArchivePath ), exe, $"l \"{ArchivePath}\" -ba -bd -sccUTF-8 -xr!*.conf -xr!*.cs -xr!*.csprog", suppressLog: true );
         return stdout.Split( '\n' )
            .Where( e => ! e.Contains( " D..." ) ) // Ignore folders, e.g. empty folders result from ignoring *.cs
            .Select( e => RemoveSize.Replace( e.Substring( 25 ).Trim(), "" ) ).ToArray();
      }

      public override string[] Install ( string modFolder ) {
         var exe = Create7z();
         var destination = modFolder + Path.DirectorySeparatorChar;
         Directory.CreateDirectory( destination );
         var stdout = AppControl.Instance.RunAndWait( destination, exe, $"x \"{ArchivePath}\" -y -bb1 -ba -bd -sccUTF-8 -xr!*.conf -xr!*.cs -xr!*.csprog" );
         if ( ! stdout.Contains( "Everything is Ok" ) ) throw new ApplicationException( stdout );
         return stdout.Split( '\n' ).Where( e => e.Length > 2 && e.StartsWith( "- ", StringComparison.Ordinal ) )
            .Select( e => Path.Combine( modFolder, e.Substring( 2 ).Trim() ) ).ToArray();
      }

      public static void Cleanup () { try {
         var exe = Path.Combine( Path.GetTempPath(), EXE );
         if ( File.Exists( exe ) )
            File.Delete( exe );
      } catch ( SystemException ) { } }
   }

   public static class NativeMethods {
      [DllImport( "USER32.DLL" )]
      internal static extern bool SetForegroundWindow ( IntPtr hWnd );
   }
}