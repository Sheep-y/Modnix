using Harmony;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sheepy.Modnix;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix.Tests {

   [TestClass()]
   public class HarmonyMigrationTest {

      // Setup assembly to load harmony. Bridge is loaded locally.
      [TestInitialize] public void TestInitialize () => ModLoader.Setup();

      [TestMethod()] public void Assumptions () {
         Assert.AreEqual( 3, Subject.Add( 1, 2 ), "Subject.Add" );
         Assert.IsNotNull( HarmonyInstance.Create( "test" ), "Harmony" );
      }

      [TestMethod()] public void ManualPatch () {
         var harmony = HarmonyInstance.Create( "manual" );
         var subject = typeof( Subject ).GetMethod( nameof( Subject.Add ) );
         var multi   = typeof( Subject ).GetMethod( nameof( Subject.Multi ) );
         var neg     = typeof( Subject ).GetMethod( nameof( Subject.Neg ) );

         harmony.Patch( subject, new HarmonyMethod( multi ) );
         Assert.AreEqual( 6, Subject.Add( 2, 3 ), "Prefix" );

         harmony.Patch( subject, null, new HarmonyMethod( neg ) );
         Assert.AreEqual( -6, Subject.Add( 2, 3 ), "Postfix" );

         harmony.Unpatch( subject, multi );
         Assert.AreEqual( -5, Subject.Add( 2, 3 ), "Unpatch" );

         harmony.UnpatchAll( "manual" );
         Assert.AreEqual( 5, Subject.Add( 2, 3 ), "UnpatchAll" );
      }

      [TestMethod()] public void AnnotationPatch () {
         var harmony = HarmonyInstance.Create( "annotation" );
         harmony.PatchAll();
         Assert.AreEqual( 9, Subject.Add( 18, 6 ), "Prepost" );
         harmony.UnpatchAll( "annotation" );
         Assert.AreEqual( 24, Subject.Add( 18, 6 ), "Prepost" );
      }
   }

   public class Subject {
      public static int Add ( int a, int b ) => a + b;
      public static void Neg ( ref int __result ) => __result = -__result;
      public static bool Multi ( int a, int b, ref int __result ) {
         __result = a * b;
         return false;
      }
   }

   [HarmonyPatch( typeof( Subject ), "Add" )]
   public class Patcher {
      public static bool Prefix ( int a, int b, ref int __result ) {
         __result = a / b;
         return false;
      }
      public static void Postfix ( ref int __result ) => __result = __result * __result;
   }
}
