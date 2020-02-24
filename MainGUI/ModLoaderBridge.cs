using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Documents;

namespace Sheepy.Modnix.MainGUI {
   internal class ModLoaderBridge {
      private AppControl App;

      public ModLoaderBridge ( AppControl app ) => App = app;

      internal object LoadModList () {
         if ( ModLoader.NeedSetup ) {
            ModLoader.SetLog( new GUILogger( App.GUI ) );
            ModLoader.Setup();
         }
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

      public override void BuildDesc ( FlowDocument doc ) {
         new TextRange( doc.ContentStart, doc.ContentEnd ).Text =
            $"{Name}\rVersion {Version}\rType {Type}\n{Mod?.Metadata?.Description}\nAuthor\t{(Author)}";
      }

      public override string Path => Mod?.Metadata?.Dlls?[0]?.Path;
      public override string Type { get {
         bool? hasInit = Mod?.Metadata?.Dlls?.Any( e => e?.Methods?.ContainsKey( "Init" ) ?? false );
         if ( hasInit == null ) return "Data";
         return hasInit.GetValueOrDefault() ? "PPML" : "DLL";
      } }
      public override string ToString () => Mod?.ToString();
   }

   internal class GUILogger : Logger {
      private readonly IAppGui GUI;
      public GUILogger ( IAppGui gUI ) => GUI = gUI;
      public override void Clear () { }
      public override void Flush () { }
      protected override void _Log ( LogEntry entry ) => GUI.Log( EntryToString( entry ) );
   }
}
