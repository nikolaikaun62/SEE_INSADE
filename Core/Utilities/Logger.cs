using System;

namespace SEE_INSADE
{
    public static class Logger
    {
        public static void Log(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[SEE_INSADE] {message}");
        }

        public static void LogError(string error)
        {
            Log($"[ERROR] {error}");
        }
    }
}