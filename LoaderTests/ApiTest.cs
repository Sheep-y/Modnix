using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using static System.Reflection.BindingFlags;

namespace Sheepy.Modnix.Tests {

   internal class ModConfigClass { public int Config_Version = 1; }

   [TestClass()]
   public class ModApiTest {

      private static readonly ModEntry ModA = new ModEntry( "//A", new ModMeta () { Id = "Test.A", Version = new Version( 1, 0, 0, 0 ) } );
      private static readonly ModEntry ModB = new ModEntry( "//B/b", new ModMeta () { Id = "Test.B", Version = new Version( 1, 2, 3, 4 ) } );
      private static readonly ModEntry ModC = new ModEntry( new ModMeta () { Id = "Test.C", Version = null } );
      private static readonly Version ZeroVersion = new Version( 0, 0, 0, 0 );

      private static void AddMod ( ModEntry mod ) {
         // Mods will be disabled without dll.
         var dll = mod.Metadata.Dlls = new DllMeta[1];
         dll[ 0 ] = new DllMeta{ Methods = new Dictionary<string, HashSet<string>>() };
         dll[ 0 ].Methods.Add( "MainMod", new HashSet<string>() );
         ModLoader.AllMods.Add( mod );
      }

      [ClassInitializeAttribute] public static void TestInitialize ( TestContext _ ) {
         ModLoader.Setup();
         ModLoader.AllMods.Clear();
         AddMod( ModA );
         AddMod( ModB );
         AddMod( ModC );
         typeof( ModResolver ).GetMethod( "Resolve", NonPublic | Static ).Invoke( null, new object[0] );
      }

      private const string TEST_CONFIG_MOD = @"({ Id : ""test.config""})";

      [TestMethod] public void AssemblyTest () {
         var loader = ModA.ModAPI( "assembly", "loader" ) as Assembly;
         Assert.IsNotNull( loader, "loader assembly" );
         Assert.AreEqual ( loader, ModA.ModAPI( "assembly", "loader" ), "loader assembly" );

         var a = ModA.ModAPI( "assemblies" );
         Assert.AreEqual( null, ModA.ModAPI( "assembly" ), "ModA assembly" );
         Assert.IsNotNull( a, "ModA assemblies" );
         Assert.AreEqual( 0, ( a as IEnumerable<Assembly> ).Count(), "ModA assemblies" );
         Assert.AreEqual( null, ModA.ModAPI( "assembly", "non-exist" ), "not found assembly" );
         Assert.AreEqual( null, ModA.ModAPI( "assemblies", "non-exist" ), "not found assemblies" );

         var list = ModA.ModAPI( "assemblies", "loader" ) as IEnumerable<Assembly>;
         Assert.IsNotNull( list, "loader assemblies" );
         Assert.AreEqual( 1, list.Count(), "loader assemblies count" );
         Assert.AreEqual( loader, list.First(), "loader assemblies[0]" );
      }

