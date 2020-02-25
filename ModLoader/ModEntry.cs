using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sheepy.Modnix {
   using DllEntryMeta = Dictionary< string, HashSet< string > >;

   [ JsonObject( MemberSerialization.OptIn ) ]
   public class ModEntry {
      public ModMeta Metadata;

      public ModEntry Parent;
      public List<ModEntry> Children;

      [ JsonProperty ]
      public bool Disabled;
      [ JsonProperty ]
      public SourceLevels LogLevel;

      public override string ToString () => $"Mod {Metadata?.Name}{(Disabled?" (Disabled)":"")}";
   }

   public class ModMeta {
      public string Id;
      public string Version;

      public TextSet Name;
      public string[] Langs;
      public TextSet Description;
      public TextSet Author;
      public TextSet Url;
      public TextSet Contact;
      public TextSet Copyright;

      public AppVer[] Requires;
      public AppVer[] Conflicts;
      public AppVer[] LoadsAfter;
      public AppVer[] LoadsBefore;

      public string[] Mods;
      public DllMeta[] Dlls;

      internal ModMeta Override ( ModMeta baseline ) {
         if ( baseline == null ) return this;
         CopyNonNull( Id, ref baseline.Id );
         CopyNonNull( Version, ref baseline.Version );
         CopyNonNull( Name, ref baseline.Name );
         CopyNonNull( Langs, ref baseline.Langs );
         CopyNonNull( Description, ref baseline.Description );
         CopyNonNull( Author, ref baseline.Author );
         CopyNonNull( Requires, ref baseline.Requires );
         CopyNonNull( Conflicts, ref baseline.Conflicts );
         CopyNonNull( LoadsAfter, ref baseline.LoadsAfter );
         CopyNonNull( LoadsBefore, ref baseline.LoadsBefore );
         CopyNonNull( Mods, ref baseline.Mods );
         CopyNonNull( Dlls, ref baseline.Dlls );
         return baseline;
      }
      
      private static void CopyNonNull<T> ( T from, ref T to ) {
         if ( from != null ) to = from;
      }

      #region Normalise
      public ModMeta Normalise () {
         Id = NormString( Id );
         Version = NormString( Version );
         NormTextSet( ref Name );
         NormStringArray( ref Langs );
         NormTextSet( ref Description );
         NormTextSet( ref Author );
         NormTextSet( ref Url );
         NormTextSet( ref Contact );
         NormTextSet( ref Copyright );
         NormAppVer( ref Requires );
         NormAppVer( ref Conflicts );
         NormAppVer( ref LoadsAfter );
         NormAppVer( ref LoadsBefore );
         NormAppVer( ref LoadsBefore );
         NormStringArray( ref Mods );
         NormDllMeta( ref Dlls );
         return this;
      }

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
         if ( val.Any( e => e == null ) )
            val = val.Where( e => e != null ).ToArray();
         if ( val.Length <= 0 ) val = null;
      }
      
      private void NormDllMeta ( ref DllMeta[] val ) {
         if ( val == null ) return;
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
      public string Min;
      public string Max;
   }

   public class DllMeta {
      public string Path;
      public DllEntryMeta Methods;
   }

   public static class ModMetaJson {
      public readonly static LoggerProxy JsonLogger = new JsonTraceLogger();
      public readonly static JsonSerializerSettings JsonOptions = new JsonSerializerSettings{
         Converters = new JsonConverter[]{ new ModMetaReader() }.ToList(),
         DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
         ReferenceLoopHandling = ReferenceLoopHandling.Error,
         Error = ( sender, err ) => JsonLogger.Error( err ),
         MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
         MissingMemberHandling = MissingMemberHandling.Ignore,
         NullValueHandling = NullValueHandling.Ignore,
         ObjectCreationHandling = ObjectCreationHandling.Replace,
         TraceWriter = JsonLogger as JsonTraceLogger,
         TypeNameHandling = TypeNameHandling.None
      };

      public static T Parse<T> ( string json ) => JsonConvert.DeserializeObject<T>( json, JsonOptions );
      public static ModMeta ParseMod ( string json ) => Parse<ModMeta>( json );
   }

   internal class ModMetaReader : JsonConverter {
      public override bool CanWrite => false;

      public override bool CanConvert ( Type objectType ) {
         if ( objectType == typeof( AppVer    ) ) return true;
         if ( objectType == typeof( AppVer[]  ) ) return true;
         if ( objectType == typeof( DllMeta   ) ) return true;
         if ( objectType == typeof( DllMeta[] ) ) return true;
         if ( objectType == typeof( TextSet   ) ) return true;
         if ( objectType == typeof( TextSet[] ) ) return true;
         return false;
      }

      public override object ReadJson ( JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer ) {
         if ( objectType == typeof( AppVer    ) ) return ParseAppVer( reader );
         if ( objectType == typeof( AppVer[]  ) ) return ParseAppVerArray( reader );
         if ( objectType == typeof( DllMeta   ) ) return ParseDllMeta( reader );
         if ( objectType == typeof( DllMeta[] ) ) return ParseDllMetaArray( reader );
         if ( objectType == typeof( TextSet   ) ) return ParseTextSet( reader );
         if ( objectType == typeof( TextSet[] ) ) return ParseTextSetArray( reader );
         throw new InvalidOperationException();
      }

      private static AppVer   ParseAppVer ( JsonReader reader ) => ParseObject<AppVer>( reader, "id", AssignAppVerProp );
      private static AppVer[] ParseAppVerArray ( JsonReader reader ) => ParseArray<AppVer>( reader, ParseAppVer );
      private static AppVer   AssignAppVerProp ( AppVer e, string prop, object val ) {
         string txt = val.ToString().Trim();
         if ( txt.Length <= 0 ) return e;
         switch ( prop ) {
            case "id"  : e.Id  = txt; break;
            case "min" : e.Min = txt; break;
            case "max" : e.Max = txt; break;
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
         switch ( prop ) {
            case "path" :
               e.Path = txt;
               break;
            default :
               var methods = e.Methods;
               if ( methods == null ) e.Methods = methods = new DllEntryMeta();
               if ( ! methods.TryGetValue( prop, out var list ) )
                  methods[ prop ] = list = new HashSet<string>();
               list.Add( txt );
               break;
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
                     var prop = r.Value?.ToString()?.ToLowerInvariant();
                     token = r.ReadAndSkipComment();
                     if ( token == JsonToken.String || token == JsonToken.Integer || token == JsonToken.Float )
                        assignProp( result, prop, r.Value );
                     if ( r.ReadAndSkipComment() == JsonToken.EndObject ) return result;
                  } else
                     throw new JsonException( $"Unexpected TokenType.{r.TokenType} when parsing {typeof(T)}" );
               } while ( true );
            }
         }
         throw new JsonException( $"String or object expected for {typeof(T)}" );
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
