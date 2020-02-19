using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix.MainGUI {
   internal class ModLoaderBridge {
      private AppControl App;

      public ModLoaderBridge ( AppControl app ) => App = app;

      internal object LoadModList () {
         ModLoader.Setup();
         ModLoader.BuildModList();
         return ModLoader.AllMods.Select( e => new GridModItem(){ Mod = e } );
      }
   }

   internal class GridModItem : ModInfo {
      internal ModEntry Mod;
      public override string Name => Mod?.Metadata?.Name?.ToString();
      public override string Version => Mod?.Metadata?.Version;
      public override string Author => Mod?.Metadata?.Author?.ToString();
      public override string Path => Mod?.Metadata?.Dlls?[0]?.Path;
      public override string Type => "PPML";
      public override string ToString () => Mod?.ToString();
   }
}
