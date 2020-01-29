using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;

namespace Sheepy.Logging {
   public abstract class Logger {
      public Logger ( int writeDelay = 100 ) {
         _WriteDelay = Math.Max( 0, writeDelay );
         ReaderWriterLockSlim locker = new ReaderWriterLockSlim();
         _Reader  = new LoggerReadLockHelper ( locker );
         _Writer  = new LoggerWriteLockHelper( locker );
         _Filters = new SynchronizedCollection<LogFilter>( locker );
         _Queue   = new List<LogEntry>();
      }

      // ============ Self Prop ============

      protected SourceLevels _Level = SourceLevels.Information;
      protected string _TimeFormat = "yyyy-MM-ddTHH:mm:ssz", _Prefix = null, _Postfix = null;
      protected readonly SynchronizedCollection< LogFilter > _Filters = null; // Exposed to public through Filter
      protected int _WriteDelay;
      protected Action< Exception > _OnError = null;

      protected readonly List< LogEntry > _Queue;
      protected readonly LockHelper _Reader, _Writer; // lock( _Writer ) during queue process, to ensure sequential output
      protected Timer _Timer;

      // ============ Public Prop ============

      public static string Stacktrace => new StackTrace( true ).ToString();

      /// Logger level. Only calls on or above the level will pass.
      public SourceLevels Level {
         get { using( _Reader.Lock ) { return _Level;  } }
         set { using( _Writer.Lock ) { _Level = value; } } }

      /// Datetime format string, default to "u" 
      public string TimeFormat {
         get { using( _Reader.Lock ) { return _TimeFormat;  } }
         set { using( _Writer.Lock ) { _TimeFormat = value; } } }

      /// String to be added to the start of every entry.
      public string Prefix {
         get { using( _Reader.Lock ) { return _Prefix;   } }
         set { using( _Writer.Lock ) { _Postfix = value; } } }

      /// String to be added to the end of every entry.
      public string Postfix {
         get { using( _Reader.Lock ) { return _Postfix;  } }
         set { using( _Writer.Lock ) { _Postfix = value; } } }

      /// Filters for processing log entries.
      public IList< LogFilter > Filters => _Filters;

      /// Delay in ms to start loggging. Set to 0 to disable threading - all loggin happens immediately
      public int WriteDelay {
         get { using( _Reader.Lock ) { return _WriteDelay;  } }
         set { using( _Writer.Lock ) { _WriteDelay = value; } } }

      /// Handles loggging errors such as filter exception or failure in writing to log.
      public Action<Exception> OnError {
         get { using( _Reader.Lock ) { return _OnError;  } }
         set { using( _Writer.Lock ) { _OnError = value; } } }

      // ============ API ============

      public void Log ( SourceLevels level, object message, params object[] args ) {
         var entry = NewEntry( level );
         if ( entry == null ) return;
         entry.Message = message;
         entry.Args = args;
         _Log( entry );
      }

      public void Log ( LogEntry entry ) {
         var prepost = NewEntry( entry.Level );
         if ( prepost == null ) return;
         if ( ! string.IsNullOrEmpty( prepost.Prefix  ) ) entry.Prefix += prepost.Prefix;
         if ( ! string.IsNullOrEmpty( prepost.Postfix ) ) entry.Postfix = prepost.Postfix + entry.Postfix;
         _Log( entry );
      }

      public void Trace ( object message = null, params object[] args ) => Log( SourceLevels.ActivityTracing, message, args );
      public void Verbo ( object message = null, params object[] args ) => Log( SourceLevels.Verbose, message, args );
      public void Info  ( object message = null, params object[] args ) => Log( SourceLevels.Information, message, args );
      public void Warn  ( object message = null, params object[] args ) => Log( SourceLevels.Warning, message, args );
      public void Error ( object message = null, params object[] args ) => Log( SourceLevels.Error, message, args );

      public virtual void Clear () {
         lock( _Queue ) {
            _Queue.Clear();
         }
      }

      public void Flush () {
         ProcessQueue();
      }

      // ============ Implementations ============

