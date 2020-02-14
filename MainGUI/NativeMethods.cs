using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Sheepy.Modnix.MainGUI {

   public static class NativeMethods {
      [DllImport( "USER32.DLL" )] 
      internal static extern bool SetForegroundWindow ( IntPtr hWnd );
   }
}
