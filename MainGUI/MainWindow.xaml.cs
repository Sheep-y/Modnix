using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using static System.Globalization.CultureInfo;
using static Sheepy.Modnix.MainGUI.WpfHelper;

namespace Sheepy.Modnix.MainGUI {

   public partial class MainWindow : Window, IAppGui {
      private readonly AppControl App = AppControl.Instance;

      private event Action ModListChanged;
      private Timer GameStatusTimer;

      public MainWindow () { try {
         InitializeComponent();
         GameStatusTimer = new Timer( CheckGameRunning, null, Timeout.Infinite, Timeout.Infinite );
         SetupGUI();
         RefreshGUI();
      } catch ( Exception ex ) { Console.WriteLine( ex ); } }

      private void SetupGUI () {
         Log( "Setting up GUI" );
         SharedGui.AppStateChanged += RefreshAppInfo;
         SharedGui.AppStateChanged += RefreshAppButtons;
         SharedGui.GamePathChanged += RefreshGameInfo;
         SharedGui.VersionChanged += RefreshAppInfo;
         SharedGui.VersionChanged += RefreshGameInfo;
         SharedGui.AppWorkingChanged += RefreshAppInfo;
         SharedGui.AppWorkingChanged += RefreshAppButtons;
         SharedGui.GameRunningChanged += RefreshAppButtons;
         ModListChanged += RefreshModList;
         ModListChanged += RefreshAppButtons;
      }

      private void RefreshGUI () { try {
         Log( "Refreshing GUI" );
         RefreshAppInfo();
         RefreshGameInfo();
         RefreshModInfo();
         RefreshAppButtons();
         RefreshUpdateStatus();
      } catch ( Exception ex ) { Log( ex ); } }

      public void SetInfo ( GuiInfo info, object value ) { this.Dispatch( () => { try {
         Log( $"Set {info} = {value}" );
         var txt = value?.ToString();
         switch ( info ) {
            case GuiInfo.VISIBILITY : ShowWindow(); break;
            case GuiInfo.APP_UPDATE : Update = value; UpdateChecked(); RefreshUpdateStatus(); break;
            case GuiInfo.MOD_LIST : SetModList( value as IEnumerable<ModInfo> ); break;
            default : SharedGui.SetInfo( info, value ); break;
         }
      } catch ( Exception ex ) { Log( ex ); } } ); }

      private void Window_Activated ( object sender, EventArgs e ) => GameStatusTimer.Change( 100, 3000 );
      private void Window_Deactivated ( object sender, EventArgs e ) => GameStatusTimer.Change( Timeout.Infinite, Timeout.Infinite );

      private void ShowWindow () {
         Log( "Checking app status" );
         App.CheckStatusAsync( true );
         if ( ! App.ParamSkipStartupCheck )
            CheckUpdate( false );
         Show();
         // WPF bug - document reset its padding on first render.
         var empty = new Thickness( 0 );
         RichAppInfo.Document.PagePadding = empty;
         RichGameInfo.Document.PagePadding = empty;
         RichModInfo.Document.PagePadding = empty;
      }

      private void CheckGameRunning ( object _ = null ) {
         var IsRunning = AppControl.IsGameRunning();
         this.Dispatch( () => SharedGui.IsGameRunning = IsRunning );
      }

      private void RefreshAppButtons () { try {
         Log( "Refreshing app buttons, " + ( SharedGui.CanModify ? "can mod" : "cannot mod" ) );
         ButtonSetup .IsEnabled = ! SharedGui.IsAppWorking;
         ButtonRunOnline .IsEnabled = SharedGui.CanModify && SharedGui.IsGameFound;
         ButtonRunOffline.IsEnabled = SharedGui.CanModify && SharedGui.IsGameFound;
         ButtonAddMod.IsEnabled = SharedGui.CanModify && Directory.Exists( App.ModFolder );
         ButtonModDir.IsEnabled = Directory.Exists( App.ModFolder );
         ButtonRefreshMod.IsEnabled = Directory.Exists( App.ModFolder ) && ! SharedGui.IsAppWorking;
         ButtonModOpenModDir.IsEnabled = CurrentMod != null;
         ButtonModConf.IsEnabled = CurrentMod != null && (bool) CurrentMod.Query( ModQueryType.HAS_CONFIG );
         ButtonModDelete.IsEnabled = SharedGui.CanModify && CurrentMod != null && ! (bool) CurrentMod.Query( ModQueryType.IS_CHILD );;
         ButtonLoaderLog.IsEnabled = File.Exists( LoaderLog );

         if ( SharedGui.IsGameRunning )
            BtnTxtSetup.Text = "Refresh";
         else if ( SharedGui.AppState == "modnix" )
            BtnTxtSetup.Text = "Revert";
         else
            BtnTxtSetup.Text = "Setup";

         LabelModList.Foreground = Brushes.Black;
         if ( SharedGui.IsGameRunning ) {
            LabelModList.Content = "GAME RUNNING";
            LabelModList.Foreground = Brushes.Red;
         } else if ( ModList == null ) {
            LabelModList.Content = "Checking";
         } else if ( SharedGui.AppState != null && ! SharedGui.IsInjected ) {
            LabelModList.Content = "NOT INSTALLED";
            LabelModList.Foreground = Brushes.Red;
         } else {
            LabelModList.Content = $"{ModList.Count()} Mods";
         }
      } catch ( Exception ex ) { Log( ex ); } }

