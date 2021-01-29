using Markdig;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Sheepy.Modnix.MainGUI {
   public static class WpfHelper {
      public static string Lf2Cr ( string text ) => text?.Replace( "\r", "" ).Replace( '\n', '\r' );
      public static StringBuilder Lf2Cr ( StringBuilder text ) => text?.Replace( "\r", "" ).Replace( '\n', '\r' );

      public static T Linkify < T > ( this T elem, Action onClick ) where T : Inline {
         elem.PreviewMouseDown += ( a, b ) => onClick();
         elem.MouseEnter += ( a, b ) => elem.TextDecorations.Add( TextDecorations.Underline );
         elem.MouseLeave += ( a, b ) => elem.TextDecorations.Clear();
         elem.Cursor = Cursors.Hand;
         return elem;
      }

      public static TextRange TextRange ( this FlowDocument doc ) => new TextRange( doc.ContentStart, doc.ContentEnd );

      public static void AddMulti ( this InlineCollection inlines, params object[] elements ) {
         foreach ( var e in elements )
            if ( e is string txt ) inlines.Add( txt );
            else if ( e is Inline i ) inlines.Add( i );
      }

      public static void Replace ( this FlowDocument doc, params Block[] blocks ) => Replace( doc, (IEnumerable<Block>) blocks );
      public static void Replace ( this FlowDocument doc, IEnumerable< Block > blocks ) {
         var body = doc.Blocks;
         body.Clear();
         foreach ( var e in blocks )
            if ( e != null )
               body.Add( e );
      }

      public static Paragraph P ( params Inline[] inlines ) => P( (IEnumerable<Inline>) inlines );
      public static Paragraph P ( IEnumerable< Inline > inlines ) {
         var result = new Paragraph();
         var body = result.Inlines;
         foreach ( var e in inlines )
            if ( e != null )
               body.Add( e );
         return result;
      }
   }

   public static class MarkdigConverter {
      public static IEnumerable< Block > Convert ( string text ) =>
         Markdown.Parse( text, new MarkdownPipelineBuilder().UseAutoLinks().Build() ).Select( BuildBlock );

      private static Block BuildBlock ( Markdig.Syntax.Block block ) {
         //return new Paragraph( new Run( block.GetType().FullName ) );
         if ( block is Markdig.Syntax.HeadingBlock h ) {
            var result = BuildInline( h.Inline );
            result.FontSize = 19 - h.Level;
            result.FontWeight = FontWeights.Bold;
            return new Paragraph( result );
         } else if ( block is Markdig.Syntax.ParagraphBlock p ) {
            return WpfHelper.P( p.Inline.Select( BuildInline ) );
         } else if ( block is Markdig.Syntax.ListBlock l ) {
            var result = new List();
            foreach ( var item in l ) {
               var li = new ListItem();
               li.Blocks.Add( BuildBlock( item ) );
               result.ListItems.Add( li );
            }
            result.MarkerStyle = l.IsOrdered ? TextMarkerStyle.Decimal : TextMarkerStyle.Disc;
            return result;
         } else if ( block is Markdig.Syntax.CodeBlock code ) {
            var result = new Paragraph();
            foreach ( var e in code.Lines ) result.Inlines.Add( new Run( e.ToString() + "\n" ) );
            result.FontFamily = new FontFamily( "Consolas, Courier New, Monospace" );
            return result;
         } else if ( block is Markdig.Syntax.ContainerBlock blk ) {
            var result = new Section();
            foreach ( var e in blk ) result.Blocks.Add( BuildBlock( e ) );
            return result;
         } else
            return new Paragraph( new Run( "[" + block.ToString() + "]" ) );
      }

      private static Inline BuildInline ( Markdig.Syntax.Inlines.Inline inline ) {
         if ( inline is Markdig.Syntax.Inlines.LiteralInline str ) {
            return new Run( str.Content.ToString() );
         } else if ( inline is Markdig.Syntax.Inlines.LinkInline link ) {
            var lnk = new Hyperlink();
            lnk.Inlines.AddRange( link.Select( BuildInline ) );
            lnk.NavigateUri = new Uri( link.Url );
            return lnk;
         } else if ( inline is Markdig.Syntax.Inlines.CodeInline code ) {
            var result = new Run( code.Content );
            result.FontFamily = new FontFamily( "Consolas, Courier New, Monospace" );
            return result;
         } else if ( inline is Markdig.Syntax.Inlines.ContainerInline grp ) {
            var result = new Span();
            result.Inlines.AddRange( grp.Select( BuildInline ) );
            if ( inline is Markdig.Syntax.Inlines.EmphasisInline em ) {
               if ( em.DelimiterCount >= 2 ) result.FontWeight = FontWeights.Bold;
               if ( em.DelimiterCount % 2 > 0 ) result.FontStyle = FontStyles.Italic;
            }
            return result;
         //} else if ( inline is Markdig.Extensions.TaskLists.TaskList task ) {
         //   return new Run( task.Checked ? "☑" : "☐" ); // Inconsistent glyph size; disabled from MarkdownPipelineBuilder
         } else if ( inline is Markdig.Syntax.Inlines.LineBreakInline ) {
            return new LineBreak();
         } else if ( inline is Markdig.Syntax.Inlines.HtmlInline html ) {
            switch ( html.Tag.ToLowerInvariant().Replace( " ", "" ) ) {
               case "<br>" : case "<br/>" :
                  return new LineBreak();
               default :
                  return new Run( html.Tag );
            }
         } else
            return new Run( "{" + inline.ToString() + "}" );
      }
   }

   public class BBCodeConverter {
      private static Regex regTagS = new Regex( "(?=\\[/?(?:[a-z]+|h[1-6]?|\\*)(?:=[^]]*)?\\]|\b(?:https?|email|tel|s?ftp)://[^\\s]+)|(?<=\\])", RegexOptions.IgnoreCase );
      private static Regex regTagM = new Regex( "^\\[(/)?([a-z]+|h[1-6]?|\\*)(=[^]]*)?\\]$|^(https?|email|tel|s?ftp)://[^\\s]+$", RegexOptions.IgnoreCase );

      public IEnumerable< Block > Convert ( string text )
         => Parse( text ).ToTree().Select( e => e.ToBlock() );

      public BBCode Parse ( string code ) {
         stack.Clear();
         tokens = regTagS.Split( code );
         pos = -1;
         advance();
         return Parse( new BBCode() );
      }

      public BBCode Parse ( BBCode code ) {
         var autoClose = BBCode.IsAutoClose( code.tag );
         while ( ! eof ) {
            string token = peek;
            if ( autoClose && code.tag.Equals( token.Substring( 1, token.Length - 2 ), StringComparison.OrdinalIgnoreCase ) )
               token = "[/" + code.tag + "]";
            else
               advance();
            var parts = regTagM.Match( token )?.Groups;
            BBCode node = null;
            if ( parts[2].Success ) { // Tag
               var tag = parts[2].Value;
               if ( parts[1].Success ) { // Close tag
                  var ltag = tag.ToLowerInvariant();
                  while ( stack.Count > 0 && stack.Contains( ltag ) )
                     if ( stack.Pop() == ltag )
                        break;
                  return code;
               } else {
                  stack.Push( tag.ToLowerInvariant() );
                  node = Parse( new BBCode( tag, parts[3].Value ) );
               }
            } else if ( parts[4].Success ) // Plain url
               node = new BBCode( "url", token ) { children = new List<BBCode> { new BBCode( null, token ) } };
            if ( code.children == null ) code.children = new List<BBCode>();
            code.children.Add( node ?? new BBCode( null, token ) );
         }
         return code;
      }

      #region parser
      private string[] tokens;
      private int pos;
      private readonly Stack< string > stack = new Stack<string>();

      // Move to next (non-empty) token
      private bool eof => pos >= tokens.Length;
      private void advance () { do { ++pos; } while ( ! eof && string.IsNullOrEmpty( tokens[ pos ] ) ); }
      private string peek => eof ? null : tokens[ pos ];
      #endregion
   }

   public class BBCode {
      public readonly string tag;
      public readonly string param;
      public List<BBCode> children;

      public BBCode ( string text = null ) : this( null, text ) { }

      public BBCode ( string tag, string param ) {
         this.tag = tag;
         this.param = param;
      }

      public void Add ( params BBCode[] items ) {
         if ( children == null ) children = new List< BBCode >();
         children.AddRange( items );
      }

      public IEnumerable< BBCode > ToTree () {
         if ( tag != "" || children == null ) return new BBCode[]{ this };
         return children;
      }

      public Inline ToInline () {
         if ( tag == "" && children == null ) return new Run( param );
         var result = new Span();
         switch ( tag.ToLowerInvariant() ) {
            case "b" : result.FontWeight = FontWeights.Bold; break; 
            case "i" : result.FontStyle = FontStyles.Italic; break;
            case "s" : result.TextDecorations = TextDecorations.Strikethrough; break;
         }
         if ( children != null )
            foreach ( var item in children )
               result.Inlines.Add( item.ToInline() );
         return result;
      }

      public Block ToBlock () {
         return new Paragraph( ToInline() );
      }

      override public string ToString () {
         var hasTag = ! string.IsNullOrEmpty( tag );
         if ( ! hasTag && param != null ) return param;
         var buf = new StringBuilder();
         if ( hasTag ) {
            buf.Append( '[' ).Append( tag );
            if ( ! string.IsNullOrEmpty( param ) ) buf.Append( '=' ).Append( param );
            buf.Append( ']' );
         }
         if ( children != null )
            foreach ( var e in children )
               buf.Append( e.ToString() );
         if ( hasTag && ! IsAutoClose( tag ) )
            buf.Append( "[/" ).Append( tag ).Append( ']' );
         return buf.ToString();
      }

      public static bool IsAutoClose ( string tag ) {
         switch ( tag?.ToLowerInvariant() ) {
            case "*" : case "td" : case "tr" : return true;
         }
         return false;
      }

   }
}
