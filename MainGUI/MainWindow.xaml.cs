using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Sheepy.Modnix.MainGUI {

   public partial class MainWindow : Window {

      private AppControl App;
      private string AppVer, AppState, GamePath, GameVer;

      public MainWindow () {
         InitializeComponent();
         RefreshGUI();
      }

      private void RefreshGUI () {
         Log( "Time is " + DateTime.Now.ToString("u") );
         Log( "Resetting GUI" );
         RefreshAppInfo();
         RefreshGameInfo();
         RefreshModInfo();
         Log( "Initiating Controller" );
         App = new AppControl( this );
         App.CheckStatusAsync();
      }

      #region App Info Area
      public void SetAppVer ( string value ) { Dispatch( () => {
         AppVer = value;
         RefreshAppInfo();
      } ); }

      /// Set and update app state.
      public void SetAppState ( string value ) { Dispatch( () => {
         Log( "App status: " + value );
         AppState = value;
         RefreshAppInfo();
      } ); }

      private void RefreshAppInfo () {
         string txt = $"Modnix\rVer {AppVer}\rStatus: ";
         if ( AppState == null )
            txt += "Working";
         else
            switch ( AppState ) {
               case "ppml"   : txt += "PPML found, need update"; break;
               case "modnix" : txt += "Injected"; break;
               case "setup"  : txt += "Requires Setup"; break;
               case "no_game": txt += "Game not found; Please do Manual Setup"; break;
               default: txt += "Unknown state; see log"; break;
            }
         richAppInfo.TextRange().Text = txt;
         RefreshAppButtons();
      }

      private void RefreshAppButtons () {
         ButtonSetup.IsEnabled  = AppState != null;
         ButtonSetup.Content = AppState == "modnix" ? "Uninstall" : "Setup";
         ButtonModDir.IsEnabled = AppState == "modnix";
         ButtonAddMod.IsEnabled = AppState == "modnix";
      }

      private void ButtonSetup_Click ( object sender, RoutedEventArgs e ) {
         switch ( AppState ) {
            case "ppml" : case "setup" :
               DoSetup();
               break;
            case "modnix" :
               DoRestore();
               break;
            default:
               DoManualSetup();
               break;
         }
      }

      private void DoSetup () {
         Log( "Calling setup" );
         SetAppState( null );
         App.DoSetupAsync();
      }

      private void DoManualSetup () {
         // TODO: Link to GitHub Doc
         MessageBox.Show( "Not Implemented." );
      }

      private void DoRestore () {
         Log( "Calling restore" );
         SetAppState( null );
         App.DoRestoreAsync();
      }

      public void SetupSuccess ( bool HasPPML ) { Dispatch( () => {
         if ( HasPPML && MessageBox.Show( "Setup success. Delete PPML to prevent accidents?", "Question", MessageBoxButton.YesNo ) == MessageBoxResult.Yes ) {
            App.DeletePPMLAsync();
         }
      } ); }

      public void RestoreSuccess () {}

      private void ButtonNexus_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "nexus", e );
      #endregion

      #region Game Info Area
      public void SetGamePath ( string value ) { Dispatch( () => {
         GamePath = value;
         RefreshGameInfo();
      } ); }
      
      public void SetGameVer ( string value ) { Dispatch( () => {
         GameVer = value;
         RefreshGameInfo();
      } ); }

      private void RefreshGameInfo () {
         string txt = "Phoenix Point";
         if ( GamePath != null ) {
            txt += "\r" + System.IO.Path.GetFullPath( GamePath );
            if ( GameVer  != null )
               txt += "\rVer: " + GameVer;
         } else
            txt += "Game not found";
         richGameInfo.TextRange().Text = txt;
      }

      private void ButtonCanny_Click   ( object sender, RoutedEventArgs e ) => OpenUrl( "canny", e );
      private void ButtonDiscord_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "discord", e );
      private void ButtonForum_Click   ( object sender, RoutedEventArgs e ) => OpenUrl( "forum", e );
      private void ButtonManual_Click  ( object sender, RoutedEventArgs e ) => OpenUrl( "manual", e );
      private void ButtonReddit_Click  ( object sender, RoutedEventArgs e ) => OpenUrl( "reddit", e );
      private void ButtonWebsite_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "www", e );
      #endregion

      #region Mod Info Area
      private void RefreshModInfo () {
         string txt = AppState == "modenix" ? "Select a mod to see info" : "";
         richModInfo.TextRange().Text = txt;
      }
      #endregion

      #region Log Tab
      public void Log ( string message ) {
         string time = DateTime.Now.ToString( "hh:mm:ss.ffff " );
         Dispatch( () => {
            textLog.AppendText( time + message );
            textLog.AppendText( "\n" );
            ButtonLogSave.IsEnabled = true;
         } );
      }

      private void ButtonLogSave_Click ( object sender, RoutedEventArgs e ) {
         var dialog = new Microsoft.Win32.SaveFileDialog {
            FileName = "ModnixGuiLog " + DateTime.Now.ToString( "u" ).Replace( ':', '-' ),
            DefaultExt = ".txt",
            Filter = "Log Files (.txt .log)|*.txt;*.log|All Files|*.*"
         };
         if ( dialog.ShowDialog().GetValueOrDefault() ) try {
            File.WriteAllText( dialog.FileName, textLog.Text );
            Process.Start("explorer.exe", "/select, \"" + dialog.FileName +"\"" );
         } catch ( Exception ex ) {
            Log( ex.ToString() );
         }
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
            case "manual" : url = "https://drive.google.com/open?id=1n8ORQeDtBkWcnn5Es4LcWBxif7NsXqet"; break;
            case "nexus"  : url = "https://www.nexusmods.com/phoenixpoint"; break;
            case "reddit" : url = "https://www.reddit.com/r/PhoenixPoint/"; break;
            case "www"    : url = "https://phoenixpoint.info/"; break;
            default       : return;
         }
         Log( "Opening " + url );
         Process.Start( url );
      }

      #region Helpers
      private void Dispatch ( Action task ) {
         if ( Dispatcher.CheckAccess() )
            task();
         else
            Dispatcher.Invoke( task );
      }
      #endregion
   }

   public static class WpfHelper {
      public static TextRange TextRange ( this RichTextBox box ) {
         return new TextRange( box.Document.ContentStart, box.Document.ContentEnd );
      }
   }
}
