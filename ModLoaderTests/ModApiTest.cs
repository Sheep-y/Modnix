using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix.Tests {

   [TestClass()]
   public class ModApiTest {

      private static ModEntry ModA = new ModEntry( "//A", new ModMeta () { Id = "Test.A", Version = new Version( 1, 0, 0, 0 ) } );
      private static ModEntry ModB = new ModEntry( "//B/b", new ModMeta () { Id = "Test.B", Version = new Version( 1, 2, 3, 4 ) } );
      private static ModEntry ModC = new ModEntry( "C:\\C", new ModMeta () { Id = "Test.C", Version = null } );
      private static Version ZeroVersion = new Version( 0, 0, 0, 0 );

      [ClassInitializeAttribute] public static void TestInitialize ( TestContext _ ) {
         ModLoader.Setup();
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
         Assert.AreEqual( ZeroVersion, ModA.ModAPI( "version", "test.c" ), "test." );
      }
   }
}
