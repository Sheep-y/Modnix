using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using static System.Reflection.BindingFlags;
using static Sheepy.Modnix.Tests.LoaderTestHelper;

namespace Sheepy.Modnix.Tests {
   using ActionDef = Dictionary<string,object>;

   [TestClass()]
   public class ActionTest {

      [ClassInitializeAttribute] public static void TestInitialize ( TestContext _ = null ) => ResetLoader();

      [TestCleanup] public void TestCleanup () => ResetLoader();

      private static readonly ModEntry ModA = new ModEntry( "//Act/A", new ModMeta () { Id = "Act.A" } );

      [TestMethod] public void DefaultActionTest () {
         ModA.Metadata.Actions = new ActionDef[]{
            CreateDef( "Action", "Default", "All", "Def1" ),
            CreateDef( "Eval", "Code1" ),
            CreateDef( "Action", "Default", "More", "Def2" ),
            CreateDef( "Skip", "Splash", "Phase", "SplashMod" ),
            CreateDef( "All", "Native" ),
            CreateDef( "Action", "Default", "More", "Def3" ),
            CreateDef( "Image", "bug.jpg" ),
         };
         ModA.Metadata.Normalise();
         AddMod( ModA );
         ResolveMods();

         var acts = ModA.Metadata.Actions;
         Assert.AreEqual( 4, acts.Length, "3 merged actions" );
         Assert.AreEqual( "Code1", acts[0]["eval"], "[0].Eval" );
         Assert.AreEqual( "Def1", acts[0]["all"], "[0].All" );
         Assert.AreEqual( "Splash", acts[1]["skip"], "[1].Skip" );
         Assert.AreEqual( "Def1", acts[1]["all"], "[1].All" );
         Assert.AreEqual( "Def2", acts[1]["more"], "[1].More" );
         Assert.AreEqual( "Native", acts[2]["all"], "[2].All" );
         Assert.AreEqual( "Def2", acts[2]["more"], "[2].More" );
         Assert.IsFalse( acts[3].ContainsKey( "eval" ), "[3].Eval" );
         Assert.AreEqual( "bug.jpg", acts[3]["image"], "[3].Img" );
         Assert.AreEqual( "Def3", acts[3]["more"], "[3].More" );
      }

      [TestMethod] public void InlistTest () {
         bool InList ( string list, string val ) => (bool) typeof( ModActions ).GetMethod( "InList", NonPublic | Static ).Invoke( null, new object[]{ list, val } );
         Assert.AreEqual( true, InList( "MainMod", "mainmod" ), "simple" );
         Assert.AreEqual( false, InList( " A", "a2" ), "longer" );
         Assert.AreEqual( false, InList( "ABC ", "ab" ), "shorter" );
         Assert.AreEqual( true, InList( " ABC , DEF , GHI ", "def" ), "multi" );
      }
   }
}