using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sheepy.Logging;
using Sheepy.Modnix;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sheepy.Modnix.Tests {

   [TestClass()]
   public class ModJsonTest {

      [TestMethod()] public void AppVerTest () {
         var appver = ModMetaJson.Parse<AppVer>( @"null" );
         Assert.IsNull( appver, "null" );
         
         appver = ModMetaJson.Parse<AppVer>( @"{}" );
         Assert.IsNull( appver, "empty" );

         appver = ModMetaJson.Parse<AppVer>( @"""text""" );
         TestAppVer( "text", appver );

         appver = ModMetaJson.Parse<AppVer>( @"{ id: ""simple"" }" );
         TestAppVer( "simple", appver );

         appver = ModMetaJson.Parse<AppVer>( @"/*A*/ { /*B*/ Id : /*C*/ ""full"" /*D*/, /*E*/ Min: 1, Max: 2.1, NonExist: 12 /*F*/ } /*G*/" );
         TestAppVer( "full", appver, "1", "2.1" );
      }

      [TestMethod()] public void AppVerArrayTest () {
         var appver = ModMetaJson.Parse<AppVer[]>( @"null" );
         Assert.IsNull( appver, "null" );

         appver = ModMetaJson.Parse<AppVer[]>( @"[]" );
         Assert.IsNull( appver, "empty" );

         appver = ModMetaJson.Parse<AppVer[]>( @"""text""" );
         Assert.IsNotNull( appver, "string => not null" );
         Assert.AreEqual( 1, appver.Length, "text => 1 element"  );
         TestAppVer( "text", appver[0] );

         appver = ModMetaJson.Parse<AppVer[]>( @"{ id: ""simple"" }" );
         Assert.AreEqual( 1, appver.Length, "simple => 1 element"  );
         TestAppVer( "simple", appver[0] );

         appver = ModMetaJson.Parse<AppVer[]>( @"[""one"", ""two""]" );
         Assert.AreEqual( 2, appver.Length, "two_text => 2 elements"  );
         TestAppVer( "one", appver[0] );
         TestAppVer( "two", appver[1] );

         appver = ModMetaJson.Parse<AppVer[]>( @"/*A*/ [ /*B*/ ""one"" /*C*/, null, /*D*/ { /*E*/ id: ""two"", min: ""1.2.3.4"" /*F*/ } /*G*/, /*H*/ ""three"" ]" );
         Assert.AreEqual( 3, appver.Length, "three => 3 elements"  );
         TestAppVer( "one", appver[0] );
         TestAppVer( "two", appver[1], "1.2.3.4" );
         TestAppVer( "three", appver[2] );
      }

      private void TestAppVer ( string id, AppVer appver, string min = null, string max = null ) {
         Assert.IsNotNull( appver, $"{id} => not null" );
         Assert.AreEqual( id, appver.Id, $"{id}.id"  );
         Assert.AreEqual( min, appver.Min, $"{id}.min"  );
         Assert.AreEqual( max, appver.Max, $"{id}.max"  );
      }

      [TestMethod()] public void ModMetaTest () {
         var appver = ModMetaJson.ParseMod( @"{ Id:""simple"", Name:""Simple"", Requires:""lib"", Dlls:""dll"" }" );
         Assert.IsNotNull( appver, "simple" );
         Assert.AreEqual( "simple", appver.Id, "simple.id" );
         Assert.AreEqual( "Simple", appver.Name.ToString(), "simple.name" );
         Assert.AreEqual( "lib", appver.Requires[0].Id, "simple.requires" );
         Assert.AreEqual( "dll", appver.Dlls[0].Path, "simple.dll" );
      }

   }

}