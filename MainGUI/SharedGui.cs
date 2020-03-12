using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Documents;
using static System.StringComparison;

namespace Sheepy.Modnix.MainGUI {
   
   internal abstract class ModInfo {
      public abstract string Name { get; }
      public abstract string Version { get; }
      public abstract string Author { get; }
      public abstract string Status { get; }
      public abstract object Query ( ModQueryType prop );
      public abstract void BuildDesc ( FlowDocument doc );
      public abstract string Path { get; }
      public abstract string Type { get; }
   }

   internal enum ModQueryType { NONE, IS_FOLDER, IS_CHILD, HAS_CONFIG, HAS_CONFIG_FILE }

   public enum AppActionType { NONE,
      SETUP, REVERT, LAUNCH_GAME, ADD_MOD, DELETE_DIR, DELETE_FILE,
      ENABLE_MOD, DISABLE_MOD, DELETE_CONFIG, RESET_CONFIG }

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

      public static void SetInfo ( GuiInfo info, object value ) {
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

      internal static void Prompt ( AppActionType action, PromptFlag flags, Exception ex, Action OnRestart ) { try {
         string actionTxt;
         switch ( action ) {
            case AppActionType.SETUP : actionTxt = "Setup"; break;
            case AppActionType.REVERT : actionTxt = "Revert"; break;
            case AppActionType.ADD_MOD : actionTxt = "Add Mod"; break;
            case AppActionType.DELETE_DIR : actionTxt = "Delete Mod Folder"; break;
            case AppActionType.DELETE_FILE : actionTxt = "Delete Mod"; break;
            case AppActionType.DELETE_CONFIG : actionTxt = "Delete config"; break;
            case AppActionType.RESET_CONFIG : actionTxt = "Reset config"; break;
            default : actionTxt = "Action"; break;
         }

         string txt;
         if ( ex != null || flags.Has( PromptFlag.ERROR ) ) {
            txt = string.Format( "{0} failed.", actionTxt );
            if ( ex != null ) txt += "\r\rError: " + ex;
            else txt += "\r\rSee log for details.";
            MessageBox.Show(  txt , "Error", MessageBoxButton.OK, MessageBoxImage.Error );
            return;
         }

         if ( action == AppActionType.SETUP ) {
            txt = $"Setup success.\nPlease re-setup after every game patch.\n\nMod folder:\nMy Documents\\{AppControl.MOD_PATH}\n";
            if ( flags.Has( PromptFlag.SETUP_MOD_MOVED ) )
               txt += "\nMods moved to new mod folder.";
            if ( flags.Has( PromptFlag.SETUP_SELF_COPY ) )
               txt += "\nModnix installed to mod folder.";
            if ( flags.Has( PromptFlag.SETUP_PPML ) )
               txt += "\nPPML renamed to prevent accidents.";
            if ( flags.Has( PromptFlag.SETUP_SELF_COPY ) ) {
               txt += "\n\nThis setup file may be deleted.\nShowing Modnix location now.\nYou may pin it to Start or send it to Desktop.";
               if ( MessageBox.Show( txt, "Success", MessageBoxButton.OKCancel, MessageBoxImage.Information ) == MessageBoxResult.OK )
                  OnRestart();
            } else {
               MessageBox.Show( txt, "Success", MessageBoxButton.OK, MessageBoxImage.Information );
            }
         } else if ( action == AppActionType.REVERT ) {
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
            win.Dispatcher.Invoke( task );
      }

      internal static bool Has ( this PromptFlag haysack, PromptFlag needle ) => ( haysack & needle ) == needle;
   }
}
