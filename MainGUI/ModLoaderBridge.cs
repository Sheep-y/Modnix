using Sheepy.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
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
            Sandbox.EnqueueSandbox();
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

      internal ModInfo[] LoadModList () {
         CheckSetup();
         App.Log( "Building mod list" );
         ModScanner.BuildModList();
         var result = Task.WhenAll( ModScanner.AllMods.Select( ConvertModTask ).ToArray() ).Result;
         return SetModOrders( result );
      }

      private ModInfo[] SetModOrders ( IEnumerable< GridModItem > list ) {
         var order = 1;
         var result = list.ToArray();
         var ordered = ModScanner.EnabledMods
            .Select( e => result.First( f => f.Mod == e ) )
            .Where( e => e != null && ! float.IsInfinity( e._Order ) && e._Order < 0 );
         App.Log( "Determining Splash phase order" );
         foreach ( var mod in ordered ) {
            if ( mod.Mod.Metadata.Dlls?.Any( e => e.Methods?.ContainsKey( "SplashMod" ) == true ) == true )
               mod._Order = order++;
         }
         App.Log( "Determining Main phase order" );
         foreach ( var mod in ordered )
            mod._Order = order++;
         return result;
      }

      private static Task< GridModItem > ConvertModTask ( ModEntry mod ) => Task.Run( () => ToGridItem( mod ) );
      internal static readonly string[] ReadmeFiles  = new string[]{ "readme.rtf", "read.txt", "readme.md", "readme", "read.me", "note", "notes"  };
      internal static readonly string[] ChangeFiles  = new string[]{ "changelog.rtf", "changelog.txt", "changelog.md", "history.rtf", "history.txt", "history.md", "changelog", "change.log" };
      internal static readonly string[] LicenseFiles = new string[]{ "license.rtf", "license.txt", "license.md", "license", "unlicense",
         "copyright.rtf", "copyright.txt", "copyright.md", "copyright" };

      private static GridModItem ToGridItem ( ModEntry mod ) { try {
         var modPath = mod.Path;
         var order = ModScanner.EnabledMods.Contains( mod ) ? -1f : float.PositiveInfinity;
         if ( string.IsNullOrWhiteSpace( modPath ) ) return new GridModItem( mod ){ _Order = order };
         var doc = new Dictionary< ModDoc, string >();
         var dir = Path.GetDirectoryName( modPath );
         AppControl.Instance.Log( "Scanning docs in " + dir );
         foreach ( var file in Directory.EnumerateFiles( dir ) ) {
            var name = Path.GetFileName( file ).ToLowerInvariant();
            if ( ! doc.ContainsKey( ModDoc.README ) && ReadmeFiles.Contains( name ) )
               doc.Add( ModDoc.README, file );
            else if ( ! doc.ContainsKey( ModDoc.CHANGELOG ) && ChangeFiles.Contains( name ) )
               doc.Add( ModDoc.CHANGELOG, file );
            else if ( ! doc.ContainsKey( ModDoc.LICENSE ) && LicenseFiles.Contains( name ) )
               doc.Add( ModDoc.LICENSE, file );
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
         return new GridModItem( mod ) { _Order = order, Docs = doc.Count == 0 ? null : doc };
      } catch ( Exception ex ) {
         AppControl.Instance.Log( ex );
         return new GridModItem( mod );
      } }

      private static ModEntry Mod ( ModInfo mod ) => ( mod as GridModItem )?.Mod;

      internal static void AddLoaderLogNotice ( ModInfo mod, string reason ) => Mod( mod ).AddNotice( TraceEventType.Warning, reason );
      
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
      internal string EditingConfig;
      public override string Id => Mod.Metadata.Id;
      internal float  _Order;
      public override float  Order => _Order;
      public override string Name => Mod.Metadata.Name?.ToString( "en" );
      public override string Version => Mod.Metadata.Version?.ToString();
      public override string Author => Mod.Metadata.Author?.ToString( "en" );
      public override string Status { get { lock ( Mod ) return Mod.Disabled ? "Off" : "On"; } }
      public override DateTime Installed { get { lock ( Mod ) return new FileInfo( Mod.Path ).LastAccessTime; } }

      private ModSettings Settings { get {
         ModSettings result = null;
         AppControl.Instance.Settings.Mods?.TryGetValue( Mod.Key, out result );
         return result;
      } }

      public override bool Is ( ModQuery prop ) { lock ( Mod ) {
         switch ( prop ) {
            case ModQuery.ENABLED :
               return ! Mod.Disabled;
            case ModQuery.FORCE_DISABLED :
               return Mod.Disabled && Settings?.Disabled != true;
            case ModQuery.IS_FOLDER :
               var path = System.IO.Path.GetDirectoryName( Path );
               return path != AppControl.Instance.ModFolder && Directory.EnumerateFileSystemEntries( path ).Count() > 1;
            case ModQuery.IS_CHILD :
               return Mod.Parent != null;
            case ModQuery.ERROR :
               return Mod.GetNotices().Any( e => e.Level == TraceEventType.Error || e.Level == TraceEventType.Critical );
            case ModQuery.WARNING :
               return Mod.GetNotices().Any( e => e.Level == TraceEventType.Warning );
            case ModQuery.EDITING :
               return EditingConfig != null && ! EditingConfig.Trim().Equals( WpfHelper.Lf2Cr( Mod.GetConfigText()?.Trim() ) );
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

      public override void Do ( AppAction action, object param = null ) {
         switch ( action ) {
            case AppAction.ENABLE_MOD :
               EnableMod();
               return;
            case AppAction.DISABLE_MOD :
               DisableMod();
               AppControl.Instance.SaveSettings();
               return;
            case AppAction.EDIT_CONFIG :
               lock ( this ) EditingConfig = param?.ToString();
               return;
            case AppAction.RESET_CONFIG :
               lock ( this ) EditingConfig = null;
               return;
            case AppAction.DELETE_CONFIG :
               Mod.DeleteConfig();
               return;
            case AppAction.SET_CONFIG_PROFILE :
               lock ( this ) EditingConfig = WpfHelper.Lf2Cr( Mod.GetDefaultConfigText() ?? GetConfigFromSandbox() );
               return;
            case AppAction.SAVE_CONFIG :
               SaveConfig();
               return;
            default:
               return;
         }
      }

      private void EnableMod () {
         if ( Settings?.Disabled != true ) return;
         Log( $"Enabling mod {Mod.Metadata.Id}" );
         Settings.Disabled = false;
         if ( Settings.GetIsDefaultSettings() ) {
            var settings = AppControl.Instance.Settings;
            settings.Mods.Remove( Mod.Key );
            if ( settings.Mods.Count == 0 )
               settings.Mods = null;
         }
      }

      private void DisableMod () {
         var mods = AppControl.Instance.Settings.Mods;
         if ( mods == null )
            mods = AppControl.Instance.Settings.Mods = new Dictionary<string, ModSettings>();
         var settings = Settings;
         if ( settings == null )
            mods.Add( Mod.Key, new ModSettings{ Disabled = true } );
         else if ( ! settings.Disabled )
            settings.Disabled = true;
         else
            return;
         Log( $"Disabling mod {Mod.Metadata.Id}" );
      }

      private void SaveConfig () {
         string conf;
         lock ( this ) conf = EditingConfig?.Trim();
         if ( string.IsNullOrEmpty( conf ) )
            Mod.DeleteConfig();
         else
            Mod.WriteConfigText( conf.Replace( '\r', '\n' ) );
         lock ( this ) EditingConfig = null;
      }

      private string GetConfigFromSandbox () {
         Sandbox proxy = null;
         try {
            var dll = Mod.Metadata.Dlls;
            proxy = Sandbox.GetSandbox();
            if ( proxy.GetError() != null ) return proxy.GetError();
            if ( dll != null ) {
               Log( $"Sandbox loading {dll.Length} dlls." );
               proxy.LoadDlls( dll.Select( e => e.Path ).ToArray() );
            }
            var typeName = Mod.Metadata.ConfigType; // Load error may not affect config resolve, so ignore error for now
            if ( string.IsNullOrWhiteSpace( typeName ) )
               return new ArgumentNullException( "ConfigType" ).ToString();
            Log( $"Sandbox resolving {typeName}" );
            return Mod.CacheDefaultConfigText( proxy.Stringify( typeName ) ) ?? proxy.GetError() ?? new TypeLoadException( $"Not found: '{typeName}'" ).ToString();
         } catch ( Exception ex ) {
            Log( ex );
            if ( ex is RemotingException ) AppControl.Instance.GetModList(); // Trigger mod refresh on remote error
            return ex.ToString();
         } finally {
            if ( proxy?.Domain != null ) try {
               ( RemotingServices.GetLifetimeService( proxy ) as ILease ).Register( proxy );
               AppDomain.Unload( proxy.Domain );
            } catch ( Exception ex ) { Log( ex ); }
         }
      }

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
         Log( "Showing conf. Editing " + EditingConfig?.Length ?? "null" );
         doc.TextRange().Text = EditingConfig ?? WpfHelper.Lf2Cr( Mod.GetConfigText() ?? Mod.GetDefaultConfigText() ?? GetConfigFromSandbox() )?.Trim()
               ?? "Error occured, please try refresh mod list.\r\rIf problem persists, please report issue.";
      } }

      private void BuildSupportDoc ( ModDoc type, FlowDocument doc, string[] fileList ) { try {
         string text = null;
         if ( Docs.TryGetValue( type, out string file ) && ! "embedded".Equals( file, StringComparison.Ordinal ) ) {
            Log( $"Reading {type} {file}" );
            text = Utils.ReadFile( file );
         } else {
            Log( $"Reading embedded {type} from {Path}" );
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
               doc.TextRange().Load( mem, DataFormats.Rtf );
            return;
         } catch ( ArgumentException ex ) { Log( ex ); }
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
         if ( Mod.Index != 0 ) list.Add( $"\rLoad Index\t{Mod.Index}" );
         if ( Mod.Index != meta.LoadIndex ) list.Add( $" (Original {meta.LoadIndex})" );
         switch ( meta.Duration ) {
            case "temp"    : list.Add( "\rMod claims to be temporary and not break saves." ); break;
            //case "instant" : list.Add( "\rMod claims to be instantaneous and not break saves." ); break;
            case "newgame" : list.Add( "\rMod claims to affect new game and not break saves." ); break;
            case "dlc"     : list.Add( "\rMod claims to not affect existing campaigns." ); break;
            case "perm"    : list.Add( "\rSaves made with this mod on may become dependent on this mod." ); break;
         }
         foreach ( var notice in Mod.GetNotices() )
            list.Add( FormatNotice( notice ) );
      }

      private static Run FormatNotice ( LogEntry notice ) {
         var txt = new Run();
         switch ( notice.Message ) {
            case "duplicate" :
               txt.Text = string.Format( "\rDisabled: Using {0}.", notice.Args[0]?.ToString() ); break;
            case "avoid" :
               txt.Text = string.Format( "\rDisabled: Avoiding conflict with {0}.", notice.Args[0]?.ToString() ); break;
            case "require" :
               txt.Text = string.Format( "\rDisabled: Missing requirement {0}.", notice.Args[0]?.ToString() ); break;
            case "disable" :
               txt.Text = string.Format( "\rDisabled by {0}.", notice.Args[0]?.ToString() ); break;
            case "manual"  :
               txt.Text = "\rManually Disabled"; break;
            case "runtime_error" :
               txt.Text = "\rRuntime error(s) detected on last run, mod may not work as expected."; break;
            case "runtime_warning" :
               txt.Text = "\rRuntime warning(s) detected on last run."; break;
            case "config_mismatch" :
               txt.Text = "\rDefaultConfig different from new instance defaults."; break;
            case "unsupported_actions" :
               txt.Text = "\rMod Actions requires Modnix 3 or above.\rMod may not work or only partially work."; break;
            case "unsupported_mod_pack" :
               txt.Text = "\rMod Pack requires Modnix 3 or above."; break;
            default :
               txt.Text = "\r" + notice.Message; break;
         }
         switch ( notice.Level ) {
            case TraceEventType.Critical :
            case TraceEventType.Error    :
               txt.Foreground = Brushes.Red; break;
            case TraceEventType.Warning  :
               txt.Foreground = Brushes.Blue; break;
            default :
               txt.Foreground = Brushes.DarkBlue; break;
         }
         if ( notice.Args?.Length > 0 && notice.Args[0] is ModEntry cause )
            WpfHelper.Linkify( txt, () => AppControl.Instance.GUI.SetInfo( GuiInfo.MOD, cause.Path ) );
         return txt;
      }

      private static void BuildProvidedDesc ( ModMeta meta, InlineCollection list ) {
         var desc = meta.Description?.ToString( "en" );
         if ( string.IsNullOrWhiteSpace( desc ) ) return;
         list.Add( desc );
      }

      private static void BuildLinks ( ModMeta meta, InlineCollection list ) {
         if ( meta.Url == null ) return;
         list.Add( "Link(s)" );
         BuildDict( meta.Url, list );
      }

      private static void BuildContacts ( ModMeta meta, InlineCollection list ) {
         if ( meta.Contact == null ) return;
         list.Add( "Contact(s)" );
         BuildDict( meta.Contact, list );
      }

      private void BuildFileList ( ModMeta meta, InlineCollection list ) {
         Func< string, string > fileName = System.IO.Path.GetFileName;
         list.Add( $"Path\r" );
         var pathTxt = new Run( System.IO.Path.GetDirectoryName( Path ) + System.IO.Path.DirectorySeparatorChar ){ Foreground = Brushes.Blue };
         list.Add( WpfHelper.Linkify( pathTxt, () => AppControl.Explore( Path ) ) );
         list.Add( "\r\rKnown File(s)" );
         var self = fileName( Path );
         var selfRun = new Run( "\r" + self );
         list.Add( selfRun );
         if ( meta.Dlls != null )
            foreach ( var e in meta.Dlls ) {
               var path = fileName( e.Path );
               var txt = "\r" + path + " [" + string.Join( ", ", e.Methods.Keys ) + "]";
               if ( path == self ) selfRun.Text = txt;
               else list.Add( txt );
            }
         if ( Mod.HasConfig ) {
            var config = Mod.CheckConfigFile();
            if ( config != null ) list.Add( $"\r{fileName(config)} [Config]" );
         }
         if ( Docs != null ) {
            foreach ( var row in Docs )
               if ( row.Value != "embedded" )
                  list.Add( "\r" + row.Value + " [Doc]" );
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

      private Block BuildBlock ( Action<ModMeta,InlineCollection> builder ) { try {
         var block = new Paragraph();
         builder( Mod.Metadata, block.Inlines );
         return block.Inlines.Count == 0 ? null : block;
      } catch ( Exception ex ) { Log( ex ); return null; } }

      private static void BuildDict ( TextSet data, InlineCollection list ) {
         if ( data.Dict == null ) {
            list.Add( data.Default );
            return;
         }
         foreach ( var e in data.Dict ) {
            string name = e.Key, link = e.Value;
            if ( string.IsNullOrWhiteSpace( name ) || string.IsNullOrWhiteSpace( link ) ) continue;
            list.Add( "\r" + name + "\t" );
            try {
               list.Add( new Hyperlink( new Run( link ){ Foreground = Brushes.Blue } ){ NavigateUri = new Uri( link ) } );
            } catch ( UriFormatException ) {
               list.Add( new Run( link ) );
            }
         }
      }

      public override string Path => Mod.Path;

      public override string Type { get { lock ( Mod ) {
         if ( Mod.Metadata.Mods != null ) return "Pack";
         var hasAction = Mod.Metadata.Actions != null;
         var dlls = Mod.Metadata.Dlls;
         if ( dlls == null ) return hasAction ? "Actions" : "???";
         var prefix = hasAction ? "Act+" : "";
         if ( dlls.Any( e => e?.Methods?.ContainsKey( "Init" ) ?? false ) ) return prefix + "PPML";
         if ( dlls.Any( e => e?.Methods?.ContainsKey( "Initialize" ) ?? false ) ) return prefix + "PPML+";
         return prefix + "DLL";
      } } }

      public static void Log ( object msg ) => AppControl.Instance.Log( msg );
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

      private static readonly Regex MalformPaths = new Regex( "(?:^[/\\\\]|\\.\\.[/\\\\])", RegexOptions.Compiled );
      private static readonly Regex IgnoreFiles = new Regex( "(?:\\.(?:cs|csproj|sln)|[/\\\\])$", RegexOptions.Compiled | RegexOptions.IgnoreCase );

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
               AssemblyLoader.GetResourceStream( EXE ).CopyTo( writer );
            }
         }
         return exe;
      }

      private static readonly Regex RemoveSize = new Regex( "^\\d+\\s+\\d+\\s+", RegexOptions.Compiled );

      public override string[] ListFiles () {
         var exe = Create7z();
         var stdout = AppControl.Instance.RunAndWait( Path.GetDirectoryName( ArchivePath ), exe, $"l \"{ArchivePath}\" -ba -bd -sccUTF-8 -xr!*.cs -xr!*.csprog -xr!*.sln", suppressLog: true );
         return stdout.Split( '\n' )
            .Where( e => e.Length > 25 && ! e.Contains( " D..." ) ) // Ignore folders, e.g. empty folders result from ignoring *.cs
            .Select( e => RemoveSize.Replace( e.Substring( 25 ).Trim(), "" ) ).ToArray();
      }

      public override string[] Install ( string modFolder ) {
         var exe = Create7z();
         var destination = modFolder + Path.DirectorySeparatorChar;
         Directory.CreateDirectory( destination );
         var stdout = AppControl.Instance.RunAndWait( destination, exe, $"x \"{ArchivePath}\" -y -bb1 -ba -bd -sccUTF-8 -xr!*.cs -xr!*.csprog -xr!*.sln" );
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

   public class Sandbox : MarshalByRefObject, ISponsor {
      public AppDomain Domain { get; private set; }
      private HashSet<Assembly> ModDlls;
      private Exception Error;

      public void Initiate () { try {
         AppDomain.CurrentDomain.AssemblyResolve += AssemblyLoader.AssemblyResolve;
         Application.ResourceAssembly = Assembly.GetExecutingAssembly();
      } catch ( Exception ex ) { Error = ex; } }


      public void LoadDlls ( string[] paths ) {
         foreach ( var dll in paths ) {
            LoadDll( dll );
            if ( Error != null ) break;
         }
      }

      private void LoadDll ( string path ) { try {
         if ( ModDlls == null ) {
            ModDlls = new HashSet<Assembly>();
            ModMetaJson.TrimVersion( new Version() ); // Call something to load ModLoader.
         }
         try {
            ModDlls.Add( Assembly.LoadFrom( path ) );
         } catch ( FileLoadException ex ) when ( ex.GetBaseException() is NotSupportedException ) { // dll blocked because of "Downloaded From Internet" flag
            RecurUnblock( Path.GetDirectoryName( path ) );
            ModDlls.Add( Assembly.Load( File.ReadAllBytes( path ) ) );
         }
      } catch ( Exception ex ) { Error = ex; } }

      // Remove "Downloaded from Internet" mark. Inefficient, but good enough for the mods we have now
      private void RecurUnblock ( string path, int level = 0 ) {
         foreach ( var file in Directory.GetFiles( path ) ) try { using ( Process p = new Process() ) {
            p.StartInfo.FileName = "cmd.exe";
            p.StartInfo.Arguments = $"/c echo. > \"{file}\":Zone.Identifier";
            p.StartInfo.WorkingDirectory = Path.GetDirectoryName( file );
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.Start();
            p.WaitForExit( 15_000 );
         } } catch ( Exception ) {}
         if ( level < 5 )
            foreach ( var dir in Directory.GetDirectories( path ) )
               RecurUnblock( dir, level + 1 );
      }

      public string Stringify ( string typeName ) { try {
         foreach ( var asm in ModDlls ) {
            var type = asm.GetType( typeName );
            if ( type == null ) continue;
            return ModMetaJson.Stringify( Activator.CreateInstance( type ) );
         }
         return null;
      } catch ( Exception ex ) { Error = ex; return null; } }

      public string GetError () => Error?.ToString();

      private static readonly ConcurrentQueue<Sandbox> Cache = new ConcurrentQueue<Sandbox>();

      internal static Sandbox GetSandbox () {
         Cache.TryDequeue( out Sandbox cache );
         EnqueueSandbox();
         return cache ?? CreateSandbox();
      }

      internal static void EnqueueSandbox () {
         if ( Cache.IsEmpty ) Task.Run( async () => {
            await Task.Delay( 500 ).ConfigureAwait( false ); // Given main task some room, i.e. app launch and conf parsing
            Cache.Enqueue( CreateSandbox() );
         } );
      }

      private static Sandbox CreateSandbox () {
         AppControl.Instance.Log( $"Creating sandbox" );
         try {
            // throw new NotSupportedException("Test");
            var domain = AppDomain.CreateDomain( "Modnix config sandbox", null, new AppDomainSetup { DisallowCodeDownload = true } );
            var proxy = domain.CreateInstanceFromAndUnwrap( AppControl.Instance.MyPath, typeof( Sandbox ).FullName ) as Sandbox;
            ( RemotingServices.GetLifetimeService( proxy ) as ILease ).Register( proxy );
            proxy.Domain = domain;
            proxy.Initiate();
            return proxy;
         } catch ( Exception ex ) {
            if ( ex is NotSupportedException )
               ex = AppControl.Instance.CreateRuntimeConfig( AppControl.Instance.MyPath ) ?? new NotSupportedException( "\nPlease restart Modnix to fix mod sandbox.\n\n", ex );
            return new Sandbox{ Error = ex };
         }
      }

      public TimeSpan Renewal ( ILease lease ) => Domain != null ? TimeSpan.FromMinutes( 30 ) : TimeSpan.Zero;
   }

   public static class NativeMethods {
      [DllImport( "USER32.DLL" )]
      internal static extern bool SetForegroundWindow ( IntPtr hWnd );
   }
}