using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using static System.Globalization.CultureInfo;

namespace Sheepy.Modnix.MainGUI {

   public partial class MainWindow : Window, IAppGui {
      private readonly AppControl App;
      private string AppVer, AppState, GamePath, GameVer;
      private System.Collections.IEnumerable ModList;

      public MainWindow ( AppControl app ) { try {
         Contract.Requires( app != null );
         App = app;
         InitializeComponent();
         RefreshGUI();
      } catch ( Exception ex ) { Console.WriteLine( ex ); } }

      private void RefreshGUI () { try {
         Log( "Resetting GUI" );
         RefreshAppInfo();
         RefreshGameInfo();
         RefreshModList();
         RefreshUpdateStatus();
         Log( "Initiating Controller" );
         App.CheckStatusAsync();
         if ( ! App.paramSkipProcessCheck )
            CheckUpdate( false );
      } catch ( Exception ex ) { Log( ex ); } }

      public void SetInfo ( string info, object value ) { this.Dispatch( () => { try {
         Log( $"Set {info} = {value}" );
         string txt = value?.ToString();
         switch ( info ) {
            case "visible" : Show(); break;
            case "version" : AppVer = txt; RefreshAppInfo(); break;
            case "state"   : AppState = txt; RefreshAppInfo(); break;
            case "game_path"    : GamePath = txt; RefreshGameInfo(); break;
            case "game_version" : GameVer  = txt; RefreshGameInfo(); break;
            case "update"  : Update = value; UpdateChecked(); RefreshUpdateStatus(); break;
            case "mod_list" : ModList = value as System.Collections.IEnumerable; RefreshModList(); break;
            default : Log( $"Unknown info {info}" ); break;
         }
      } catch ( Exception ex ) { Log( ex ); } } ); }

      #region App Info Area
      private void RefreshAppInfo () { try {
         Log( "Refreshing app info" );
         string txt = $"Modnix\rVer {AppVer}\rStatus: ";
         if ( AppState == null )
            txt += "Busy";
         else
            switch ( AppState ) {
               case "ppml"   : txt += "PPML found, need update"; break;
               case "modnix" : txt += "Injected"; break;
               case "setup"  : txt += "Requires Setup"; break;
               case "running": txt += "Game is running"; break;
               case "no_game": txt += "Game not found; Please do Manual Setup"; break;
               default: txt += "Unknown state; see log"; break;
            }
         richAppInfo.TextRange().Text = txt;
         RefreshAppButtons();
         RefreshModList();
      } catch ( Exception ex ) { Log( ex ); } }