      #region App Info Area
      private void RefreshAppInfo () { try {
         Log( "Refreshing app info" );
         string txt;
         if ( SharedGui.IsAppWorking || SharedGui.AppState == null )
            txt = "Busy";
         else
            switch ( SharedGui.AppState ) {
               case "ppml"   : txt = "PPML only, need setup"; break;
               case "both"   : txt = "PPML found, can remove"; break;
               case "modnix" : txt = "Injected"; break;
               case "none"   : txt = "Requires Setup"; break;
               case "no_game": txt = "Game not found; Please do Manual Setup"; break;
               default: txt = "Unknown state; see log"; break;
            }
         var state = new Run( txt );
         if ( SharedGui.AppState != "modnix" ) state.Foreground = Brushes.Red;
         RichAppInfo.Document.Replace( P( new Bold( new Run( AppControl.LIVE_NAME ) ), new Run( $"\tVer {SharedGui.AppVer}\rStatus: " ), state ) );
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonWiki_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "wiki", e );
      private void ButtonGitHub_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "home", e );
      private void ButtonUserGuide_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "guide", e );

      private void ButtonSetup_Click ( object sender, RoutedEventArgs e ) { try {
         Log( "Main action button clicked" );
         if ( e?.Source is UIElement src ) src.Focus();
         if ( SharedGui.IsGameRunning ) { // Refresh
            CheckGameRunning();
            return;
         }
         switch ( SharedGui.AppState ) {
            case "ppml" : case "both" : case "none" :
               DoSetup();
               break;
            case "modnix" :
               if ( MessageBox.Show( "Remove Modnix from Phoenix Poing?", "Revert", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No ) == MessageBoxResult.Yes )
                  DoRestore();
               break;
            default:
               DoManualSetup();
               break;
         }
      } catch ( Exception ex ) { Log( ex ); } }

      private void DoSetup () {
         Log( "Calling setup" );
         SharedGui.IsAppWorking = true;
         App.DoSetupAsync();
      }

      private void DoManualSetup () => OpenUrl( "my_doc", null );

      private void DoRestore () {
         Log( "Calling restore" );
         SharedGui.IsAppWorking = true;
         App.DoRestoreAsync();
      }

      private void ButtonModDir_Click ( object sender, RoutedEventArgs e ) { try {
         var arg = $"/select, \"{Path.Combine( App.ModFolder, App.ModGuiExe )}\"";
         Log( $"Launching explorer.exe {arg}" );
         Process.Start( "explorer.exe", arg );
      } catch ( Exception ex ) { Log( ex ); } }

      public void Prompt ( AppActionType action, PromptFlag flags = PromptFlag.NONE, Exception ex = null ) { this.Dispatch( () => { try {
         Log( $"Prompt {action} {flags}" );
         SharedGui.Prompt( action, flags, ex, () => AppControl.Explore( App.ModGuiExe ) );
         if ( action == AppActionType.ADD_MOD || action == AppActionType.DELETE_DIR || action == AppActionType.DELETE_FILE )
            ModListChanged.Invoke();
      } catch ( Exception err ) { Log( err ); } } ); }

