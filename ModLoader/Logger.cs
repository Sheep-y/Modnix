using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Collections;

namespace Sheepy.Logging {

   // A thread-safe base logger with basic properties and methods.
   public abstract class Logger {

      protected Logger () {
         ReaderWriterLockSlim locker = new ReaderWriterLockSlim();
         _Reader  = new LoggerReadLockHelper ( locker );
         _Writer  = new LoggerWriteLockHelper( locker );
         _Filters = new RwlsList<LogFilter>( _Reader, _Writer );
      }

      // ============ Self Prop ============

      protected SourceLevels _Level = SourceLevels.Information;
      protected string _TimeFormat = "yyyy-MM-ddTHH:mm:ssz", _Prefix = null, _Postfix = null;
      protected readonly RwlsList< LogFilter > _Filters;
      protected Action< Exception > _OnError = null;

      protected readonly LoggerReadLockHelper  _Reader;
      protected readonly LoggerWriteLockHelper _Writer; // lock( _Writer ) during queue process, to ensure sequential output

      // ============ Public Prop ============

      public static string Stacktrace => new StackTrace( true ).ToString();

      // Logger level. Only calls on or above the level will pass.
      public SourceLevels Level {
         get { using( _Reader.Lock ) { return _Level;  } }
         set { using( _Writer.Lock ) { _Level = value; } } }

      // Datetime format string, default to "u"
      public string TimeFormat {
         get { using( _Reader.Lock ) { return _TimeFormat;  } }
         set { using( _Writer.Lock ) { _TimeFormat = value; } } }

      // Filters for processing log entries.
      public IList< LogFilter > Filters => _Filters;

      // Handles loggging errors such as filter exception or failure in writing to log.
      public Action<Exception> OnError {
         get { using( _Reader.Lock ) { return _OnError;  } }
         set { using( _Writer.Lock ) { _OnError = value; } } }

      // ============ API ============

      public virtual void Log ( TraceEventType level, object message, params object[] args ) {
         if ( ( level & (TraceEventType) Level ) != level ) return;
         if ( args != null && args.Length <= 0 ) args = null;
         _Log( new LogEntry(){ Time = DateTime.Now, Level = level, Message = message, Args = args } );
      }

      public virtual void Log ( TraceLevel level, object message, params object[] args ) {
         TraceEventType logLevel;
         switch ( level ) {
            case TraceLevel.Off     : return;
            case TraceLevel.Error   : logLevel = TraceEventType.Error; break;
            case TraceLevel.Warning : logLevel = TraceEventType.Warning; break;
            case TraceLevel.Info    : logLevel = TraceEventType.Information; break;
            case TraceLevel.Verbose : logLevel = TraceEventType.Verbose; break;
            default: return;
         }
         Log( logLevel, message, args );
      }

      public void Log ( SourceLevels level, object message, params object[] args ) {
         TraceEventType logLevel;
         switch ( level ) {
            case SourceLevels.Critical    : logLevel = TraceEventType.Critical; break;
            case SourceLevels.Error       : logLevel = TraceEventType.Error; break;
            case SourceLevels.Warning     : logLevel = TraceEventType.Warning; break;
            case SourceLevels.Information : logLevel = TraceEventType.Information; break;
            case SourceLevels.Verbose     : logLevel = TraceEventType.Verbose; break;
            default                       : logLevel = TraceEventType.Transfer; break;
         }
         Log( logLevel, message, args );
      }

      public virtual void Log ( LogEntry entry ) {
         if ( ( entry.Level & (TraceEventType) Level ) != entry.Level ) return;
         _Log( entry );
      }

      public void Trace ( object message, params object[] args ) => Log( SourceLevels.ActivityTracing, message, args );
      public void Verbo ( object message, params object[] args ) => Log( SourceLevels.Verbose, message, args );
      public void Info  ( object message, params object[] args ) => Log( SourceLevels.Information, message, args );
      public void Warn  ( object message, params object[] args ) => Log( SourceLevels.Warning, message, args );
      public void Error ( object message, params object[] args ) => Log( SourceLevels.Error, message, args );

      // Clear the log.
      public abstract void Clear ();

      // Immediately process all queued messages. The call blocks until they finish processing on this thread.
      public abstract void Flush ();

