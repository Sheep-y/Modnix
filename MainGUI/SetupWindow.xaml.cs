using System;
using System.Collections.Generic;
using System.Diagnostics;
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
      private string AppVer, AppState, GamePath = null;
      private bool IsGameRunning;
      private string Mode = "log"; // launch, setup, log
      private string LogContent;

      public SetupWindow ( string mode ) { try {
         Mode = mode;
         InitializeComponent();
         RefreshInfo();
         App.CheckStatusAsync( false );
      } catch ( Exception ex ) { Console.WriteLine( ex ); } }

      private void Window_Activated ( object sender, EventArgs e ) {
         SetInfo( GuiInfo.GAME_RUNNING, AppControl.IsGameRunning() );
      }

      public void SetInfo ( GuiInfo info, object value ) { this.Dispatch( () => {
         Log( $"Set {info} = {value}" );
         try {
            string txt = value?.ToString();
            switch ( info ) {
               case GuiInfo.VISIBILITY : Show(); break;
               case GuiInfo.APP_VER : AppVer = txt; break;
               case GuiInfo.APP_STATE : AppState = txt; break;
               case GuiInfo.GAME_RUNNING : IsGameRunning = (bool) value; break;
               case GuiInfo.GAME_PATH : GamePath = txt; ButtonAction.Focus(); break;
               case GuiInfo.GAME_VER :
               case GuiInfo.APP_UPDATE :
               case GuiInfo.MOD_LIST :
                  break;
               default: Log( $"Unknown info {info}" ); return;
            }
            RefreshInfo();
         } catch ( Exception ex ) { Log( ex ); }
      } ); }

      private void RefreshInfo () { try {
         Log( $"Refreshing {Mode}" );
         if ( Mode == "log" )
            return;
         string txt = $"Modnix {AppVer}\n";
         if ( Mode == "launch" ) {
            txt += "Installed at " + App.ModGuiExe + "\n\nUse it to resetup or restore.";
            EnableLaunch();
         } else { // Mode == "setup"
            ButtonAction.IsEnabled = ! IsGameRunning;
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

      public void Prompt ( PromptFlag parts, Exception ex = null ) {
         this.Dispatch( () => { try {
            Log( $"Prompt {parts}" );
            if ( AppState == "modnix" || AppState == "both" )
               EnableLaunch();
            SharedGui.Prompt( parts, ex, () => {
               AppControl.Explore( App.ModGuiExe );
               Close();
            } );
         } catch ( Exception err ) { Log( err ); } } );
      }

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
