using System;
using System.Collections.Generic;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix.Tests {
   class LoaderTestHelper {
      public static void ResetLoader () {
         ModLoader.Setup();
         ModLoader.AllMods.Clear();
         ModLoader.EnabledMods.Clear();
         ModLoader.ModsInPhase.Clear();
      }

      public static void ResolveMods () => 
         typeof( ModResolver ).GetMethod( "Resolve", NonPublic | Static ).Invoke( null, new object[0] );

      public static void AddMod ( ModEntry mod ) {
         if ( mod.Metadata.Actions == null && mod.Metadata.Dlls == null ) {
            // Mods will be disabled without content.
            var dll = mod.Metadata.Dlls = new DllMeta[1];
            dll[ 0 ] = new DllMeta{ Methods = new Dictionary<string, HashSet<string>>() };
            dll[ 0 ].Methods.Add( "ActionMod", new HashSet<string>{ "Dummy" } );
         }
         ModLoader.AllMods.Add( mod );
      }

      public static Version Ver ( int val ) => new Version( val, 0, 0, 0 );
      public static Version Ver ( string val ) {
         Json.ParseVersion( val, out Version v );
         return v;
      }

      public static Dictionary<string, object> CreateDef ( params string[] keyValues ) {
         var result = new Dictionary<string, object>();
         for ( int i = 0 ; i < keyValues.Length ; i += 2 )
            result.Add( keyValues[i]?.ToString(), keyValues[ i+1 ] );
         return result;
      }
   }
}
