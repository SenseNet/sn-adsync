using System;
using System.Text;
using SenseNet.Diagnostics;
using System.Collections.Concurrent;
using System.Threading;

namespace SenseNet.DirectoryServices
{

    public static class AdLog
    {
        private enum EventType
        {
            Info,
            Error,
            Warning,
            Verbose
        }

        /* ==================================================================================== Subscriptions */
        private static readonly object _subscriberSync = new object();
        // ReSharper disable once InconsistentNaming
        private static ConcurrentDictionary<int,StringBuilder> __subscribers;
        private static ConcurrentDictionary<int, StringBuilder> Subscribers
        {
            get
            {
                if (__subscribers == null)
                {
                    lock (_subscriberSync)
                    {
                        if (__subscribers == null)
                        {
                            __subscribers = new ConcurrentDictionary<int, StringBuilder>();
                        }
                    }
                }
                return __subscribers;
            }
        }
        public static int SubscribeToLog()
        {
            // get current thread id, and add id-stringbuilder pair to the subscriber list.
            // subscribers this way will only see adsync events that occurred on their own thread.
            var threadid = Thread.CurrentThread.GetHashCode();
            var sb = new StringBuilder();
            sb.AppendLine(GetMsgWithTimeStamp("AD sync started."));
            Subscribers.TryAdd(threadid, sb);
            return threadid;
        }
        public static string GetLogAndRemoveSubscription(int id)
        {
            StringBuilder sb;
            Subscribers.TryRemove(id, out sb);
            sb.AppendLine(GetMsgWithTimeStamp("AD sync finished."));
            return sb.ToString();
        }


        /* ==================================================================================== Consts */
        public const string AdSync = "AdSync";
        public static readonly string[] AdSyncLogCategory = new[] { AdSync };


        /* ==================================================================================== Properties */
        private static readonly object ErrorsSync = new object();
        // ReSharper disable once InconsistentNaming
        private static int __errors;
        private static int Errors
        {
            get 
            {
                lock (ErrorsSync)
                {
                    return __errors;
                }
            }
        }
        private static void IncreaseError()
        {
            lock (ErrorsSync)
            {
                __errors++;
            }
        }
        private static readonly object WarningSync = new object();
        // ReSharper disable once InconsistentNaming
        private static int __warnings;
        private static int Warnings
        {
            get
            {
                lock (WarningSync)
                {
                    return __warnings;
                }
            }
        }
        private static void IncreaseWarning()
        {
            lock (WarningSync)
            {
                __warnings++;
            }
        }


        /* ==================================================================================== Methods */
        private static string GetMsgWithTimeStamp(string msg) 
        {
            return string.Format("{0}: {1}", DateTime.UtcNow.ToLongTimeString(), msg);
        }
        public static void StartLog()
        {
            LogLine("AD Sync started", EventType.Info);
        }
        public static void EndLog()
        {
            LogLine($"AD sync finished with {Warnings} warnings, {Errors} errors", EventType.Info);
        }
        private static void LogLine(string msg, EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Error:
                    SnLog.WriteError(msg, categories: AdSyncLogCategory);
                    break;
                case EventType.Warning:
                    SnLog.WriteWarning(msg, categories: AdSyncLogCategory);
                    break;
                case EventType.Info:
                    SnLog.WriteInformation(msg, categories: AdSyncLogCategory);
                    break;
                case EventType.Verbose:
                    SnTrace.Write("{0}: {1}", AdSync, msg);
                    break;
            }

            Console.WriteLine(msg);

            // log event for subscriber of the current thread
            StringBuilder sb;
            if (Subscribers.TryGetValue(Thread.CurrentThread.GetHashCode(), out sb))
            {
                if (sb != null)
                    sb.AppendLine(GetMsgWithTimeStamp(msg));
            }
        }
        public static void Log(string msg)
        {
            LogLine(string.Format("       {0}", msg), EventType.Verbose);
        }
        public static void LogOuter(string msg)
        {
            LogLine(string.Format("    {0}", msg), EventType.Verbose);
        }
        public static void LogMain(string msg)
        {
            LogLine(msg, EventType.Info);
        }
        public static void LogMainActivity(string msg, string adPath, string portalPath)
        {
            LogMain(string.Format("{0} ({1} --> {2})", msg, adPath, portalPath));
        }
        public static void LogError(string msg)
        {
            LogLine(string.Format("ERROR: {0}", msg), EventType.Error);
            IncreaseError();
        }
        public static void LogWarning(string msg)
        {
            LogLine(string.Format("WARNING: {0}", msg), EventType.Warning);
            IncreaseWarning();
        }
        // ReSharper disable once InconsistentNaming
        public static void LogErrorADObject(string msg, string obj)
        {
            LogError(string.Format("{0} (AD object: {1})", msg, obj));
        }
        public static void LogErrorPortalObject(string msg, string obj)
        {
            LogError(string.Format("{0} (Portal object: {1})", msg, obj));
        }
        public static void LogErrorObjects(string msg, string adObj, string portalObj)
        {
            LogError(string.Format("{0} (AD object: {1}; portal object: {2})", msg, adObj, portalObj));
        }
        // ReSharper disable once InconsistentNaming
        public static void LogADObject(string msg, string obj)
        {
            Log(string.Format("{0} (AD object: {1})", msg, obj));
        }
        public static void LogPortalObject(string msg, string obj)
        {
            Log(string.Format("{0} (Portal object: {1})", msg, obj));
        }
        // ReSharper disable once InconsistentNaming
        public static void LogOuterADObject(string msg, string obj)
        {
            LogOuter(string.Format("{0} (AD object: {1})", msg, obj));
        }
        public static void LogObjects(string msg, string adObj, string portalObj)
        {
            Log(string.Format("{0} (AD object: {1}; portal object: {2})", msg, adObj, portalObj));
        }
        public static void LogException(Exception ex)
        {
            SnLog.WriteException(ex, categories: AdSyncLogCategory);

            Console.WriteLine($"ERROR - exception: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            if (ex.InnerException != null)
                Console.WriteLine(ex.InnerException.Message);

            // log event for subscriber of the current thread
            StringBuilder sb;
            if (Subscribers.TryGetValue(Thread.CurrentThread.GetHashCode(), out sb))
            {
                if (sb != null)
                {
                    sb.AppendLine($"ERROR - exception: {ex.Message}");
                    sb.AppendLine(ex.StackTrace);
                    if (ex.InnerException != null)
                        sb.AppendLine(ex.InnerException.Message);
                }
            }

            IncreaseError();
        }
    }
}
