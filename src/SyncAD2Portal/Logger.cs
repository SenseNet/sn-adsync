using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SyncAD2Portal
{
    internal class Logger
    {
        internal static void WriteInformation(int eventId, string message)
        {
            LogWriteLine(String.Format("INFORMATION\tEventId:{0}: {1}", eventId, message));
        }

        internal static void WriteError(int eventId, string message, string[] adSyncLogCategory)
        {
            LogWriteLine(String.Format("ERROR\tEventId:{0}: {1}", eventId, message));
        }

        internal static void WriteWarning(int eventId, string message, string[] adSyncLogCategory)
        {
            LogWriteLine(String.Format("WARNING\tEventId:{0}: {1}", eventId, message));
        }

        internal static void WriteInformation(int eventId, string message, string[] adSyncLogCategory)
        {
            LogWriteLine(String.Format("INFORMATION\tEventId:{0}: {1}", eventId, message));
        }

        internal static void WriteVerbose(string message, string[] adSyncLogCategory)
        {
            LogWriteLine("verbose\t" + message);
        }

        internal static void WriteException(Exception ex, string[] adSyncLogCategory)
        {
            LogWriteLine(String.Format("EXCEPTION\tEventId:{0}: {1}", 0, ex));
        }

        // ================================================================================================================= Logger

        private static readonly string CR = Environment.NewLine;

        private static string __logFilePath;
        private static string LogFilePath
        {
            get
            {
                if (__logFilePath == null)
                    CreateLog(true);
                return __logFilePath;
            }
        }

        private static string _logFolder = null;
        public static string LogFolder
        {
            get
            {
                if (_logFolder == null)
                    _logFolder = AppDomain.CurrentDomain.BaseDirectory;
                return _logFolder;
            }
            set
            {
                if (!Directory.Exists(value))
                    Directory.CreateDirectory(value);
                _logFolder = value;
            }
        }

        private static bool _lineStart;
        private static readonly object _logSync = new object();

        public static void LogWrite(params object[] values)
        {
            lock (_logSync)
            {
                using (StreamWriter writer = OpenLog())
                {
                    WriteToLog(writer, values, false);
                }
            }
            _lineStart = false;
        }
        public static void LogWriteLine(params object[] values)
        {
            lock (_logSync)
            {
                using (StreamWriter writer = OpenLog())
                {
                    WriteToLog(writer, values, true);
                }
            }
            _lineStart = true;
        }
        public static void CreateLog(bool createNew)
        {
            _lineStart = true;
            __logFilePath = Path.Combine(LogFolder, "SyncAD2Portal_" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".log");
            if (!File.Exists(__logFilePath) || createNew)
            {
                using (var fs = new FileStream(__logFilePath, FileMode.Create))
                {
                    using (var wr = new StreamWriter(fs))
                    {
                        wr.WriteLine("Start: {0}", DateTime.UtcNow);
                        wr.WriteLine();
                    }
                }
            }
            else
            {
                LogWriteLine(CR, CR, "CONTINUING", CR, CR);
            }
        }
        private static StreamWriter OpenLog()
        {
            return new StreamWriter(LogFilePath, true);
        }
        private static void WriteToLog(StreamWriter writer, object[] values, bool newLine)
        {
            if (_lineStart)
            {
                writer.Write(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffff"));
                writer.Write("\t");
            }
            foreach (var value in values)
            {
                writer.Write(value);
            }
            if (newLine)
            {
                writer.WriteLine();
            }
        }

    }
}
