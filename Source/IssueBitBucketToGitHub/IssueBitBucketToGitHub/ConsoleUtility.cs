using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ContentTypeTextNet.IssueBitBucketToGitHub
{
    public static class ConsoleUtility
    {
        #region property

        public static int RetryCount { get; set; } = 3;

        #endregion

        #region function

        public static string ReadRetry(string subject, string defaultValue, int retryCount)
        {
            for(var i = 0; i < retryCount; i++) {
                Console.Write("{0}({1}/{2}) [{3}]: ", subject, i + 1, retryCount, defaultValue);
                var s = Console.ReadLine();
                if(string.IsNullOrWhiteSpace(s)) {
                    if(!string.IsNullOrEmpty(defaultValue)) {
                        return defaultValue;
                    }
                } else {
                    return s;
                }
            }

            throw new InvalidOperationException(subject);
        }

        public static string ReadRetryDefault(string subject, string defaultValue) => ReadRetry(subject, defaultValue, RetryCount);


        #endregion
    }
}
