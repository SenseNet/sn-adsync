using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.Text;
using System.Linq;
using SenseNet.Client;
using System.Threading.Tasks;
using SenseNet.Tools;

namespace SyncAD2Portal
{
    public static class Common
    {
        public static readonly string[] ADRelatedBaseContentTypes = new[] { "ADFolder", "Domain", "User", "Group", "OrganizationalUnit" };
        public static readonly string[] Fields = new[] {"Id", "Name", "ParentId", "Path", "Type", "SyncGuid", "LastSync", "ModificationDate" };
        
        public static class PropertyNames
        {
            public static readonly string SyncGuid = "SyncGuid";
            public static readonly string LastSync = "LastSync";
            public static readonly string ModificationDate = "ModificationDate";
            public static readonly string ImageData = "ImageData";
        }

        public static class ADPropertyNames
        {
            public static readonly string ObjectClass = "objectClass";
            public static readonly string ThumbnailPhoto = "thumbnailPhoto";
            public static readonly string Member = "member";
            public static readonly string WhenChanged = "whenchanged";
        }

        public static class Paths
        {
            public static readonly string IMS = "/Root/IMS";
        }

        /* ==================================================================================== Static Methods */

        public static DirectoryEntry ConnectToAD(string ldapPath, Server server)
        {
            var deADConn = new DirectoryEntry(ldapPath);
            var credentials = server.LogonCredentials;

            if (credentials != null)
            {
                if (credentials.Anonymous)
                {
                    deADConn.AuthenticationType = AuthenticationTypes.Anonymous;
                }
                else if (!string.IsNullOrEmpty(credentials.Username))
                {
                    deADConn.AuthenticationType |= AuthenticationTypes.ServerBind;
                    deADConn.Username = credentials.Username;
                    deADConn.Password = credentials.Password;
                }
            }
            else
            {
                deADConn.AuthenticationType = AuthenticationTypes.Anonymous;
            }

            if (server.UseSsl)
                deADConn.AuthenticationType |= AuthenticationTypes.SecureSocketsLayer;

            //TODO: authentication: use SASL

            Exception exADConnectException = null;
            var bError = false;

            for (var i = 0; i < 3; i++)
            {
                try
                {
                    var oNativeObject = deADConn.NativeObject;
                    bError = false;
                    break;
                }
                catch (Exception ex)
                {
                    bError = true;
                    exADConnectException = ex;
                    System.Threading.Thread.Sleep(3000);
                }
            }

            if (bError)
            {
                AdLog.LogException(exADConnectException);
                throw new Exception("Connecting to AD server failed", exADConnectException);
            }

            // NOVELL - use a searcher to retrieve the objects' GUID 
            // - directoryentry properties does not include guid when connecting to Novell eDirectory
            if (server.Novell)
            {
                var dsDirSearcher = new DirectorySearcher(deADConn);
                dsDirSearcher.PropertiesToLoad.Add(server.GuidProperty);
                dsDirSearcher.SearchScope = SearchScope.Base;
                var result = dsDirSearcher.FindOne();
                var guid = result.Properties[server.GuidProperty][0];
                deADConn.Properties[server.GuidProperty].Add(guid);
            }

            return deADConn;
        }
        public static SearchResultCollection Search(DirectoryEntry searchRoot, string filter, bool novellSupport, string guidProp)
        {
            var dsDirSearcher = new DirectorySearcher(searchRoot)
            {
                Filter = filter,
                SizeLimit = 10000,
                PageSize = 10000
            };

            // Set the search filter

            // NOVELL - force searcher to retrieve the objects' GUID 
            // - this is not done by default when connecting to Novell eDirectory
            if (novellSupport)
                dsDirSearcher.PropertiesToLoad.Add(guidProp);

            try
            {
                return dsDirSearcher.FindAll();
            }
            catch (Exception e)
            {
                AdLog.LogException(e);
            }
            return null;
        }
        public static async Task EnsurePath(string path, string containerTypeName = null)
        {
            if (!await Content.ExistsAsync(path))
            {
                var parentPath = RepositoryPath.GetParentPath(path);

                // ensure parent
                await EnsurePath(parentPath, containerTypeName);

                var name = RepositoryPath.GetFileName(path);
                var folder = Content.CreateNew(parentPath, containerTypeName ?? "Folder", name);

                await folder.SaveAsync();
            }
        }

