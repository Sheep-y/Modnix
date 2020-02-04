using System;
using System.Runtime.InteropServices;

namespace Sheepy.Modnix.MainGUI {
   // Contains external Api, so that they are not loaded until used
   class ApiExternal {
      [DllImport( "USER32.DLL" )]
      public static extern bool SetForegroundWindow ( IntPtr hWnd );
   }
}
