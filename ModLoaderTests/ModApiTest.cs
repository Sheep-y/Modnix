using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Collections.Generic;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix.Tests {

   [TestClass()]
   public class ModApiTest {

      private static ModEntry ModA = new ModEntry( "//A", new ModMeta () { Id = "Test.A", Version = new Version( 1, 0, 0, 0 ) } );
      private static ModEntry ModB = new ModEntry( "//B/b", new ModMeta () { Id = "Test.B", Version = new Version( 1, 2, 3, 4 ) } );
      private static ModEntry ModC = new ModEntry( new ModMeta () { Id = "Test.C", Version = null } );
      private static Version ZeroVersion = new Version( 0, 0, 0, 0 );

      [ClassInitializeAttribute] public static void TestInitialize ( TestContext _ ) {
         ModLoader.Setup();
         ModScanner.AllMods.Clear();
         ModScanner.AllMods.Add( ModA );
         ModScanner.AllMods.Add( ModB );
         ModScanner.AllMods.Add( ModC );
         typeof( ModScanner ).GetMethod( "ResolveMods", NonPublic | Static ).Invoke( null, new object[0] );
      }

      [TestMethod()] public void ContextTest () {
         Assert.AreEqual( 3, ModScanner.EnabledMods.Count, "mod count" );
      }

      [TestMethod()] public void VersionTest () {
         Assert.AreEqual( ModA.Metadata.Version, ModA.ModAPI( "version", null ), "A null" );
         Assert.AreEqual( ModA.Metadata.Version, ModA.ModAPI( "version", "" ), "A empty" );
         Assert.AreEqual( ModA.Metadata.Version, ModA.ModAPI( "version", " " ), "A blank" );

         Assert.AreEqual( ModLoader.LoaderVersion, ModA.ModAPI( "version", "Modnix" ), "modnix" );
         Assert.AreEqual( ModLoader.LoaderVersion, ModA.ModAPI( "version", "loader" ), "loader" );
         Assert.AreEqual( ModLoader.GameVersion, ModA.ModAPI( "version", "Phoenix Point" ), "pp" );
         Assert.AreEqual( ModLoader.GameVersion, ModA.ModAPI( "version", "game" ), "game" );

         Assert.AreEqual( ModB.Metadata.Version, ModA.ModAPI( "version", "Test.B" ), "Test.B" );
         Assert.AreEqual( ZeroVersion, ModA.ModAPI( "version", "test.c" ), "test.c" );
      }

      [TestMethod()] public void PathTest () {
         Assert.AreEqual( ModA.Path, ModA.ModAPI( "path", null ), "A null" );
         Assert.AreEqual( ModA.Path, ModA.ModAPI( "path", "" ), "A empty" );
         Assert.AreEqual( ModA.Path, ModA.ModAPI( "path", " " ), "A blank" );

         Assert.AreEqual( ModLoader.ModDirectory, ModA.ModAPI( "path", "mods_root" ), "root" );

         Assert.AreEqual( ModB.Path, ModA.ModAPI( "path", "Test.B" ), "Test.B" );
         Assert.AreEqual( null, ModA.ModAPI( "path", "test.c" ), "test.c" );
      }

      [TestMethod()] public void ModInfoTest () {
         Assert.AreEqual( ModA.Metadata.Id, ( ModA.ModAPI( "mod_info", null ) as ModMeta ).Id, "A null" );
         Assert.AreEqual( ModA.Metadata.Id, ( ModA.ModAPI( "mod_info", "" ) as ModMeta ).Id, "A empty" );
         Assert.AreEqual( ModA.Metadata.Id, ( ModA.ModAPI( "mod_info", " " ) as ModMeta ).Id, "A blank" );
         Assert.AreEqual( ModB.Metadata.Id, ( ModA.ModAPI( "mod_info", "Test.B" ) as ModMeta ).Id, "Test.B" );
         Assert.AreEqual( ModC.Metadata.Id, ( ModA.ModAPI( "mod_info", "test.c" ) as ModMeta ).Id, "test.c" );
      }

      [TestMethod()] public void ModListTest () {
         var list = (IEnumerable<string>) ModA.ModAPI( "mod_list", null );
         Assert.AreEqual( 3, list.Count(), "list count" );
         Assert.IsTrue( list.Contains( ModA.Metadata.Id ), "A" );
         Assert.IsTrue( list.Contains( ModB.Metadata.Id ), "B" );
         Assert.IsTrue( list.Contains( ModC.Metadata.Id ), "C" );

         list = (IEnumerable<string>) ModA.ModAPI( "mod_list", "test" );
         Assert.AreEqual( 3, list.Count(), "test list count" );

         list = (IEnumerable<string>) ModA.ModAPI( "mod_list", "none" );
         Assert.AreEqual( 0, list.Count(), "empty list count" );
      }

      [TestMethod()] public void ModExtTest () {
         Assert.AreEqual( false, ModA.ModAPI( "reg_action" , null ), "null action" );
         Assert.AreEqual( false, ModA.ModAPI( "reg_action" , ""   ), "empty action" );
         Assert.AreEqual( false, ModA.ModAPI( "reg_action" , " "  ), "blank action" );
         Assert.AreEqual( false, ModA.ModAPI( "reg_handler", "mod_info"  ), "reg_action mod_info" );
         Assert.AreEqual( false, ModA.ModAPI( "reg_action" , "mod_info"  ), "reg_action mod_info" );
         Assert.AreEqual( false, ModA.ModAPI( "unreg_action", "A" ), "unreg_action A (0)" );

         Assert.AreEqual( true, ModA.ModAPI( "reg_action" , "A" ), "reg_action A" );
         Assert.AreEqual( true, ModA.ModAPI( "reg_handler", (Func<object,string>) A_Ext ), "reg_handler A" );
         Assert.AreEqual( "BA", ModB.ModAPI( "a", "B" ), "call api A" );
         Assert.AreEqual( false, ModA.ModAPI( "reg_handler", (Func<object,string>) A_Ext ), "reg_handler A (2)" );

         Assert.AreEqual( false, ModB.ModAPI( "reg_action", "A" ), "reg_action A (2)" );
         Assert.AreEqual( false, ModB.ModAPI( "reg_handler", (Func<object,string>) A_Ext ), "reg_handler A (3)" );
         Assert.AreEqual( false, ModB.ModAPI( "unreg_action", "A" ), "unreg_action A (1)" );

         Assert.AreEqual( true, ModA.ModAPI( "unreg_action", "a" ), "unreg_action A (2)" );
         Assert.AreEqual( true, ModB.ModAPI( "reg_action", "A" ), "reg_action A (3)" );
         Assert.IsNull( ModB.ModAPI( "A", "B" ), "call api A (2)" );
      }

      private static string A_Ext ( object e ) => e.ToString() + "A";
   }
}
