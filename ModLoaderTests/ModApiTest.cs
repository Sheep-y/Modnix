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
         Func<object,string> ExtA = ( e )=> e.ToString() + "A";
         Func<string,object,string> ExtB = ( t, e )=> t + e.ToString() + "B";
         Func<string> ExtC = () => "";

         Assert.AreEqual( false, ModA.ModAPI( "api_add" , null ), "no name null action" );
         Assert.AreEqual( true , ModA.ModAPI( "api_add A.A", ExtA ), "A.A => ExtA" );
         Assert.AreEqual( true , ModA.ModAPI( "api_add  A.B", ExtB ), "A.B => ExtB" );

         Assert.AreEqual( false, ModA.ModAPI( "api_add AAA", ExtA ), "no dot" );
         Assert.AreEqual( false, ModA.ModAPI( "api_add .A", ExtA ), "too short" );
         Assert.AreEqual( false, ModA.ModAPI( "api_add A.A", ExtA ), "duplucate reg" );
         Assert.AreEqual( false, ModA.ModAPI( "api_add A.C", null ), "null action" );
         Assert.AreEqual( false, ModA.ModAPI( "api_add A.C", ExtC ), "Invalid action" );

         Assert.AreEqual( "0A", ModB.ModAPI( "a.A", "0" ), "call api a.A" );
         Assert.AreEqual( "c0B", ModB.ModAPI( "A.b c", "0" ), "call api A.b" );
         Assert.AreEqual( false, ModB.ModAPI( "api_remove A.B" ), "api_remove non-owner" );
         Assert.AreEqual( true , ModA.ModAPI( " api_remove   a.b " ), "api_remove owner" );
         Assert.AreEqual( null , ModA.ModAPI( "A.b c", "0" ), "call after api_remove" );

         Assert.AreEqual( true , ModB.ModAPI( " api_add   A.B ", ExtB ), "A.B => ExtB (ModB)" );
         Assert.AreEqual( "c0B", ModA.ModAPI( "A.b c", "0" ), "call api A.b (Mod B)" );
      }
   }
}
