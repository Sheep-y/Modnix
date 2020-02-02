using System;
using System.Collections.Generic;
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

      private string AppVer, AppState, GamePath, GameVer;

      public MainWindow () {
         InitializeComponent();
         RefreshGUI();
      }

      private void RefreshGUI () {
         Log( "Resetting GUI" );
         RefreshAppInfo();
         RefreshGameInfo();
         RefreshModInfo();
         Log( "Initiating Controller" );
         new AppControl( this ).CheckAppStateAsync();
      }

      #region App Info
      public void SetAppVer ( string value ) { Dispatch( () => {
         AppVer = value;
         RefreshAppInfo();
      } ); }

      /// Set and update app state.
      /// Value param May be "ppml", "modnix", "setup", "no_game", or "missing".
      public void SetAppState ( string value ) { Dispatch( () => {
         Log( "Injection status: " + value );
         AppState = value;
         RefreshAppInfo();
      } ); }

      private void RefreshAppInfo () {
         string txt = $"Modnix\rVer {AppVer}\rStatus: ";
         if ( AppState == null )
            txt += "Checking";
         else
            switch ( AppState ) {
               case "ppml"   : txt += "PPML found, need update"; break;
               case "modnix" : txt += "Injected"; break;
               case "setup"  : txt += "Requires Setup"; break;
               case "no_game": txt += "Requires Manual Setup"; break;
               case "missing": txt += "Setup files missing, please re-download"; break;
               default: txt += "Need Setup"; break;
            }
         richAppInfo.TextRange().Text = txt;
      }
      #endregion

      #region Game Info
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
      #endregion

      private void RefreshModInfo () {
         string txt = AppState == "modenix" ? "Select a mod to see info" : "";
         richModInfo.TextRange().Text = txt;
      }

      private void ButtonCanny_Click   ( object sender, RoutedEventArgs e ) => OpenUrl( "canny", e );
      private void ButtonDiscord_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "discord", e );
      private void ButtonForum_Click   ( object sender, RoutedEventArgs e ) => OpenUrl( "forum", e );
      private void ButtonManual_Click  ( object sender, RoutedEventArgs e ) => OpenUrl( "manual", e );
      private void ButtonReddit_Click  ( object sender, RoutedEventArgs e ) => OpenUrl( "reddit", e );
      private void ButtonWebsite_Click ( object sender, RoutedEventArgs e ) => OpenUrl( "www", e );

      private void OpenUrl ( string type, RoutedEventArgs e = null ) {
         if ( e != null )
            if ( e.Source is UIElement src )
               src.Focus();
         string url;
         switch ( type ) {
            case "canny"  : url = "https://phoenixpoint.canny.io/feedback?sort=trending"; break;
            case "discord": url = "https://discordapp.com/invite/phoenixpoint"; break;
            case "forum"  : url = "https://forums.snapshotgames.com/c/phoenix-point"; break;
            case "manual" : url = "https://drive.google.com/open?id=1n8ORQeDtBkWcnn5Es4LcWBxif7NsXqet"; break;
            case "reddit" : url = "https://www.reddit.com/r/PhoenixPoint/"; break;
            case "www"    : url = "https://phoenixpoint.info/"; break;
            default       : return;
         }
         System.Diagnostics.Process.Start( url );
      }

      #region Helpers
      public void Log ( string message ) {
         string time = DateTime.Now.ToString( "hh:mm:ss.ffff " );
         Dispatch( () => {
            textLog.AppendText( time + message );
            textLog.AppendText( "\n" );
         } );
      }

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
