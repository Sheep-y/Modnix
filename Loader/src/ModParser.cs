﻿using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Sheepy.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Sheepy.Modnix {

   /// <summary>
   /// Read verious mod info types in flexible format.
   /// </summary>
   public class ModMetaReader : JsonConverter {
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
         if ( objectType == typeof( AppVer ) ) return ParseAppVer( reader );
         if ( objectType == typeof( AppVer[] ) ) return ParseAppVerArray( reader );
         if ( objectType == typeof( DllMeta ) ) return ParseDllMeta( reader );
         if ( objectType == typeof( DllMeta[] ) ) return ParseDllMetaArray( reader );
         if ( objectType == typeof( TextSet ) ) return ParseTextSet( reader );
         if ( objectType == typeof( TextSet[] ) ) return ParseTextSetArray( reader );
         if ( objectType == typeof( Version ) ) return ParseVersion( reader );
         if ( objectType == typeof( string[] ) ) return ParseStringArray( reader );
         throw new InvalidOperationException();
      }

      private static AppVer ParseAppVer ( JsonReader reader ) => ParseObject<AppVer>( reader, "id", AssignAppVerProp );
      private static AppVer[] ParseAppVerArray ( JsonReader reader ) => ParseArray<AppVer>( reader, ParseAppVer );
      private static AppVer AssignAppVerProp ( AppVer e, string prop, object val ) {
         var txt = val.ToString().Trim();
         if ( txt.Length == 0 ) return e;
         switch ( prop.ToLowerInvariant() ) {
            case "id": e.Id = txt; break;
            case "min":
               Json.ParseVersion( txt, out e.Min );
               break;
            case "max":
               Json.ParseVersion( txt, out e.Max );
               break;
            case "name": e.Name = txt; break;
            case "url": e.Url = txt; break;
         }
         return e;
      }

      private static DllMeta ParseDllMeta ( JsonReader reader ) => ParseObject<DllMeta>( reader, "path", AssignDllMetaProp );
      private static DllMeta[] ParseDllMetaArray ( JsonReader reader ) => ParseArray<DllMeta>( reader, ParseDllMeta );
      private static DllMeta AssignDllMetaProp ( DllMeta e, string prop, object val ) {
         prop = prop.Trim();
         var txt = val.ToString().Trim();
         if ( prop.Length <= 0 || txt.Length <= 0 ) return e;
         if ( prop.Equals( "path", StringComparison.OrdinalIgnoreCase ) ) {
            e.Path = txt;
         } else {
            var methods = e.Methods;
            if ( methods == null ) e.Methods = methods = new Dictionary< string, HashSet< string > >();
            if ( ! methods.TryGetValue( prop, out var list ) )
               methods[ prop ] = list = new HashSet<string>();
            list.Add( txt );
         }
         return e;
      }

      private static TextSet ParseTextSet ( JsonReader reader ) => ParseObject<TextSet>( reader, "*", AssignTextSetProp );
      private static TextSet[] ParseTextSetArray ( JsonReader reader ) => ParseArray<TextSet>( reader, ParseTextSet );
      private static TextSet AssignTextSetProp ( TextSet e, string prop, object val ) {
         prop = prop.Trim();
         var txt = val.ToString().Trim();
         if ( prop.Length == 0 || txt.Length == 0 ) return e;
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
            var result = new List<string>();
            do {
               var node = ParseString( r );
               if ( node != null ) result.Add( node );
               if ( r.ReadAndSkipComment() == JsonToken.EndArray )
                  return result.Count > 0 ? result.ToArray() : null;
            } while ( true );
         }
         throw new JsonException( $"String or array expected for string[]" );
      }

      private static T[] ParseArray<T> ( JsonReader r, Func<JsonReader, T> objParser ) where T : class, new() {
         var token = r.SkipComment();
         if ( token == JsonToken.Null || token == JsonToken.Undefined ) return null;
         if ( token == JsonToken.String || token == JsonToken.StartObject )
            return new T[] { objParser( r ) };
         if ( token == JsonToken.StartArray ) {
            if ( r.ReadAndSkipComment() == JsonToken.EndArray ) return null;
            var result = new List<T>();
            do {
               T node = objParser( r );
               if ( node != null ) result.Add( node );
               if ( r.ReadAndSkipComment() == JsonToken.EndArray )
                  return result.Count > 0 ? result.ToArray() : null;
            } while ( true );
         }
         throw new JsonException( $"String, object, or array expected for {typeof( T )}" );
      }

      private static T ParseObject<T> ( JsonReader r, string defaultProp, Func<T, string, object, T> assignProp ) where T : class, new() {
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
                     throw new JsonException( $"Unexpected TokenType.{r.TokenType} when parsing {typeof( T )}" );
               } while ( true );
            }
         }
         throw new JsonException( $"String or object expected for {typeof( T )}" );
      }

      private static string FloatToString ( object val ) =>
         string.Format( string.Format( "{0:F18}", val ).TrimEnd( new char[] { '0' } ) );

      private static Version ParseVersion ( JsonReader r ) {
         Version result;
         var token = r.SkipComment();
         if ( token == JsonToken.Null || token == JsonToken.Undefined )
            result = null;
         else if ( token == JsonToken.String ) {
            if ( ! Json.ParseVersion( r.Value.ToString(), out result ) )
               result = null;
         } else if ( token == JsonToken.Integer ) {
            result = new Version( (int) (long) r.Value, 0, 0, 0 );
         } else if ( token == JsonToken.Float ) {
            if ( ! Json.ParseVersion( FloatToString( r.Value ), out result ) )
               result = null;
         } else
            throw new JsonException( $"String or number expected for Version" );
         r.SkipComment();
         return result;
      }

      public override void WriteJson ( JsonWriter writer, object value, JsonSerializer serializer ) {
         throw new InvalidOperationException();
      }
   }

   /// <summary>
   /// Forward Newtonsoft parsing messages to Sheepy.Logging
   /// </summary>
   internal class JsonTraceLogger : LoggerProxy, ITraceWriter {
      internal JsonTraceLogger ( params Logger[] Masters ) : base( false, Masters ) { }
      public TraceLevel LevelFilter { get; set; }
      public void Trace ( TraceLevel level, string message, Exception ex ) {
         if ( level > LevelFilter ) return;
         Log( level, message );
      }
   }
}