      private void RefreshAppButtons () { try {
         Log( "Refreshing app buttons" );
         ButtonSetup.IsEnabled  = AppState != null;
         switch ( AppState ) {
            case "modnix"  : ButtonSetup.Content = "Revert"; break;
            case "running" : ButtonSetup.Content = "Refresh"; break;
            default        : ButtonSetup.Content = "Setup"; break;
         }
         ButtonModDir.IsEnabled = AppState == "modnix";
         ButtonAddMod.IsEnabled = AppState == "modnix";
         ButtonLoaderLog.IsEnabled = AppState == "modnix";
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonSetup_Click ( object sender, RoutedEventArgs e ) { try {
         Log( "Main action button clicked" );
         if ( e?.Source is UIElement src ) src.Focus();
         switch ( AppState ) {
            case "ppml" : case "setup" :
               DoSetup();
               break;
            case "modnix" :
               if ( MessageBox.Show( "Remove Modnix from Phoenix Poing?", "Revert", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No ) == MessageBoxResult.Yes )
                  DoRestore();
               break;
            case "running" :
               App.CheckStatusAsync();
               break;
            default:
               DoManualSetup();
               break;
         }
      } catch ( Exception ex ) { Log( ex ); } }

      private void DoSetup () {
         Log( "Calling setup" );
         AppState = null;
         RefreshAppInfo();
         App.DoSetupAsync();
      }

      private void DoManualSetup () => OpenUrl( "my_doc", null );

      private void DoRestore () {
         Log( "Calling restore" );
         AppState = null;
         RefreshAppInfo();
         App.DoRestoreAsync();
      }

      private void ButtonModDir_Click ( object sender, RoutedEventArgs e ) { try {
         string arg = $"/select, \"{Path.Combine( App.ModFolder, App.ModGuiExe )}\"";
         Log( $"Launching explorer.exe {arg}" );
         Process.Start( "explorer.exe", arg );
      } catch ( Exception ex ) { Log( ex ); } }

      public void Prompt ( string parts, Exception ex = null ) { this.Dispatch( () => { try {
         Log( $"Prompt {parts}" );
         SharedGui.Prompt( parts, ex, () => {
            App.LaunchInstalledModnix();
            Close();
         } );
      } catch ( Exception err ) { Log( err ); } } ); }

      private void ButtonNexus_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "nexus", e );
      #endregion

      #region Game Info Area
      private void RefreshGameInfo () { try {
         Log( "Refreshing game info" );
         string txt = "Phoenix Point";
         if ( GamePath != null ) {
            txt += "\r" + Path.GetFullPath( GamePath );
            if ( GameVer  != null )
               txt += "\rVer: " + GameVer;
         } else
            txt += "Game not found";
         richGameInfo.TextRange().Text = txt;
         ButtonRunOnline .IsEnabled = GamePath != null;
         ButtonRunOffline.IsEnabled = GamePath != null;
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonOnline_Click  ( object sender, RoutedEventArgs e ) => App.LaunchGame( "online" );
      private void ButtonOffline_Click ( object sender, RoutedEventArgs e ) => App.LaunchGame( "offline" );
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

      private void RefreshModList () { try {
         Log( "Refreshing mod list" );
         bool IsInjected = AppState == "modnix";
         ButtonAddMod.IsEnabled = IsInjected;
         ButtonRefreshMod.IsEnabled = IsInjected;
         if ( ! IsInjected ) {
            LabelModList.Content = "Requires Setup";
            richModInfo.TextRange().Text = "";
            GridModList.ItemsSource = null;
         }
         if ( GridModList.ItemsSource != ModList )
            GridModList.ItemsSource = ModList;
         GridModList.Items?.Refresh();
      } catch ( Exception ex ) { Log( ex ); } }

      private void RefreshModInfo ( ModInfo mod ) {
         if ( mod == CurrentMod ) return;
         CurrentMod = mod;
         RefreshModInfo();
      }

      private void RefreshModInfo () { try {
         ButtonModDelete.IsEnabled = CurrentMod == null;
         ButtonModOpenModDir.IsEnabled = CurrentMod == null;
         if ( CurrentMod == null ) {
            Log( "Clearing mod info" );
            return;
         }
         Log( $"Refreshing mod {CurrentMod}" );
         richModInfo.TextRange().Text = 
            $"{CurrentMod.Name}\rVer {CurrentMod.Version}\rType {CurrentMod.Type}\r{CurrentMod.Path}\nAuthor\t{(CurrentMod.Author)}";
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonAddMod_Click ( object sender, RoutedEventArgs e ) {
         RefreshModInfo( null );
         RefreshModList();
      }

      private void GridModList_CurrentCellChanged ( object sender, EventArgs e ) {
         if ( GridModList.CurrentItem == null ) return;
         RefreshModInfo( GridModList.CurrentItem as ModInfo );
      }
      #endregion

      #region Updater
      private object Update;

      private void CheckUpdate ( bool manual ) { try {
         if ( ! manual ) {
            DateTime lastCheck = Properties.Settings.Default.Last_Update_Check;
            Log( $"Last update check was {lastCheck}" );
            if ( lastCheck != null && ( DateTime.Now - lastCheck ).TotalDays < 7 ) return;
         }
         Log( "Checking update" );
         Update = "checking";
         RefreshUpdateStatus();
         App.CheckUpdateAsync();
      } catch ( Exception ex ) { Log( ex ); } }

      private void UpdateChecked () { try {
         Log( $"Updating last update check time." );
         Properties.Settings.Default.Last_Update_Check = DateTime.Now;
         Properties.Settings.Default.Save();
         ButtonCheckUpdate.IsEnabled = true;
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonGitHub_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "home", e );
      private void ButtonCheckUpdate_Click ( object sender, RoutedEventArgs e ) => CheckUpdate( true );

      private void RefreshUpdateStatus () { try {
         Log( $"Update is {(Update ?? "null")}" );
         if ( Object.Equals( "checking", Update ) ) {
            ButtonCheckUpdate.IsEnabled = false;
            ButtonCheckUpdate.Content = "Checking...";
            return;
         }
         ButtonCheckUpdate.IsEnabled = true;
         ButtonCheckUpdate.Content = "Check Update";
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
         string time = DateTime.Now.ToString( "hh:mm:ss.ffff ", InvariantCulture );
         this.Dispatch( () => { try {
            TextLog.AppendText( time + message?.ToString() + "\n" );
            TextLog.ScrollToEnd();
            ButtonLogSave.IsEnabled = true;
         } catch ( Exception ex ) { Console.WriteLine( ex ); } } );
      }

      private void ButtonLogSave_Click ( object sender, RoutedEventArgs e ) { try {
         var dialog = new Microsoft.Win32.SaveFileDialog {
            FileName = AppControl.LIVE_NAME + " Log " + DateTime.Now.ToString( "u", InvariantCulture ).Replace( ':', '-' ),
            DefaultExt = ".txt",
            Filter = "Log Files (.txt .log)|*.txt;*.log|All Files|*.*"
         };
         if ( dialog.ShowDialog().GetValueOrDefault() ) {
            File.WriteAllText( dialog.FileName, TextLog.Text );
            Explore( dialog.FileName );
         }
      } catch ( Exception ex ) { Log( ex ); } }

      private void ButtonLoaderLog_Click ( object sender, RoutedEventArgs e ) {
         string path = Path.Combine( App.ModFolder, "ModnixLoader.log" );
         if ( ! File.Exists( path ) )
            MessageBox.Show( "Launch the game at least once to create loader log." );
         else
            Explore( path );
      }

      private void ButtonLogClear_Click ( object sender, RoutedEventArgs e ) {
         TextLog.Clear();
         ButtonLogSave.IsEnabled = false;
      }
      #endregion

      #region Openers
      private void OpenUrl ( string type, RoutedEventArgs e = null ) {
         Log( "OpenUrl " + type );
         if ( e?.Source is UIElement src ) src.Focus();
         string url;
         switch ( type ) {
            case "canny"  : url = "https://phoenixpoint.canny.io/feedback?sort=trending"; break;
            case "discord": url = "https://discordapp.com/invite/phoenixpoint"; break;
            case "forum"  : url = "https://forums.snapshotgames.com/c/phoenix-point"; break;
            case "home"   : url = "https://github.com/Sheep-y/Modnix"; break;
            case "manual" : url = "https://drive.google.com/open?id=1n8ORQeDtBkWcnn5Es4LcWBxif7NsXqet"; break;
            case "my_doc" : url = "https://github.com/Sheep-y/Modnix/wiki/Manual-Setup#wiki-wrapper"; break;
            case "nexus"  : url = "https://www.nexusmods.com/phoenixpoint/mods/?BH=0"; break;
            case "reddit" : url = "https://www.reddit.com/r/PhoenixPoint/"; break;
            case "twitter": url = "https://twitter.com/Phoenix_Point"; break;
            case "www"    : url = "https://phoenixpoint.info/"; break;
            default       : return;
         }
         Log( $"Opening {url}" );
         Process.Start( url );
      }

      private void Explore ( string filename ) {
         Process.Start( "explorer.exe", $"/select, \"{filename}\"" );
      }
      #endregion
   }

   public static class WpfHelper {
      public static TextRange TextRange ( this RichTextBox box ) {
         Contract.Requires( box != null );
         return new TextRange( box.Document.ContentStart, box.Document.ContentEnd );
      }
   }
}