      // ============ Implementations ============

      // Internal method to convert an entry to string. Return null if any filter says no or input/result is null.
      protected virtual string EntryToString ( LogEntry entry, IEnumerable<LogFilter> filters = null ) {
         if ( entry == null ) return null;
         if ( filters == null ) filters = _Filters;
         foreach ( LogFilter filter in filters ) try {
            if ( ! filter( entry ) ) return null;
         } catch ( Exception ex ) { CallOnError( ex ); }

         string txt = entry.Message?.ToString();
         if ( string.IsNullOrEmpty( txt ) ) return null;

         if ( entry.Args != null && entry.Args.Length > 0 && txt != null ) try {
            return string.Format( txt, entry.Args );
         } catch ( FormatException ex ) { CallOnError( ex ); }
         return txt;
      }

      // Internal method to queue an entry for processing
      protected abstract void _Log ( LogEntry entry );

      // Called on exception. If no error handler, throw the exception by default.
      protected virtual void CallOnError ( Exception ex ) {
         if ( ex == null ) return;
         var err = OnError;
         if ( err == null ) {
            Console.Error.WriteLine( ex );
            return;
         }
         try {
            err.Invoke( ex );
         } catch ( Exception e ) {
            Console.Error.WriteLine( e );
            Console.Error.WriteLine( ex );
         }
      }

      public override string ToString () => GetType().ToString();
   }

   // A base logger that queue and process log entries in the background.
   public abstract class BackgroundLogger : Logger {
      protected BackgroundLogger ( int writeDelay = 100 ) {
         _WriteDelay = Math.Max( 0, writeDelay );
         _Queue   = new List<LogEntry>();
      }

      ~BackgroundLogger () => Flush();

      // ============ Properties ============

      protected int _WriteDelay;
      protected readonly List< LogEntry > _Queue;
      protected Timer _Timer;

      // Delay in ms to start loggging. Set to 0 to disable threading - all loggin happens immediately
      public int WriteDelay {
         get { using( _Reader.Lock ) { return _WriteDelay;  } }
         set { using( _Writer.Lock ) { _WriteDelay = value; } } }

      // ============ API ============

      public override void Clear () {
         lock( _Queue ) {
            _Queue.Clear();
         }
      }

      public override void Flush () => ProcessQueue();

      // ============ Implementations ============

      protected override void _Log ( LogEntry entry ) {
         int delay = WriteDelay;
         lock ( _Queue ) {
            _Queue.Add( entry );
            if ( delay > 0 ) {
               if ( _Timer == null )
                  _Timer = new Timer( TimerCallback, null, delay, Timeout.Infinite );
               return;
            }
         }
         Flush(); // No wait time = Flush immedialy
      }

      private void TimerCallback ( object State ) => ProcessQueue();

      // Process entry queue. Entries and states are copied and processed out of common locks.
      protected virtual void ProcessQueue () {
         string timeFormat;
         LogEntry[] entries;
         LogFilter[] filters;
         lock ( _Writer ) {
            lock ( _Queue ) {
               _Timer?.Dispose();
               _Timer = null;
               if ( _Queue.Count <= 0 ) return;
               entries = _Queue.ToArray();
               _Queue.Clear();
            }
            timeFormat = TimeFormat;
            filters = _Filters.ToArray();
            try {
               StartProcess();
               foreach ( LogEntry line in entries ) try {
                  string txt = EntryToString( line, filters );
                  if ( ! string.IsNullOrEmpty( txt ) )
                     ProcessEntry( line, txt, timeFormat );
               } catch ( Exception ex ) {
                  CallOnError( ex );
               }
            } finally {
               EndProcess();
            }
         }
      }

      // Called before queue is processed.
      protected virtual void StartProcess () { }

      // Process each log entry.
      protected abstract void ProcessEntry ( LogEntry entry, string txt, string timeFormat );

      // Called after queue is processed. Will always be called even with exceptions.
      protected virtual void EndProcess () { }
   }

   // Log to file.  Log is processed and written in a threadpool thread.
   public class FileLogger : BackgroundLogger {
      public FileLogger ( string file, int writeDelay = 500 ) : base ( writeDelay ) {
         if ( string.IsNullOrWhiteSpace( file ) ) throw new ArgumentNullException( "file" );
         LogFile = file.Trim();
         _TimeFormat += ' ';
      }

