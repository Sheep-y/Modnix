using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix.MainGUI {
   internal class ModLoaderBridge {

      internal static object LoadModList () {
         ModLoader.Setup();
         ModLoader.BuildModList();
         return ModLoader.AllMods.Select( e => new GridModItem(){ Mod = e } );
      }
   }

   internal class GridModItem {
      internal ModEntry Mod;
      public string Name => Mod?.Metadata?.Name?.ToString();
      public string Version => Mod?.Metadata?.Version;
      public string Author => Mod?.Metadata?.Author?.ToString();
      public string Type => "PPML";
   }
}
