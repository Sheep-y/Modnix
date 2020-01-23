using System;
using System.IO;

namespace Modnix
{
    internal static class Logger
    {
        internal static string LogPath { get; set; }

        internal static void LogException(string message, Exception e)
        {
            using (var logWriter = File.AppendText(LogPath))
            {
                logWriter.WriteLine(message);
                logWriter.WriteLine(e.ToString());
            }
        }

        internal static void Log(string message, params object[] formatObjects)
        {
            if (string.IsNullOrEmpty(LogPath)) return;
            using (var logWriter = File.AppendText(LogPath))
            {
                logWriter.WriteLine(message, formatObjects);
            }
        }

        internal static void LogWithDate(string message, params object[] formatObjects)
        {
            if (string.IsNullOrEmpty(LogPath)) return;
            using (var logWriter = File.AppendText(LogPath))
            {
                logWriter.WriteLine(DateTime.Now.ToLongTimeString() + " - " + message, formatObjects);
            }
        }
    }
}
