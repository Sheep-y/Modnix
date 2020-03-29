using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading;

namespace Sheepy.Logging.Tests {

   [TestClass()]
   public class LoggerTests {
      private readonly FileLogger Log = new FileLogger( "logtest.tmp" );

      private long LogSize => new FileInfo( Log.LogFile ).Length;
      private string LogContent => File.ReadAllText( Log.LogFile );
      private Exception Error;

      [TestInitialize] public void TestInitialize () {
         Error = null;
         Log.Clear();
         Log.OnError = ( err ) => Error = err;
      }

      [TestCleanup] public void TestCleanup () {
         Log.Filters.Clear();
      }

      [TestMethod()][ExpectedException(typeof(ArgumentNullException))]
      public void NullFileLoggerThrow () => new FileLogger( null );

      [TestMethod()][ExpectedException(typeof(ArgumentNullException))]
      public void EmptyFileLoggerThrow () => new FileLogger( "" );

      [TestMethod()] public void FileLogger () {
         Log.WriteDelay = 100;

         // Check that log is not written immediately
         Log.Info( "Background Info" );
         Assert.IsFalse( File.Exists( Log.LogFile ), "Write is deferred" ); 

         // But is written after a while
         Thread.Sleep( 200 );
         long AfterWait = LogSize;
         Assert.IsTrue( AfterWait > 0, "Wrote on time" );

         // And is written immediately on flush
         Log.Info( "Background Flush" );
         Log.Flush();
         long AfterFlush = LogSize;
         Assert.IsTrue( AfterFlush > AfterWait, "Flush is immediate" );

         // Test log content
         string content = LogContent;
         Assert.IsTrue( content.Contains( "Background Info" ) && content.Contains( "Background Flush" ), "Has correct content" );
         Assert.IsTrue( content.LastIndexOf( "Background Info" ) < content.LastIndexOf( "Background Flush" ), "In correct order" );

         // Test log delete
         Log.Info( "Background Cleared" );
         Log.Clear();
         Assert.IsFalse( File.Exists( Log.LogFile ), "Clear delete log" );

         Assert.AreEqual( null, Error, "OnError" );
      }

      [TestMethod()] public void LoggerProxy () {
         Log.WriteDelay = 1000;
         var Log2 = new FileLogger( "logtest2.tmp", 1000 );
         LoggerProxy proxy = new LoggerProxy( true, Log );
         proxy.Masters.Add( Log2 );

         proxy.Info( "Proxy" );
         proxy.Flush();
         Assert.IsTrue( LogContent.Contains( "Proxy" ), "Proxy is forwarding to 1st log" );
         Assert.IsTrue( File.ReadAllText( Log2.LogFile ).Contains( "Proxy" ), "Proxy is forwarding to 2nd log" );

         proxy.Clear();
         Assert.IsFalse( File.Exists( Log .LogFile ), "Proxy is clearing 1st Log" );
         Assert.IsFalse( File.Exists( Log2.LogFile ), "Proxy is clearing 2nd Log" );

         try {
            new LoggerProxy( false ).Clear();
            Assert.IsTrue( false, "Proxy failed to disallow clear" );
         } catch ( InvalidOperationException ) {
            Assert.IsTrue( true, "Proxy may disallow clear" );
         }

         Assert.AreEqual( null, Error, "OnError" );
      }

      [TestMethod()] public void MultiParamFilter () {
         Log.WriteDelay = 0;
         Log.Filters.Add( LogFilters.AutoMultiParam );

         Log.Info( "{0}", 1, 2 );
         Assert.IsTrue( ! LogContent.Contains( "1 2" ), "Does not trigger on formatted string" );

         Log.Info( "3", 4, 5 );
         Assert.IsTrue( LogContent.Contains( "3 4 5" ), "Triggers on non-formatted string" );

         Log.Info( 6, 7, 8 );
         Assert.IsTrue( LogContent.Contains( "6 7 8" ), "Triggers on non-string" );

         Assert.AreEqual( null, Error, "OnError" );
      }

      [TestMethod()] public void FormatParamFilter () {
         Log.WriteDelay = 0;
         Log.Filters.Add( LogFilters.FormatParams );

         Log.Info( "File created" );
         Assert.IsTrue( LogContent.Contains( "File created" ), "String (IEnumerable) not converted" );

         Assert.IsFalse( LogContent.Contains( "null" ), "Clean file assertion" );
         Log.Info( null );
         Assert.IsTrue( LogContent.Contains( "null" ), "Null message conversion" );

         Log.Info( "a{0}", null, "param args" );
         Assert.IsTrue( LogContent.Contains( "anull" ), "Null param conversion" );

         Log.Info( new object[]{ 1, new object[]{ 2, 3 } } );
         Assert.IsTrue( LogContent.Contains( "Object[]{ 1, Object[]{ 2, 3, }, }" ), "Array conversion" );

         Assert.AreEqual( null, Error, "OnError" );
      }

      [TestMethod()] public void ResolveLazyFilter () {
         Log.WriteDelay = 0;
         Log.Filters.Add( LogFilters.ResolveLazy );

         Log.Info( (Func<string>)( () => "123" ) );
         Assert.IsTrue( LogContent.Contains( "123" ), "Resolve message" );

         Log.Info( "{0}", (Func<string>)( () => "456" ) );
         Assert.IsTrue( LogContent.Contains( "456" ), "Resolve param" );

         Assert.AreEqual( null, Error, "OnError" );

         Log.Info( "789 {0}", (Func<string>)( () => throw new Exception( "Dummy" ) ) );
         Assert.IsTrue( LogContent.Contains( "789" ), "Filter exceptions suppressed" );

         Assert.IsNotNull( Error, "OnError triggered" );
      }

      [TestMethod()] public void IngoreDupErrorFilter () {
         Log.WriteDelay = 0;
         Log.Filters.Add( LogFilters.IgnoreDuplicateExceptions );

         Exception subject = new Exception();
         Log.Error( subject );
         Assert.IsTrue( LogContent.Contains( "Exception" ), "Exception is logged" );
         long len = LogSize;

         Log.Error( subject );
         Assert.IsTrue( LogSize == len, "Duplicate is suppressed" );

         Log.Error( subject.ToString() );
         Assert.IsTrue( LogSize > len, "Non exceptions are allowed" );
         len = LogSize;

         Log.Filters.Add( LogFilters.AddPrefix( "ABC" ) );
         Log.Filters.Add( LogFilters.AddPostfix( "DEF" ) );
         Log.Error( subject );
         Assert.IsTrue( LogSize == len, "Duplicate is suppressed with pre post" );

         Log.Error( new FormatException() );
         Assert.IsTrue( LogContent.Contains( "FormatException" ), "New exception is logged" );
         len = LogSize;

         Log.Error( subject );
         Assert.IsTrue( LogSize == len, "Duplicate is still suppressed" );

         Assert.AreEqual( null, Error, "OnError" );
      }

      [TestMethod()] public void PrefixPostfixFilter () {
         Log.WriteDelay = 0;
         Log.Filters.Add( LogFilters.AddPrefix( "A" ) );
         Log.Filters.Add( LogFilters.AddPostfix( "Z" ) );

         Exception subject = new Exception();
         Log.Info( null );
         Assert.IsTrue( LogContent.Contains( "AZ" ), "Added to null" );

         Log.Info( "BC XY" );
         Assert.IsTrue( LogContent.Contains( "ABC XYZ" ), "Added to message" );

         Assert.AreEqual( null, Error, "OnError" );
      }
   }

}