      [TestMethod] public void ConfigTest () {
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

      [TestMethod] public void ContextTest () {
         Assert.AreEqual( 3, ModLoader.EnabledMods.Count, "mod count" );
      }

      private static string lastRun;

      private static void PlainAction () => lastRun = "PA";
      private static void ObjAction ( object _ ) => lastRun = "OA";
      private static void StrAction ( string _ ) => lastRun = "SA";
      private static void OOAction ( object _, object __ ) => lastRun = "OOA";
      private static void OSAction ( object _, string __ ) => lastRun = "SSA";

      private static object ObjFunc () => lastRun = "OF";
      private static object OOFunc ( object _ ) => lastRun = "OOF";
      private static bool   OBFunc ( object _ ) { lastRun = "OBF"; return true; }
      private static int    OIFunc ( object _ ) { lastRun = "OIF"; return 1; }
      private static string SSFunc ( string _ ) => lastRun = "SSF";

      private static object SOOFunc ( string _, object __ ) => lastRun = "SOOF";
      private static bool   OOBFunc ( object _, object __ ) { lastRun = "OOBF"; return true; }
      private static int    OOIFunc ( object _, object __ ) { lastRun = "OOIF"; return 1; }
      private static string SSSFunc ( string _, string __ ) => lastRun = "SSSF";

      private delegate string RefFuncType ( ref string a, object __ );
      private delegate string OutFuncType ( out string a, object __ );
      private delegate string InFuncType ( in string a, object __ );

      private static string RefFunc ( ref string a, object __ ) => a = lastRun = "RefF";
      private static string OutFunc ( out string a, object __ ) => a = lastRun = "OutF";
      private static string InFunc ( in string a, object __ ) => lastRun = a;

      [TestMethod] public void ModExtTypeTest () {
         // No param
         Assert.AreEqual( true, ModA.ModAPI( "api_add A.PA", (Action) PlainAction ), "PlainAction" );
         Assert.AreEqual( true, ModA.ModAPI( "A.PA", null ), "A.PA" );
         Assert.AreEqual( "PA", lastRun, "After A.PA" );
         Assert.AreEqual( true, ModA.ModAPI( "api_add A.OF", (Func<object>) ObjFunc ), "ObjFunc" );
         Assert.AreEqual( "OF", ModA.ModAPI( "A.OF", null ), "A.OF" );
         Assert.AreEqual( "OF", lastRun, "After A.OF" );

         // One param
         Assert.AreEqual( true, ModA.ModAPI( "api_add A.OA", (Action<object>) ObjAction ), "ObjAction" );
         Assert.AreEqual( true, ModA.ModAPI( "A.OA", null ), "A.OA" );
         Assert.AreEqual( "OA", lastRun, "After A.OA" );
         Assert.AreEqual( true, ModA.ModAPI( "api_add A.OOF", (Func<object,object>) OOFunc ), "OOFunc" );
         Assert.AreEqual( "OOF", ModA.ModAPI( "A.OOF", null ), "A.OOF" );
         Assert.AreEqual( "OOF", lastRun, "After A.OOF" );
         Assert.AreEqual( true, ModA.ModAPI( "api_add A.OBF", (Func<object,bool>) OBFunc ), "OBFunc" );
         Assert.AreEqual( true, ModA.ModAPI( "A.OBF", null ), "A.OBF" );
         Assert.AreEqual( "OBF", lastRun, "After A.OBF" );
         Assert.AreEqual( true, ModA.ModAPI( "api_add A.OIF", (Func<object,int>) OIFunc ), "OIFunc" );
         Assert.AreEqual( 1, ModA.ModAPI( "A.OIF", null ), "A.OIF" );
         Assert.AreEqual( "OIF", lastRun, "After A.OIF" );

         // Two params
         Assert.AreEqual( true, ModA.ModAPI( "api_add A.OOA", (Action<object,object>) OOAction ), "OOAction" );
         Assert.AreEqual( true, ModA.ModAPI( "A.OOA", null ), "A.OOA" );
         Assert.AreEqual( "OOA", lastRun, "After A.OOA" );
         Assert.AreEqual( true, ModA.ModAPI( "api_add A.SOO", (Func<string,object,object>) SOOFunc ), "SOOFunc" );
         Assert.AreEqual( "SOOF", ModA.ModAPI( "A.SOO", null ), "A.SOO" );
         Assert.AreEqual( "SOOF", lastRun, "After A.SOO" );
         Assert.AreEqual( true, ModA.ModAPI( "api_add A.OOB", (Func<object,object,bool>) OOBFunc ), "OOBFunc" );
         Assert.AreEqual( true, ModA.ModAPI( "A.OOB", null ), "A.OOB" );
         Assert.AreEqual( "OOBF", lastRun, "After A.OOB" );
         Assert.AreEqual( true, ModA.ModAPI( "api_add A.OOI", (Func<object,object,int>) OOIFunc ), "OOIFunc" );
         Assert.AreEqual( 1, ModA.ModAPI( "A.OOI", null ), "A.OOI" );
         Assert.AreEqual( "OOIF", lastRun, "After A.OOI" );

         // Fails
         IsException( ModA.ModAPI( "api_add A.SA", (Action<string>) StrAction ), "StrAction" );
         IsException( ModA.ModAPI( "api_add A.OSA", (Action<object,string>) OSAction ), "OSAction" );
         IsException( ModA.ModAPI( "api_add A.SSF", (Func<string,string>) SSFunc ), "SSFunc" );
         IsException( ModA.ModAPI( "api_add A.SSSF", (Func<string,string,string>) SSSFunc ), "SSSFunc" );
         IsException( ModA.ModAPI( "api_add A.RefF", (RefFuncType) RefFunc ), "RefF" );
         IsException( ModA.ModAPI( "api_add A.OutF", (OutFuncType) OutFunc ), "OutF" );
         IsException( ModA.ModAPI( "api_add A.InF", (InFuncType) InFunc ), "InF" );

         foreach ( var id in ModA.ModAPI( "api_list", "A." ) as IEnumerable<string> )
            ModA.ModAPI( "api_remove " + id );
      }

      private static string ExtA ( object e )=> e.ToString() + "A";
      private static string ExtB ( object t, object e ) => t.ToString() + e.ToString() + "B";
      private static bool ExtC ( object e ) => e == null;
      private static void ExtD ( Version _ ) { }

      [TestMethod] public void ModExtTest () {
         Func<object,string> A = ExtA;
         Func<object,object,string> B = ExtB;
         Predicate<object> C = ExtC;
         Action<Version> D = ExtD;

         IsException( ModA.ModAPI( "api_add" , null ), "no name null action" );
         Assert.AreEqual( true , ModA.ModAPI( "api_add A.A", A ), "A.A => ExtA" );
         Assert.AreEqual( true , ModA.ModAPI( "api_add  A.B", B ), "A.B => ExtB" );
         Assert.AreEqual( true , ModA.ModAPI( "api_add A.C", C ), "A.C => ExtC" );

         var list = ModA.ModAPI( "api_list", "A." ) as IEnumerable<string>;
         Assert.AreEqual( 3, list.Count(), "api_list.Length" );
         Assert.AreEqual( "a.a", list.First(), "api_list[0]" );
         Assert.AreEqual( A.GetMethodInfo(), ModA.ModAPI( "api_info", "A.A" ), "api_info A.A" );
         Assert.AreEqual( C.GetMethodInfo(), ModA.ModAPI( "api_info", "A.C" ), "api_info A.C" );

         IsException( ModA.ModAPI( "api_add AAA", A ), "no dot" );
         IsException( ModA.ModAPI( "api_add .A", A ), "too short" );
         IsException( ModA.ModAPI( "api_add A.A", A ), "duplucate reg" );
         IsException( ModA.ModAPI( "api_add @A.C", A ), "invalid char" );
         IsException( ModA.ModAPI( "api_add A.C", null ), "null action" );
         IsException( ModA.ModAPI( "api_add A.D", D ), "Invalid action" );
         IsException( ModA.ModAPI( "api_add A.D DEF", D ), "Extra spec" );

         Assert.AreEqual( "0A" , ModB.ModAPI( "a.A", "0" ), "call api a.A" );
         Assert.AreEqual( "c0B", ModB.ModAPI( "\v A.b c", "0" ), "call api A.b" );
         Assert.AreEqual( true , ModB.ModAPI( "a.C", null ), "call api a.C null" );
         Assert.AreEqual( false, ModB.ModAPI( "a.C", "" ), "call api a.C non-null" );
         IsException( ModB.ModAPI( "api_remove A.B" ), "api_remove non-owner" );
         IsException( ModA.ModAPI( "api_remove A.B CDE" ), "api_remove extra spec" );
         Assert.AreEqual( true , ModA.ModAPI( " api_remove   a.b " ), "api_remove owner" );
         Assert.AreEqual( null , ModA.ModAPI( "A.b c", "0" ), "call after api_remove" );

         Assert.AreEqual( true , ModB.ModAPI( " api_add   A.B ", B ), "A.B => ExtB (ModB)" );
         Assert.AreEqual( "c0B", ModA.ModAPI( "A.b c", "0" ), "call api A.b (Mod B)" );
         foreach ( var id in ModA.ModAPI( "api_list", "A." ) as IEnumerable<string> )
            ModA.ModAPI( "api_remove " + id );
      }

      private void IsException ( object value, string message ) =>
         Assert.IsInstanceOfType( value, typeof( Exception ), message );

      [TestMethod] public void ModInfoTest () {
         Assert.AreEqual( ModA.Metadata.Id, ( ModA.ModAPI( "mod_info", null ) as ModMeta ).Id, "A null" );
         Assert.AreEqual( ModA.Metadata.Id, ( ModA.ModAPI( "mod_info", "" ) as ModMeta ).Id, "A empty" );
         Assert.AreEqual( ModA.Metadata.Id, ( ModA.ModAPI( "mod_info", " " ) as ModMeta ).Id, "A blank" );
         Assert.AreEqual( ModB.Metadata.Id, ( ModA.ModAPI( "mod_info", "Test.B" ) as ModMeta ).Id, "Test.B" );
         Assert.AreEqual( ModC.Metadata.Id, ( ModA.ModAPI( "mod_info", "test.c" ) as ModMeta ).Id, "test.c" );
         Assert.AreEqual( null, ModA.ModAPI( "mod_info", "test.d" ), "test.d" );
      }

      [TestMethod] public void ModListTest () {
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

      [TestMethod] public void PathTest () {
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

      [TestMethod] public void StackTest () {
         string aId = ModA.Metadata.Id, bId = ModB.Metadata.Id;
         //AssertStrAry( ModA.ModAPI( "api_Stack acTion" ), "A1a", "api_Stack acTion" );
         //AssertStrAry( ModA.ModAPI( "api_stack mod" ), "A1a", aId );
         Assert.AreEqual( true, ModA.ModAPI( "api_add A.Stack", (Func<string,object,object>) AStack ), "Register A.Stack" );
         Assert.AreEqual( true, ModB.ModAPI( "api_add B.Stack", (Func<string,object,object>) BStack ), "Register B.Stack" );

         Task SA = Task.Run( () => {
            AssertStrAry( ModA.ModAPI( "A.Stack mod", 2 ), "A2m", aId, bId, aId );
            AssertStrAry( ModA.ModAPI( "A.Stack acTion", 2 ), "A2a", "A.Stack acTion", "B.Stack acTion", "A.Stack acTion" );
            AssertStrAry( ModA.ModAPI( "A.Stack command", 2 ), "A2c", "a.stack", "b.stack", "a.stack" );
            AssertStrAry( ModA.ModAPI( "A.Stack Spec", 2 ), "A2s", "Spec", "Spec", "Spec" );
         });

         Task SB = Task.Run( () => {
            AssertStrAry( ModB.ModAPI( "B.Stack mod", 2 ), "B2m", bId, aId, bId );
            AssertStrAry( ModB.ModAPI( "B.Stack acTion", 2 ), "B2a", "B.Stack acTion", "A.Stack acTion", "B.Stack acTion" );
            AssertStrAry( ModB.ModAPI( "B.Stack command", 2 ), "B2c", "b.stack", "a.stack", "b.stack" );
            AssertStrAry( ModB.ModAPI( "B.Stack Spec", 2 ), "B2s", "Spec", "Spec", "Spec" );
         });

         Task.WaitAll( SA, SB );

         Assert.AreEqual( true, ModA.ModAPI( "api_remove A.Stack" ), "remove A" );
         Assert.AreEqual( true, ModB.ModAPI( "api_remove B.Stack" ), "remove B" );

         Assert.AreEqual( null, ModA.ModAPI( "api_stack", new Thread( ()=>{ } ) ), "Invalid stack" );
      }

      private static void AssertStrAry ( object ary, string name, params string[] check ) {
         var stack = ary as string[];
         Assert.IsNotNull( stack, name + " not null" );
         Assert.AreEqual( check.Length, stack.Length, name + ".Length" );
         for ( var i = 0 ; i < check.Length ; i++ )
            Assert.AreEqual( check[i], stack[i], name + "["+ i + "]" );
      }

      private static Barrier StackLatch = new Barrier( 2 );

      private static object AStack ( string spec, object param ) {
         var count = param as int? ?? 0;
         if ( count <= 0 ) {
            var result = ModA.ModAPI( "api_stack " + spec );
            StackLatch.SignalAndWait( 1000 );
            return result;
         }
         return ModB.ModAPI( "B.Stack " + spec, count - 1 );
      }

      private static object BStack (string spec, object param ) {
         var count = param as int? ?? 0;
         if ( count <= 0 ) {
            var result = ModB.ModAPI( "api_stack " + spec );
            StackLatch.SignalAndWait( 1000 );
            return result;
         }
         return ModA.ModAPI( "A.Stack " + spec, count - 1 );
      }

      [TestMethod] public void VersionTest () {
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