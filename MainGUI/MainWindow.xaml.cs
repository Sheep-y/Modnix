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

      public MainWindow ( AppControl app ) {
         Contract.Requires( app != null );
         App = app;
         InitializeComponent();
         RefreshGUI();
      }

      private void RefreshGUI () {
         Log( "Resetting GUI" );
         RefreshAppInfo();
         RefreshGameInfo();
         RefreshModInfo();
         RefreshUpdateStatus();
         Log( "Initiating Controller" );
         App.CheckStatusAsync();
         App.CheckUpdateAsync();
      }

      public void SetInfo ( string info, object value ) { this.Dispatch( () => {
         Log( $"Set {info} = {value}" );
         string txt = value?.ToString();
         switch ( info ) {
            case "visible" : Show(); break;
            case "version" : AppVer = txt; RefreshAppInfo(); break;
            case "state"   : AppState = txt; RefreshAppInfo(); break;
            case "game_path"    : GamePath = txt; RefreshGameInfo(); break;
            case "game_version" : GameVer  = txt; RefreshGameInfo(); break;
            case "update"  : Update = value; RefreshUpdateStatus(); break;
            default : Log( $"Unknown info {info}" ); break;
         }
      } ); }

      #region App Info Area
      private void RefreshAppInfo () {
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
      }

      private void RefreshAppButtons () {
         ButtonSetup.IsEnabled  = AppState != null;
         switch ( AppState ) {
            case "modnix"  : ButtonSetup.Content = "Revert"; break;
            case "running" : ButtonSetup.Content = "Refresh"; break;
            default        : ButtonSetup.Content = "Setup"; break;
         }
         ButtonModDir.IsEnabled = AppState == "modnix";
         ButtonAddMod.IsEnabled = AppState == "modnix";
      }

      private void ButtonSetup_Click ( object sender, RoutedEventArgs e ) {
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
      }

      private void DoSetup () {
         Log( "Calling setup" );
         SetInfo( "state", null );
         App.DoSetupAsync();
      }

      private void DoManualSetup () {
         // TODO: Link to GitHub Doc
         MessageBox.Show( "Manual Setup not documented." );
      }

      private void DoRestore () {
         Log( "Calling restore" );
         SetInfo( "state", null );
         App.DoRestoreAsync();
      }

      private void ButtonModDir_Click ( object sender, RoutedEventArgs e ) {
         string arg = $"/select, \"{Path.Combine( App.ModFolder, App.ModGuiExe )}\"";
         Log( $"Launching explorer.exe {arg}" );
         Process.Start( "explorer.exe", arg );
      }

      public void Prompt ( string parts, Exception ex = null ) { this.Dispatch( () => {
         Log( $"Prompt {parts}" );
         SharedGui.Prompt( parts, ex, () => {
            Process.Start( App.ModGuiExe, "/i " + Process.GetCurrentProcess().Id );
            Close();
         } );
      } ); }

      private void ButtonNexus_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "nexus", e );
      #endregion

      #region Game Info Area
      private void RefreshGameInfo () {
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
      }

      private void ButtonOnline_Click ( object sender, RoutedEventArgs e ) => App.LaunchGame( "online" );
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
      private void RefreshModInfo () {
         //string txt = AppState == "modenix" ? "Select a mod to see info" : "";
         string txt = AppState == "modenix" ? "Mod list is being implemented" : "";
         richModInfo.TextRange().Text = txt;
      }
      #endregion

      #region Updater
      private object Update;

      private void CheckUpdate () {
         App.CheckUpdateAsync();
         Update = "checking";
         RefreshUpdateStatus();
      }

      private void ButtonGitHub_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "home", e );
      private void ButtonCheckUpdate_Click ( object sender, RoutedEventArgs e ) => CheckUpdate();

      private void RefreshUpdateStatus () {
         if ( Object.Equals( "checking", Update ) ) {
            ButtonCheckUpdate.IsEnabled = false;
            ButtonCheckUpdate.Content = "Checking...";
            return;
         }
         ButtonCheckUpdate.IsEnabled = true;
         ButtonCheckUpdate.Content = "Check Update";
         GithubRelease release = Update as GithubRelease;
         if ( release == null ) return;

         MessageBoxResult result = MessageBox.Show( $"Update {release.tag_name} released.\nOpen download page?", "Updater", MessageBoxButton.YesNo );
         if ( result == MessageBoxResult.No ) return;
         if ( ! String.IsNullOrWhiteSpace( release.html_url ) )
            Process.Start( release.html_url );
      }
      #endregion

      #region Logging
      public void Log ( string message ) {
         string time = DateTime.Now.ToString( "hh:mm:ss.ffff ", InvariantCulture );
         this.Dispatch( () => {
            textLog.AppendText( time + message );
            textLog.AppendText( "\n" );
            ButtonLogSave.IsEnabled = true;
         } );
      }

      private void ButtonLogSave_Click ( object sender, RoutedEventArgs e ) {
         var dialog = new Microsoft.Win32.SaveFileDialog {
            FileName = AppControl.LIVE_NAME + " Log " + DateTime.Now.ToString( "u", InvariantCulture ).Replace( ':', '-' ),
            DefaultExt = ".txt",
            Filter = "Log Files (.txt .log)|*.txt;*.log|All Files|*.*"
         };
         if ( dialog.ShowDialog().GetValueOrDefault() ) try {
            File.WriteAllText( dialog.FileName, textLog.Text );
            Process.Start("explorer.exe", $"/select, \"{dialog.FileName}\"" );
         } catch ( Exception ex ) {
            Log( ex.ToString() );
         }
      }

      private void ButtonAddMod_Click ( object sender, RoutedEventArgs e ) {
          MessageBox.Show( "Not Implemened", "Sorry", MessageBoxButton.OK, MessageBoxImage.Exclamation );
      }

      private void ButtonLogClear_Click ( object sender, RoutedEventArgs e ) {
         textLog.Clear();
         ButtonLogSave.IsEnabled = false;
      }
      #endregion

      private void OpenUrl ( string type, RoutedEventArgs e = null ) {
         Log( "OpenUrl " + type );
         if ( e != null )
            if ( e.Source is UIElement src )
               src.Focus();
         string url;
         switch ( type ) {
            case "canny"  : url = "https://phoenixpoint.canny.io/feedback?sort=trending"; break;
            case "discord": url = "https://discordapp.com/invite/phoenixpoint"; break;
            case "forum"  : url = "https://forums.snapshotgames.com/c/phoenix-point"; break;
            case "home"   : url = "https://github.com/Sheep-y/Modnix"; break;
            case "manual" : url = "https://drive.google.com/open?id=1n8ORQeDtBkWcnn5Es4LcWBxif7NsXqet"; break;
            case "nexus"  : url = "https://www.nexusmods.com/phoenixpoint/mods/?BH=0"; break;
            case "reddit" : url = "https://www.reddit.com/r/PhoenixPoint/"; break;
            case "twitter": url = "https://twitter.com/Phoenix_Point"; break;
            case "www"    : url = "https://phoenixpoint.info/"; break;
            default       : return;
         }
         Log( $"Opening {url}" );
         Process.Start( url );
      }
   }

   public static class WpfHelper {
      public static TextRange TextRange ( this RichTextBox box ) {
         Contract.Requires( box != null );
         return new TextRange( box.Document.ContentStart, box.Document.ContentEnd );
      }
   }
}