      // ============ Properties ============

      public readonly string LogFile;

      // ============ API ============

      public override void Clear () {
         lock ( _Writer ) {
            base.Clear();
            try {
               File.Delete( LogFile );
            } catch ( Exception ex ) {
               CallOnError( ex );
            }
         }
      }

      // ============ Implementations ============

      private readonly StringBuilder buf = new StringBuilder();

      protected override void ProcessEntry ( LogEntry entry, string txt, string timeFormat ) {
         if ( ! string.IsNullOrEmpty( timeFormat ) )
            buf.Append( entry.Time.ToString( timeFormat ) );

         string levelText;
         switch ( entry.Level ) {
            case TraceEventType.Critical    :  levelText = "CRIT "; break;
            case TraceEventType.Error       :  levelText = "EROR "; break;
            case TraceEventType.Warning     :  levelText = "WARN "; break;
            case TraceEventType.Information :  levelText = "INFO "; break;
            case TraceEventType.Verbose     :  levelText = "VEBO "; break;
            default                         :  levelText = "TRAC "; break;
         }
         buf.Append( levelText ).Append( txt ).Append( Environment.NewLine );
      }

      protected override void EndProcess () {
         if ( buf.Length <= 0 ) return;
         File.AppendAllText( LogFile, buf.ToString() );
         buf.Clear();
      }

      public override string ToString () => $"{GetType().ToString()}({LogFile},{WriteDelay})";
   }

   // A Logger that forwards messages to one or more loggers.  The proxy itself does not run in background.  TimeFormat is ignored.
   public class LoggerProxy : Logger {
      private bool _AllowClear;
      private readonly RwlsList< Logger > _Masters;

      public LoggerProxy ( bool AllowClear = true, params Logger[] Masters ) {
         _AllowClear = AllowClear;
         _Masters = new RwlsList< Logger >( _Reader, _Writer );
         if ( Masters != null && Masters.Length > 0 )
            foreach ( var master in Masters )
               _Masters.Add( master );
      }
      public LoggerProxy ( params Logger[] Masters ) : this( true, Masters ) { }

      public IList< Logger > Masters => _Masters;

      public override void Clear () {
         if ( ! _AllowClear ) throw new InvalidOperationException();
         foreach ( Logger master in _Masters.ToArray() ) try {
            master.Clear();
         } catch ( Exception ex ) { CallOnError( ex ); }
      }

      public override void Flush () {
         foreach ( Logger master in _Masters.ToArray() ) try {
            master.Flush();
         } catch ( Exception ex ) { CallOnError( ex ); }
      }

      protected override void _Log ( LogEntry entry ) {
         foreach ( LogFilter filter in _Filters.ToArray() ) try {
            if ( ! filter( entry ) ) return;
         } catch ( Exception ex ) { CallOnError( ex ); }
         foreach ( Logger master in _Masters.ToArray() ) try {
            master.Log( entry );
         } catch ( Exception ex ) { CallOnError( ex ); }
      }
   }

   // Represents a log entry, to be queued for processing or forwarded to another logger.
   public class LogEntry {
      public DateTime Time;
      public TraceEventType Level;
      public object Message;
      public object[] Args;
   }

   // Process a log entry, converting it and optionally reject it by returning false.  Returning true to keep it.
   public delegate bool LogFilter ( LogEntry entry );

   public static class LogFilters {

      // If message is not string, and there are multiple params, the message is converted to a list of params
      public static bool AutoMultiParam ( LogEntry entry ) {
         if ( entry.Args == null || entry.Args.Length <= 0 ) return true;
         if ( entry.Message is string txt && txt.Contains( '{' ) && txt.Contains( '}' ) ) return true;

         int len = entry.Args.Length + 1;
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
         entry.Message = RecurFormatParam( entry.Message );
         if ( entry.Args != null )
            entry.Args = entry.Args.Select( RecurFormatParam ).ToArray();
         return true;
      }

