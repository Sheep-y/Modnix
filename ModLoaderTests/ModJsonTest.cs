using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sheepy.Logging;
using Sheepy.Modnix;
using System;
using System.Collections.Generic;
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
         AssertAppVer( "text", appver );

         appver = ModMetaJson.Parse<AppVer>( @"{ id: ""simple"" }" );
         AssertAppVer( "simple", appver );

         appver = ModMetaJson.Parse<AppVer>( @"/*A*/ { /*B*/ Id : /*C*/ ""full"" /*D*/, /*E*/ Min: 1, Max: 2.1, NonExist: 12 /*F*/ } /*G*/" );
         AssertAppVer( "full", appver, "1", "2.1" );
      }

      [TestMethod()] public void AppVerArrayTest () {
         var appver = ModMetaJson.Parse<AppVer[]>( @"null" );
         Assert.IsNull( appver, "null" );

         appver = ModMetaJson.Parse<AppVer[]>( @"[]" );
         Assert.IsNull( appver, "empty" );

         appver = ModMetaJson.Parse<AppVer[]>( @"""text""" );
         Assert.IsNotNull( appver, "string => not null" );
         Assert.AreEqual( 1, appver.Length, "text => 1 element"  );
         AssertAppVer( "text", appver[0] );

         appver = ModMetaJson.Parse<AppVer[]>( @"{ id: ""simple"" }" );
         Assert.AreEqual( 1, appver.Length, "simple => 1 element"  );
         AssertAppVer( "simple", appver[0] );

         appver = ModMetaJson.Parse<AppVer[]>( @"[""one"", ""two""]" );
         Assert.AreEqual( 2, appver.Length, "two_text => 2 elements"  );
         AssertAppVer( "one", appver[0] );
         AssertAppVer( "two", appver[1] );

         appver = ModMetaJson.Parse<AppVer[]>( @"/*A*/ [ /*B*/ ""one"" /*C*/, null, /*D*/ { /*E*/ id: ""two"", min: ""1.2.3.4"" /*F*/ } /*G*/, /*H*/ ""three"" ]" );
         Assert.AreEqual( 3, appver.Length, "three => 3 elements"  );
         AssertAppVer( "one", appver[0] );
         AssertAppVer( "two", appver[1], "1.2.3.4" );
         AssertAppVer( "three", appver[2] );
      }

      private void AssertAppVer ( string id, AppVer appver, string min = null, string max = null ) {
         Assert.IsNotNull( appver, $"{id} => not null" );
         Assert.AreEqual( id, appver.Id, $"{id}.id"  );
         Assert.AreEqual( min, appver.Min, $"{id}.min"  );
         Assert.AreEqual( max, appver.Max, $"{id}.max"  );
      }

      [TestMethod()] public void VersionTest () {
         var appver = ModMetaJson.Parse<Version>( "null" );
         Assert.IsNull( appver, "null" );

         appver = ModMetaJson.Parse<Version>( @"""1.2.3.4""" );
         Assert.AreEqual( new Version( 1,2,3,4 ), appver, "1.2.3.4" );

         appver = ModMetaJson.Parse<Version>( @"1" );
         Assert.AreEqual( new Version( 1, 0 ), appver, "1" );

         appver = ModMetaJson.Parse<Version>( @"1234.5678" );
         Assert.AreEqual( new Version( 1234, 5678 ), appver, "1" );
      }

      [TestMethod()] public void ModMetaNormTest () {
         var dict = new Dictionary<string, string>();
         dict.Add( "*", "def" );
         var meta = new ModMeta(){
            Id = "",
            Langs = new string[ 0 ],
            Mods = new string[]{ "" },
            Name = new TextSet{ Default = " " },
            Description = new TextSet{ Default = "a", Dict = new Dictionary<string, string>() },
            Author = new TextSet{ Default = " ", Dict = dict },
            Requires = new AppVer[0],
            Conflicts = new AppVer[]{ new AppVer{ Id = "" } },
            Dlls = new DllMeta[]{ new DllMeta{ Path = "" } },
         };
         meta.Normalise();
         Assert.IsNull( meta.Id, "Id" );
         Assert.IsNull( meta.Langs, "Langs" );
         Assert.IsNull( meta.Mods, "Mods" );
         Assert.IsNull( meta.Name, "Name" );
         Assert.IsNull( meta.Description.Dict, "Description" );
         Assert.AreEqual( "def", meta.Author.Default, "Author.Default" );
         Assert.IsNull( meta.Requires, "Requires" );
         Assert.IsNull( meta.Conflicts, "Conflicts" );
         Assert.IsNull( meta.Dlls, "Dlls" );
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