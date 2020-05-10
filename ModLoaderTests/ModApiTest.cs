using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;
using System.Collections.Generic;
using static System.Reflection.BindingFlags;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace Sheepy.Modnix.Tests {

   internal class ModConfigClass { public int Config_Version = 1; }

   [TestClass()]
   public class ModApiTest {

      private static readonly ModEntry ModA = new ModEntry( "//A", new ModMeta () { Id = "Test.A", Version = new Version( 1, 0, 0, 0 ) } );
      private static readonly ModEntry ModB = new ModEntry( "//B/b", new ModMeta () { Id = "Test.B", Version = new Version( 1, 2, 3, 4 ) } );
      private static readonly ModEntry ModC = new ModEntry( new ModMeta () { Id = "Test.C", Version = null } );
      private static readonly Version ZeroVersion = new Version( 0, 0, 0, 0 );

      [ClassInitializeAttribute] public static void TestInitialize ( TestContext _ ) {
         ModLoader.Setup();
         ModScanner.AllMods.Clear();
         ModScanner.AllMods.Add( ModA );
         ModScanner.AllMods.Add( ModB );
         ModScanner.AllMods.Add( ModC );
         typeof( ModScanner ).GetMethod( "ResolveMods", NonPublic | Static ).Invoke( null, new object[0] );
      }

      private const string TEST_CONFIG_MOD = @"({ Id : ""test.config""})";

      [TestMethod()] public void AssemblyTest () {
         var loader = ModA.ModAPI( "assembly", "loader" ) as Assembly;
         Assert.IsNotNull( loader, "loader assembly" );
         Assert.AreEqual ( loader, ModA.ModAPI( "assembly", "loader" ), "loader assembly" );

         var a = ModA.ModAPI( "assemblies" );
         Assert.IsNull( ModA.ModAPI( "assembly" ), "ModA assembly" );
         Assert.IsNotNull( a, "ModA assemblies" );
         Assert.AreEqual( 0, ( a as IEnumerable<Assembly> ).Count(), "ModA assemblies" );
         Assert.IsNull( ModA.ModAPI( "assembly", "non-exist" ), "not found assembly" );
         Assert.IsNull( ModA.ModAPI( "assemblies", "non-exist" ), "not found assemblies" );

         var list = ModA.ModAPI( "assemblies", "loader" ) as IEnumerable<Assembly>;
         Assert.IsNotNull( list, "loader assemblies" );
         Assert.AreEqual( 1, list.Count(), "loader assemblies count" );
         Assert.AreEqual( loader, list.First(), "loader assemblies[0]" );
      }

      [TestMethod()] public void ConfigTest () {
         var modFile = Path.Combine( Path.GetTempPath(), "mod_info.js" );
         File.WriteAllText( modFile, TEST_CONFIG_MOD );
         try {
            var parseMod = typeof( ModScanner ).GetMethod( "ParseMod", Public | NonPublic | Static )
               .CreateDelegate( typeof( Func<string,string,ModEntry> ) ) as Func<string,string,ModEntry>;
            var mod = parseMod( modFile, null );
            Assert.AreEqual( "test.config", mod.Metadata.Id, "Test mod is parsed" );
            Assert.AreEqual( true, mod.ModAPI( "config delete" ), "Delete config" );

            // String config tests
            const string defConf = "def\tault";
            const string textConf = "line1\nline2";
            Assert.AreEqual( defConf, mod.ModAPI( "config", defConf ), "default text" );
            SaveConfig( mod, textConf, "text" );
            Assert.AreEqual( textConf, mod.ModAPI( "config", defConf ), "config text" );
            Assert.AreEqual( textConf, mod.ModAPI( "config", typeof( string ) ), "config string type" );
            Assert.AreEqual( defConf, mod.ModAPI( "config default", defConf ), "config def text" );
            Assert.AreEqual( true, mod.ModAPI( "config delete" ), "Delete text config" );

            // Json config tests
            lock ( mod.Metadata ) mod.Metadata.ConfigType = typeof( ModConfigClass ).FullName;
            var asmList = new Assembly[]{ Assembly.GetExecutingAssembly() }.ToList();
            typeof( ModEntry ).GetField( "ModAssemblies", Public | NonPublic | Instance ).SetValue( mod, asmList );
            Assert.IsNotNull( mod.ModAPI( "assembly" ), "Assembly set" );

            var implicitDef = mod.ModAPI( "config" );
            var config =  implicitDef as ModConfigClass;
            Assert.AreEqual( typeof( ModConfigClass ), implicitDef?.GetType(), "ConfigType is working" );
            Assert.AreEqual( 1, config.Config_Version, "Config_Version = 1" );

            config.Config_Version = 2;
            SaveConfig( mod, config, "json2" );

            config = mod.ModAPI( "config" ) as ModConfigClass;
            Assert.AreEqual( 2, config?.Config_Version, "Config_Version = 2" );

            config = mod.ModAPI( "config default" ) as ModConfigClass;
            Assert.AreEqual( 1, config?.Config_Version, "Explicit default Config_Version = 1" );
         } finally {
            if ( File.Exists( modFile ) ) File.Delete( modFile );
         }
      }

      [TestMethod()] public void ContextTest () {
         Assert.AreEqual( 3, ModScanner.EnabledMods.Count, "mod count" );
      }

      private static string ExtB ( object t, object e ) => t.ToString() + e.ToString() + "B";

      [TestMethod()] public void ModExtTest () {
         Func<object,string> ExtA = ( e )=> e.ToString() + "A";
         Predicate<object> ExtC = ( e ) => e == null;
         Func<string> ExtD = () => "";
         Action<object> ExtE = ( _ ) => { };

         Assert.AreEqual( false, ModA.ModAPI( "api_add" , null ), "no name null action" );
         Assert.AreEqual( true , ModA.ModAPI( "api_add A.A", ExtA ), "A.A => ExtA" );
         Assert.AreEqual( true , ModA.ModAPI( "api_add  A.B", (Func<object,object,string>) ExtB ), "A.B => ExtB" );
         Assert.AreEqual( true , ModA.ModAPI( "api_add A.C", ExtC ), "A.C => ExtC" );
         Assert.AreEqual( true , ModA.ModAPI( "api_add A.E", ExtE ), "A.C => ExtE" );

         var list = ModA.ModAPI( "api_list", "A." ) as IEnumerable<string>;
         Assert.AreEqual( 4, list.Count(), "api_list.Length" );
         Assert.AreEqual( "a.a", list.First(), "api_list[0]" );
         Assert.AreEqual( ExtA.GetMethodInfo(), ModA.ModAPI( "api_info", "A.A" ), "api_info A.A" );
         Assert.AreEqual( ExtC.GetMethodInfo(), ModA.ModAPI( "api_info", "A.C" ), "api_info A.C" );
         Assert.AreEqual( ExtE.GetMethodInfo(), ModA.ModAPI( "api_info", "A.E" ), "api_info A.E" );

         Assert.AreEqual( false, ModA.ModAPI( "api_add AAA", ExtA ), "no dot" );
         Assert.AreEqual( false, ModA.ModAPI( "api_add .A", ExtA ), "too short" );
         Assert.AreEqual( false, ModA.ModAPI( "api_add A.A", ExtA ), "duplucate reg" );
         Assert.AreEqual( false, ModA.ModAPI( "api_add A.C", null ), "null action" );
         Assert.AreEqual( false, ModA.ModAPI( "api_add A.D", ExtD ), "Invalid action" );
         Assert.AreEqual( false, ModA.ModAPI( "api_add A.D DEF", ExtD ), "Extra spec" );

         Assert.AreEqual( "0A" , ModB.ModAPI( "a.A", "0" ), "call api a.A" );
         Assert.AreEqual( "c0B", ModB.ModAPI( "A.b c", "0" ), "call api A.b" );
         Assert.AreEqual( true , ModB.ModAPI( "a.C", null ), "call api a.C null" );
         Assert.AreEqual( false, ModB.ModAPI( "a.C", "" ), "call api a.C non-null" );
         Assert.AreEqual( null , ModA.ModAPI( "A.E", ExtD ), "call api a.e" );
         Assert.AreEqual( false, ModB.ModAPI( "api_remove A.B" ), "api_remove non-owner" );
         Assert.AreEqual( false, ModA.ModAPI( "api_remove A.B CDE" ), "api_remove extra spec" );
         Assert.AreEqual( true , ModA.ModAPI( " api_remove   a.b " ), "api_remove owner" );
         Assert.AreEqual( null , ModA.ModAPI( "A.b c", "0" ), "call after api_remove" );

         Assert.AreEqual( true , ModB.ModAPI( " api_add   A.B ", (Func<object,object,string>) ExtB ), "A.B => ExtB (ModB)" );
         Assert.AreEqual( "c0B", ModA.ModAPI( "A.b c", "0" ), "call api A.b (Mod B)" );
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

      private void SaveConfig ( ModEntry mod, object param, string type ) {
         var task = mod.ModAPI( "config save", param ) as Task;
         Assert.IsNotNull( task, $"config save {type} returns Task" );
         task.Wait( 3000 );
         Assert.IsTrue( task.IsCompleted, $"config {type} saved" );
         Assert.IsNull( task.Exception, $"config {type} saved without error" );
      }

      [TestMethod()] public void PathTest () {
         var mod_dir = ModA.ModAPI( "dir", "mods_root" )?.ToString();
         var loader_dir = ModA.ModAPI( "dir", "loader" )?.ToString();
         var loader_path = ModA.ModAPI( "path", "loader" )?.ToString();
         
         Assert.IsTrue( loader_path.EndsWith( "Loader.dll" ) , "Modnix path" );
         Assert.IsTrue( mod_dir.EndsWith( "My Games\\Phoenix Point\\Mods" ), "Mod root" );
         Assert.AreEqual( loader_dir, Path.GetDirectoryName( loader_path ), "Modnix = mods root" );

         Assert.AreEqual( ModA.Path, ModA.ModAPI( "path", null ), "A null" );
         Assert.AreEqual( ModA.Path, ModA.ModAPI( "path", "" ), "A empty" );
         Assert.AreEqual( ModA.Path, ModA.ModAPI( "path", " " ), "A blank" );

         Assert.AreEqual( ModB.Path, ModA.ModAPI( "path", "Test.B" ), "Test.B" );
         Assert.AreEqual( null, ModA.ModAPI( "path", "test.c" ), "test.c" );
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
   }
}
