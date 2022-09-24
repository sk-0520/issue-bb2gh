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

        private class ColorRestore: IDisposable
        {
            public ColorRestore(ConsoleColor foreground, ConsoleColor background)
            {
                Foreground = foreground;
                Background = background;
            }

            #region property

            private ConsoleColor Foreground { get; }
            private ConsoleColor Background { get; }

            #endregion

            #region IDisposable

            protected virtual void Dispose(bool disposing)
            {
                Console.ForegroundColor = Foreground;
                Console.BackgroundColor = Background;
            }

            // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
            // ~ColorRestore()
            // {
            //     // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
            //     Dispose(disposing: false);
            // }

            public void Dispose()
            {
                // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
            #endregion
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

        public static IDisposable ChangeColor(ConsoleColor foreground, ConsoleColor background)
        {
            var prev = new ColorRestore(Console.ForegroundColor, Console.BackgroundColor);

            Console.ForegroundColor = foreground;
            Console.BackgroundColor = background;

            return prev;
        }

        public static void Title(string title)
        {
            using var restore = ChangeColor(ConsoleColor.White, ConsoleColor.DarkYellow);

            Console.WriteLine("========================");
            Console.WriteLine("[{0}]", title);
            Console.WriteLine("========================");
        }

        public static void Subject(string title)
        {
            using var restore = ChangeColor(ConsoleColor.White, ConsoleColor.DarkGreen);

            Console.WriteLine("------------------------");
            Console.WriteLine("{0}", title);
        }

        private static void Log(LogLevel logLevel, string message)
        {
            using var restore = logLevel switch {
                LogLevel.Trace => ChangeColor(ConsoleColor.DarkGray, ConsoleColor.Black),
                LogLevel.Debug => ChangeColor(ConsoleColor.Gray, ConsoleColor.Black),
                LogLevel.Information => ChangeColor(ConsoleColor.White, ConsoleColor.Black),
                LogLevel.Warning => ChangeColor(ConsoleColor.Yellow, ConsoleColor.Black),
                LogLevel.Error => ChangeColor(ConsoleColor.Black, ConsoleColor.Red),
                _ => throw new NotImplementedException(),
            };

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
