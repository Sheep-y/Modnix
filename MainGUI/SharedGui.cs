using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using static System.StringComparison;

namespace Sheepy.Modnix.MainGUI {

   internal abstract class ModInfo {
      public abstract string Id { get; }
      public abstract float Order { get; }
      public abstract string Name { get; }
      public abstract string Version { get; }
      public abstract string Author { get; }
      public abstract string Status { get; }
      public abstract string Type { get; }
      public abstract DateTime Installed { get; }
      public abstract bool Is ( ModQuery prop );
      public abstract void Do ( AppAction action, object param = null );
      public abstract void BuildDocument ( ModDoc type, FlowDocument doc );
      public abstract string Path { get; }
   }

   internal enum ModQuery { NONE, ENABLED, FORCE_DISABLED, EDITING, WARNING, IS_FOLDER, IS_CHILD,
      HAS_CONFIG, HAS_CONFIG_FILE, HAS_README, HAS_CHANGELOG, HAS_LICENSE }
   internal enum ModDoc { NONE, SUMMARY, INFO, CONFIG, README, CHANGELOG, LICENSE }

   public enum AppAction { NONE,
      SETUP, REVERT, LAUNCH_GAME, ADD_MOD, DEL_MOD, ENABLE_MOD, DISABLE_MOD,
      EDIT_CONFIG, SAVE_CONFIG, RESET_CONFIG, DELETE_CONFIG, SET_CONFIG_PROFILE }

   [Flags]
   public enum PromptFlag { NONE, ERROR = 1,
      SETUP_MOD_MOVED = 2, SETUP_SELF_COPY = 4, SETUP_PPML = 8,
   }

   internal static class SharedGui {
      internal static string _AppVer, _AppState, _GamePath, _GameVer;

      internal static string AppVer { get => _AppVer; set {
         if ( _AppVer == value ) return;
         _AppVer = value;
         VersionChanged.Invoke();
      } }

      internal static string AppState { get => _AppState; set {
         if ( _AppState == value ) return;
         _AppState = value;
         AppStateChanged.Invoke();
      } }

      internal static string GamePath { get => _GamePath; set {
         if ( _GamePath == value ) return;
         _GamePath = value;
         GamePathChanged.Invoke();
      } }

      internal static string GameVer { get => _GameVer; set {
         if ( _GameVer == value ) return;
         _GameVer = value;
         VersionChanged.Invoke();
      } }

      internal static bool _IsGameRunning;
      internal static bool IsGameRunning { get => _IsGameRunning; set {
         if ( _IsGameRunning == value ) return;
         _IsGameRunning = value;
         GameRunningChanged.Invoke();
      } }

      internal static bool _IsAppWorking;
      internal static bool IsAppWorking { get => _IsAppWorking; set {
         if ( _IsAppWorking == value ) return;
         _IsAppWorking = value;
         AppWorkingChanged.Invoke();
      } }

      internal static bool IsInjected => AppState == "modnix" || AppState == "both";
      internal static bool IsGameFound => GamePath != null;
      internal static bool CanModify => AppState != null && ! IsGameRunning && ! IsAppWorking;

      internal static event Action AppStateChanged;
      internal static event Action GamePathChanged;
      internal static event Action VersionChanged;
      internal static event Action AppWorkingChanged;
      internal static event Action GameRunningChanged;

      internal static void SetInfo ( GuiInfo info, object value ) {
         string txt = value?.ToString();
         switch ( info ) {
            case GuiInfo.APP_VER : AppVer = txt; break;
            case GuiInfo.APP_STATE : AppState = txt; break;
            case GuiInfo.GAME_RUNNING : IsGameRunning = (bool) value; break;
            case GuiInfo.GAME_PATH : GamePath = txt; break;
            case GuiInfo.GAME_VER : GameVer  = txt; break;
            default :
               throw new InvalidOperationException( $"Unknown info {info}" );
         }
      }

