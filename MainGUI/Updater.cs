using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace Sheepy.Modnix.MainGUI {

   public class GithubRelease {
      public string Tag_Name;
      public string Html_Url;
      public bool Prerelease = true;
      public GithubAsset[] Assets;
   }

   public class GithubAsset {
      public string Name;
      public string State;
      public long   Size;
      public string Browser_Download_Url;
   }

   internal class Updater {
      private const string RELEASE  = "https://api.github.com/repos/Sheep-y/Modnix/releases";

      private readonly AppControl App = AppControl.Instance;
      private JsonSerializerSettings JsonOptions;

      internal Updater () {
         ServicePointManager.Expect100Continue = true;
         ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
         JsonOptions = new JsonSerializerSettings() {
            DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate,
            Error = ( sender, err ) => App.Log( err ),
            MissingMemberHandling = MissingMemberHandling.Ignore,
            TraceWriter = new JsonLogger() { LevelFilter = TraceLevel.Info }
         };
      }

      private bool Checking;

      internal GithubRelease FindUpdate ( Version update_from ) { try {
         lock ( App ) {
            if ( Checking ) return null;
            Checking = true;
         }
         App.Log( $"Checking update from {RELEASE}" );
         HttpWebRequest request = WebRequest.Create( new Uri( RELEASE ) ) as HttpWebRequest;
         if ( request == null )
            return App.Log<GithubRelease>( "WebRequest is not HttpWebRequest", null );
         request.Credentials = CredentialCache.DefaultCredentials;
         request.UserAgent = $"{AppControl.LIVE_NAME}-Updater/{App.CheckAppVer()}";
         request.Accept = "application/vnd.github.v3+json";

         string json = null;
         try {
            using ( WebResponse response = request.GetResponse() ) {
               json = ReadAsString( request.GetResponse() );
               App.Log( json );
            }
         } catch ( WebException wex ) {
            App.Log( wex );
            return App.Log<GithubRelease>( ReadAsString( wex.Response ), null );
         }

         if ( HasNewRelease( JsonConvert.DeserializeObject<GithubRelease[]>( json, JsonOptions ), update_from, out GithubRelease release ) ) 
            return release;
         return null;
      } catch ( Exception ex ) {
         return App.Log<GithubRelease>( ex, null );
      } finally {
         lock( App ) { Checking = false; }
      } }

      private bool HasNewRelease ( GithubRelease[] releases, Version update_from, out GithubRelease release ) {
         release = null;
         App.Log( $"Found {releases?.Length} releases." );
         if ( releases == null || releases.Length <= 0 ) return false;
         bool isDevChannel = App.ModBridge.GetSettings()?.UpdateChannel == "dev";
         foreach ( var e in releases ) try {
            App.Log( $"{e.Tag_Name} ({(e.Prerelease?"Prerelease":"Production")}) {e.Assets?.Length??0} asset(s)" );
            if ( String.IsNullOrWhiteSpace( e.Tag_Name ) || e.Tag_Name[0] != 'v' ) continue;
            if ( e.Assets == null || e.Assets.Length <= 0 ) continue;
            if ( isDevChannel && e.Prerelease ) continue;
            Version eVer = Version.Parse( e.Tag_Name.Substring( 1 ) );
            if ( eVer <= update_from ) continue;
            foreach ( var a in e.Assets ) {
               App.Log( $"{a.Name} {a.State} {a.Size} bytes {a.Browser_Download_Url}" );
               if ( a.State == "uploaded" && a.Name.EndsWith( ".exe", StringComparison.OrdinalIgnoreCase ) ) {
                  e.Assets = new GithubAsset[] { a };
                  release = e;
                  return true;
               }
            }
         } catch ( Exception ex ) { App.Log( ex ); } 
         return false;
      }

      private static string ReadAsString ( WebResponse response ) {
         using ( StreamReader reader = new StreamReader( response.GetResponseStream() ) ) {
            return reader.ReadToEnd ();
         }
      }
   }

   internal class JsonLogger : ITraceWriter {
      internal AppControl App = AppControl.Instance;
      public TraceLevel LevelFilter { get; set; }
      public void Trace ( TraceLevel level, string message, Exception ex ) {
         if ( level > LevelFilter ) return;
         App.Log( message ?? ex?.ToString() );
      }
   }
}
