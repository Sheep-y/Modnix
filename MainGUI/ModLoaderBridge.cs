using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix.MainGUI {
   internal class ModLoaderBridge {
      private AppControl App;

      public ModLoaderBridge ( AppControl app ) => App = app;

      internal object LoadModList () {
         ModLoader.Setup();
         App.Log( "Building mod list" );
         ModLoader.BuildModList();
         return ModLoader.AllMods.Select( e => new GridModItem(){ Mod = e } );
      }

      internal void Delete ( ModInfo mod ) {
         App.Log( $"Deleting Mod {mod.Name}" );
         string path = mod.Path;
         App.Log( $"Deleting {path}" );
         if ( ! File.Exists( path ) ) throw new FileNotFoundException( path );
         File.Delete( path );
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
