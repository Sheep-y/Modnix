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

      private string AppVer, AppState, GameVer, GameState;

      public MainWindow () {
         InitializeComponent();
         RefreshGUI();
      }

      private void RefreshGUI () {
         Log( "Initiating Controller" );
         new AppControl( this ).CheckAppStateAsync();
      }

      public void Log ( string message ) { Dispatch( () => {
         textLog.AppendText( message );
         textLog.AppendText( "\n" );
      } ); }

      public void SetAppVer ( string value ) { Dispatch( () => {
         Log( "Modnix version: " + value );
         AppVer = value;
         RefreshAppVer();
      } ); }

      public void SetAppState ( string value ) { Dispatch( () => {
         Log( "Injection status: " + value );
         AppState = value;
         RefreshAppVer();
      } ); }

      private void RefreshAppVer () {
         string txt = "Modnix\rVer " + AppVer + "\rStatus: ";
         if ( AppState == null )
            txt += "Checking";
         else
            switch ( AppState ) {
               case "ppml" : txt += "PPML found, need update"; break;
               case "modnix" : txt += "Injected"; break;
               default: txt += "Need Setup"; break;
            }
         richAppInfo.TextRange().Text = txt;
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

      private void Dispatch ( Action task ) => Dispatcher.Invoke( task );
   }

   public static class WpfHelper {
      public static TextRange TextRange ( this RichTextBox box ) {
         return new TextRange( box.Document.ContentStart, box.Document.ContentEnd );
      }
   }
}
