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
         Assert.AreEqual( "text", appver.Id, "string => id"  );
         Assert.IsNull( appver.Min, "string => no min"  );
         Assert.IsNull( appver.Max, "string => no max"  );

         appver = ModMetaJson.Parse<AppVer>( @"{ id: ""simple"" }" );
         Assert.IsNotNull( appver, "simple => not null" );
         Assert.AreEqual( "simple", appver.Id, "simple => id" );
         Assert.IsNull( appver.Min, "string => no min"  );
         Assert.IsNull( appver.Max, "string => no max"  );

         appver = ModMetaJson.Parse<AppVer>( @"/*A*/ { /*B*/ Id : /*C*/ ""full"" /*D*/, /*E*/ Min: 1, Max: 2.1, NonExist: 12 /*F*/ } /*G*/" );
         Assert.IsNotNull( appver, "full => AppVer is not null" );
         Assert.AreEqual( "full", appver.Id, "full => AppVer id"  );
         Assert.AreEqual( "1", appver.Min, "full => AppVer min"  );
         Assert.AreEqual( "2.1", appver.Max, "full => AppVer max"  );
      }
   }

}