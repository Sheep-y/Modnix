using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sheepy.Logging;
using Sheepy.Modnix;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Sheepy.Logging.Tests {

   [TestClass()]
   public class ModJsonTest {
      [TestMethod()] public void AppVerTest () {

         var appver = ModMetaJson.Parse<AppVer>( @"""text""" );
         Assert.IsNotNull( appver, "string => AppVer is not null" );
         Assert.IsTrue( appver.Id == "text", "string => AppVer id"  );

         appver = ModMetaJson.Parse<AppVer>( @"{""id"":""simple""}" );
         Assert.IsNotNull( appver, "simple => AppVer is not null" );
         Assert.IsTrue( appver.Id == "simple", "simple => AppVer id"  );
      }
   }

}