      internal static bool BrowseGame () {
         var app = AppControl.Instance;
         app.Log( "Prompting for game path" );
         var dialog = new Microsoft.Win32.OpenFileDialog{
            DefaultExt = "PhoenixPointWin64.exe",
            Filter = "PhoenixPointWin64.exe|PhoenixPointWin64.exe",
            Title = "Find Phoenix Point",
         };
         if ( ! dialog.ShowDialog().GetValueOrDefault() ) return false;
         string exe = dialog.FileName, dir = Path.GetDirectoryName( exe );
         if ( ! app.IsGamePath( dir ) ) {
            MessageBox.Show( $"Game not found at {dir}", "Error", MessageBoxButton.OK, MessageBoxImage.Error );
            return false;
         }
         app.SetGamePath( dir );
         AppState = null;
         return true;
      }

      internal static void Prompt ( AppAction action, PromptFlag flags, Exception ex, Action OnRestart ) { try {
         string actionTxt;
         switch ( action ) {
            case AppAction.SETUP : actionTxt = "Setup"; break;
            case AppAction.REVERT : actionTxt = "Revert"; break;
            case AppAction.ADD_MOD : actionTxt = "Add Mod"; break;
            case AppAction.DEL_MOD : actionTxt = "Delete Mod"; break;
            case AppAction.DELETE_CONFIG : actionTxt = "Delete config"; break;
            case AppAction.SAVE_CONFIG : actionTxt = "Save config"; break;
            default : actionTxt = "Action"; break;
         }

         string txt;
         if ( ex != null || flags.Has( PromptFlag.ERROR ) ) {
            AppControl.Instance.Log( ex );
            txt = string.Format( "{0} failed.", actionTxt );
            if ( ex != null ) txt += "\r\rError: " + ex;
            else txt += "\r\rSee log for details.";
            MessageBox.Show(  txt , "Error", MessageBoxButton.OK, MessageBoxImage.Error );
            return;
         }

         if ( action == AppAction.SETUP ) {
            txt = $"Setup success.";
            if ( flags.Has( PromptFlag.SETUP_SELF_COPY ) )
               txt += "\nModnix installed to mod folder.";
            else
               txt += "\nPlease re-setup after every game patch.";
            if ( flags.Has( PromptFlag.SETUP_MOD_MOVED ) )
               txt += "\nMods moved to new mod folder.";
            if ( flags.Has( PromptFlag.SETUP_PPML ) )
               txt += "\nPPML renamed to prevent accidents.";
            if ( flags.Has( PromptFlag.SETUP_SELF_COPY ) ) {
               txt += "\n\nThis setup file may be deleted.\nRe-setup can be done in Modnix,\nplease re-setup after every game patch."+
                      "\n\nShowing Modnix location now.\nRight click it to pin to Start or send to Desktop.";
               if ( MessageBox.Show( txt, "Success", MessageBoxButton.OKCancel, MessageBoxImage.Information ) == MessageBoxResult.OK )
                  OnRestart();
            } else {
               MessageBox.Show( txt, "Success", MessageBoxButton.OK, MessageBoxImage.Information );
            }
         } else if ( action == AppAction.REVERT ) {
            MessageBox.Show( "Revert success.\nGame is now Modnix-free.", "Success" );
         } else {
            MessageBox.Show( string.Format( "{0} success.", actionTxt ), "Success" );
         }
      } finally {
         IsAppWorking = false;
      } }

      internal static void Dispatch ( this Window win, Action task ) {
         if ( win.Dispatcher.CheckAccess() )
            task();
         else
            win.Dispatcher.InvokeAsync( task );
      }

      internal static Action Dispatcher ( this Window win, Action task ) => () => win.Dispatch( task );

      internal static bool Has ( this PromptFlag haysack, PromptFlag needle ) => ( haysack & needle ) == needle;
   }

   public static class WpfHelper {
      public static string Lf2Cr ( string text ) => text.Replace( "\r", "" ).Replace( '\n', '\r' );
      public static StringBuilder Lf2Cr ( StringBuilder text ) => text.Replace( "\r", "" ).Replace( '\n', '\r' );

      public static TextRange TextRange ( this FlowDocument doc ) => new TextRange( doc.ContentStart, doc.ContentEnd );

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
