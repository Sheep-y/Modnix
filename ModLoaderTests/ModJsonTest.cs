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
         Assert.IsNotNull( appver, "string => not null" );
         Assert.IsTrue( appver.Id == "text", "string => id"  );
         Assert.IsNull( appver.Min, "string => no min"  );
         Assert.IsNull( appver.Max, "string => no max"  );

         appver = ModMetaJson.Parse<AppVer>( @"{ id: ""simple"" }" );
         Assert.IsNotNull( appver, "simple => not null" );
         Assert.IsTrue( appver.Id == "simple", "simple => id" );
         Assert.IsNull( appver.Min, "string => no min"  );
         Assert.IsNull( appver.Max, "string => no max"  );

         appver = ModMetaJson.Parse<AppVer>( @"/*A*/ { /*B*/ Id : /*C*/ ""full"" /*D*/, /*E*/ Min: 1, Max: ""2.0a"", NonExist: 12 /*F*/ } /*G*/" );
         Assert.IsNotNull( appver, "full => AppVer is not null" );
         Assert.IsTrue( appver.Id == "full", "full => AppVer id"  );
         Assert.IsTrue( appver.Min == "1", "full => AppVer min"  );
         Assert.IsTrue( appver.Max == "2.0a", "full => AppVer max"  );
      }
   }

}