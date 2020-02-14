using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using static System.Globalization.CultureInfo;

namespace Sheepy.Modnix.MainGUI {

   public partial class SetupWindow : Window, IAppGui {

      private readonly AppControl App;
      private string AppVer, AppState, GamePath = "Detecting...";
      private string Mode = "log"; // launch, setup, log
      private string LogContent;

      public SetupWindow ( AppControl app, string mode ) {
         Contract.Requires( app != null );
         App = app;
         Mode = mode;
         InitializeComponent();
         RefreshInfo();
         App.CheckStatusAsync();
      }

      public void SetInfo ( string info, string value ) { this.Dispatch( () => {
         switch ( info ) {
            case "visible": Show(); break;
            case "version": AppVer = value; break;
            case "state": AppState = value; break;
            case "game_path": GamePath = value; break;
            case "game_version": break;
            default: Log( $"Unknown info {info}" ); return;
         }
         RefreshInfo();
         if ( info == "game_path" ) ButtonAction.Focus();
      } ); }

      private void RefreshInfo () {
         if ( Mode == "log" ) {
            if ( AppState == "modnix" )
               EnableLaunch();
            return;
         }
         string txt = $"Modnix {AppVer}\n";
         if ( Mode == "launch" ) {
            txt += "Installed at " + App.ModGuiExe + "\n\nUse it to resetup or restore.";
            EnableLaunch();
         } else { // Mode == "setup"
            txt += $"\nPhoenix Point\n{GamePath}";
            AccessAction.Text = "_Setup";
            ButtonAction.IsEnabled = GamePath != null;
         }
         TextMessage.Text = txt;
      }

      private void EnableLaunch () {
         AccessAction.Text = "_Launch";
         ButtonAction.IsEnabled = true;
      }

      private void ButtonAction_Click ( object sender, RoutedEventArgs e ) {
         Log( $"\"{Mode}\" initiated" );
         if ( e.Source is Button btn ) btn.Focus();
         if ( Mode == "setup" ) {
            // Switch to log mode and call setup
            AccessAction.Text = "Wait";
            Mode = "log";
            ButtonAction.IsEnabled = false;
            TextMessage.TextAlignment = TextAlignment.Left;
            TextMessage.FontSize = 12;
            TextMessage.Text = LogContent;
            TextMessage.ScrollToEnd();
            RefreshInfo();
            App.DoSetupAsync();

         } else { // Launch
            Process.Start( App.ModGuiExe, "/i " + Process.GetCurrentProcess().Id );
            Close();
         }
      }

      public void Prompt ( string parts, Exception ex = null ) {
         this.Dispatch( () => {
            Log( $"Prompt {parts}" );
            SharedGui.Prompt( parts, ex, () => {
               Process.Start( App.ModGuiExe, "/i " + Process.GetCurrentProcess().Id );
               Close();
            } );
         } );
      }

      public void Log ( string message ) {
         string time = DateTime.Now.ToString( "hh:mm:ss.ffff ", InvariantCulture );
         string line = $"{time} {message}\n";
         this.Dispatch( () => {
            if ( Mode == "log" ) {
               TextMessage.AppendText( line );
               TextMessage.ScrollToEnd();
            }  else
               LogContent += line;
         } );
      }

   }
}