        public static Guid? GetGuid(byte[] byteArray)
        {
            if (byteArray == null)
                return null;

            if (byteArray.Length != 16)
                return null;

            return new Guid(byteArray);
        }
        public static Guid? GetADResultGuid(SearchResult result, string guidProp)
        {
            var props = result.Properties[guidProp];
            if ((props == null) || (props.Count < 1))
                return null;

            return GetGuid(props[0] as byte[]);
        }
        public static Guid? GetADObjectGuid(DirectoryEntry entry, string guidProp)
        {
            var props = (entry.Properties[guidProp]);
            if ((props == null) || (props.Count < 1))
                return null;

            return GetGuid(props[0] as byte[]);
        }
        public static Guid? GetPortalObjectGuid(dynamic content)
        {
            if(!IsADRelatedContent(content))
                return null;
            string guidStr = content.SyncGuid;
            if (string.IsNullOrEmpty(guidStr))
                return null;
            return new Guid(guidStr);
        }

        public static bool IsAccountDisabled(DirectoryEntry adUser, bool novellSupport)
        {
            // NOVELL - this property is not supported when connecting to Novell eDirectory
            // created users will always be enabled
            if (novellSupport)
                return false;

            int iFlagIndicator = (int)adUser.Properties["userAccountControl"].Value &
                                 Convert.ToInt32(ADAccountOptions.UF_ACCOUNTDISABLE);
            if (iFlagIndicator > 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public static bool IsADObjectUser(DirectoryEntry adObject, bool novellSupport)
        {
            // NOVELL - only objectClass is supported and person instead of user
            if (novellSupport)
                return adObject.Properties[ADPropertyNames.ObjectClass].ContainsValue("Person");

            if (adObject.Properties["objectCategory"].Value.ToString().ToLower().IndexOf("person", StringComparison.Ordinal) != -1)
                return adObject.Properties[ADPropertyNames.ObjectClass].ContainsValue("user");

            return false;
        }
        public static bool IsADObjectGroup(DirectoryEntry adObject)
        {
            return adObject.Properties[ADPropertyNames.ObjectClass].ContainsValue("group");
        }
        public static bool IsADObjectOrgUnit(DirectoryEntry adObject)
        {
            return adObject.Properties[ADPropertyNames.ObjectClass].ContainsValue("organizationalUnit");
        }
        public static bool IsADObjectOrganization(DirectoryEntry adObject)
        {
            return adObject.Properties[ADPropertyNames.ObjectClass].ContainsValue("Organization");
        }
        public static bool IsADObjectDomain(DirectoryEntry adObject)
        {
            return adObject.Properties[ADPropertyNames.ObjectClass].ContainsValue("domain");
        }
        public static bool IsADObjectContainer(DirectoryEntry adObject)
        {
            return adObject.Properties[ADPropertyNames.ObjectClass].ContainsValue("container");
        }
        public static bool IsADObjectBuiltinDomain(DirectoryEntry adObject)
        {
            return adObject.Properties[ADPropertyNames.ObjectClass].ContainsValue("builtinDomain");
        }
        public static ADObjectType GetADObjectType(DirectoryEntry entry, bool novellSupport)
        {
            if (IsADObjectUser(entry, novellSupport))
                return ADObjectType.User;
            if (IsADObjectGroup(entry))
                return ADObjectType.Group;
            if (IsADObjectOrgUnit(entry))
                return ADObjectType.OrgUnit;
            if (IsADObjectOrganization(entry))
                return ADObjectType.Organization;
            if (IsADObjectContainer(entry))
                return ADObjectType.Container;
            if (IsADObjectDomain(entry))
                return ADObjectType.Domain;
            if (IsADObjectBuiltinDomain(entry))
                return ADObjectType.Container;
            return ADObjectType.None;
        }
        public static bool IsADRelatedContent(dynamic content)
        {
            string contentTypeName = content.Type;

            return contentTypeName != null && SyncConfiguration.ADRelatedContentTypes.Contains(contentTypeName);
        }
        public static bool IsADObjectPathSynced(Guid guid, SyncTree syncTree)
        {
            // Check if the AD object corresponding to the given portal guid exists under any of the 
            // synchronized trees - if not, it should be deleted from the portal.
            var adPath = syncTree.GetADPath(guid.ToString());
            if (!string.IsNullOrEmpty(adPath))
            {
                if (SyncConfiguration.Current.SyncTrees.Any(t => t.ContainsADPath(adPath)))
                    return true;
            }

            return false;
        }
        public static string GetADObjectName(string name)
        {
            // gets the object name from the name as it comes from AD (ie: ExampleOrg from OU=ExampleOrg)
            return StripADName(name.Substring(name.IndexOf("=", StringComparison.Ordinal) + 1));
        }
        public static async Task UpdateChanges(Content content, Guid? guid)
        {
            // set the LastSync property of portal node indicating the date of the last synchronization
            if (guid.HasValue)
                content[PropertyNames.SyncGuid] = guid.Value.ToString();

            var syncDate = DateTime.UtcNow;

            content[PropertyNames.LastSync] = syncDate;
            content[PropertyNames.ModificationDate] = syncDate;

            await content.SaveAsync();
        }
        public static bool IsPortalObjectInvalid(dynamic content, ADSearchResult result, bool novellSupport)
        {
            // should the portal object be synchronized?
            // note: this is only to decide whether the properties/name of the object has changed
            //       moving of objects is carried out independently

            // NOVELL - objects are always synced, as there is no such property named "whenchanged"
            if (novellSupport)
                return true;

            var lastSyncDate = (DateTime)content.LastSync;
            lastSyncDate = TimeZoneInfo.ConvertTime(lastSyncDate, TimeZoneInfo.Utc);

            var contentModificationDate = (DateTime)content.ModificationDate;
            contentModificationDate = TimeZoneInfo.ConvertTime(contentModificationDate, TimeZoneInfo.Utc);

            // When comparing content modification date to last sync date, we have to add a small delta,
            // because modification date will always be a few ticks bigger than last sync date.
            return result.WhenChanged > lastSyncDate || contentModificationDate > lastSyncDate.AddSeconds(10);
        }
        public static void UpdatePortalUserCustomProperties(DirectoryEntry entry, dynamic content, SyncTree syncTree)
        {
            // sAMAccountName -> Name
            if (syncTree.Server.SyncUserName)
            {
                content.Name = entry.Properties[syncTree.UserNameProperty].Value.ToString();

                // in case of AD users the content name and login name are the same
                content.LoginName = content.Name;
            }

            // user actions
            foreach (var propMapping in syncTree.Mappings)
            {
                if (propMapping.AdProperties.Count == 1)
                {
                    if (propMapping.PortalProperties.Count == 1)
                    {
                        // 1 ADproperty + 1 portalproperty
                        var portalProp = propMapping.PortalProperties[0];
                        var adProp = propMapping.AdProperties[0];
                        var adValue = GetEntryValue(entry, adProp);
                        SetContentValue(content, portalProp, adValue, ADObjectType.User);

                        // Email is a special case: if it is empty, the user cannot be synced, at least we log it here.
                        if (string.CompareOrdinal(portalProp.Name, "Email") == 0 && string.IsNullOrEmpty(adValue))
                            AdLog.LogWarning("Email is empty for user " + entry.Path);
                    }
                    else
                    {
                        // 1 ADproperty + n portalproperty
                        // split AD value (preserving spaces) and put them into portal properties
                        var adProp = propMapping.AdProperties[0];
                        var adValues = GetEntryValue(entry, adProp).Split(new[] { propMapping.Separator }, StringSplitOptions.None);
                        int index = 0;
                        foreach (var portalProp in propMapping.PortalProperties)
                        {
                            var adValue = (index < adValues.Length) ? adValues[index] : null;
                            SetContentValue(content, portalProp, adValue, ADObjectType.User);
                            index++;
                        }
                    }
                }
                else
                {
                    // 1 portalproperty + n ADproperty
                    // concat AD property values and put it into the single portal property
                    var portalProp = propMapping.PortalProperties[0];
                    var adValue = propMapping.ConcatAdPropValues(entry);
                    SetContentValue(content, portalProp, adValue, ADObjectType.User);
                }
            }
        }
        public static void DisablePortalUserCustomProperties(Content content, SyncTree syncTree)
        {
            content.Name = content.Name.PrefixDeleted();

            foreach (var propMapping in syncTree.Mappings)
            {
                foreach (var portalProp in propMapping.PortalProperties)
                {
                    if (!portalProp.Unique)
                        continue;

                    var propValue = GetContentValue(content, portalProp) ?? string.Empty;
                    var setValue = propValue.PrefixDeleted();

                    SetContentValue(content, portalProp, setValue, ADObjectType.User);
                }
            }
        }
        public static string GetContentValue(Content content, SyncProperty portalProp)
        {
            var propValue = content[portalProp.Name];
            if (propValue == null)
                return null;
            return propValue.ToString();
        }
        public static void SetContentValue(Content content, SyncProperty portalProp, string value, ADObjectType type)
        {
            var propValue = value.MaximizeLength(portalProp.MaxLength);

            if (portalProp.Name == "Name")
            {
                content.Name = propValue;
                
                // in case of AD users the login name should be the same as the content name
                if (type == ADObjectType.User)
                    content["LoginName"] = propValue;
            }
            else
            {
                content[portalProp.Name] = propValue;
            }
        }

        public static string GetEntryValue(DirectoryEntry entry, SyncProperty adProp)
        {
            var propValColl = entry.Properties[adProp.Name];

            if (propValColl == null)
                return string.Empty;

            string value = null;
            if (propValColl.Count >= 1)
            {
                value = propValColl[0] as string;
            }
            return value ?? string.Empty;
        }
        public static async Task<IEnumerable<Content>> GetContainerUsers(string path)
        {
            return await Content.QueryForAdminAsync("+TypeIs:" + ADObjectType.User + " +InTree'" + path + "'");
        }
        public static string StripADName(string name)
        {
            // AD name may come in with ',' at the end ('ou=example,')
            name = name.Trim(',');

            var sb = new StringBuilder(name.Length);
            foreach (var t in name.Where(t => !RepositoryPath.IsInvalidNameChar(t)))
            {
                sb.Append(t);
            }
            return sb.ToString().TrimEnd('.').Trim();
        }

        public static SyncTree GetSyncTreeForObject(string objectPath)
        {
            return SyncConfiguration.Current.SyncTrees.FirstOrDefault(syncTree => syncTree.ContainsADPath(objectPath));
        }

        public static async Task<IEnumerable<Content>> QueryAllContent(ADObjectType objType, SyncTree syncTree, bool allFields = false)
        {
            return await QueryContentByTypeAndPath(objType, syncTree.PortalPath.TrimEnd('/'), allFields ? null : Common.Fields);
        }
        public static async Task<IEnumerable<Content>> QueryContentByTypeAndPath(ADObjectType objType, string startPath, string[] select = null, int skip = 0, int top = 0)
        {
            try
            {
                var types = GetContentTypeNames(objType);
                QuerySettings settings = null;
                if (skip > 0 || top > 0)
                {
                    settings = new QuerySettings
                    {
                        Skip = skip,
                        Top = top
                    };
                }

                return await Content.QueryForAdminAsync("+InTree:'" + startPath + "' +TypeIs:(" + string.Join(" ", types) + ") .SORT:Path", select, settings: settings);
            }
            catch (Exception ex)
            {
                AdLog.LogException(ex);
            }

            return new Content[0];
        }
        private static IEnumerable<string> GetContentTypeNames(ADObjectType type)
        {
            return (type == ADObjectType.AllContainers)
                ? new[] { GetContentTypeName(ADObjectType.OrgUnit), GetContentTypeName(ADObjectType.Container), GetContentTypeName(ADObjectType.Domain) }
                : new[] { GetContentTypeName(type) };
        }
        private static string GetContentTypeName(ADObjectType type)
        {
            switch (type)
            {
                case ADObjectType.User:
                    return "User";
                case ADObjectType.Group:
                    return "Group";
                case ADObjectType.OrgUnit:
                case ADObjectType.Organization:
                    return "OrganizationalUnit";
                case ADObjectType.Domain:
                    return "Domain";
                case ADObjectType.Container:
                    return "ADFolder";
                default:
                    throw new NotSupportedException("Unknown content type token: " + type);
            }
        }

        // ==================================================================================== Client tools

        private static readonly string[] _allowedExceptions = new[] { "LockedTreeException", "NodeIsOutOfDateException" };
        public static async Task RetryAsync(Func<Task> operation)
        {
            await Retrier.RetryAsync(5, 100, async () => await operation(), (i, ex) =>
            {
                // no error
                if (ex == null)
                    return true;

                // These exceptions (treelock, out of date, etc) are not real errors, we should retry the operation.
                var cex = ex as ClientException;
                if (cex != null && cex.ErrorData != null && cex.ErrorData.ExceptionType != null && _allowedExceptions.Contains(cex.ErrorData.ExceptionType))
                    return false;

                // unknown error
                throw ex;
            });
        }

        public static async Task<Content> LoadContentAsync(string guid)
        {
            return (await Content.QueryForAdminAsync(string.Format("+SyncGuid:'{0}'", guid), Common.Fields)).FirstOrDefault();
        }

        public static string FormatClientException(ClientException cex)
        {
            if (cex == null)
                return null;

            if (cex.ErrorData != null)
                return string.Format("Message:{0}, ErrorCode:{1}, ExceptionType:{2}, StatusCode:{3} {4}", cex.Message, 
                    cex.ErrorData.ErrorCode,
                    cex.ErrorData.ExceptionType,
                    cex.ErrorData.HttpStatusCode,
                    cex);

            return cex.ToString();
        }
    }
}
