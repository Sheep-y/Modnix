using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix.Tests {

   [TestClass()]
   public class ModLoaderTest {

      [ClassInitializeAttribute] public static void TestInitialize ( TestContext _ = null ) {
         ModLoader.Setup();
         ModScanner.AllMods.Clear();
         ModScanner.EnabledMods.Clear();
      }

      [TestCleanup] public void TestCleanup () => TestInitialize();

      private static void ResolveMods () => 
         typeof( ModScanner ).GetMethod( "ResolveMods", NonPublic | Static ).Invoke( null, new object[0] );

      [TestMethod()] public void NameMatchTest () {
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

      [TestMethod()] public void DisabledModTest () {
         ModScanner.AllMods.Add( new ModEntry( new ModMeta{ Id = "A" } ) );
         ModScanner.AllMods.Add( new ModEntry( new ModMeta{ Id = "B" } ){ Disabled = true } );
         ResolveMods();
         Assert.AreEqual( 2, ModScanner.AllMods.Count );
         Assert.AreEqual( 1, ModScanner.EnabledMods.Count );
      }

      private static Version Ver ( string val ) => Version.Parse( val );

      [TestMethod()] public void DuplicateTest () {
         var AlphaMod  = new ModEntry( new ModMeta{ Id = "dup~", Version = Ver( "1.2" ) } );
         var BetaMod   = new ModEntry( new ModMeta{ Id = "dup!", Version = Ver( "2.3" ) } );
         var GoldMod   = new ModEntry( new ModMeta{ Id = "dup#", Version = Ver( "4.5" ) } );
         var SilverMod = new ModEntry( new ModMeta{ Id = "dup$", Version = Ver( "3.4" ) } );

         var AllMods = ModScanner.AllMods;
         AllMods.Add( AlphaMod );
         AllMods.Add( BetaMod );
         AllMods.Add( GoldMod );
         AllMods.Add( SilverMod );
         ResolveMods();

         Assert.AreEqual( 4, AllMods.Count );
         Assert.IsTrue ( AlphaMod.Disabled, "Alpha" );
         Assert.IsTrue ( BetaMod.Disabled, "Beta" );
         Assert.IsTrue ( SilverMod.Disabled, "Silver" );
         Assert.IsFalse( GoldMod.Disabled, "Gold" );
         Assert.AreEqual( 1, ModScanner.EnabledMods.Count );
      }

      [TestMethod()] public void RequirementTest () {
         var ModnixMin = new ModEntry( new ModMeta{ Id = "ModnixMin", Requires = new AppVer[]{ new AppVer( "Modnix", Ver( "99.99" ) ) } }.Normalise() );
         var ModnixOk  = new ModEntry( new ModMeta{ Id = "ModnixOk" , Requires = new AppVer[]{ new AppVer( "Modnix", Ver( "0.75" ) ) } }.Normalise() );
         var ModnixMax = new ModEntry( new ModMeta{ Id = "ModnixMax", Requires = new AppVer[]{ new AppVer( "Modnix", max : Ver( "0.0" ) ) } }.Normalise() );
         var PPMin     = new ModEntry( new ModMeta{ Id = "PPMin", Requires = new AppVer[]{ new AppVer( "PhoenixPoint", Ver( "1.0.23456" ) ) } }.Normalise() );
         var PPOk      = new ModEntry( new ModMeta{ Id = "PPOk" , Requires = new AppVer[]{ new AppVer( "PhoenixPoint", Ver( "1.0.12345" ) ) } }.Normalise() );
         var PPMax     = new ModEntry( new ModMeta{ Id = "PPMax", Requires = new AppVer[]{ new AppVer( "Phoenix Point", max : Ver( "1.0.4321" ) ) } }.Normalise() );
         var PPMLMin   = new ModEntry( new ModMeta{ Id = "PPMLMin", Requires = new AppVer[]{ new AppVer( "ppml", Ver( "99.99" ) ) } }.Normalise() );
         var PPMLOk    = new ModEntry( new ModMeta{ Id = "PPMLOk" , Requires = new AppVer[]{ new AppVer( "PhoenixPointModLoader", Ver( "0.1" ) ) } }.Normalise() );
         var PPMLMax   = new ModEntry( new ModMeta{ Id = "PPMLMax", Requires = new AppVer[]{ new AppVer( "Phoenix Point Mod Loader", max : Ver( "0.0" ) ) } }.Normalise() );
         var MultiOK   = new ModEntry( new ModMeta{ Id = "MultiOK", Requires = new AppVer[]{ new AppVer( "ppml", Ver( "1.0" ) ), new AppVer( "ppml", max : Ver( "0.2" ) ) } }.Normalise() );
         var MultiFail = new ModEntry( new ModMeta{ Id = "MultiOK", Requires = new AppVer[]{ new AppVer( "ppml", Ver( "1.0" ) ), new AppVer( "ppml", max : Ver( "0.0" ) ) } }.Normalise() );
         var NonModnix = new ModEntry( new ModMeta{ Id = "NonModnix", Requires = new AppVer[]{ new AppVer( "NonModnix" ) } }.Normalise() );
         var Yes       = new ModEntry( new ModMeta{ Id = "NonModnix", Requires = new AppVer[]{ new AppVer( "ModnixOK" ) } }.Normalise() );
         var No        = new ModEntry( new ModMeta{ Id = "NonModnix", Requires = new AppVer[]{ new AppVer( "ModnixOK" ), new AppVer( "ModnixMax" ) } }.Normalise() );

         var AllMods = ModScanner.AllMods;
         AllMods.Add( Yes );
         AllMods.Add( No );
         AllMods.Add( ModnixMin );
         AllMods.Add( ModnixOk );
         AllMods.Add( ModnixMax );
         AllMods.Add( PPMin );
         AllMods.Add( PPOk );
         AllMods.Add( PPMax );
         AllMods.Add( PPMLMin );
         AllMods.Add( PPMLOk );
         AllMods.Add( PPMLMax );
         AllMods.Add( MultiOK );
         AllMods.Add( MultiFail );
         AllMods.Add( NonModnix );

         ModLoader.GameVersion = new Version( "1.0.12345" );
         ResolveMods();

         Assert.AreEqual( 14, AllMods.Count );
         Assert.IsTrue ( ModnixMin.Disabled, "ModnixMin" );
         Assert.IsFalse( ModnixOk.Disabled, "ModnixOk" );
         Assert.IsTrue ( ModnixMax.Disabled, "ModnixMax" );
         Assert.IsTrue ( PPMin.Disabled, "PPMin" );
         Assert.IsFalse( PPOk.Disabled, "PPOk" );
         Assert.IsTrue ( PPMax.Disabled, "PPMax" );
         Assert.IsTrue ( PPMLMin.Disabled, "PPMLMin" );
         Assert.IsFalse( PPMLOk.Disabled, "PPMLOk" );
         Assert.IsTrue ( PPMLMax.Disabled, "PPMLMax" );
         Assert.IsFalse( MultiOK.Disabled, "MultiOK" );
         Assert.IsTrue ( NonModnix.Disabled, "NonModnix" );
         Assert.IsFalse( Yes.Disabled, "Yes" );
         Assert.IsTrue ( No.Disabled, "No" );
         Assert.AreEqual( 5, ModScanner.EnabledMods.Count );
      }

      [TestMethod()] public void ConflictTest () {
         // A conflicts with B, B conflicts with C, D conflicts with A B D.
         // A disables B, B is skipped, D disables A and B and skip itself, leaving C and D
         var A = new ModEntry( new ModMeta{ Id = "A", Version = Ver( "1.2" ), Disables = new AppVer[]{ new AppVer( "B" ), new AppVer( "D", Ver( "4.5" ) ) } } );
         var B = new ModEntry( new ModMeta{ Id = "B", Version = Ver( "2.3" ), Disables = new AppVer[]{ new AppVer( "C" ) } } );
         var C = new ModEntry( new ModMeta{ Id = "C", Version = Ver( "4.5" ), Disables = new AppVer[]{ new AppVer( "D", max : Ver( "2.0" ) ) } } );
         var D = new ModEntry( new ModMeta{ Id = "D", Version = Ver( "3.4" ), Disables = new AppVer[]{ new AppVer( "A" ), new AppVer( "B" ),  new AppVer( "D" ) } } );

         var AllMods = ModScanner.AllMods;
         AllMods.Add( A );
         AllMods.Add( B );
         AllMods.Add( C );
         AllMods.Add( D );
         ResolveMods();

         Assert.AreEqual( 4, AllMods.Count );
         Assert.IsTrue ( A.Disabled, "A" );
         Assert.IsTrue ( B.Disabled, "B" );
         Assert.IsFalse( C.Disabled, "C" );
         Assert.IsFalse( D.Disabled, "D" );
         Assert.AreEqual( 2, ModScanner.EnabledMods.Count );
      }
   }
}
