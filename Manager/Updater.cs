using System;
using System.Diagnostics;
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

      public Updater () {
         ServicePointManager.Expect100Continue = true;
         ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
      }

      private bool Checking;

      internal GithubRelease FindUpdate ( Version update_from ) { try {
         lock ( RELEASE ) {
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
               json = Tools.ReadText( request.GetResponse().GetResponseStream() );
               App.Log( json );
            }
         } catch ( WebException wex ) {
            App.Log( wex );
            return App.Log<GithubRelease>( Tools.ReadText( wex.Response.GetResponseStream() ), null );
         }

         if ( HasNewRelease( Json.Parse<GithubRelease[]>( json ), update_from, out GithubRelease release ) )
            return release;
         return null;
      } catch ( Exception ex ) {
         return App.Log<GithubRelease>( ex, null );
      } finally {
         lock( RELEASE ) { Checking = false; }
      } }

      private bool HasNewRelease ( GithubRelease[] releases, Version update_from, out GithubRelease release ) {
         release = null;
         App.Log( $"Found {releases?.Length} releases." );
         if ( releases == null || releases.Length <= 0 ) return false;
         bool isDevChannel = App.Settings?.UpdateChannel == "dev";
         foreach ( var e in releases ) try {
            App.Log( $"{e.Tag_Name} ({(e.Prerelease?"Prerelease":"Production")}) {e.Assets?.Length??0} asset(s)" );
            if ( String.IsNullOrWhiteSpace( e.Tag_Name ) || e.Tag_Name[0] != 'v' ) continue;
            if ( e.Assets == null || e.Assets.Length == 0 ) continue;
            if ( ! isDevChannel && e.Prerelease ) continue;
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
   }
}
