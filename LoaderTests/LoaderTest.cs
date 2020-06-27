using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Security.Policy;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix.Tests {

   [TestClass()]
   public class ModLoaderTest {

      [ClassInitializeAttribute] public static void TestInitialize ( TestContext _ = null ) {
         ModLoader.Setup();
         ModLoader.AllMods.Clear();
         ModLoader.EnabledMods.Clear();
         ModLoader.ModsInPhase.Clear();
      }

      [TestCleanup] public void TestCleanup () => TestInitialize();

      private static void ResolveMods () => 
         typeof( ModResolver ).GetMethod( "Resolve", NonPublic | Static ).Invoke( null, new object[0] );

      private static void AddMod ( ModEntry mod ) {
         // Mods will be disabled without dll.
         var dll = mod.Metadata.Dlls = new DllMeta[1];
         dll[ 0 ] = new DllMeta{ Methods = new Dictionary<string, HashSet<string>>() };
         dll[ 0 ].Methods.Add( "MainMod", new HashSet<string>() );
         ModLoader.AllMods.Add( mod );
      }

      [TestMethod] public void NameMatchTest () {
         Assert.IsTrue( ModScanner.NameMatch( "abc", "ABC" ), "ignore case" );
         Assert.IsTrue( ModScanner.NameMatch( "ExampleMod", "Example Mod" ), "ignore space" );
         Assert.IsTrue( ModScanner.NameMatch( "Mod", "Mod - Copy" ), "One copy" );
         Assert.IsTrue( ModScanner.NameMatch( "Mod", "Mod - Copy - Copy" ), "Two copy" );
         Assert.IsTrue( ModScanner.NameMatch( "Mod", "Mod(123)" ), "Browser copy A" );
         Assert.IsTrue( ModScanner.NameMatch( "Mod", "Mod (123)" ), "Browser copy B" );
         Assert.IsTrue( ModScanner.NameMatch( "AIM-2-3", "AIM-0-1-1234568" ), "nexus id" );
         Assert.IsTrue( ModScanner.NameMatch( "ExampleMod", "ExampleMod-1-2-345 - Copy(123)" ), "Multiple copy" );
         Assert.IsTrue( ModScanner.NameMatch( "Mod.tar", "Mod" ), "Manual extraction" );

         Assert.IsFalse( ModScanner.NameMatch( "a", "A" ), "Too short" );
         Assert.IsFalse( ModScanner.NameMatch( "abc", "def" ), "diff name" );
         Assert.IsFalse( ModScanner.NameMatch( "DebugConsole", "EnableConsole" ), "console mods" );
      }

      [TestMethod] public void DisabledModTest () {
         AddMod( new ModEntry( new ModMeta{ Id = "A" } ) );
         AddMod( new ModEntry( new ModMeta{ Id = "B" } ){ Disabled = true } );
         ResolveMods();
         Assert.AreEqual( 2, ModLoader.AllMods.Count );
         Assert.AreEqual( 1, ModLoader.EnabledMods.Count );
      }

      private static Version Ver ( int val ) => new Version( val, 0, 0, 0 );
      private static Version Ver ( string val ) {
         Json.ParseVersion( val, out Version v );
         return v;
      }

      [TestMethod] public void DuplicateTest () {
         var AlphaMod  = new ModEntry( new ModMeta{ Id = "dup~", Version = Ver( 1 ) } );
         var BetaMod   = new ModEntry( new ModMeta{ Id = "dup!", Version = Ver( 2 ) } );
         var GoldMod   = new ModEntry( new ModMeta{ Id = "dup#", Version = Ver( 4 ) } );
         var SilverMod = new ModEntry( new ModMeta{ Id = "dup$", Version = Ver( 3 ) } );

         var AllMods = ModLoader.AllMods;
         AddMod( AlphaMod );
         AddMod( BetaMod );
         AddMod( GoldMod );
         AddMod( SilverMod );
         ResolveMods();

         Assert.AreEqual( 4, AllMods.Count );
         Assert.IsTrue ( AlphaMod.Disabled, "Alpha" );
         Assert.IsTrue ( BetaMod.Disabled, "Beta" );
         Assert.IsTrue ( SilverMod.Disabled, "Silver" );
         Assert.IsFalse( GoldMod.Disabled, "Gold" );
         Assert.AreEqual( 1, ModLoader.EnabledMods.Count );
      }

      [TestMethod] public void RequiresTest () {
         var LoaderMin = new ModEntry( new ModMeta{ Id = "LoaderMin", Requires = new AppVer[]{ new AppVer( "Loader", Ver( 99 ) ) } }.Normalise() );
         var LoaderOk  = new ModEntry( new ModMeta{ Id = "LoaderOk" , Requires = new AppVer[]{ new AppVer( "Loader", Ver( 1 ) ) } }.Normalise() );
         var LoaderMax = new ModEntry( new ModMeta{ Id = "LoaderMax", Requires = new AppVer[]{ new AppVer( "Loader", max : Ver( 0 ) ) } }.Normalise() );
         var PPMin     = new ModEntry( new ModMeta{ Id = "PPMin", Requires = new AppVer[]{ new AppVer( "PhoenixPoint", Ver( "1.0.23456" ) ) } }.Normalise() );
         var PPOk      = new ModEntry( new ModMeta{ Id = "PPOk" , Requires = new AppVer[]{ new AppVer( "PhoenixPoint", Ver( "1.0.12345" ) ) } }.Normalise() );
         var PPMax     = new ModEntry( new ModMeta{ Id = "PPMax", Requires = new AppVer[]{ new AppVer( "Phoenix Point", max : Ver( "1.0.4321" ) ) } }.Normalise() );
         var PPMLMin   = new ModEntry( new ModMeta{ Id = "PPMLMin", Requires = new AppVer[]{ new AppVer( "ppml", Ver( 99 ) ) } }.Normalise() );
         var PPMLOk    = new ModEntry( new ModMeta{ Id = "PPMLOk" , Requires = new AppVer[]{ new AppVer( "PhoenixPointModLoader", Ver( 0 ) ) } }.Normalise() );
         var PPMLMax   = new ModEntry( new ModMeta{ Id = "PPMLMax", Requires = new AppVer[]{ new AppVer( "Phoenix Point Mod Loader", max : Ver( 0 ) ) } }.Normalise() );
         var MultiOK   = new ModEntry( new ModMeta{ Id = "MultiOK", Requires = new AppVer[]{ new AppVer( "ppml", Ver( 1 ) ), new AppVer( "ppml", max : Ver( "0.2" ) ) } }.Normalise() );
         var MultiFail = new ModEntry( new ModMeta{ Id = "MultiOK", Requires = new AppVer[]{ new AppVer( "ppml", Ver( 1 ) ), new AppVer( "ppml", max : Ver( 0 ) ) } }.Normalise() );
         var Exclude   = new ModEntry( new ModMeta{ Id = "NonLoader", Requires = new AppVer[]{ new AppVer( "NonModnix" ) } }.Normalise() );
         var Yes       = new ModEntry( new ModMeta{ Id = "NonLoader", Requires = new AppVer[]{ new AppVer( "LoaderOK" ) } }.Normalise() );
         var No        = new ModEntry( new ModMeta{ Id = "NonLoader", Requires = new AppVer[]{ new AppVer( "LoaderOK" ), new AppVer( "LoaderMax" ) } }.Normalise() );

         var AllMods = ModLoader.AllMods;
         AddMod( Yes );
         AddMod( No );
         AddMod( LoaderMin );
         AddMod( LoaderOk );
         AddMod( LoaderMax );
         AddMod( PPMin );
         AddMod( PPOk );
         AddMod( PPMax );
         AddMod( PPMLMin );
         AddMod( PPMLOk );
         AddMod( PPMLMax );
         AddMod( MultiOK );
         AddMod( MultiFail );
         AddMod( Exclude );

         ModLoader.GameVersion = Ver( "1.0.12345" );
         ResolveMods();

         Assert.AreEqual( 14, AllMods.Count );
         Assert.IsTrue ( LoaderMin.Disabled, "ModnixMin" );
         Assert.IsFalse( LoaderOk.Disabled, "ModnixOk" );
         Assert.IsTrue ( LoaderMax.Disabled, "ModnixMax" );
         Assert.IsTrue ( PPMin.Disabled, "PPMin" );
         Assert.IsFalse( PPOk.Disabled, "PPOk" );
         Assert.IsTrue ( PPMax.Disabled, "PPMax" );
         Assert.IsTrue ( PPMLMin.Disabled, "PPMLMin" );
         Assert.IsFalse( PPMLOk.Disabled, "PPMLOk" );
         Assert.IsTrue ( PPMLMax.Disabled, "PPMLMax" );
         Assert.IsFalse( MultiOK.Disabled, "MultiOK" );
         Assert.IsTrue ( Exclude.Disabled, "NonModnix" );
         Assert.IsFalse( Yes.Disabled, "Yes" );
         Assert.IsTrue ( No.Disabled, "No" );
         Assert.AreEqual( 5, ModLoader.EnabledMods.Count );
      }

      [TestMethod] public void AvoidsTest () {
         // A conflicts with B, B conflicts with C, D conflicts with B D E.
         // A avoids B, B is skipped, D avoids E (non-exist) and skip itself, leaving C and D
         var A = new ModEntry( new ModMeta{ Id = "A", Version = Ver( 1 ), Avoids = new AppVer[]{ new AppVer( "B", max : Ver( 1 ) ), new AppVer( "B", Ver( 2 ) ), new AppVer( "D", Ver( 4 ) ) } } );
         var B = new ModEntry( new ModMeta{ Id = "B", Version = Ver( 2 ), Avoids = new AppVer[]{ new AppVer( "C" ) } } );
         var C = new ModEntry( new ModMeta{ Id = "C", Version = Ver( 4 ), Avoids = new AppVer[]{ new AppVer( "D", max : Ver( 2 ) ) } } );
         var D = new ModEntry( new ModMeta{ Id = "D", Version = Ver( 3 ), Avoids = new AppVer[]{ new AppVer( "E" ), new AppVer( "B" ),  new AppVer( "D" ) } } );

         var AllMods = ModLoader.AllMods;
         AddMod( A );
         AddMod( B );
         AddMod( C );
         AddMod( D );
         ResolveMods();

         Assert.AreEqual( 4, AllMods.Count );
         Assert.IsTrue ( A.Disabled, "A" );
         Assert.IsTrue ( B.Disabled, "B" );
         Assert.IsFalse( C.Disabled, "C" );
         Assert.IsFalse( D.Disabled, "D" );
         Assert.AreEqual( 2, ModLoader.EnabledMods.Count );
      }

      [TestMethod] public void LibraryTest () {
         // A requries B, C is disabled
         var A = new ModEntry( new ModMeta{ Id = "A", Requires = new AppVer[]{ new AppVer( "B" ) } } );
         var B = new ModEntry( new ModMeta{ Id = "B", Flags = new string[]{ "Library" } } );
         var C = new ModEntry( new ModMeta{ Id = "C", Flags = new string[]{ "Library" } } );

         var AllMods = ModLoader.AllMods;
         AddMod( A );
         AddMod( B );
         AddMod( C );
         ResolveMods();

         Assert.AreEqual( 3, AllMods.Count );
         Assert.IsFalse( A.Disabled, "A" );
         Assert.IsFalse( B.Disabled, "B" );
         Assert.IsTrue ( C.Disabled, "C" );
         Assert.AreEqual( 2, ModLoader.EnabledMods.Count );
      }

      [TestMethod] public void DisablesTest () {
         // A conflicts with B, B conflicts with C, D conflicts with A B D.
         // A disables B, B is skipped, D disables A and B and skip itself, leaving C and D
         var A = new ModEntry( new ModMeta{ Id = "A", Version = Ver( 1 ), Disables = new AppVer[]{ new AppVer( "B" ), new AppVer( "D", Ver( 4 ) ) } } );
         var B = new ModEntry( new ModMeta{ Id = "B", Version = Ver( 2 ), Disables = new AppVer[]{ new AppVer( "C" ) } } );
         var C = new ModEntry( new ModMeta{ Id = "C", Version = Ver( 4 ), Disables = new AppVer[]{ new AppVer( "D", max : Ver( 2 ) ) } } );
         var D = new ModEntry( new ModMeta{ Id = "D", Version = Ver( 3 ), Disables = new AppVer[]{ new AppVer( "A" ), new AppVer( "B" ),  new AppVer( "D" ) } } );

         var AllMods = ModLoader.AllMods;
         AddMod( A );
         AddMod( B );
         AddMod( C );
         AddMod( D );
         ResolveMods();

         Assert.AreEqual( 4, AllMods.Count );
         Assert.IsTrue ( A.Disabled, "A" );
         Assert.IsTrue ( B.Disabled, "B" );
         Assert.IsFalse( C.Disabled, "C" );
         Assert.IsFalse( D.Disabled, "D" );
         Assert.AreEqual( 2, ModLoader.EnabledMods.Count );
      }
   }
}
