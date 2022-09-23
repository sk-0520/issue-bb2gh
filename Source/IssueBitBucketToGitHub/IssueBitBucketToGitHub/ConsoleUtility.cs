using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Octokit;

namespace ContentTypeTextNet.IssueBitBucketToGitHub
{
    public static class ConsoleUtility
    {
        #region define

        enum LogLevel
        {
            Trace,
            Debug,
            Information,
            Warning,
            Error,
        }

        #endregion

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

        public static void Title(string title)
        {
            Console.WriteLine("========================");
            Console.WriteLine("[{0}]", title);
            Console.WriteLine("========================");
        }

        public static void Subject(string title)
        {
            Console.WriteLine("------------------------");
            Console.WriteLine("{0}", title);
        }

        private static void Log(LogLevel logLevel, string message)
        {
            Console.WriteLine("[{0}] {1}", logLevel, message);
        }

        public static void LogTrace(string message) => Log(LogLevel.Trace, message);
        public static void LogDebug(string message) => Log(LogLevel.Debug, message);
        public static void LogInformation(string message) => Log(LogLevel.Information, message);
        public static void LogWarning(string message) => Log(LogLevel.Warning, message);
        public static void LogError(string message) => Log(LogLevel.Error, message);

        #endregion
    }
}
