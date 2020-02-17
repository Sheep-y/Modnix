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

   [ JsonObject( MemberSerialization.OptIn ) ]
   public class ModEntry {
      public ModMeta Metadata { get; set; }
      public List<ModEntry> Children { get; set; }

      [ JsonProperty ]
      public bool Disabled { get; set; }
      [ JsonProperty ]
      public SourceLevels LogLevel { get; set; }

      public ModEntry ( ModMeta metadata ) {
         Metadata = metadata;
      }
   }

   public class ModMeta {
      public string Id;
      public string Version;
      public string Phase;

      public L10nText Name;
      public string[] Langs;
      public L10nText Description;
      public L10nText Author;
      public L10nText[] Url;
      public L10nText[] Contact;

      public AppVer AppVer;
      public AppVer[] Requires;
      public AppVer[] Conflicts;
      public AppVer[] LoadsAfter;
      public AppVer[] LoadsBefore;

      public string[] Mods;
      public DllMeta[] Dlls;
   }

   public class L10nText {
      public static string CurrentLang = "en";
      public static readonly List<string> AllowedLang = new string[]{ "en", "de", "es", "fr", "it", "pl", "ru", "zh" }.ToList();

      public string Default;
      public Dictionary<string, string> Localised;
      public override string ToString () {
         if ( Localised != null ) {
            if ( Localised.TryGetValue( CurrentLang, out string txt ) ) return txt;
            if ( Localised.TryGetValue( "en", out string eng ) ) return eng;
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
      public string Method;
   }

   public class ModMetaJson {
      public readonly static LoggerProxy JsonLogger = new JsonTraceLogger();
      public readonly static JsonSerializerSettings JsonOptions = new JsonSerializerSettings() {
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
         if ( objectType == typeof( AppVer ) ) return true;
         if ( objectType == typeof( AppVer[] ) ) return true;
         if ( objectType == typeof( DllMeta ) ) return true;
         if ( objectType == typeof( DllMeta[] ) ) return true;
         if ( objectType == typeof( L10nText ) ) return true;
         if ( objectType == typeof( L10nText[] ) ) return true;
         return false;
      }

      public override object ReadJson ( JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer ) {
         if ( objectType == typeof( AppVer ) ) return ParseAppVer( reader );
         if ( objectType == typeof( AppVer[] ) ) return true;
         if ( objectType == typeof( DllMeta ) ) return true;
         if ( objectType == typeof( DllMeta[] ) ) return true;
         if ( objectType == typeof( L10nText ) ) return true;
         if ( objectType == typeof( L10nText[] ) ) return true;
         throw new InvalidOperationException();
      }

      private static AppVer ParseAppVer ( JsonReader reader ) => ParseObject<AppVer>( reader, NewAppVer, "id", AssignAppVerProp );
      private static AppVer NewAppVer () => new AppVer();
      private static AppVer AssignAppVerProp ( AppVer e, string prop, object val ) {
         string txt = val.ToString();
         switch ( prop ) {
            case "id"  : e.Id  = txt; break;
            case "min" : e.Min = txt; break;
            case "max" : e.Max = txt; break;
         }
         return e;
      }

      private static T ParseObject < T > ( JsonReader r, Func<T> newResult, string defaultProp, Func<T,string,object,T> assignProp ) {
         var firstToken = r.SkipComment();
         if ( firstToken == JsonToken.Null || firstToken == JsonToken.Undefined ) return default( T );
         if ( firstToken == JsonToken.String ) {
            return assignProp( newResult(), defaultProp, r.Value );
         } else if ( firstToken == JsonToken.StartObject ) {
            if ( r.ReadAndSkipComment() == JsonToken.EndObject ) return default( T );
            T result = newResult();
            do {
               if ( r.TokenType == JsonToken.PropertyName ) {
                  string prop = r.Value?.ToString()?.ToLowerInvariant();
                  r.ReadAndSkipComment();
                  if ( r.TokenType == JsonToken.String )
                     assignProp( result, prop, r.Value );
                  if ( r.ReadAndSkipComment() == JsonToken.EndObject ) return result;
               } else
                  throw new JsonException( $"Unexpected TokenType.{r.TokenType} when parsing AppVerMeta" );
            } while ( true );
         }
         throw new JsonException( "String or object expected for AppVerMeta" );
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
         SourceLevels logLevel;
         switch ( level ) {
            case TraceLevel.Error   : logLevel = SourceLevels.Critical; break;
            case TraceLevel.Warning : logLevel = SourceLevels.Warning; break;
            case TraceLevel.Info    : logLevel = SourceLevels.Information; break;
            case TraceLevel.Verbose : logLevel = SourceLevels.Verbose; break;
            default: return;
         }
         Log( logLevel, message );
      }
   }

}
