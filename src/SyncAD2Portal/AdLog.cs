using System;
using System.Text;
using System.Collections.Concurrent;
using System.Threading;
using SenseNet.Diagnostics;

namespace SyncAD2Portal
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

        public static readonly string[] AdSyncLogCategory = new[] { "AdSync" };

        /* ==================================================================================== Properties */

        private static int __errors;
        private static int __objectErrors;
        internal static int Errors
        {
            get 
            {
                return __errors;
            }
        }
        internal static int ObjectErrors
        {
            get
            {
                return __objectErrors;
            }
        }
        private static void IncreaseError()
        {
            Interlocked.Increment(ref __errors);
        }
        internal static void IncreaseADObjectError()
        {
            Interlocked.Increment(ref __objectErrors);
        }

        private static int __warnings;
        private static int Warnings
        {
            get
            {
                return __warnings;
            }
        }
        private static void IncreaseWarning()
        {
            Interlocked.Increment(ref __warnings);
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
            LogLine(string.Format("AD sync finished with {0} warnings, {1} errors", Warnings, Errors), EventType.Info);
        }
        private static void LogLine(string msg, EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Error:
                    Logger.WriteError(EventId.NotDefined, msg, AdSyncLogCategory);
                    break;
                case EventType.Warning:
                    SnLog.WriteWarning(msg, categories: AdSyncLogCategory);
                    break;
                case EventType.Info:
                    SnLog.WriteInformation(msg, categories: AdSyncLogCategory);
                    break;
                case EventType.Verbose:
                    Logger.WriteVerbose(string.Format("  {0}", msg), AdSyncLogCategory);
                    break;
            }

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
        public static void LogMainActivity(string msg, string ADPath, string portalPath)
        {
            LogMain(string.Format("{0} ({1} --> {2})", msg, ADPath, portalPath));
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
        public static void LogErrorADObject(string msg, string obj)
        {
            LogError(string.Format("{0} (AD object: {1})", msg, obj));
            IncreaseADObjectError();
        }
        public static void LogErrorPortalObject(string msg, string obj)
        {
            LogError(string.Format("{0} (Portal object: {1})", msg, obj));
            IncreaseADObjectError();
        }
        public static void LogErrorObjects(string msg, string ADobj, string portalObj)
        {
            LogError(string.Format("{0} (AD object: {1}; portal object: {2})", msg, ADobj, portalObj));
            IncreaseADObjectError();
        }
        public static void LogADObject(string msg, string obj)
        {
            Log(string.Format("{0} (AD object: {1})", msg, obj));
        }
        public static void LogPortalObject(string msg, string obj)
        {
            Log(string.Format("{0} (Portal object: {1})", msg, obj));
        }
        public static void LogOuterADObject(string msg, string obj)
        {
            LogOuter(string.Format("{0} (AD object: {1})", msg, obj));
        }
        public static void LogObjects(string msg, string ADobj, string portalObj)
        {
            Log(string.Format("{0} (AD object: {1}; portal object: {2})", msg, ADobj, portalObj));
        }
        public static void LogException(Exception ex)
        {
            SnLog.WriteException(ex, categories: AdSyncLogCategory);

            // log event for subscriber of the current thread
            StringBuilder sb;
            if (Subscribers.TryGetValue(Thread.CurrentThread.GetHashCode(), out sb))
            {
                if (sb != null)
                {
                    sb.AppendLine(string.Format("ERROR - exception: {0}", ex.Message));
                    sb.AppendLine(ex.StackTrace);
                    if (ex.InnerException != null)
                        sb.AppendLine(ex.InnerException.Message);
                }
            }

            IncreaseError();
        }
    }
}
