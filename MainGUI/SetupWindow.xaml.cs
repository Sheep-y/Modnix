using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using static System.Globalization.CultureInfo;

namespace Sheepy.Modnix.MainGUI {

   public partial class SetupWindow : Window, IAppGui {

      private readonly AppControl App = AppControl.Instance;
      private string Mode = "log"; // launch, setup, log
      private string LogContent;

      public SetupWindow ( string mode ) { try {
         Mode = mode;
         InitializeComponent();
         SharedGui.AppStateChanged += RefreshInfo;
         SharedGui.GamePathChanged += RefreshInfo;
         SharedGui.VersionChanged += RefreshInfo;
         SharedGui.AppWorkingChanged += RefreshInfo;
         SharedGui.GameRunningChanged += RefreshInfo;
      } catch ( Exception ex ) { Console.WriteLine( ex ); } }

      private void Window_Activated ( object sender, EventArgs e ) {
         SetInfo( GuiInfo.GAME_RUNNING, AppControl.IsGameRunning() );
      }

      public void SetInfo ( GuiInfo info, object value ) { this.Dispatch( () => {
         Log( $"Set {info} = {value}" );
         try {
            string txt = value?.ToString();
            switch ( info ) {
               case GuiInfo.VISIBILITY :
                  RefreshInfo();
                  App.CheckStatusAsync( false );
                  Show();
                  break;
               case GuiInfo.APP_UPDATE :
               case GuiInfo.MOD_LIST :
                  break;
               default:
                  SharedGui.SetInfo( info, value );
                  break;
            }
         } catch ( Exception ex ) { Log( ex ); }
      } ); }

      private void RefreshInfo () { try {
         Log( $"Refreshing {Mode}" );
         if ( Mode == "log" )
            return;
         string txt = $"Modnix {SharedGui.AppVer}\n";
         if ( Mode == "launch" ) {
            txt += "Installed at " + App.ModGuiExe + "\n\nUse it to resetup or restore.";
            EnableLaunch();
         } else { // Mode == "setup"
            ButtonAction.IsEnabled = ! SharedGui.IsGameRunning;
            txt += "\nPhoenix Point\n";
            if ( SharedGui.AppState == "no_game" ) {
               txt += "Not Found";
               AccessAction.Text = "Manual _Setup";
               ButtonAction.IsEnabled = true;
            } else {
               txt += SharedGui.GamePath ?? "Detecting...";
               AccessAction.Text = "_Setup";
               ButtonAction.IsEnabled = SharedGui.GamePath != null;
            }
         }
         TextMessage.Text = txt;
      } catch ( Exception ex ) { Log( ex ); } }

      private void EnableLaunch () {
         AccessAction.Text = "_Launch";
         ButtonAction.IsEnabled = File.Exists( App.ModGuiExe );
      }

      private void ButtonAction_Click ( object sender, RoutedEventArgs e ) { try {
         Log( $"\"{Mode}\" initiated" );
         if ( e.Source is Button btn ) btn.Focus();
         if ( Mode == "setup" ) {
            if ( SharedGui.AppState == "no_game" ) {
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

      public void Prompt ( AppAction action, PromptFlag flags = PromptFlag.NONE, Exception ex = null ) { this.Dispatch( () => { try {
         Log( $"Prompt {action} {flags}" );
         if ( SharedGui.AppState == "modnix" || SharedGui.AppState == "both" )
            EnableLaunch();
         SharedGui.Prompt( action, flags, ex, () => {
            AppControl.Explore( App.ModGuiExe );
            Close();
         } );
      } catch ( Exception err ) { Log( err ); } } ); }

      public void Log ( object message ) {
         string time = DateTime.Now.ToString( "hh:mm:ss.ffff ", InvariantCulture );
         string line = $"{time} {message}\n";
         Console.Write( line );
         this.Dispatch( () => { try {
            if ( Mode == "log" ) {
               TextMessage.AppendText( line );
               TextMessage.ScrollToEnd();
            } else
               LogContent += line;
         } catch ( Exception ex ) { Console.WriteLine( ex ); } } );
      }

   }
}
