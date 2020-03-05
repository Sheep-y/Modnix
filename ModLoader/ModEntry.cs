using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace Sheepy.Modnix {
   using DllEntryMeta = Dictionary< string, HashSet< string > >;

   [ JsonObject( MemberSerialization.OptIn ) ]
   public class ModEntry {
      public readonly string Path;
      public readonly ModMeta Metadata;

      public ModEntry ( ModMeta meta ) : this( null, meta ) { }
      public ModEntry ( string path, ModMeta meta ) {
         Path = path;
         if ( path != null ) LastModified = new FileInfo( path ).LastWriteTime;
         Metadata = meta ?? throw new ArgumentNullException( nameof( meta ) );
      }

      internal readonly DateTime LastModified;
      internal LoggerProxy Logger; // Created when and only when an initialiser accepts a logging function
      internal object Instance; // Created when and only when a non-static initialiser is called
      internal string Key => ModScanner.NormaliseModId( Metadata.Id );

      public ModEntry Parent;
      public List<ModEntry> Children;
      public LogEntry Notices;

      [ JsonProperty ]
      public bool ManualDisable;
      [ JsonProperty ]
      public SourceLevels LogLevel = SourceLevels.Information;
      [ JsonProperty ]
      public long? ManualPriority;

      public bool Disabled;
      public long Priority => ManualPriority ?? Metadata.Priority ?? 0;

      internal void AddNotice ( SourceLevels lv, string reason, params object[] augs ) =>
         Notices = new LogEntry{ Level = lv, Message = reason, Args = augs };
      public override string ToString () => $"Mod {Metadata.Name}{(Disabled?" (Disabled)":"")}";
   }

   public class ModMeta {
      public string   Id;
      public Version  Version;

      public TextSet  Name;
      public string[] Lang;
      public TextSet  Description;
      public TextSet  Author;
      public TextSet  Url;
      public TextSet  Contact;
      public TextSet  Copyright;

      public AppVer[] Requires;
      public AppVer[] Conflicts;
      public long? Priority;

      public string[] Mods;
      public DllMeta[] Dlls;

      public bool HasContent => Mods == null && Dlls == null;

      internal ModMeta ImportFrom ( ModMeta overrider ) {
         if ( overrider == null ) return this;
         lock ( this ) { lock ( overrider ) {
            CopyNonNull( overrider.Id, ref Id );
            CopyNonNull( overrider.Version, ref Version );
            CopyNonNull( overrider.Name, ref Name );
            CopyNonNull( overrider.Lang, ref Lang );
            CopyNonNull( overrider.Description, ref Description );
            CopyNonNull( overrider.Author, ref Author );
            CopyNonNull( overrider.Url, ref Url );
            CopyNonNull( overrider.Contact, ref Contact );
            CopyNonNull( overrider.Copyright, ref Copyright );
            CopyNonNull( overrider.Requires, ref Requires );
            CopyNonNull( overrider.Conflicts, ref Conflicts );
            CopyNonNull( overrider.Priority, ref Priority );
            CopyNonNull( overrider.Mods, ref Mods );
            CopyNonNull( overrider.Dlls, ref Dlls );
         } }
         return overrider;
      }

      internal ModMeta EraseModsAndDlls () {
         Mods = null;
         Dlls = null;
         return this;
      }

      private static void CopyNonNull<T> ( T from, ref T to ) {
         if ( from != null ) to = from;
      }

      #region Normalise
      public ModMeta Normalise () { lock ( this ) {
         Id = NormString( Id );
         NormTextSet( ref Name );
         if ( Name == null && Id != null )
            Name = new TextSet{ Default = Id };
         NormStringArray( ref Lang );
         NormTextSet( ref Description );
         NormTextSet( ref Author );
         NormTextSet( ref Url );
         NormTextSet( ref Contact );
         NormTextSet( ref Copyright );
         NormAppVer( ref Requires );
         NormAppVer( ref Conflicts );
         NormStringArray( ref Mods );
         NormDllMeta( ref Dlls );
         return this;
      } }

      private static string NormString ( string val ) {
         if ( val == null ) return null;
         val = val.Trim();
         if ( val.Length <= 0 ) return null;
         return val;
      }

      private static void NormStringArray ( ref string[] val ) {
         if ( val == null ) return;
         val = val.Select( NormString ).Where( e => e != null ).ToArray();
         if ( val.Length <= 0 ) val = null;
      }

      private static void NormTextSet ( ref TextSet val ) {
         if ( val == null ) return;
         var dict = val.Dict;
         if ( dict != null && dict.Count <= 0 )
            val.Dict = dict = null;
         val.Default = NormString( val.Default );
         if ( val.Default == null ) {
            val.Default = dict?.First().Value;
            if ( val.Default == null ) val = null;
         }
      }

      private static void NormAppVer ( ref AppVer[] val ) {
         if ( val == null ) return;
         for ( int i = val.Length - 1 ; i >= 0 ; i-- ) {
            val[i].Id = NormString( val[i].Id );
            if ( val[i].Id == null ) val[i] = null;
         }
         if ( val.Any( e => e == null ) )
            val = val.Where( e => e != null ).ToArray();
         if ( val.Length <= 0 ) val = null;
      }
      
      private void NormDllMeta ( ref DllMeta[] val ) {
         if ( val == null ) return;
         for ( int i = val.Length - 1 ; i >= 0 ; i-- ) {
            val[i].Path = NormString( val[i].Path );
            if ( val[i].Path == null ) val[i] = null;
         }
         if ( val.Any( e => e == null || e.Path == null ) )
            val = val.Where( e => e != null && e.Path != null ).ToArray();
         if ( val.Length <= 0 ) val = null;
      }
      #endregion
   }

   public class TextSet {
      public string Default { get; set; }
      public Dictionary<string, string> Dict;
      public override string ToString () => ToString( null );
      public string ToString ( string preferred, string fallback = null ) {
         if ( preferred == null ) return Default;
         if ( Dict != null ) {
            if ( Dict.TryGetValue( preferred, out string txt ) ) return txt;
            if ( fallback != null && Dict.TryGetValue( fallback, out string eng ) ) return eng;
         }
         return Default;
      }
   }

   public class AppVer {
      public string Id;
      public Version Min;
      public Version Max;
   }

   public class DllMeta {
      public string Path;
      public DllEntryMeta Methods;
   }

   public static class ModMetaJson {
      public readonly static LoggerProxy JsonLogger = new JsonTraceLogger();
      public readonly static JsonSerializerSettings JsonOptions = new JsonSerializerSettings{
         Converters = new JsonConverter[]{ new ModMetaReader() }.ToList(),
         ContractResolver = new DefaultContractResolver(),
         DefaultValueHandling = DefaultValueHandling.Include,
         ReferenceLoopHandling = ReferenceLoopHandling.Error,
         Error = ( sender, err ) => JsonLogger.Error( err ),
         MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
         MissingMemberHandling = MissingMemberHandling.Ignore,
         NullValueHandling = NullValueHandling.Include,
         ObjectCreationHandling = ObjectCreationHandling.Replace,
         TraceWriter = JsonLogger as JsonTraceLogger,
         TypeNameHandling = TypeNameHandling.None
      };

      public static T Parse<T> ( string json ) => JsonConvert.DeserializeObject<T>( json, JsonOptions );
      public static ModMeta ParseMod ( string json ) => Parse<ModMeta>( json );
   }

   internal class ModMetaReader : JsonConverter {
      public override bool CanWrite => false;

      private static readonly Type[] TYPES = new Type[]{
         typeof( AppVer    ), typeof( AppVer [] ),
         typeof( DllMeta   ), typeof( DllMeta[] ),
         typeof( TextSet   ), typeof( TextSet[] ),
         typeof( Version   ),
         typeof( string[]  ),
      };

      public override bool CanConvert ( Type objectType ) => Array.IndexOf( TYPES, objectType ) >= 0;

      public override object ReadJson ( JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer ) {
         if ( objectType == typeof( AppVer    ) ) return ParseAppVer( reader );
         if ( objectType == typeof( AppVer[]  ) ) return ParseAppVerArray( reader );
         if ( objectType == typeof( DllMeta   ) ) return ParseDllMeta( reader );
         if ( objectType == typeof( DllMeta[] ) ) return ParseDllMetaArray( reader );
         if ( objectType == typeof( TextSet   ) ) return ParseTextSet( reader );
         if ( objectType == typeof( TextSet[] ) ) return ParseTextSetArray( reader );
         if ( objectType == typeof( Version   ) ) return ParseVersion( reader );
         if ( objectType == typeof( string[]  ) ) return ParseStringArray( reader );
         throw new InvalidOperationException();
      }

      private static AppVer   ParseAppVer ( JsonReader reader ) => ParseObject<AppVer>( reader, "id", AssignAppVerProp );
      private static AppVer[] ParseAppVerArray ( JsonReader reader ) => ParseArray<AppVer>( reader, ParseAppVer );
      private static AppVer   AssignAppVerProp ( AppVer e, string prop, object val ) {
         string txt = val.ToString().Trim();
         if ( txt.Length <= 0 ) return e;
         switch ( prop.ToLowerInvariant() ) {
            case "id"  : e.Id  = txt; break;
            case "min" : 
               if ( ! txt.Contains( '.' ) ) txt += ".0";
               Version.TryParse( txt, out e.Min );
               break;
            case "max" :
               if ( ! txt.Contains( '.' ) ) txt += ".0";
               Version.TryParse( txt, out e.Max );
               break;
            default: break;
         }
         return e;
      }

      private static DllMeta   ParseDllMeta ( JsonReader reader ) => ParseObject<DllMeta>( reader, "path", AssignDllMetaProp );
      private static DllMeta[] ParseDllMetaArray ( JsonReader reader ) => ParseArray<DllMeta>( reader, ParseDllMeta );
      private static DllMeta   AssignDllMetaProp ( DllMeta e, string prop, object val ) {
         prop = prop.Trim();
         string txt = val.ToString().Trim();
         if ( prop.Length <= 0 || txt.Length <= 0 ) return e;
         if ( prop.Equals( "path", StringComparison.InvariantCultureIgnoreCase ) ) {
            e.Path = txt;
         } else {
            var methods = e.Methods;
            if ( methods == null ) e.Methods = methods = new DllEntryMeta();
            if ( ! methods.TryGetValue( prop, out var list ) )
               methods[ prop ] = list = new HashSet<string>();
            list.Add( txt );
         }
         return e;
      }

      private static TextSet   ParseTextSet ( JsonReader reader ) => ParseObject<TextSet>( reader, "*", AssignTextSetProp );
      private static TextSet[] ParseTextSetArray ( JsonReader reader ) => ParseArray<TextSet>( reader, ParseTextSet );
      private static TextSet   AssignTextSetProp ( TextSet e, string prop, object val ) {
         prop = prop.Trim();
         string txt = val.ToString().Trim();
         if ( prop.Length <= 0 || txt.Length <= 0 ) return e;
         if ( e.Default == null ) {
            e.Default = txt;
            e.Dict = new Dictionary<string, string>();
         }
         e.Dict.Add( prop, txt );
         return e;
      }

      private static string ParseString ( JsonReader r ) {
         var token = r.SkipComment();
         if ( token == JsonToken.Null || token == JsonToken.Undefined ) return null;
         if ( token == JsonToken.String ) return r.Value.ToString();
         throw new JsonException( $"String expected" );
      }

      private static string[] ParseStringArray ( JsonReader r ) {
         var token = r.SkipComment();
         if ( token == JsonToken.Null || token == JsonToken.Undefined ) return null;
         if ( token == JsonToken.String ) return new string[] { r.Value.ToString() };
         if ( token == JsonToken.StartArray ) {
            if ( r.ReadAndSkipComment() == JsonToken.EndArray ) return null;
            List<string> result = new List<string>();
            do {
               string node = ParseString( r );
               if ( node != null ) result.Add( node );
               if ( r.ReadAndSkipComment() == JsonToken.EndArray )
                  return result.Count > 0 ? result.ToArray() : null;
            } while ( true );
         }
         throw new JsonException( $"String or array expected for string[]" );
      }

      private static T[] ParseArray < T > ( JsonReader r, Func< JsonReader, T > objParser ) where T : class, new() {
         var token = r.SkipComment();
         if ( token == JsonToken.Null || token == JsonToken.Undefined ) return null;
         if ( token == JsonToken.String || token == JsonToken.StartObject )
            return new T[] { objParser( r ) };
         if ( token == JsonToken.StartArray ) {
            if ( r.ReadAndSkipComment() == JsonToken.EndArray ) return null;
            List<T> result = new List<T>();
            do {
               T node = objParser( r );
               if ( node != null ) result.Add( node );
               if ( r.ReadAndSkipComment() == JsonToken.EndArray )
                  return result.Count > 0 ? result.ToArray() : null;
            } while ( true );
         }
         throw new JsonException( $"String, object, or array expected for {typeof(T)}" );
      }

      private static T ParseObject < T > ( JsonReader r, string defaultProp, Func<T,string,object,T> assignProp ) where T : class, new() {
         var token = r.SkipComment();
         if ( token == JsonToken.Null || token == JsonToken.Undefined ) return null;
         var result = new T();
         lock ( result ) {
            if ( token == JsonToken.String )
               return assignProp( result, defaultProp, r.Value );
            if ( token == JsonToken.StartObject ) {
               if ( r.ReadAndSkipComment() == JsonToken.EndObject ) return null;
               do {
                  if ( r.TokenType == JsonToken.PropertyName ) {
                     var prop = r.Value?.ToString();
                     token = r.ReadAndSkipComment();
                     if ( token == JsonToken.String || token == JsonToken.Integer )
                        assignProp( result, prop, r.Value );
                     else if ( token == JsonToken.Float )
                        assignProp( result, prop, FloatToString( r.Value ) );
                     if ( r.ReadAndSkipComment() == JsonToken.EndObject ) return result;
                  } else
                     throw new JsonException( $"Unexpected TokenType.{r.TokenType} when parsing {typeof(T)}" );
               } while ( true );
            }
         }
         throw new JsonException( $"String or object expected for {typeof(T)}" );
      }

      private static string FloatToString ( object val ) =>
         string.Format( string.Format( "{0:F18}", val ).TrimEnd( new char[]{ '0' }) );

      private static Version ParseVersion ( JsonReader r ) {
         Version result;
         var token = r.SkipComment();
         if ( token == JsonToken.Null || token == JsonToken.Undefined )
            result = null;
         else if ( token == JsonToken.String ) {
            if ( ! Version.TryParse( r.Value.ToString(), out result ) )
               result = null;
         } else if ( token == JsonToken.Integer ) {
            result = new Version( (int) (long) r.Value, 0 );
         } else if ( token == JsonToken.Float ) {
            if ( ! Version.TryParse( FloatToString( r.Value ), out result ) )
               result = null;
         } else
            throw new JsonException( $"String or number expected for Version" );
         r.ReadAndSkipComment();
         return result;
      }

      public override void WriteJson ( JsonWriter writer, object value, JsonSerializer serializer ) {
         throw new InvalidOperationException();
      }

   }

   internal static class JsonReaderExtension {
      internal static JsonToken ReadAndSkipComment ( this JsonReader r ) {
         r.Read();
         return SkipComment( r );
      }
      internal static JsonToken SkipComment ( this JsonReader r ) {
         while ( r.TokenType == JsonToken.Comment && r.Read() );
         return r.TokenType;
      }
   }

   internal class JsonTraceLogger : LoggerProxy, ITraceWriter {
      internal JsonTraceLogger ( params Logger[] Masters ) : base( false, Masters ) { }
      public TraceLevel LevelFilter { get; set; }
      public void Trace ( TraceLevel level, string message, Exception ex ) {
         if ( level > LevelFilter ) return;
         Log( level, message );
      }
   }

}
