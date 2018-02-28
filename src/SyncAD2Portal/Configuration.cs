using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using SenseNet.Client;

namespace SyncAD2Portal
{
    public class SyncConfiguration
    {
        // ============================================================================== Serialized proprties

        public bool Enabled { get; set; }

        public int ParallelOperations
        {
            get { return Math.Min(100, Math.Max(1, _parallelOperations)); }
            set { _parallelOperations = value; }
        }

        public List<Server> Servers { get; set; }
        public List<SyncTree> SyncTrees { get; set; }
        public List<MappingDefinition> MappingDefinitions { get; set; }

        // ============================================================================== Computed properties

        protected static readonly string SettingsPath = "/Root/System/Settings/SyncAD2Portal.settings";
        protected static string _siteUrl = string.Empty;

        private static readonly object SyncLock = new object();
        private static SyncConfiguration _current;
        private int _parallelOperations;

        public int ContentPageSize = 20000;

        public static SyncConfiguration Current
        {
            get
            {
                if (_current == null)
                {
                    lock (SyncLock)
                    {
                        if (_current == null)
                            _current = LoadConfiguration().Result;
                    }
                }
                return _current;
            }
        }

        public static string[] ADRelatedContentTypes { get; set; }

        // ============================================================================== Static methods

        private static async Task<SyncConfiguration> LoadConfiguration()
        {
            try
            {
                dynamic settingsContent = Content.LoadAsync(SettingsPath).Result;
                if (settingsContent == null)
                    return null;

                string binaryUrl = _siteUrl.TrimEnd('/') + settingsContent.Binary.__mediaresource.media_src + 
                    "&includepasswords=true";

                var settingsText = await RESTCaller.GetResponseStringAsync(new Uri(binaryUrl));
                var config = JsonHelper.Deserialize<SyncConfiguration>(settingsText);

                // decrypt passwords and inject them back to the configuration
                foreach (var server in config.Servers.Where(server => server.LogonCredentials != null && !string.IsNullOrEmpty(server.LogonCredentials.Password)))
                {
                    var request = new ODataRequest
                    {
                        ActionName = "Decrypt",
                        Path = "Root",
                        IsCollectionRequest = false,
                        SiteUrl = _siteUrl
                    };

                    try
                    {
                        server.LogonCredentials.Password = await RESTCaller.GetResponseStringAsync(
                            request.GetUri(),
                            ClientContext.Current.Servers[0],
                            HttpMethod.Post,
                            JsonHelper.Serialize(new { text = server.LogonCredentials.Password }));
                    }
                    catch (ClientException cex)
                    {
                        AdLog.LogError("Error during password decryption. " + Common.FormatClientException(cex));
                    }
                    catch (Exception ex)
                    {
                        AdLog.LogException(ex);
                    }
                }

                // preload all AD-related content types from the server
                ADRelatedContentTypes = await LoadADRelatedContentTypes();

                return config;
            }
            catch (Exception ex)
            {
                AdLog.LogException(ex);
            }

            return null;
        }

        private static async Task<string[]> LoadADRelatedContentTypes()
        {
            // load base types for their path
            var baseTypes = await Content.QueryForAdminAsync("+TypeIs:ContentType +Name:(" + string.Join(" ", Common.ADRelatedBaseContentTypes) + ")", 
                new[] {"Id", "Name", "Path"});

            // load all types (base types and their children) for their name
            var baseTypePaths = baseTypes.Select(bt => "'" + bt.Path + "'"); // add quotes because of the query
            var allTypes = await Content.QueryForAdminAsync("+TypeIs:ContentType +InTree:(" + string.Join(" ", baseTypePaths) + ")",
                new[] { "Id", "Name", "Path" });

            return allTypes.Select(t => t.Name).OrderBy(t => t).ToArray();
        }

        public static void Initialize(string siteUrl)
        {
            _siteUrl = siteUrl;
            _current = null;
        }

        // ============================================================================== Instance methods

        /// <summary>
        /// Validates the configuration. Returns false only if it is absolutely 
        /// not possible to execute ad sync. Othervise only logs warnings.
        /// </summary>
        public bool Validate()
        {
            if (this.Servers.Count == 0)
                AdLog.LogWarning("No servers are configured.");
            if (this.SyncTrees.Count == 0)
                AdLog.LogWarning("No sync trees are configured.");

            // validate server properties
            var invalidServers = new List<Server>();
            foreach (var server in this.Servers)
            {
                if (string.IsNullOrEmpty(server.LdapServer))
                {
                    AdLog.LogWarning("LDAP server address is missing.");
                    invalidServers.Add(server);
                }
                if (!server.VerifyConnection())
                {
                    AdLog.LogWarning("LDAP server connection failed.");
                    invalidServers.Add(server);
                }
            }

            // remove incorrectly configured servers
            foreach (var server in invalidServers)
            {
                this.Servers.Remove(server);
            }

            // validate sync tree properties
            var invalidSyncTrees = new List<SyncTree>();
            foreach (var syncTree in this.SyncTrees)
            {
                if (string.IsNullOrEmpty(syncTree.BaseDn))
                {
                    AdLog.LogWarning(string.Format("Sync tree {0} has no AD path (base DN) configured.", string.IsNullOrEmpty(syncTree.PortalPath) ? syncTree.BaseDn : syncTree.PortalPath));
                    invalidSyncTrees.Add(syncTree);
                }
                if (string.IsNullOrEmpty(syncTree.PortalPath))
                {
                    AdLog.LogWarning(string.Format("Sync tree {0} has no portal path configured.", syncTree.BaseDn));
                    invalidSyncTrees.Add(syncTree);
                }
                if (syncTree.Server == null)
                {
                    AdLog.LogWarning(string.Format("Sync tree {0} has no valid server configured.", string.IsNullOrEmpty(syncTree.BaseDn) ? syncTree.PortalPath : syncTree.BaseDn));
                    invalidSyncTrees.Add(syncTree);
                }
            }

            // remove sync trees that are not possible to sync so that we do not have to deal with errors later
            foreach (var syncTree in invalidSyncTrees)
            {
                this.SyncTrees.Remove(syncTree);
            }

            if (this.Servers.Count == 0 || this.SyncTrees.Count == 0)
                return false;

            return true;
        }
    }
}
