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
      internal static void Prompt ( PromptFlag parts, Exception ex, Action OnRestart ) {
         var action = "Action";
         if ( parts.Has( PromptFlag.SETUP ) ) action = "Setup";
         else if ( parts.Has( PromptFlag.REVERT ) ) action = "Revert";
         else if ( parts.Has( PromptFlag.ADD_MOD ) ) action = "Add Mod";
         else if ( parts.Has( PromptFlag.DEL_MOD ) ) action = "Delete Mod";

         string txt;
         if ( parts.Has( PromptFlag.ERROR ) ) {
            txt = string.Format( "{0} failed. See log for details.", action );
            if ( ex != null ) txt += "Error: " + ex;
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
      }

      internal static void Dispatch ( this Window win, Action task ) {
         if ( win.Dispatcher.CheckAccess() )
            task();
         else
            win.Dispatcher.Invoke( task );
      }

      internal static bool Has ( this PromptFlag haysack, PromptFlag needle ) => ( haysack | needle ) == needle;
   }
}
