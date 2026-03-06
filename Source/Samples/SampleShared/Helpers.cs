using System;
using System.Collections.Specialized;
using System.Threading;
using Microsoft.Extensions.Logging;
using Serilog;

namespace SampleShared
{
    public static class Helpers
    {
        public static string ReadSetting(this NameValueCollection collection, string key)
        {
            return collection[key] ?? string.Empty;
        }

        public static ILoggerFactory CreateForSerilog()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddFilter(level => level >= LogLevel.Trace)
                        .AddSerilog();
                }
            );
            return loggerFactory;
        }

        /// <summary>
        /// Blocks until Ctrl+C is pressed, allowing using blocks to dispose gracefully.
        /// In .NET 8+, Console.ReadKey does not survive Ctrl+C — the process terminates
        /// before Dispose runs. This helper traps Ctrl+C and signals a wait handle instead.
        /// </summary>
        public static void WaitForCancelKeyPress()
        {
            var shutdownEvent = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true; // prevent immediate process termination
                Console.WriteLine("Shutting down...");
                shutdownEvent.Set();
            };
            Console.WriteLine("Processing messages - press Ctrl+C to stop");
            shutdownEvent.Wait();
        }
    }
}