      private LogEntry NewEntry ( SourceLevels level ) {
         string pre, post;
         using ( _Reader.Lock ) {
            if ( ( level & _Level ) != level ) return null;
            pre = _Prefix;
            post = _Postfix;
         }
         return new LogEntry() { Time = DateTime.Now, Level = level, Prefix = pre, Postfix = post };
      }

      protected virtual void _Log ( LogEntry entry ) {
         int delay = WriteDelay;
         lock ( _Queue ) {
            _Queue.Add( entry );
            if ( delay > 0 ) {
               if ( _Timer == null )
                  _Timer = new Timer( TimerCallback, null, delay, Timeout.Infinite );
               return;
            }
         }
         Flush(); // Flush immedialy
      }

      private void TimerCallback ( object State ) => ProcessQueue();

      private void ProcessQueue () {
         string timeFormat;
         LogEntry[] entries;
         LogFilter[] filters;
         lock ( _Writer ) { // Used only here to control log sequence. Not the same as _Writer.Lock
            lock ( _Queue ) {
               _Timer?.Dispose();
               _Timer = null;
               if ( _Queue.Count <= 0 ) return;
               entries = _Queue.ToArray();
               _Queue.Clear();
            }
            using ( _Reader.Lock ) {
               timeFormat = _TimeFormat;
            }
            lock ( _Filters.SyncRoot ) {
               filters = _Filters.ToArray();
            }
            try {
               StartProcess();
               foreach ( LogEntry line in entries ) try {
                  foreach ( LogFilter filter in filters ) try {
                     if ( ! filter( line ) ) continue;
                  } catch ( Exception ex ) { CallOnError( ex, false ); }
                  string txt = line.Message?.ToString();
                  if ( ! string.IsNullOrEmpty( txt ) )
                     ProcessEntry( line, txt, timeFormat );
               } catch ( Exception ex ) {
                  CallOnError( ex, false );
               }
            } finally {
               EndProcess();
            }
         }
      }

      /// Called before queue is processed.
      protected abstract void StartProcess ();

      /// Process each log entry.
      protected abstract void ProcessEntry ( LogEntry entry, string txt, string timeFormat );

      /// Called after queue is processed. Will always be called even with exceptions.
      protected abstract void EndProcess ();

      /// Called on exception. If no error handler, throw the exception.
      protected virtual void CallOnError ( Exception ex, bool throwIfNull = true ) {
         var err = OnError;
         if ( ex == null ) return;
         if ( err == null || ex.StackTrace.Contains( ".CallOnError(" ) ) {
            if ( throwIfNull ) throw ex;
            return;
         }
         try {
            err.Invoke( ex );
         } catch ( Exception e ) {
            throw e;
         }
      }
   }

   public class FileLogger : Logger {
      public FileLogger ( string file, int writeDelay = 500 ) : base ( writeDelay ) {
         if ( string.IsNullOrEmpty( file ) ) throw new NullReferenceException();
         LogFile = file.Trim();
         _TimeFormat += ' ';
      }

      // ============ Public Prop ============

      public readonly string LogFile;

      // ============ API ============

      public override void Clear () {
         base.Clear();
         try {
            File.Delete( LogFile );
         } catch ( Exception ex ) {
            CallOnError( ex );
         }
      }

      // ============ Implementations ============

      private StringBuilder buf;

      protected override void StartProcess () => buf = new StringBuilder();

      protected override void ProcessEntry ( LogEntry entry, string txt, string timeFormat ) {
         if ( ! string.IsNullOrEmpty( timeFormat ) )
            buf.Append( entry.Time.ToString( timeFormat ) );

         SourceLevels level = entry.Level;
         string levelText;
         if      ( level <= SourceLevels.Error       ) levelText = "EROR ";
         else if ( level <= SourceLevels.Warning     ) levelText = "WARN ";
         else if ( level <= SourceLevels.Information ) levelText = "INFO ";
         else if ( level <= SourceLevels.Verbose     ) levelText = "FINE ";
         else levelText = "TRAC ";
         buf.Append( levelText );

         buf.Append( entry.Prefix );
         if ( entry.Args != null && entry.Args.Length > 0 && txt != null ) try {
            txt = string.Format( txt, entry.Args );
         } catch ( FormatException ) { /* Leave unformatable string as is */ }
         buf.Append( txt ).Append( entry.Postfix ).Append( Environment.NewLine );
      }

