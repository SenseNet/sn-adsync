using SenseNet.Client;
using System;

namespace SyncAD2Portal
{
    internal class Program
    {
        public static string SiteUrl { get; set; }
        public static string Username { get; set; }
        public static string Password { get; set; }

        private static void Main(string[] args)
        {
            if (!ParseParameters(args))
            {
                Logger.WriteWarning(0, "ADSync process arguments are not correct.", new[] { "" });
                return;
            }

            // Client API initialization
            ClientContext.Initialize(new[] { new ServerContext { Url = SiteUrl, Username = Username, Password = Password } });
            ClientContext.Current.ChunkSizeInBytes = 419430;

            var directoryServices = new SyncAD2Portal(SiteUrl);
            directoryServices.SyncFromAdAsync().Wait();
        }

        private static bool ParseParameters(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.StartsWith("USERNAME:", StringComparison.OrdinalIgnoreCase))
                {
                    Username = GetParameterValue(arg);
                }
                else if (arg.StartsWith("PASSWORD:", StringComparison.OrdinalIgnoreCase))
                {
                    Password = GetParameterValue(arg);
                }
                else if (arg.StartsWith("DATA:", StringComparison.OrdinalIgnoreCase))
                {
                    var data = GetParameterValue(arg).Replace("\"\"", "\"");
                    dynamic taskData = JsonHelper.Deserialize(data);

                    SiteUrl = taskData.SiteUrl;
                }
            }

            return !string.IsNullOrEmpty(SiteUrl);
        }

        private static string GetParameterValue(string arg)
        {
            return arg.Substring(arg.IndexOf(":", StringComparison.Ordinal) + 1).TrimStart('\'', '"').TrimEnd('\'', '"');
        }
    }
}
