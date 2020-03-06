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

   internal enum ModActionType { NONE, ENABLE, DISABLE, DELETE_DIR, DELETE_FILE, DELETE_SETTINGS }
   internal enum ModQueryType { NONE, IS_FOLDER, IS_CHILD, HAS_SETTINGS }

   [Flags]
   public enum PromptFlag { NONE, 
      ERROR = 1,
      SETUP = 2, REVERT = 4, ADD_MOD = 8, DEL_MOD = 16,
      SETUP_MOD_MOVED = 32, SETUP_SELF_COPY = 64, SETUP_PPML = 128,
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

      internal static void Prompt ( PromptFlag parts, Exception ex, Action OnRestart ) { try {
         var action = "Action";
         if ( parts.Has( PromptFlag.SETUP ) ) action = "Setup";
         else if ( parts.Has( PromptFlag.REVERT ) ) action = "Revert";
         else if ( parts.Has( PromptFlag.ADD_MOD ) ) action = "Add Mod";
         else if ( parts.Has( PromptFlag.DEL_MOD ) ) action = "Delete Mod";

         string txt;
         if ( parts.Has( PromptFlag.ERROR ) ) {
            txt = string.Format( "{0} failed. See log for details.", action );
            if ( ex != null ) txt += "\r\rError: " + ex;
            MessageBox.Show(  txt , "Error", MessageBoxButton.OK, MessageBoxImage.Error );
            return;
         }

         if ( parts.Has( PromptFlag.SETUP ) ) {
            txt = $"Setup success.\nPlease re-setup after every game patch.\n\nMod folder:\nMy Documents\\{AppControl.MOD_PATH}\n";
            if ( parts.Has( PromptFlag.SETUP_MOD_MOVED ) )
               txt += "\nMods moved to new mod folder.";
            if ( parts.Has( PromptFlag.SETUP_SELF_COPY ) )
               txt += "\nModnix installed to mod folder.";
            if ( parts.Has( PromptFlag.SETUP_PPML ) )
               txt += "\nPPML renamed to prevent accidents.";
            if ( parts.Has( PromptFlag.SETUP_SELF_COPY ) ) {
               txt += "\n\nThis setup file may be deleted.\nShowing Modnix location now.\nYou may pin it to Start or send it to Desktop.";
               if ( MessageBox.Show( txt, "Success", MessageBoxButton.OKCancel, MessageBoxImage.Information ) == MessageBoxResult.OK )
                  OnRestart();
            } else {
               MessageBox.Show( txt, "Success", MessageBoxButton.OK, MessageBoxImage.Information );
            }
         } else if ( parts.Has( PromptFlag.REVERT ) ) {
            MessageBox.Show( "Revert success.\nGame is now Modnix-free.", "Success" );
         } else {
            MessageBox.Show( string.Format( "{0} success.", action ), "Success" );
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