      protected override void EndProcess () {
         if ( buf != null && buf.Length > 0 )
            File.AppendAllText( LogFile, buf.ToString() );
         buf = null;
      }
   }

   public class LogEntry {
      public DateTime Time;
      public SourceLevels Level;
      public string Prefix;
      public string Postfix;
      public object Message;
      public object[] Args;
   }

   public delegate bool LogFilter ( LogEntry entry );

   public class LogFilters {

      // If message is not string, and there are multiple params, the message is converted to a list of params
      public static bool AutoMultiParam ( LogEntry entry ) {
         if ( entry.Message is string ) return true;
         if ( entry.Args == null || entry.Args.Length <= 0 ) return true;

         int len = entry.Args.Length;
         object[] newArg = new object[ len + 1 ];
         newArg[ 0 ] = entry.Message;
         entry.Args.CopyTo( newArg, 1 );
         entry.Args = newArg;

         StringBuilder message = new StringBuilder( len * 4 );
         for ( int i = 0 ; i < len ; i++ )
            message.Append( '{' ).Append( i ).Append( "} " );
         message.Length -= 1;
         entry.Message = message.ToString();

         return true;
      }


      // Expand enumerables and convert null (value) to "null" (string)
      public static bool FormatParams ( LogEntry entry ) {
         if ( entry.Message == null ) {
            entry.Message = "null";
            entry.Args = null;

         } else if ( entry.Args != null )
            entry.Args = entry.Args.Select( RecurFormatParam ).ToArray();

         return true;
      }

      private static object RecurFormatParam ( object param, int level = 0 ) {
         if ( param == null ) return "null";
         if ( level > 10 ) return "...";
         if ( param is System.Collections.IEnumerable collections ) {
            StringBuilder result = new StringBuilder().Append( collections.GetType().Name ).Append( '[' );
            foreach ( var e in collections ) result.Append( RecurFormatParam( e, level + 1 ) ).Append( ',' );
            result.Length -= 1;
            return result.Append( ']' ).ToString();
         }
         return param;
      }


      // Log each exception once.  Exceptions are the same if their ToString are same.
      public static LogFilter IgnoreDuplicateExceptions { get {
         HashSet< string > ignored = new HashSet<string>();
         return ( entry ) => {
            if ( ! ( entry.Message is Exception ex ) ) return true;
            string txt = ex.ToString();
            lock( ignored ) {
               if ( ignored.Contains( txt ) ) return false;
               ignored.Add( txt );
            }
            return true;
         };
      } }
   }

   #region Lock helpers
   /// Helper class to allow locks to be used with the using keyword
   public abstract class LockHelper : IDisposable {
      public abstract IDisposable Lock { get; }
      public abstract void Dispose ();
   }

   /// Helper to allow the read lock of a ReaderWriterLockSlim to be used with the using keyword
   public class LoggerReadLockHelper : LockHelper {
      public readonly ReaderWriterLockSlim RwLock;
      public LoggerReadLockHelper ( ReaderWriterLockSlim rwlock ) { RwLock = rwlock; }
      public override IDisposable Lock { get { RwLock.EnterReadLock(); return this; } }
      public override void Dispose () => RwLock.ExitReadLock();
   }

   /// Helper to allow the read lock of a ReaderWriterLockSlim to be used with the using keyword
   public class LoggerWriteLockHelper : LockHelper {
      public readonly ReaderWriterLockSlim RwLock;
      public LoggerWriteLockHelper ( ReaderWriterLockSlim rwlock ) { RwLock = rwlock; }
      public override IDisposable Lock { get { RwLock.EnterWriteLock(); return this; } }
      public override void Dispose () => RwLock.ExitWriteLock();
   }
   #endregion
}