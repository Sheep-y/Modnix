using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sheepy.Logging;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sheepy.Logging.Tests {

   [TestClass()]
   public class LoggerTests {
      private readonly FileLogger Log = new FileLogger( "logtest.tmp", 100 );

      [TestMethod()][ExpectedException(typeof(ArgumentNullException))]
      public void NullFileLoggerThrow () => new FileLogger( null );

      [TestMethod()][ExpectedException(typeof(ArgumentNullException))]
      public void EmptyFileLoggerThrow () => new FileLogger( "" );

      [TestMethod()]
      public void FileLoggerBackgroundMode () {
         Log.Clear();
         Assert.IsFalse( File.Exists( Log.LogFile ) );

         // Check that log is not written immediately
         Log.Info( "Background Info" );
         Assert.IsFalse( File.Exists( Log.LogFile ) ); 

         // But is written after a while
         Thread.Sleep( 200 );
         long AfterWait = new FileInfo( Log.LogFile ).Length;
         Assert.IsTrue( AfterWait > 0 );

         // And is written immediately on flush
         Log.Info( "Background Flush" );
         Log.Flush();
         long AfterFlush = new FileInfo( Log.LogFile ).Length;
         Assert.IsTrue( AfterFlush > AfterWait );

         // Test log content
         string content = File.ReadAllText( Log.LogFile );
         Assert.IsTrue( content.Contains( "Background Info" ) && content.Contains( "Background Flush" ) );
         Assert.IsTrue( content.LastIndexOf( "Background Info" ) < content.LastIndexOf( "Background Flush" ) );

         // Test log delete
         Log.Clear();
         Assert.IsFalse( File.Exists( Log.LogFile ) );
      }
   }

}