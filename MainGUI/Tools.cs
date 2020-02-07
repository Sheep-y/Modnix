using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Sheepy.Modnix.MainGUI {
   // Contains external Api, so that they are not loaded until used
   public static class Tools {
      [DllImport( "USER32.DLL" )]
      public static extern bool SetForegroundWindow ( IntPtr hWnd );

      internal static string FixSlash ( this string path ) => path.Replace( '/', Path.DirectorySeparatorChar );

      public static void CreateShortcut ( string dir, string name, string linkTo, string desc = null ) {
         /*
         var link = (IWshShortcut) new WshShell().CreateShortcut( Path.Combine( dir, name + ".lnk" ) );
         link.Description = desc;
         //shortcut.IconLocation = null;
         link.TargetPath = linkTo;
         link.Save();
         */
      }
   }
}
