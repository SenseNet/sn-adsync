using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.DirectoryServices;
using Newtonsoft.Json;
using SenseNet.Client;

namespace SyncAD2Portal
{
    public class SyncTree
    {
        private static readonly string UserFilterTemplate = "(&(objectCategory=Person)(objectClass=user)(cn=*))";
        private static readonly string UserFilterTemplateForNovell = "objectClass=Person";

        private static readonly string ContainerFilterTemplate = "(|(objectClass=Organization)(objectClass=organizationalUnit)(objectClass=domain)(objectClass=container)(objectClass=builtinDomain))";
        private static readonly string GroupFilterTemplate = "objectClass=group";

        // ========================================================================== Serialized properties

        [JsonProperty(PropertyName = "Server")]
        protected string ServerName { get; set; }

        [JsonProperty(PropertyName = "Mappings")]
        protected string MappingsDefinition { get; set; }
        
        public string BaseDn { get; set; }
        public string PortalPath { get; set; }
        public List<string> Exceptions { get; set; }

        public string UserFilter { get; set; }
        public string GroupFilter { get; set; }
        public string ContainerFilter { get; set; }
        public bool SyncGroups { get; set; }
        public bool SyncPhotos { get; set; }

        // ========================================================================== Computed properties

        private Server _server;
        [JsonIgnore]
        internal Server Server
        {
            get
            {
                return _server ?? (_server = SyncConfiguration.Current.Servers.FirstOrDefault(s => s.Name == ServerName));
            }
        }

        private List<Mapping> _mappings;
        [JsonIgnore]
        public List<Mapping> Mappings
        {
            get
            {
                if (_mappings == null)
                {
                    var mpng = SyncConfiguration.Current.MappingDefinitions.FirstOrDefault(m => m.Name == MappingsDefinition);
                    if (mpng != null)
                        _mappings = mpng.Mappings;
                }

                return _mappings;
            }
        }
        [JsonIgnore]
        public string UserNameProperty
        {
            get
            {
                foreach (var propMapping in this.Mappings)
                {
                    if (propMapping.PortalProperties[0].Name == "Name")
                        return propMapping.AdProperties[0].Name;
                }
                return "sAMAccountName";
            }
        }
        [JsonIgnore]
        public string ServerPath
        {
            get
            {
                return string.Format("LDAP://{0}{1}/", 
                    this.Server.LdapServer,
                    this.Server.Port > 0 ? ":" + this.Server.Port : string.Empty);
            }
        }
        [JsonIgnore]
        public ADSearchResult Root { get; private set; }
        [JsonIgnore]
        public string RootContentName { get { return RepositoryPath.GetFileName(this.PortalPath); } }

        private IEnumerable<ADSearchResult> _allADUsers;
        [JsonIgnore]
        public IEnumerable<ADSearchResult> AllADUsers
        {
            get
            {
                if (_allADUsers == null)
                {
                    if (this.Server == null)
                        return _allADUsers = new ADSearchResult[0];

                    var sFilter = this.Server.Novell 
                        ? ComputeFilter(UserFilterTemplateForNovell, UserFilter) 
                        : ComputeFilter(UserFilterTemplate, UserFilter);

                    using (var root = ConnectToObject(this.BaseDn))
                    {
                        // store results in a list to be able to work with it using the parallel API
                        // (SearchResultCollection is not thread safe)
                        using (var searchResults = Common.Search(root, sFilter, this.Server.Novell, this.Server.GuidProperty))
                        {
                            _allADUsers = searchResults.Cast<SearchResult>().Select(sr => sr.ToADSearchResult(Server)).ToList();
                        }
                    }
                }
                return _allADUsers;
            }
        }

        private IEnumerable<ADSearchResult> _allADContainers;
        [JsonIgnore]
        public IEnumerable<ADSearchResult> AllADContainers
        {
            get
            {
                if (_allADContainers == null)
                {
                    if (this.Server == null)
                        return _allADContainers = new ADSearchResult[0];

                    var sFilter = ComputeFilter(ContainerFilterTemplate, ContainerFilter);

                    using (var root = ConnectToObject(this.BaseDn))
                    {
                        this.Root = root.ToADSearchResult(this.Server);

                        using (var searchResults = Common.Search(root, sFilter, this.Server.Novell, this.Server.GuidProperty))
                        {
                            // store results in a list to be able to work with it using the parallel API
                            // (SearchResultCollection is not thread safe)
                            _allADContainers = searchResults.Cast<SearchResult>().Select(sr => sr.ToADSearchResult(Server)).ToList();
                        }
                    }
                }
                return _allADContainers;
            }
        }

        private IEnumerable<ADSearchResult> _allADGroups;
        [JsonIgnore]
        public IEnumerable<ADSearchResult> AllADGroups
        {
            get
            {
                if (_allADGroups == null)
                {
                    if (this.Server == null)
                        return _allADGroups = new ADSearchResult[0];

                    var sFilter = ComputeFilter(GroupFilterTemplate, GroupFilter);

                    using (var root = ConnectToObject(this.BaseDn))
                    {
                        // store results in a list to be able to work with it using the parallel API
                        // (SearchResultCollection is not thread safe)
                        using (var searchResults = Common.Search(root, sFilter, this.Server.Novell, this.Server.GuidProperty))
                        {
                             _allADGroups = searchResults.Cast<SearchResult>().Select(sr => sr.ToADSearchResult(Server)).ToList();
                        }
                    }
                }
                return _allADGroups;
            }
        }

