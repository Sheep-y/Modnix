using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;

namespace Sheepy.Modnix.Tests {

   [TestClass()]
   public class ModJsonTest {

      [TestMethod()] public void ParseVersionTest () {
         AssertVer( false, null );
         AssertVer( false, "" );
         AssertVer( false, " " );
         AssertVer( false, "a" );
         AssertVer( true, "1", new Version( 1, 0, 0, 0 ) );
         AssertVer( true, "1.2", new Version( 1, 2, 0, 0 ) );
         AssertVer( true, "1.2.3", new Version( 1, 2, 3, 0 ) );
         AssertVer( true, "1.2.3.4", new Version( 1, 2, 3, 4 ) );
         AssertVer( false, "1...4" );
         AssertVer( false, "1.2.3.4.5" );
         AssertVer( false, "1.2a.3.4" );
      }

      private void AssertVer ( bool canParse, string input, Version expected = null ) {
         var actual = ModMetaJson.ParseVersion( input, out Version parsed );
         Assert.AreEqual( canParse, actual, input );
         Assert.AreEqual( parsed, expected, input );
      }

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
         AssertAppVer( "full", appver, new Version( 1, 0, 0, 0 ), new Version( 2, 1, 0, 0 ) );
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
         AssertAppVer( "two", appver[1], new Version( 1,2,3,4 ) );
         AssertAppVer( "three", appver[2] );
      }

      private void AssertAppVer ( string id, AppVer appver, Version min = null, Version max = null ) {
         Assert.IsNotNull( appver, $"{id} => not null" );
         Assert.AreEqual( id, appver.Id, $"{id}.id"  );
         Assert.AreEqual( min, appver.Min, $"{id}.min"  );
         Assert.AreEqual( max, appver.Max, $"{id}.max"  );
      }

      [TestMethod()] public void StringArrayTest () {
         var val = ModMetaJson.Parse<string[]>( "null" );
         Assert.IsNull( val, "null" );

         val = ModMetaJson.Parse<string[]>( "[]" );
         Assert.IsNull( val, "[]" );

         val = ModMetaJson.Parse<string[]>( "[ null ]" );
         Assert.IsNull( val, "[null]" );

         val = ModMetaJson.Parse<string[]>( @"""en""" );
         Assert.IsNotNull( val, "en not null" );
         Assert.AreEqual( 1, val.Length, "en.Length" );
         Assert.AreEqual( "en", val[0], "en" );

         val = ModMetaJson.Parse<string[]>( @"/*A*/ [ /*B*/ ""en"" /*C*/, /*D*/ ""zh"" /*E*/ ]" );
         Assert.IsNotNull( val, "zh not null" );
         Assert.AreEqual( 2, val.Length, "zh.Length" );
         Assert.AreEqual( "zh", val[1], "zh" );
      }

      [TestMethod()] public void TextSetTest () {
         var val = ModMetaJson.Parse<TextSet>( "null" );
         Assert.IsNull( val, "null" );

         val = ModMetaJson.Parse<TextSet>( @"""Lorem""" );
         Assert.IsNotNull( val, "Lorem" );
         Assert.AreEqual( "Lorem", val.Default, "Lorem.Default" );
         Assert.AreEqual( "Lorem", val.ToString(), "Lorem.ToString" );

         val = ModMetaJson.Parse<TextSet>( @"/*1*/ { /*2*/ Ab: /*3*/ ""Cd"" /*4*/, e: ""f"" /*5*/ }" );
         Assert.IsNotNull( val, "Abe" );
         Assert.AreEqual( "Cd", val.Default, "Abe.Default" );
         Assert.AreEqual( "f", val.ToString( "e" ), "Abe.ToString" );
      }

      [TestMethod()] public void VersionTest () {
         var appver = ModMetaJson.Parse<Version>( "null" );
         Assert.IsNull( appver, "null" );

         appver = ModMetaJson.Parse<Version>( @"""1.2.3.4""" );
         Assert.AreEqual( new Version( 1,2,3,4 ), appver, "1.2.3.4" );

         appver = ModMetaJson.Parse<Version>( @"1" );
         Assert.AreEqual( new Version( 1,0,0,0 ), appver, "1" );

         appver = ModMetaJson.Parse<Version>( @"1234.5678" );
         Assert.AreEqual( new Version( 1234, 5678, 0, 0 ), appver, "1" );
      }

      [TestMethod()] public void ModMetaNormTest () {
         var dict = new Dictionary<string, string>();
         dict.Add( "*", "def" );
         var meta = new ModMeta(){
            Id = "",
            Lang = new string[ 0 ],
            Mods = new string[]{ "" },
            Name = new TextSet{ Default = " " },
            Description = new TextSet{ Default = "a", Dict = new Dictionary<string, string>() },
            Author = new TextSet{ Default = " ", Dict = dict },
            Requires = new AppVer[0],
            Disables = new AppVer[]{ new AppVer{ Id = "" } },
            Dlls = new DllMeta[]{ new DllMeta{ Path = "" } },
         };
         meta.Normalise();
         Assert.IsNull( meta.Id, "Id" );
         Assert.IsNull( meta.Lang, "Langs" );
         Assert.IsNull( meta.Mods, "Mods" );
         Assert.IsNull( meta.Name, "Name" );
         Assert.IsNull( meta.Description.Dict, "Description" );
         Assert.AreEqual( "def", meta.Author.Default, "Author.Default" );
         Assert.IsNull( meta.Requires, "Requires" );
         Assert.IsNull( meta.Disables, "Disables" );
         Assert.IsNull( meta.Dlls, "Dlls" );

         meta.Id = "Abc";
         meta.Normalise();
         Assert.AreEqual( "Abc", meta.Id, "Id 2" );
         Assert.AreEqual( "Abc", meta.Name.ToString(), "Name 2" );
      }

      [TestMethod()] public void ModMetaTest () {
         var meta = ModMetaJson.ParseMod( @"{ Id:""simple"", Name:""Simple"", Author: { en: ""EN"", zh: ""ZH"" }, ""Version"": ""1.0"", Requires:""lib"", Dlls:""dll"" }" );
         Assert.IsNotNull( meta, "simple" );
         Assert.AreEqual( "simple", meta.Id, "simple.id" );
         Assert.AreEqual( "Simple", meta.Name.ToString(), "simple.name" );
         Assert.AreEqual( "lib", meta.Requires[0].Id, "simple.requires" );
         Assert.AreEqual( "dll", meta.Dlls[0].Path, "simple.dll" );
         Assert.AreEqual( "EN", meta.Author.ToString(), "simple.author" );
         Assert.AreEqual( "ZH", meta.Author.ToString("zh"), "simple.author.zh" );
         Assert.AreEqual( new Version( 1,0,0,0 ), meta.Version, "simple.version" );
      }

   }

}