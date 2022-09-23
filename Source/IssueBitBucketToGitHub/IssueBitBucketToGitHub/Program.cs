using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ContentTypeTextNet.IssueBitBucketToGitHub
{
    internal class Program
    {
        public static Task Main()
        {
            var app = new Application();
            return app.ExecuteAsync();
        }
    }
}