        // =============================================================================== AD Methods

        public DirectoryEntry ConnectToObject(string objectPath)
        {
            var sLDAPPath = objectPath;

            if (!sLDAPPath.StartsWith("LDAP://") && !sLDAPPath.StartsWith("LDAPS://"))
            {
                sLDAPPath = string.Concat(this.ServerPath, sLDAPPath.Replace("/", "\\/"));
            }

            return Common.ConnectToAD(sLDAPPath, this.Server);
        }
        public bool IsADPathExcluded(string objectADPath)
        {
            return Exceptions != null && Exceptions.Where(e => !string.IsNullOrEmpty(e)).Any(objectADPath.EndsWith);
        }
        
        public bool ContainsADPath(string objectADPath)
        {
            // objectADPath: LDAP://192.168.0.75/OU=MyOrg,OU=OtherOrg,OU=ExampleOrg,DC=Nativ,DC=local

            // if the current synctree contains the given AD path
            return objectADPath.EndsWith(BaseDn) && !IsADPathExcluded(objectADPath);
        }

        public string GetPortalPath(string objectADPath)
        {
            // objectADPath: LDAP://192.168.0.75/OU=OtherOrg,OU=ExampleOrg,DC=Nativ,DC=local
            // BaseDn: "OU=ExampleOrg,DC=Nativ,DC=Local"
            // PortalPath: "/Root/IMS/ExampleOrg"

            if (!this.ContainsADPath(objectADPath))
                return null;

            // trim serverpath from beginning
            string path = objectADPath;
            if (path.StartsWith(ServerPath))
                path = path.Substring(ServerPath.Length, path.Length - this.ServerPath.Length); // OU=OtherOrg,OU=ExampleOrg,DC=Nativ,DC=local

            // trim adpath from end
            path = path.Substring(0, path.Length - this.BaseDn.Length); // OU=OtherOrg,

            var directories = path.Split(new[] { "OU=", "CN=", "ou=", "cn=" }, StringSplitOptions.RemoveEmptyEntries);
            var objectPortalPath = directories.Aggregate(string.Empty, (current, dir) => RepositoryPath.Combine(Common.StripADName(dir), current));

            // /Root/IMS/ExampleOrg/OtherOrg
            return RepositoryPath.Combine(this.PortalPath, objectPortalPath).TrimEnd('/');
        }

        public string GetPortalParentPath(string objectADPath)
        {
            var objectPath = GetPortalPath(objectADPath);
            if (objectPath == null)
                return null;

            // pl.: /Root/IMS/ExampleOrg
            return RepositoryPath.GetParentPath(objectPath);
        }

        public string GetADParentObjectPath(string objectADPath)
        {
            // objectADPath: LDAP://192.168.0.75/OU=OtherOrg,OU=ExampleOrg,DC=Nativ,DC=local

            var path = objectADPath;
            if (objectADPath.StartsWith(ServerPath))
                path = objectADPath.Substring(ServerPath.Length, objectADPath.Length - ServerPath.Length); // OU=OtherOrg,OU=ExampleOrg,DC=Nativ,DC=local

            var parentPath = path.Substring(path.IndexOf(",", StringComparison.Ordinal) + 1);

            // parent path: ExampleOrg,DC=Nativ,DC=local
            return parentPath;
        }

        private readonly object _guidLock = new object();
        private ReadOnlyDictionary<string, string> _guidPathDictionary;

        private ReadOnlyDictionary<string, string> GuidPathDictionary
        {
            get
            {
                // Collect all AD object path values and cache them by their AD guid. This collection
                // is needed because getting the guid from a searchresult is performed many times.
                if (_guidPathDictionary == null)
                {
                    lock (_guidLock)
                    {
                        if (_guidPathDictionary != null)
                            return _guidPathDictionary;

                        var localDict = AllADContainers
                            .Concat(AllADGroups)
                            .Concat(AllADUsers)
                            .Where(searchResult => searchResult.SyncGuid.HasValue)
                            .ToDictionary(searchResult => searchResult.SyncGuid.Value.ToString(), searchResult => searchResult.Path);

                        _guidPathDictionary = new ReadOnlyDictionary<string, string>(localDict);
                    }
                }

                return _guidPathDictionary;
            }
        }

        public string GetADPath(string guid)
        {
            string path;
            return GuidPathDictionary.TryGetValue(guid, out path) ? path : string.Empty;
        }

        // =============================================================================== Helper methods

        private static bool IsFilterSet(string filter)
        {
            return !string.IsNullOrEmpty(filter) && filter != "*";
        }

        private static string ComputeFilter(string template, string filter)
        {
            return IsFilterSet(filter) ? string.Format("(&({0})({1}))", template, filter) : template;
        }
    }
}
