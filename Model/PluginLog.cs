using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;

namespace RATrendCompensatorNINA.Model {
    internal static class PluginLog {
        private static readonly object Sync = new object();
        private static readonly string LogFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NINA",
            "Logs",
            "RATrendCompensatorNINA.log");
        private static int startupHeaderWritten;

        public static void WriteStartupHeader() {
            if (Interlocked.Exchange(ref startupHeaderWritten, 1) != 0) return;

            var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown";
            Write("Startup", $"Plugin startup. Version={version}");
        }

        public static void Write(string source, string message) {
            var line = $"[RATrendCompensatorNINA][{source}] {DateTime.Now:O} {message}";
            Trace.WriteLine(line);

            try {
                var logDirectory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(logDirectory)) {
                    Directory.CreateDirectory(logDirectory);
                }

                lock (Sync) {
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
            } catch {
                // Do not allow diagnostics to break plugin execution.
            }
        }
    }
}