      private static object RecurFormatParam ( object param, int level = 0 ) {
         if ( param == null ) return "null";
         if ( level > 10 ) return "...";
         if ( param is ICollection collections ) {
            StringBuilder result = new StringBuilder().Append( collections.GetType().Name ).Append( "{ " );
            foreach ( var e in collections ) result.Append( RecurFormatParam( e, level + 1 ) ).Append( ", " );
            --result.Length;
            return result.Append( " }" ).ToString();
         }
         return param;
      }


      // Expand Func< string > to their results.
      public static bool ResolveLazy ( LogEntry entry ) {
         ResolveLazyFunc( ref entry.Message );
         var args = entry.Args;
         if ( args != null )
            for ( int i = args.Length - 1 ; i >= 0 ; i -- )
               ResolveLazyFunc( ref args[ i ] );
         return true;
      }

      private static void ResolveLazyFunc ( ref object param ) {
         if ( param is Func< string > lazy )
            param = lazy();
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

      public static LogFilter AddPrefix ( string prefix ) {
         return ( entry ) => {
            if ( entry.Message is Exception ) return true;
            entry.Message = prefix + entry.Message?.ToString();
            return true;
         };
      }

      public static LogFilter AddPostfix ( string postfix ) {
         return ( entry ) => {
            if ( entry.Message is Exception ) return true;
            entry.Message = entry.Message?.ToString() + postfix;
            return true;
         };
      }
   }

   #region Lock helpers
   // Helper class to allow locks to be used with the using keyword
   public abstract class LockHelper : IDisposable {
      public abstract IDisposable Lock { get; }
      public abstract void Dispose ();
   }

   // Helper to allow the read lock of a ReaderWriterLockSlim to be used with the using keyword
   public class LoggerReadLockHelper : LockHelper {
      public readonly ReaderWriterLockSlim RwLock;
      public LoggerReadLockHelper ( ReaderWriterLockSlim rwlock ) { RwLock = rwlock; }
      public override IDisposable Lock { get { RwLock.EnterReadLock(); return this; } }
      public override void Dispose () => RwLock.ExitReadLock();
   }

   // Helper to allow the read lock of a ReaderWriterLockSlim to be used with the using keyword
   public class LoggerWriteLockHelper : LockHelper {
      public readonly ReaderWriterLockSlim RwLock;
      public LoggerWriteLockHelper ( ReaderWriterLockSlim rwlock ) { RwLock = rwlock; }
      public override IDisposable Lock { get { RwLock.EnterWriteLock(); return this; } }
      public override void Dispose () => RwLock.ExitWriteLock();
   }

   public class RwlsList<T> : IList<T> {
      private readonly List<T> _List = new List<T>();
      private readonly LockHelper _Reader, _Writer;

      public RwlsList ( LoggerReadLockHelper reader, LoggerWriteLockHelper writer ) {
         _Reader = reader ?? throw new ArgumentNullException( "reader" );
         _Writer = writer ?? throw new ArgumentNullException( "writer" );
      }

      public T this[ int index ] {
         get { using ( _Reader.Lock ) { return _List[ index ]; } }
         set { using ( _Writer.Lock ) { _List[ index ] = value; } } }
      public int Count { get { using ( _Reader.Lock ) { return _List.Count; } } }
      public bool IsReadOnly => false;

      public void Add ( T item ) { using ( _Writer.Lock ) { _List.Add( item ); } }
      public void Clear () { using ( _Writer.Lock ) { _List.Clear(); } }
      public bool Contains ( T item ) { using ( _Reader.Lock ) { return _List.Contains( item ); } }
      public void CopyTo ( T[] array, int arrayIndex = 0 ) { using ( _Reader.Lock ) { _List.CopyTo( array, arrayIndex ); } }
      public int IndexOf ( T item ) { using ( _Reader.Lock ) { return _List.IndexOf( item ); } }
      public void Insert ( int index, T item ) { using ( _Writer.Lock ) { _List.Insert( index, item ); } }
      public bool Remove ( T item ) { using ( _Writer.Lock ) { return _List.Remove( item ); } }
      public void RemoveAt ( int index )  { using ( _Writer.Lock ) { _List.RemoveAt( index ); } }
      public T[] ToArray () { using ( _Reader.Lock ) { return _List.ToArray(); } }
      public IEnumerator<T> GetEnumerator () { return _List.GetEnumerator(); }
      IEnumerator IEnumerable.GetEnumerator () => GetEnumerator();
   }
   #endregion
}