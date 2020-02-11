using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace Sheepy.Modnix.MainGUI {
   internal static class SharedGui {
      internal static void Prompt ( string parts, Exception ex, Action OnRestart ) {
         string txt;
         if ( parts.StartsWith( "setup_ok" ) ) {
            txt = $"Setup success.\nCheck status after every game patch.\n\nMod folder:\nMy Documents\\{AppControl.MOD_PATH}\n";
            if ( parts.Contains( ",mod_moved" ) )
               txt += "\nMods moved to new mod folder.";
            if ( parts.Contains( ",self_copy" ) )
               txt += "\nModnix installed to mod folder.";
            if ( parts.Contains( ",ppml" ) )
               txt += "\nPPML renamed to prevent accidents.";
            if ( parts.Contains( ",self_copy" ) ) {
               txt += "\n\nThis setup file may be deleted.\nStarting Modnix now.";
               if ( MessageBox.Show( txt, "Success", MessageBoxButton.OKCancel, MessageBoxImage.Information ) == MessageBoxResult.OK )
                  OnRestart();
            } else {
               MessageBox.Show( txt, "Success", MessageBoxButton.OK, MessageBoxImage.Information );
            }
         } else if ( parts.StartsWith( "restore_ok" ) ) {
            MessageBox.Show( "Uninstall successful.", "Success" );
         } else if ( parts.StartsWith( "error" ) ) {
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
