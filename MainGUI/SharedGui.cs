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
      public abstract object Query ( string prop );
      public abstract void BuildDesc ( FlowDocument doc );
      public abstract string Path { get; }
      public abstract string Type { get; }
   }

   internal enum ModAction { NONE, DELETE, ENABLE, DISABLE }

   internal static class SharedGui {
      internal static void Prompt ( string parts, Exception ex, Action OnRestart ) {
         string txt;
         if ( parts.StartsWith( "setup_ok", InvariantCulture ) ) {
            txt = $"Setup success.\nPlease re-setup after every game patch.\n\nMod folder:\nMy Documents\\{AppControl.MOD_PATH}\n";
            if ( parts.Contains( ",mod_moved" ) )
               txt += "\nMods moved to new mod folder.";
            if ( parts.Contains( ",self_copy" ) )
               txt += "\nModnix installed to mod folder.";
            if ( parts.Contains( ",ppml" ) )
               txt += "\nPPML renamed to prevent accidents.";
            if ( parts.Contains( ",self_copy" ) ) {
               txt += "\n\nThis setup file may be deleted.\nShowing Modnix location now.\nYou may pin it to Start or send it to Desktop.";
               if ( MessageBox.Show( txt, "Success", MessageBoxButton.OKCancel, MessageBoxImage.Information ) == MessageBoxResult.OK )
                  OnRestart();
            } else {
               MessageBox.Show( txt, "Success", MessageBoxButton.OK, MessageBoxImage.Information );
            }
         } else if ( parts.StartsWith( "restore_ok", InvariantCulture ) ) {
            MessageBox.Show( "Revert successful.\nGame is now Modnix-free.", "Success" );
         } else if ( parts.StartsWith( "error", InvariantCulture ) ) {
            txt = "Action failed. See log for details.";
            if ( ex != null ) txt += "Error: " + ex;
            MessageBox.Show(  txt , "Error", MessageBoxButton.OK, MessageBoxImage.Error );
         }
      }

      internal static void Dispatch ( this Window win, Action task ) {
         if ( win.Dispatcher.CheckAccess() )
            task();
         else
            win.Dispatcher.Invoke( task );
      }
   }
}