      private void ButtonNexus_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "nexus", e );
      #endregion

      #region Game Info Area
      private void RefreshGameInfo () { try {
         Log( "Refreshing game info" );
         var p = new Paragraph( new Bold( new Run( "Phoenix Point" ) ) );
         if ( SharedGui.IsGameFound ) {
            if ( SharedGui.GameVer != null )
               p.Inlines.Add( $"\tVer {SharedGui.GameVer}" );
            p.Inlines.Add( "\r" + Path.GetFullPath( SharedGui.GamePath ) );
         } else
            p.Inlines.Add( new Run( "\rGame not found" ){ Foreground = Brushes.Red } );
         RichGameInfo.Document.Replace( p );
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonOnline_Click  ( object sender, RoutedEventArgs e ) {
         App.LaunchGame( "online" );
         SetInfo( GuiInfo.GAME_RUNNING, true );
         GameStatusTimer.Change( Timeout.Infinite, 10_000 ); // Should be overrode by activate/deactivate, but just in case
      }

      private void ButtonOffline_Click ( object sender, RoutedEventArgs e ) {
         App.LaunchGame( "offline" );
         SetInfo( GuiInfo.GAME_RUNNING, true );
         GameStatusTimer.Change( Timeout.Infinite, 10_000 ); // Should be overrode by activate/deactivate, but just in case
      }

      private void ButtonCanny_Click   ( object sender, RoutedEventArgs e ) => OpenUrl( "canny", e );
      private void ButtonDiscord_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "discord", e );
      private void ButtonForum_Click   ( object sender, RoutedEventArgs e ) => OpenUrl( "forum", e );
      private void ButtonManual_Click  ( object sender, RoutedEventArgs e ) => OpenUrl( "manual", e );
      private void ButtonReddit_Click  ( object sender, RoutedEventArgs e ) => OpenUrl( "reddit", e );
      private void ButtonTwitter_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "twitter", e );
      private void ButtonWebsite_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "www", e );
      #endregion

      #region Mod Info Area
      private ModInfo CurrentMod;
      private IEnumerable<ModInfo> ModList;

      private void SetModList ( IEnumerable<ModInfo> list ) {
         ModList = list;
         RefreshModList();
         RefreshAppButtons();
      }

      private void RefreshModList () { try {
         Log( "Refreshing mod list" );
         if ( GridModList.ItemsSource != ModList ) {
            Log( "New mod list, clearing selection" );
            GridModList.ItemsSource = ModList;
            SetSelectedMod( null );
         }
         GridModList.Items?.Refresh();
      } catch ( Exception ex ) { Log( ex ); } }

      private void SetSelectedMod ( ModInfo mod ) {
         if ( mod == CurrentMod ) return;
         CurrentMod = mod;
         RefreshModInfo();
         RefreshAppButtons();
      }

      private void RefreshModInfo () { try {
         if ( CurrentMod == null ) {
            Log( "Clearing mod info" );
            RichModInfo.TextRange().Text = "";
            BkgdModeInfo.Opacity = 0.5;
            return;
         }
         Log( $"Refreshing mod {CurrentMod}" );
         BkgdModeInfo.Opacity = 0;
         CurrentMod.BuildDesc( RichModInfo.Document );
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonAddMod_Click ( object sender, RoutedEventArgs e ) {
         var dialog = new Microsoft.Win32.OpenFileDialog {
            DefaultExt = "*.js;*.dll;*.zip;*.7z",
            Filter = "All Mods|*.7z;*.dll;*.js;*.xz;*.zip|"+
                     "Mod Packages (.zip,.7z)|*.7z;*.xz;*.zip|"+
                     "Single File Mods (.dll,.js)|*.dll;*.js|"+
                     "All Files|*.*"
         };
         if ( ! dialog.ShowDialog().GetValueOrDefault() ) return;
         var target = dialog.FileName;
         if ( target.StartsWith( App.ModFolder ) ) {
            MessageBox.Show( "Add Mod failed.\rFile is already in mod folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation );
            return;
         }
         if ( App.CurrentGame != null && target.StartsWith( App.CurrentGame.GameDir ) ) {
            MessageBox.Show( "Add Mod failed.\rFile is in game folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Exclamation );
            return;
         }
         App.AddMod( target );
      }

      private Timer RefreshModTimer;

      private void ButtonRefreshMod_Click ( object sender, RoutedEventArgs e ) {
         if ( RefreshModTimer != null ) return;
         SetModList( null );
         RefreshModTimer = new Timer( ( _ ) => {
            App.GetModList();
            this.Dispatch( () => {
               RefreshModTimer?.Dispose();
               RefreshModTimer = null;
            } );
         }, null, 100, Timeout.Infinite );
         if ( SharedGui.IsGameRunning ) CheckGameRunning();
      }

      private void ButtonModOpenModDir_Click ( object sender, RoutedEventArgs e ) {
         var path = CurrentMod?.Path;
         if ( string.IsNullOrWhiteSpace( path ) ) return;
         AppControl.Explore( path );
      }

      private void ButtonModConf_Click ( object sender, RoutedEventArgs e ) {
         var msg = (bool) CurrentMod.Query( ModQueryType.HAS_CONFIG_FILE ) ? $"Reset config file of \"{CurrentMod.Name}\"?" : $"Create config file of \"{CurrentMod.Name}\"?";
         if ( MessageBox.Show( msg, "Reset Config", MessageBoxButton.OKCancel, MessageBoxImage.Warning, MessageBoxResult.Cancel )
            != MessageBoxResult.OK ) return;
         App.DoModActionAsync( AppActionType.RESET_CONFIG, CurrentMod );
      }

      private void ButtonModDelete_Click ( object sender, RoutedEventArgs e ) {
         if ( CurrentMod == null ) return;
         if ( (bool) CurrentMod.Query( ModQueryType.HAS_CONFIG_FILE ) ) {
            var ans = MessageBox.Show( $"Delete \"{CurrentMod.Name}\".\nKeep settings?", "Confirm",
                  MessageBoxButton.YesNoCancel, MessageBoxImage.Question, MessageBoxResult.Cancel );
            if ( ans == MessageBoxResult.Cancel ) return;
            if ( ans == MessageBoxResult.No )
               App.DoModActionAsync( AppActionType.DELETE_CONFIG, CurrentMod );
         } else {
            var ans = MessageBox.Show( $"Delete \"{CurrentMod.Name}\"?", "Confirm",
                  MessageBoxButton.OKCancel, MessageBoxImage.Question, MessageBoxResult.Cancel );
            if ( ans == MessageBoxResult.Cancel ) return;
         }
         ButtonModDelete.IsEnabled = false;
         App.DoModActionAsync( (bool) CurrentMod.Query( ModQueryType.IS_FOLDER ) ? AppActionType.DELETE_DIR : AppActionType.DELETE_FILE, CurrentMod );
      }

      private void GridModList_CurrentCellChanged ( object sender, EventArgs e ) {
         if ( GridModList.CurrentItem == null ) return;
         SetSelectedMod( GridModList.CurrentItem as ModInfo );
      }
      #endregion

      #region Updater
      private object Update;

      private void CheckUpdate ( bool manual ) { try {
         if ( ! manual ) {
            var lastCheck = App.ModBridge.GetSettings().LastCheckUpdate;
            if ( ! lastCheck.HasValue )
               Log( "Last update check was never" );
            else {
               Log( $"Last update check was {lastCheck}" );
               if ( lastCheck != null && ( DateTime.Now - lastCheck.Value ).TotalDays < 7 ) return;
            }
         }
         Log( "Checking update" );
         Update = "checking";
         RefreshUpdateStatus();
         App.CheckUpdateAsync();
      } catch ( Exception ex ) { Log( ex ); } }

      private void UpdateChecked () { try {
         Log( $"Updating last update check time." );
         App.ModBridge.GetSettings().LastCheckUpdate = DateTime.Now;
         App.ModBridge.SaveSettings();
         ButtonCheckUpdate.IsEnabled = true;
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonCheckUpdate_Click ( object sender, RoutedEventArgs e ) => CheckUpdate( true );

      private void RefreshUpdateStatus () { try {
         Log( $"Update is {(Update ?? "null")}" );
         if ( Object.Equals( "checking", Update ) ) {
            ButtonCheckUpdate.IsEnabled = false;
            BtnTextCheckUpdate.Text = "Checking...";
            return;
         }
         ButtonCheckUpdate.IsEnabled = true;
         BtnTextCheckUpdate.Text = "Check Update";
         GithubRelease release = Update as GithubRelease;
         if ( release == null ) return;

         MessageBoxResult result = MessageBox.Show( $"Update {release.Tag_Name} released.\nOpen download page?", "Updater", MessageBoxButton.YesNo );
         if ( result == MessageBoxResult.No ) return;
         if ( ! String.IsNullOrWhiteSpace( release.Html_Url ) )
            Process.Start( release.Html_Url );
      } catch ( Exception ex ) { Log( ex ); } }
      #endregion

      #region Logging
      public void Log ( object message ) {
         var txt = message?.ToString();
         Console.WriteLine( txt );
         string time = DateTime.Now.ToString( "hh:mm:ss.ffff ", InvariantCulture );
         this.Dispatch( () => { try {
            TextLog.AppendText( time + txt + "\n" );
            TextLog.ScrollToEnd();
            ButtonLogSave.IsEnabled = true;
         } catch ( Exception ex ) { Console.WriteLine( ex ); } } );
      }

      private void ButtonLogSave_Click ( object sender, RoutedEventArgs e ) { try {
         var name =  ButtonLicense.IsChecked == true
               ? AppControl.LIVE_NAME + $" License"
               : AppControl.LIVE_NAME + $" Log " + DateTime.Now.ToString( "u", InvariantCulture ).Replace( ':', '-' );
         var dialog = new Microsoft.Win32.SaveFileDialog {
            FileName = name,
            DefaultExt = ".txt",
            Filter = "Log Files (.txt .log)|*.txt;*.log|All Files|*.*"
         };
         if ( dialog.ShowDialog().GetValueOrDefault() ) {
            File.WriteAllText( dialog.FileName, ButtonLicense.IsChecked == true ? TextLicense.Text : TextLog.Text );
            AppControl.Explore( dialog.FileName );
         }
      } catch ( Exception ex ) { Log( ex ); } }

      private string LoaderLog => Path.Combine( App.ModFolder, "ModnixLoader.log" );

      private void ButtonLoaderLog_Click ( object sender, RoutedEventArgs e ) {
         if ( ! File.Exists( LoaderLog ) )
            MessageBox.Show( "Launch the game at least once to create loader log." );
         else
            AppControl.Explore( LoaderLog );
      }

      private void ButtonLogClear_Click ( object sender, RoutedEventArgs e ) {
         TextLog.Clear();
         ButtonLogSave.IsEnabled = false;
      }

      private void ButtonLicense_Checked ( object sender, RoutedEventArgs e ) { try {
         if ( ButtonLicense.IsChecked == true ) {
            if ( string.IsNullOrEmpty( TextLicense.Text ) )
               using ( var reader = new StreamReader( AppControl.GetResource( "License.txt" ), Encoding.UTF8 ) ) {
                  TextLicense.Text = reader.ReadToEnd();
               }
            LabelLogTitle.Content = "License";
            TextLog.Visibility = ButtonLogClear.Visibility = Visibility.Hidden;
            TextLicense.Visibility = Visibility.Visible;
         } else {
            LabelLogTitle.Content = "Diagnostic Log";
            TextLog.Visibility = ButtonLogClear.Visibility = Visibility.Visible;
            TextLicense.Visibility = Visibility.Hidden;
         }
      } catch ( Exception ex ) { Log( ex ); } }
      #endregion

      #region Helpers
      private void OpenUrl ( string type, RoutedEventArgs e = null ) {
         Log( "OpenUrl " + type );
         if ( e?.Source is UIElement src ) src.Focus();
         string url;
         switch ( type ) {
            case "canny"  : url = "https://phoenixpoint.canny.io/feedback?sort=trending"; break;
            case "discord": url = "https://discordapp.com/invite/phoenixpoint"; break;
            case "forum"  : url = "https://forums.snapshotgames.com/c/phoenix-point"; break;
            case "guide"  : url = "https://github.com/Sheep-y/Modnix/wiki/"; break;
            case "home"   : url = "https://github.com/Sheep-y/Modnix"; break;
            case "manual" : url = "https://drive.google.com/open?id=1n8ORQeDtBkWcnn5Es4LcWBxif7NsXqet"; break;
            case "my_doc" : url = "https://github.com/Sheep-y/Modnix/wiki/Manual-Setup#wiki-wrapper"; break;
            case "nexus"  : url = "https://www.nexusmods.com/phoenixpoint/mods/?BH=0"; break;
            case "reddit" : url = "https://www.reddit.com/r/PhoenixPoint/"; break;
            case "twitter": url = "https://twitter.com/Phoenix_Point"; break;
            case "wiki"   : url = "https://phoenixpoint.fandom.com/wiki/"; break;
            case "www"    : url = "https://phoenixpoint.info/"; break;
            default       : return;
         }
         Log( $"Opening {url}" );
         Process.Start( url );
      }
      #endregion
   }

   public static class WpfHelper {
      public static TextRange TextRange ( this RichTextBox box ) {
         return new TextRange( box.Document.ContentStart, box.Document.ContentEnd );
      }

      public static void Replace ( this FlowDocument doc, params Block[] blocks ) => Replace( doc, (IEnumerable<Block>) blocks );
      public static void Replace ( this FlowDocument doc, IEnumerable< Block > blocks ) {
         var body = doc.Blocks;
         body.Clear();
         foreach ( var e in blocks )
            if ( e != null )
               body.Add( e );
      }

      public static Paragraph P ( params Inline[] inlines ) => P( (IEnumerable<Inline>) inlines );
      public static Paragraph P ( IEnumerable< Inline > inlines ) {
         var result = new Paragraph();
         var body = result.Inlines;
         foreach ( var e in inlines )
            if ( e != null )
               body.Add( e );
         return result;
      }
   }
}