using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix {
   public class Mod {
      public bool Disabled { get; set; }
      public bool NoPingback { get; set; }
      public ModMeta Metadata { get; set; }
   }

   public class ModMeta {
      public string Id;
      public string Version;
      public string Phase;

      public object Name;
      public string[] Langs;
      public object Description;
      public object Author;
      public object Url;
      public string Pingback;
      public object Contact;

      public AppVerMeta AppVer;
      public AppVerMeta[] Requires;
      public AppVerMeta[] Conflicts;
      public AppVerMeta[] LoadsAfter;
      public AppVerMeta[] LoadsBefore;

      public string[] Mods;
      public DllMeta[] Dlls;
   }

   public class AppVerMeta {
      public string Id;
      public string Min;
      public string Max;
   }

   public class DllMeta {
      public string Path;
      public string Method;
   }
}
