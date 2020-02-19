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
using static System.Globalization.CultureInfo;

namespace Sheepy.Modnix.MainGUI {

   public partial class SetupWindow : Window, IAppGui {

      private readonly AppControl App;
      private string AppVer, AppState, GamePath = null;
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

      public void SetInfo ( string info, object value ) { this.Dispatch( () => {
         try {
            string txt = value?.ToString();
            switch ( info ) {
               case "visible": Show(); break;
               case "version": AppVer = txt; break;
               case "state": AppState = txt; break;
               case "game_path": GamePath = txt; break;
               case "game_version": 
               case "update": 
                  break;
               default: Log( $"Unknown info {info}" ); return;
            }
            RefreshInfo();
            if ( info == "game_path" ) ButtonAction.Focus();
         } catch ( Exception ex ) { Log( ex ); }
      } ); }

      private void RefreshInfo () { try {
         if ( Mode == "log" ) {
            return;
         }
         string txt = $"Modnix {AppVer}\n";
         if ( Mode == "launch" ) {
            txt += "Installed at " + App.ModGuiExe + "\n\nUse it to resetup or restore.";
            EnableLaunch();
         } else { // Mode == "setup"
            txt += "\nPhoenix Point\n";
            if ( AppState == "no_game" ) {
               txt += "Not Found";
               AccessAction.Text = "Manual _Setup";
               ButtonAction.IsEnabled = true;
            } else {
               txt += GamePath ?? "Detecting...";
               AccessAction.Text = "_Setup";
               ButtonAction.IsEnabled = GamePath != null;
            }
         }
         TextMessage.Text = txt;
      } catch ( Exception ex ) { Log( ex ); } }

      private void EnableLaunch () {
         AccessAction.Text = "_Launch";
         ButtonAction.IsEnabled = true;
      }

      private void ButtonAction_Click ( object sender, RoutedEventArgs e ) { try {
         Log( $"\"{Mode}\" initiated" );
         if ( e.Source is Button btn ) btn.Focus();
         if ( Mode == "setup" ) {
            if ( AppState == "no_game" ) {
               Process.Start( "https://github.com/Sheep-y/Modnix/wiki/Manual-Setup#wiki-wrapper" );
               return;
            }
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
            App.LaunchInstalledModnix();
            Close();
         }
      } catch ( Exception ex ) { Log( ex ); } }

      public void Prompt ( string parts, Exception ex = null ) {
         this.Dispatch( () => {
            if ( AppState == "modnix" )
               EnableLaunch();
            Log( $"Prompt {parts}" );
            SharedGui.Prompt( parts, ex, () => {
               App.LaunchInstalledModnix();
               Close();
            } );
         } );
      }

      public void Log ( object message ) {
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
