using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.DirectoryServices;
using System.IO;
using System.Net;
using System.Runtime.Caching;
using System.Threading;
using SenseNet.Client;
using System.Threading.Tasks;
using SenseNet.TaskManagement.Core;

namespace SyncAD2Portal
{
    public class SyncAD2Portal
    {
        // ==================================================================================== Members

        private ConcurrentDictionary<string, int> _portalUsers;
        private ConcurrentDictionary<string, string> _portalContainers;
        private ConcurrentDictionary<string, int> _portalGroups;

        private readonly MemoryCache _contentCache;

        private static readonly int TIMER_INTERVAL = 3;
        private Timer _taskTimer;
        private readonly Stopwatch _stopWatch = new Stopwatch();

        private SnSubtask _subtask;
        private int _processedObjects;
        private int _syncedObjects;
        private int _syncedObjectsTotal;
        private int _maxObjects;
        private int _overallProgress;

        private bool _updating;
        private ConcurrentBag<Guid> _postponedObjects; 

        // ==================================================================================== Construction

        public SyncAD2Portal(string siteUrl)
        {
            SyncConfiguration.Initialize(siteUrl);

            // Use the default memory cache instance, because we do not need any other cache in the app
            _contentCache = MemoryCache.Default;
        }

        // ==================================================================================== AD -> portal : Methods

        private string GetADDomainName(DirectoryEntry entry)
        {
            // DC=Nativ,DC=Local    -->   NATIV
            // DC=Nativ,DC=local    --> /Root/IMS/NATIV
            var adDomainName = entry.Properties["distinguishedName"][0] as string ?? string.Empty;

            foreach (var syncTree in SyncConfiguration.Current.SyncTrees)
            {
                // if this synctree contains the domain
                if (syncTree.BaseDn.EndsWith(adDomainName))
                {
                    var imsFound = false;
                    foreach (var pathPart in syncTree.PortalPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (imsFound)
                            return pathPart;
                        if (pathPart == "IMS")
                            imsFound = true;
                    }
                }
            }
            return null;
        }

        private async Task CreateNewPortalContainer(DirectoryEntry entry, string parentPath, Guid guid, SyncTree syncTree)
        {
            switch (Common.GetADObjectType(entry, syncTree.Server.Novell))
            {
                case ADObjectType.Organization:
                case ADObjectType.OrgUnit:
                    await CreateNewPortalOrgUnit(entry, parentPath, guid, syncTree);
                    break;
                case ADObjectType.Container:
                    await CreateNewPortalFolder(entry, parentPath, guid, syncTree);
                    break;
                case ADObjectType.Domain:
                    await CreateNewPortalDomain(entry, parentPath, guid, syncTree);
                    break;
                default:
                    AdLog.LogErrorADObject("Unsupported AD object!", entry.Path);
                    break;
            }
        }
        private async Task CreateNewPortalDomain(DirectoryEntry entry, string parentPath, Guid guid, SyncTree syncTree)
        {
            var tryReload = false;

            try
            {
                AdLog.LogADObject(string.Format("New portal domain - creating under {0}", parentPath), entry.Path);
                var newDomain = Content.CreateNew(parentPath, "Domain", "??");

                await UpdatePortalDomainProperties(entry, newDomain, syncTree);
                await Common.UpdateChanges(newDomain, guid);

                _portalContainers.TryAdd(guid.ToString(), newDomain.Path);
            }
            catch (ClientException cex)
            {
                // if it already exist, we will simply reload it
                if (string.CompareOrdinal(cex.ErrorData.ExceptionType, "NodeAlreadyExistsException") == 0)
                    tryReload = true;
                else
                    AdLog.LogException(new Exception("Error during domain creation: " + entry.Path, cex));
            }
            catch (Exception ex)
            {
                AdLog.LogException(ex);
                AdLog.IncreaseADObjectError();
            }

            if (tryReload)
            {
                var content = await Common.LoadContentAsync(guid.ToString());
                if (content != null)
                    _portalContainers.TryAdd(guid.ToString(), content.Path);
            }
        }
        private async Task CreateNewPortalFolder(DirectoryEntry entry, string parentPath, Guid guid, SyncTree syncTree)
        {
            var tryReload = false;

            try
            {
                AdLog.LogADObject(string.Format("New portal folder - creating under {0}", parentPath), entry.Path);
                var newFolder = Content.CreateNew(parentPath, "ADFolder", "??");

                await UpdatePortalFolderProperties(entry, newFolder, syncTree);
                await Common.UpdateChanges(newFolder, guid);

                _portalContainers.TryAdd(guid.ToString(), newFolder.Path);
            }
            catch (ClientException cex)
            {
                // if it already exist, we will simply reload it
                if (string.CompareOrdinal(cex.ErrorData.ExceptionType, "NodeAlreadyExistsException") == 0)
                    tryReload = true;
                else
                    AdLog.LogException(new Exception("Error during folder creation: " + entry.Path, cex));
            }
            catch (Exception ex)
            {
                AdLog.LogException(ex);
            }

            if (tryReload)
            {
                var content = await Common.LoadContentAsync(guid.ToString());
                if (content != null)
                    _portalContainers.TryAdd(guid.ToString(), content.Path);
            }
        }
        private async Task CreateNewPortalOrgUnit(DirectoryEntry entry, string parentPath, Guid guid, SyncTree syncTree)
        {
            var tryReload = false;

            try
            {
                AdLog.LogADObject(string.Format("New portal orgunit - creating under {0}", parentPath), entry.Path);
                var newOrgUnit = Content.CreateNew(parentPath, "OrganizationalUnit", "??");

                await UpdatePortalOrgUnitProperties(entry, newOrgUnit, syncTree);
                await Common.UpdateChanges(newOrgUnit, guid);

                _portalContainers.TryAdd(guid.ToString(), newOrgUnit.Path);
            }
            catch (ClientException cex)
            {
                // if it already exist, we will simply reload it
                if (string.CompareOrdinal(cex.ErrorData.ExceptionType, "NodeAlreadyExistsException") == 0)
                    tryReload = true;
                else
                    AdLog.LogException(new Exception("Error during orgunit creation: " + entry.Path, cex));
            }
            catch (Exception ex)
            {
                AdLog.LogException(ex);
                AdLog.IncreaseADObjectError();
            }

            if (tryReload)
            {
                var content = await Common.LoadContentAsync(guid.ToString());
                if (content != null)
                    _portalContainers.TryAdd(guid.ToString(), content.Path);
            }
        }
        private async Task CreateNewPortalUser(DirectoryEntry entry, string parentPath, Guid guid, SyncTree syncTree)
        {
            try
            {
                AdLog.LogADObject(string.Format("New portal user - creating under {0}", parentPath), entry.Path);
                var newUser = Content.CreateNew(parentPath, syncTree.Server.UserType, "??");

                await UpdatePortalUserProperties(entry, newUser, syncTree);

                // this will save the content
                await Common.UpdateChanges(newUser, guid);
                
                if (syncTree.SyncPhotos)
                {
                    // if the upload really happened, we have to update sync date on the server
                    var uploaded = await UploadImage(entry, newUser);
                    if (uploaded)
                        await Common.UpdateChanges(newUser, guid);
                }

                _portalUsers.TryAdd(guid.ToString(), newUser.Id);
            }
            catch (Exception ex)
            {
                AdLog.LogException(new Exception("Error creating portal user for object " + entry.Path + ". Message: " + ex.Message, ex));
                AdLog.IncreaseADObjectError();
            }
        }
        private async Task CreateNewPortalGroup(DirectoryEntry entry, string parentPath, Guid guid, SyncTree syncTree)
        {
            try
            {
                AdLog.LogADObject(string.Format("New portal group - creating under {0}", parentPath), entry.Path);
                var newGroup = Content.CreateNew(parentPath, "Group", "??");

                await UpdatePortalGroupProperties(entry, newGroup, syncTree);
                await Common.UpdateChanges(newGroup, guid);

                _portalGroups.TryAdd(guid.ToString(), newGroup.Id);
            }
            catch (Exception ex)
            {
                AdLog.LogException(ex);
                AdLog.IncreaseADObjectError();
            }
        }

        private async Task UpdatePortalContainerProperties(DirectoryEntry entry, Content content, SyncTree syncTree)
        {
            switch (Common.GetADObjectType(entry, syncTree.Server.Novell))
            {
                case ADObjectType.OrgUnit:
                    await UpdatePortalOrgUnitProperties(entry, content, syncTree);
                    break;
                case ADObjectType.Container:
                   await  UpdatePortalFolderProperties(entry, content, syncTree);
                    break;
                case ADObjectType.Domain:
                    await UpdatePortalDomainProperties(entry, content, syncTree);
                    break;
            }
        }
        private Task UpdatePortalOrgUnitProperties(DirectoryEntry entry, Content content, SyncTree syncTree)
        {
            // If this is the root of the sync tree, we have to use the name provided
            // by the admin in the portal path of the sync tree. This is necessary
            // in cases when the AD and portal paths differ.
            content.Name = syncTree.Root != null && entry.Guid == syncTree.Root.SyncGuid 
                ? syncTree.RootContentName 
                : Common.GetADObjectName(entry.Name);

            return Task.FromResult<object>(null);
        }

        private Task UpdatePortalDomainProperties(DirectoryEntry entry, Content content, SyncTree syncTree)
        {
            content.Name = GetADDomainName(entry);

            return Task.FromResult<object>(null);
        }
        private Task UpdatePortalFolderProperties(DirectoryEntry entry, Content content, SyncTree syncTree)
        {
            content.Name = Common.GetADObjectName(entry.Name);

            return Task.FromResult<object>(null);
        }
        private Task UpdatePortalUserProperties(DirectoryEntry entry, dynamic content, SyncTree syncTree)
        {
            if (syncTree.Server.SyncEnabledState)
                content.Enabled = !Common.IsAccountDisabled(entry, syncTree.Server.Novell);

            Common.UpdatePortalUserCustomProperties(entry, content, syncTree);

            return Task.FromResult<object>(null);
        }
        private async Task UpdatePortalGroupProperties(DirectoryEntry entry, dynamic content, SyncTree syncTree)
        {
            if (content == null)
            {
                AdLog.LogWarning("Update group: Content parameter is empty. Path: " + entry.Path);
                return;
            }

            content.Name = Common.GetADObjectName(entry.Name);

            var originalMembers = content.Id == 0
                ? new Content[0]
                : (await Content.LoadReferencesAsync((int)content.Id, "Members", Common.Fields)).ToArray();

            var adMembers = GetADGroupMembers(entry, syncTree);
            var newMembers = new List<int>();
            var removeMembers = new List<int>();

            // add new members: 
            foreach (var guid in adMembers.Keys)
            {
                try
                {
                    var referredContentId = 0;
                    var guidStr = guid.ToString();

                    // see if we created and cached this member before
                    switch (adMembers[guid].objType)
                    {
                        case ADObjectType.User:
                            _portalUsers.TryGetValue(guidStr, out referredContentId);
                            break;
                        case ADObjectType.Group:
                            _portalGroups.TryGetValue(guidStr, out referredContentId);
                            break;
                    }

                    if (referredContentId == 0)
                    {
                        // we may have found a group that should be synced, but it is not synced yet.

                        if (!_updating)
                        {
                            // This is the first round: if the member should be synced, we will revisit 
                            // the parent (!) later to add this missing member.
                            if (Common.IsADObjectPathSynced(guid, syncTree))
                            {
                                AdLog.Log("Postponing group, because it contains a member that is not yet synced. Group: " + entry.Path + " Member: " + adMembers[guid].Path);
                                _postponedObjects.Add(entry.Guid);
                            }
                        }
                        else
                        {
                            // We are in the update phase. At this point all content should exist, 
                            // even if there is a circle in group memberships.
                            AdLog.LogWarning("Member does not exist in portal: " + adMembers[guid].Path);
                        }

                        continue;
                    }

                    // the referred content is already in the list
                    if (originalMembers.Any(n => n.Id == referredContentId))
                        continue;

                    switch (adMembers[guid].objType)
                    {
                        case ADObjectType.Group:
                        case ADObjectType.User:
                            newMembers.Add(referredContentId);
                            break;
                        default:
                            AdLog.LogErrorObjects("Member is neither a user nor a group", adMembers[guid].Path, referredContentId.ToString());
                            break;
                    }
                }
                catch
                {
                    AdLog.LogErrorADObject("Could not add member to group", adMembers[guid].Path);
                }
            }

            // remove old members
            // add portal group members to removeMembers list that have no corresponding AD objects
            foreach (dynamic member in originalMembers)
            {
                var guidStr = (string)member[Common.PropertyNames.SyncGuid];
                if (guidStr != null)
                {
                    if (!adMembers.Keys.Contains(new Guid(guidStr)))
                        removeMembers.Add(member.Id);
                }
                else
                {
                    AdLog.LogError(string.Format("Portal group contains unsynchronized object (group: {0}, object: {1}", content.Path, member.Path));
                }
            }

            // In case of new groups we set the Members property and save it later in one request
            // instead of sending the members in a separate request.
            if (content.Id == 0)
            {
                if (newMembers.Any())
                    content.Members = newMembers.ToArray();

                return;
            }

            if (newMembers.Any())
            {
                AdLog.Log(string.Format("Adding {0} new members to group {1}.", newMembers.Count, content.Path));
                await Group.AddMembersAsync(content.Id, newMembers.ToArray());
            }
            if (removeMembers.Any())
            {
                AdLog.Log(string.Format("Removing {0} members from group {1}.", removeMembers.Count, content.Path));
                await Group.RemoveMembersAsync(content.Id, removeMembers.ToArray());
            }
        }
        private async Task DeletePortalContainer(Content container, SyncTree syncTree)
        {
            var contentPath = container.Path;

            try
            {
                AdLog.LogPortalObject("Deleting portal container (orgunit/domain/folder)", contentPath);

                if (await Content.ExistsAsync(contentPath))
                {
                    var users = await Common.GetContainerUsers(contentPath);

                    // delete users first (move all users to the 'deleted' folder)
                    await users.ForEachAsync(SyncConfiguration.Current.ParallelOperations, async userContent =>
                    {
                        await DeletePortalUser(userContent, syncTree);
                    });

                    // delete container if empty
                    if (await Content.GetCountAsync(contentPath, "+TypeIs:User +InTree'" + contentPath + "' .AUTOFILTERS:OFF") == 0)
                    {
                        await Common.RetryAsync(async () => await container.DeleteAsync());
                    }
                    else
                    {
                        AdLog.LogErrorPortalObject("Portal container cannot be deleted, it contains users!", contentPath);
                    }
                }
            }
            catch (Exception ex)
            {
                AdLog.LogErrorADObject(GetErrorMessage(ex), contentPath);
            }
        }
        private async Task DeletePortalUser(Content user, SyncTree syncTree)
        {
            try
            {
                AdLog.LogPortalObject("Deleting (disabling) portal user", user.Path);

                user["Enabled"] = false;

                Common.DisablePortalUserCustomProperties(user, syncTree);

                await Common.RetryAsync(async () => await Common.UpdateChanges(user, null));
                await Common.RetryAsync(async () => await user.MoveToAsync(await syncTree.Server.GetDeletedPortalObjectsPath()));
            }
            catch (Exception ex)
            {
                AdLog.LogErrorADObject(GetErrorMessage(ex), user.Path);
            }
        }
        private async Task DeletePortalGroup(Content group, SyncTree syncTree)
        {
            try
            {
                AdLog.LogPortalObject("Deleting portal group", group.Path);

                await Common.RetryAsync(async () => await group.DeleteAsync());
            }
            catch (Exception ex)
            {
                AdLog.LogErrorADObject(GetErrorMessage(ex), group.Path);
            }
        }

        private async Task<bool> UploadImage(DirectoryEntry entry, Content user)
        {
            if (entry == null || user == null)
                return false;

            try
            {
                var property = entry.Properties[Common.ADPropertyNames.ThumbnailPhoto];
                if (property == null)
                    return false;

                var data = property.Value as byte[];
                if (data != null)
                {
                    AdLog.Log("Uploading image for user " + user.Path);
                    using (var imageStream = new MemoryStream(data))
                    {
                        await Content.UploadAsync(user.ParentPath, user.Name, imageStream, "User", Common.PropertyNames.ImageData);
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                AdLog.LogException(new Exception("Error during uploading image for user " + user.Path, ex));
            }

            return false;
        }

        private Dictionary<Guid, ADGroupMember> GetADGroupMembers(DirectoryEntry group, SyncTree syncTree)
        {
            var members = new Dictionary<Guid, ADGroupMember>();
            var memberCount = group.Properties[Common.ADPropertyNames.Member].Count;
            AdLog.LogADObject(string.Format("Group contains {0} member(s).", memberCount), group.Path);

            for (var i = 0; i < memberCount; i++)
            {
                var sMemberDN = group.Properties[Common.ADPropertyNames.Member][i].ToString();

                var objSyncTree = Common.GetSyncTreeForObject(sMemberDN);
                if (objSyncTree == null)
                {
                    AdLog.LogWarning(string.Format("AD group contains an object that is not contained in any of the synctrees, group's synctree will be used to retrieve the object (group: {0}, object: {1})", group.Path, sMemberDN));
                    objSyncTree = syncTree;
                }

                using (var oADMember = objSyncTree.ConnectToObject(sMemberDN))
                {
                    if (oADMember != null)
                    {
                        var guid = Common.GetADObjectGuid(oADMember, syncTree.Server.GuidProperty);
                        if (!guid.HasValue)
                            continue;

                        var userNameProp = oADMember.Properties[syncTree.UserNameProperty];
                        var userNameValue = userNameProp == null ? null : userNameProp.Value;
                        if (userNameValue == null)
                        {
                            AdLog.LogError(string.Format("Property {0} of AD group member \"{1}\" is missing or value is null", syncTree.UserNameProperty, sMemberDN));
                            continue;
                        }

                        members.Add(
                            guid.Value,
                            new ADGroupMember()
                            {
                                objType = Common.GetADObjectType(oADMember, syncTree.Server.Novell),
                                Path = oADMember.Path,
                                SamAccountName = userNameValue.ToString()
                            });
                    }
                    else
                    {
                        AdLog.LogWarning(string.Format("AD group member could not be retrieved (group: {0}, object: {1})", group.Path, sMemberDN));
                    }
                }
            }
            return members;
        }
        
        private async Task LoadAllPortalContainers()
        {
            _portalContainers = new ConcurrentDictionary<string, string>();

            var contentList = await Common.QueryContentByTypeAndPath(ADObjectType.AllContainers, Common.Paths.IMS, Common.Fields);

            foreach (var content in contentList.Cast<dynamic>().Where(c => !string.IsNullOrEmpty((string)c.SyncGuid)))
            {
                _contentCache.Set((string)content.SyncGuid, content, DateTimeOffset.Now.AddDays(1));
                _portalContainers.TryAdd((string)content.SyncGuid, (string)content.Path);
            }
        }
        private async Task LoadAllPortalUsers()
        {
            _portalUsers = new ConcurrentDictionary<string, int>();

            var contentList = await Common.QueryContentByTypeAndPath(ADObjectType.User, Common.Paths.IMS, Common.Fields);

            foreach (var content in contentList.Cast<dynamic>().Where(c => !string.IsNullOrEmpty((string)c.SyncGuid)))
                _portalUsers.TryAdd((string)content.SyncGuid, content.Id);
        }
        private async Task LoadPortalUsers(SyncTree syncTree, int skip = 0, int top = 0)
        {
            if (_portalUsers == null)
                _portalUsers = new ConcurrentDictionary<string, int>();

            var contentList = await Common.QueryContentByTypeAndPath(ADObjectType.User, syncTree.PortalPath, Common.Fields, skip, top);
            var count = 0;

            foreach (var content in contentList.Cast<dynamic>().Where(c => !string.IsNullOrEmpty((string)c.SyncGuid)))
            {
                _contentCache.Set((string)content.SyncGuid, content, DateTimeOffset.Now.AddDays(1));
                _portalUsers.TryAdd((string)content.SyncGuid, content.Id);

                count++;
            }

            AdLog.Log("Loaded users: " + count);
        }
        private async Task LoadAllPortalGroups()
        {
            _portalGroups = new ConcurrentDictionary<string, int>();

            var contentList = await Common.QueryContentByTypeAndPath(ADObjectType.Group, Common.Paths.IMS, Common.Fields);

            foreach (var content in contentList.Cast<dynamic>().Where(c => !string.IsNullOrEmpty((string)c.SyncGuid)))
            {
                _contentCache.Set((string)content.SyncGuid, content, DateTimeOffset.Now.AddDays(1));
                _portalGroups.TryAdd((string)content.SyncGuid, content.Id);
            }
        }

        private async Task EnsurePortalPath(SyncTree syncTree, string ADPath, string portalParentPath)
        {
            // adpath: OU=OtherOrg,OU=ExampleOrg,DC=Nativ,DC=local
            // portalParentPath: "/Root/IMS/Nativ.Local/ExampleOrg"

            // check if parent path exist on the portal
            if (!_portalContainers.Values.Contains(portalParentPath) && !await Content.ExistsAsync(portalParentPath))
            {
                // get parent AD object
                var adParentPath = syncTree.GetADParentObjectPath(ADPath);

                await EnsurePortalPath(syncTree, adParentPath, RepositoryPath.GetParentPath(portalParentPath));
            }

            // portalParentPath exists, so AD object should be synchronized here
            // domain, container, orgunit
            using (var entry = syncTree.ConnectToObject(ADPath))
            {
                var guid = Common.GetADObjectGuid(entry, syncTree.Server.GuidProperty);
                if (!guid.HasValue)
                    return;

                await SyncOneADObject(null, entry, guid.Value, ADObjectType.AllContainers, portalParentPath, CreateNewPortalContainer, UpdatePortalContainerProperties, syncTree);
            }
        }
        
        /* ==================================================================================== AD -> portal : Main algorithms */
        
        private async Task SyncOneADObject(ADSearchResult result, DirectoryEntry adEntry, Guid guid, ADObjectType objType, string nodePortalParentPath,
            Func<DirectoryEntry, string, Guid, SyncTree, Task> createNewObjectAction,
            Func<DirectoryEntry, Content, SyncTree, Task> updatePropertiesAction,
            SyncTree syncTree)
        {
            // Callers:
            // - SyncObjectsFromAD --> by a SearchResult object
            // - SyncObjectsFromAD/EnsurePath --> by an Entryt object
            //      - in the latter case if the content already exists on a different path, than MOVE it 
            //        instead of creating a new one to avoid having duplicate GUIDs in the portal.

            dynamic content = null;
            var guidStr = guid.ToString();

            switch (objType)
            {
                case ADObjectType.AllContainers:
                    content = await GetOrLoadContentAsync(_portalContainers.Keys, guidStr);
                    break;
                case ADObjectType.User:
                    content = await GetOrLoadContentAsync(_portalUsers.Keys, guidStr);
                    break;
                case ADObjectType.Group:
                    content = await GetOrLoadContentAsync(_portalGroups.Keys, guidStr);
                    break;
            }
            if (content != null)
            {
                // existing portal object
                try
                {
                    var isNodeSynced = false;

                    // check path, move object if necessary
                    // This is case insesitive, because AD object names sometimes differ from portal configuration values (e.g. lowercase/uppercase domain).
                    if (string.Compare(RepositoryPath.GetParentPath(content.Path), nodePortalParentPath, StringComparison.InvariantCultureIgnoreCase) != 0)
                    {
                        AdLog.LogADObject(string.Format("Moving object from {0} to {1}", content.Path, nodePortalParentPath), result.Path);

                        Content contentToMove = content;
                        await Common.RetryAsync(async () => await contentToMove.MoveToAsync(nodePortalParentPath));

                        // reload content for further processing (set properties)
                        content = await Content.LoadAsync(content.Id);
                        isNodeSynced = true;
                    }

                    if (adEntry != null)
                    {
                        // the call comes from EnsurePath, force sync
                        await updatePropertiesAction(adEntry, content, syncTree);
                        AdLog.LogADObject(string.Format("Saving synced portal object: {0}", content.Path), adEntry.Path);
                        
                        if (objType == ADObjectType.User && syncTree.SyncPhotos)
                            await UploadImage(adEntry, content);

                        // this saves the content
                        await Common.UpdateChanges(content, null);

                        if (!_updating)
                            Interlocked.Increment(ref _syncedObjects);
                    }
                    else
                    {
                        // the call comes from SyncObjects, we only have a result object but not entry

                        // Check sync and modification dates to decide whether the object should be synced or not.
                        // If we are in the update process, force sync, because the dates are already up-to-date.
                        if (_updating || Common.IsPortalObjectInvalid(content, result, syncTree.Server.Novell))
                        {
                            using (var entry = result.GetDirectoryEntry())
                            {
                                await updatePropertiesAction(entry, content, syncTree);
                                isNodeSynced = true;

                                if (objType == ADObjectType.User && syncTree.SyncPhotos)
                                    await UploadImage(entry, content);
                            }
                        }

                        if (isNodeSynced)
                        {
                            AdLog.LogADObject(string.Format("Saving synced portal object: {0}", content.Path), result.Path);
                            await Common.UpdateChanges(content, null);

                            if (!_updating)
                                Interlocked.Increment(ref _syncedObjects);
                        }
                    }
                }
                catch (Exception ex)
                {
                    AdLog.LogException(ex);

                    if (result != null)
                        AdLog.LogErrorADObject("Syncing of AD object not successful.", result.Path);
                }
            }
            else
            {
                // create new content
                if (adEntry != null)
                {
                    // the call comes from EnsurePath
                    await createNewObjectAction(adEntry, nodePortalParentPath, guid, syncTree);
                }
                else
                {
                    // the call comes from SyncObjects
                    using (var entry = result.GetDirectoryEntry())
                    {
                        await createNewObjectAction(entry, nodePortalParentPath, guid, syncTree);
                    }
                }

                Interlocked.Increment(ref _syncedObjects);
            }

            if (!_updating)
                Interlocked.Increment(ref _processedObjects);
        }

        private async Task SyncADObjectWithParents(ADSearchResult adObject, SyncTree syncTree, ADObjectType objType,
            Func<DirectoryEntry, string, Guid, SyncTree, Task> createNewObjectAction,
            Func<DirectoryEntry, Content, SyncTree, Task> updatePropertiesAction)
        {
            try
            {
                var nodeADpath = adObject.Path;
                if (syncTree.IsADPathExcluded(nodeADpath))
                    return;

                AdLog.LogOuterADObject("Syncing", adObject.Path);

                if (!adObject.SyncGuid.HasValue)
                {
                    // no AD guid present for object
                    AdLog.LogErrorADObject("No AD GUID present", adObject.Path);
                    return;
                }

                // creating new objects or moving existing ones
                var nodePortalParentPath = syncTree.GetPortalParentPath(nodeADpath);
                if (!_portalContainers.Values.Contains(nodePortalParentPath) && !await Content.ExistsAsync(nodePortalParentPath))
                {
                    // adpath: OU=OtherOrg,OU=ExampleOrg,DC=Nativ,DC=local
                    // portalParentPath: "/Root/IMS/NATIV/ExampleOrg"
                    await EnsurePortalPath(syncTree, syncTree.GetADParentObjectPath(adObject.Path), RepositoryPath.GetParentPath(nodePortalParentPath));
                }

                await SyncOneADObject(adObject, null,
                    adObject.SyncGuid.Value,
                    objType,
                    nodePortalParentPath,
                    createNewObjectAction,
                    updatePropertiesAction,
                    syncTree);
            }
            catch (Exception ex)
            {
                // syncing of one object of the current tree failed
                AdLog.LogException(ex);
            }
        }

        private async Task DeleteObjectsFromAD(SyncTree syncTree, ADObjectType objType, Func<Content, SyncTree, Task> deletePortalObjectAction)
        {
            // delete portal objects that have no corresponding synchronized objects in AD
            try
            {
                AdLog.LogOuter("Querying all portal objects...");
                var contents = await Common.QueryAllContent(objType, syncTree);

                AdLog.LogOuter("Checking if portal objects exist under synchronized path in AD...");

                await contents.ForEachAsync(SyncConfiguration.Current.ParallelOperations, async content =>
                {
                    try
                    {
                        // check if object exists under a synchronized path in AD
                        var guid = Common.GetPortalObjectGuid(content);
                        if (!guid.HasValue || !Common.IsADObjectPathSynced((Guid) guid, syncTree))
                        {
                            if (!guid.HasValue)
                                AdLog.Log(string.Format("No guid set for portal object: {0} ", content.Path));

                            // Reload is important here to have all the fields of the content. This is needed
                            // because in case of users we do not delete the content but only move it to a different
                            // folder after prefixing their unique fields.
                            if (objType == ADObjectType.User)
                                content = await Content.LoadAsync(content.Id);

                            // deleted from AD or not under a synchronized path anymore
                            await deletePortalObjectAction(content, syncTree);
                        }
                    }
                    catch (Exception ex)
                    {
                        var cex = ex as ClientException;

                        // log only if the result was not 404 (because the content is already deleted)
                        if (cex == null || cex.StatusCode != HttpStatusCode.NotFound)
                            AdLog.LogException(ex);
                    }
                });
            }
            catch (Exception ex)
            {
                AdLog.LogException(ex);
            }
        }

        private async Task SyncContainersFromAD(SyncTree syncTree)
        {
            AdLog.LogMainActivity("Syncing containers (domains, orgunits, containers)", syncTree.BaseDn, syncTree.PortalPath);

            // collect all parent paths
            var parentPaths = syncTree.AllADContainers.Select(ado => ado.GetParentPath()).Distinct().ToList();

            // This is important: we want to sort parents in an ascending order, but LDAP path strings
            // contain elements in reverse order. We have to convert them to a portal path first, than
            // we can compare them.
            parentPaths.Sort((p1, p2) => string.CompareOrdinal(syncTree.GetPortalPath(p1), syncTree.GetPortalPath(p2)));

            // Iterate through all parents and synchronize child containers in parallel. 
            // This permits at least a certain level of parallellization while still
            // making sure that the parent is already synced when it comes to the children.
            foreach (var parentPath in parentPaths)
            {
                AdLog.Log("Syncing CHILDREN of " + parentPath);

                // find all child containers that belong to this parent
                var childContainers = syncTree.AllADContainers.Where(adc => string.CompareOrdinal(adc.GetParentPath(), parentPath) == 0).ToArray();

                await childContainers.ForEachAsync(SyncConfiguration.Current.ParallelOperations, async adObject =>
                {
                    await SyncADObjectWithParents(adObject, syncTree, ADObjectType.AllContainers, CreateNewPortalContainer, UpdatePortalContainerProperties);
                });
            }
        }
        private async Task SyncUsersFromAD(SyncTree syncTree)
        {
            AdLog.LogMainActivity("Syncing users", syncTree.BaseDn, syncTree.PortalPath);

            // Because user count can be huge, we have to load and sync them in batches. Because we
            // have to load the same users from the portal as the users in AD (in case of existing ones),
            // it is important to SORT both lists by Path to have the same users on the same 'page'.
            var userCount = syncTree.AllADUsers.Count();
            var processedCount = 0;
            var top = SyncConfiguration.Current.ContentPageSize;

            // This sorts AD users by their portal Path.
            var sortedUserList = syncTree.AllADUsers.OrderBy(u => syncTree.GetPortalPath(u.Path));
            
            while (processedCount < userCount)
            {
                AdLog.Log(string.Format("Querying users (from {0} to {1}) in sync tree {2}.", processedCount, processedCount + top * 1.5, syncTree.PortalPath));

                // clear the cache to make space for existing user content
                _contentCache.Trim(100);

                // load one 'page' of user content from the portal to the cache
                await LoadPortalUsers(syncTree, processedCount, Convert.ToInt32(top * 1.5));

                // process users only on this page in parallel
                await sortedUserList.Skip(processedCount).Take(top).ForEachAsync(SyncConfiguration.Current.ParallelOperations, async adObject =>
                {
                    await SyncADObjectWithParents(adObject, syncTree, ADObjectType.User, CreateNewPortalUser, UpdatePortalUserProperties);
                });

                // proceed to the next page
                processedCount += top;
            }
        }
        private async Task SyncGroupsFromAD(SyncTree syncTree)
        {
            if (!syncTree.SyncGroups)
            {
                AdLog.LogMainActivity("Groups under synctree are skipped", syncTree.BaseDn, syncTree.PortalPath);
                return;
            }

            AdLog.LogMainActivity("Syncing groups", syncTree.BaseDn, syncTree.PortalPath);

            _postponedObjects = new ConcurrentBag<Guid>();
            _updating = false;

            await syncTree.AllADGroups.ForEachAsync(SyncConfiguration.Current.ParallelOperations, async adObject =>
            {
                await SyncADObjectWithParents(adObject, syncTree, ADObjectType.Group, CreateNewPortalGroup, UpdatePortalGroupProperties);
            });

            // there are groups that need to be revisited, because they contain members that were not synced before
            if (!_postponedObjects.IsEmpty)
            {
                AdLog.Log(string.Format("Syncing postponed groups ({0}).", _postponedObjects.Count));

                _updating = true;

                var postponedGroups = syncTree.AllADGroups.Where(group => @group.SyncGuid.HasValue && _postponedObjects.Contains(@group.SyncGuid.Value));

                await postponedGroups.ForEachAsync(SyncConfiguration.Current.ParallelOperations, async adObject =>
                {
                    await SyncADObjectWithParents(adObject, syncTree, ADObjectType.Group, CreateNewPortalGroup, UpdatePortalGroupProperties);
                });
            }
        }
        
        private async Task DeletePortalUsers(SyncTree syncTree)
        {
            AdLog.LogMainActivity("Deleting portal users", syncTree.BaseDn, syncTree.PortalPath);
            await DeleteObjectsFromAD(syncTree,
                ADObjectType.User,
                DeletePortalUser);
        }
        private async Task DeletePortalGroups(SyncTree syncTree)
        {
            AdLog.LogMainActivity("Deleting portal groups", syncTree.BaseDn, syncTree.PortalPath);
            await DeleteObjectsFromAD(syncTree,
                ADObjectType.Group,
                DeletePortalGroup);
        }
        private async Task DeletePortalContainers(SyncTree syncTree)
        {
            AdLog.LogMainActivity("Deleting portal containers (domains, orgunits, containers)", syncTree.BaseDn, syncTree.PortalPath);
            await DeleteObjectsFromAD(syncTree,
                ADObjectType.AllContainers,
                DeletePortalContainer);
        }

        // ==================================================================================== AD -> portal : Public methods
        
        /// <summary>
        /// Syncs all objects of all configured sync trees from Active Directory(ies).
        /// </summary>
        public async Task SyncFromAdAsync()
        {
            _stopWatch.Start();

            if (!LoadConfiguration())
                return;

            AdLog.Log("Portal address: " + string.Join(", ", ClientContext.Current.Servers.Select(s => s.Url)));
            AdLog.Log("Max cache size: " + _contentCache.CacheMemoryLimit / (1024 * 1024) + " MB");
            AdLog.Log("Parallel operations: " + SyncConfiguration.Current.ParallelOperations);
            AdLog.Log("AD-related content types: " + string.Join(", ", SyncConfiguration.ADRelatedContentTypes));

            _subtask = new SnSubtask("Downloading", "Downloading containers from the portal.");
            _subtask.Start();

            AdLog.LogMain("Downloading containers from the portal...");
            await LoadAllPortalContainers();

            _subtask.Progress(100, 100, 5, 100, "Containers loaded.");
            _subtask.Finish("Containers loaded.");

            _subtask = new SnSubtask("Querying", "Querying LDAP directory.");
            _subtask.Start();

            AdLog.LogMain("Querying LDAP directory...");

            // start parallel tasks for querying different types of AD objects
            var t4 = Task.Run(() => { return SyncConfiguration.Current.SyncTrees.Select(st => st.AllADContainers.Count()).Sum();});
            var t5 = Task.Run(() => { return SyncConfiguration.Current.SyncTrees.Select(st => st.AllADGroups.Count()).Sum(); });
            var t6 = Task.Run(() => { return SyncConfiguration.Current.SyncTrees.Select(st => st.AllADUsers.Count()).Sum(); });

            await Task.WhenAll(t4, t5, t6);

            var maxContainers = await t4;
            var maxGroups= await t5;
            var maxUsers = await t6;

            AdLog.LogMain(string.Format("Syncing {0} containers, {1} groups and {2} users.", maxContainers, maxGroups, maxUsers));

            _taskTimer = new Timer(TaskTimerElapsed, null, Timeout.Infinite, Timeout.Infinite);

            FinishSubtask(10, "LDAP objects loaded.", maxContainers, "Syncing containers", "Syncing containers from the LDAP directory to the portal.");

            // sync containers (parallel under multiple sync trees)
            await Task.WhenAll(SyncConfiguration.Current.SyncTrees.Select(SyncContainersFromAD));

            FinishSubtask(20, "Container sync finished.", maxUsers, "Syncing users", "Syncing users from the LDAP directory to the portal.");

            // clear the cache to make room for other types of objects
            _contentCache.Trim(100);

            AdLog.LogMain("Downloading users from the portal...");
            await LoadAllPortalUsers();

            // Syncing users has to be synchronous among sync trees, because we clear the cache 
            // from time to time. Users are still synced in parallel under a tree.
            foreach (var syncTree in SyncConfiguration.Current.SyncTrees)
            {
                await SyncUsersFromAD(syncTree);
            }

            FinishSubtask(60, "User sync finished.", maxGroups, "Syncing groups", "Syncing groups from the LDAP directory to the portal.");

            // clear the cache to make room for other types of objects
            _contentCache.Trim(100);

            AdLog.LogMain("Downloading groups from the portal...");
            await LoadAllPortalGroups();

            // sync groups (parallel under multiple sync trees)
            await Task.WhenAll(SyncConfiguration.Current.SyncTrees.Select(SyncGroupsFromAD));

            // clear the cache to make room for other types of objects
            _contentCache.Trim(100);

            FinishSubtask(75, "Group sync finished.", maxUsers, "Deleting users", "Deleting users from the portal.");

            foreach (var syncTree in SyncConfiguration.Current.SyncTrees)
            {
                await DeletePortalUsers(syncTree);
            }

            FinishSubtask(90, "User delete finished.", maxGroups, "Deleting groups", "Deleting groups from the portal.");

            foreach (var syncTree in SyncConfiguration.Current.SyncTrees)
            {
                await DeletePortalGroups(syncTree);
            }

            FinishSubtask(95, "Group delete finished.", maxGroups, "Deleting containers", "Deleting containers from the portal.");

            foreach (var syncTree in SyncConfiguration.Current.SyncTrees)
            {
                await DeletePortalContainers(syncTree);
            }

            FinishSubtask(99, "Container delete finished.", 100);

            _stopWatch.Stop();

            AdLog.EndLog();

            FinishAdSyncTask();

            if (_contentCache != null)
                _contentCache.Dispose();
        }

        // ==================================================================================== Helper methods

        private void FinishSubtask(int overallProgress, string subtaskFinishMessage, int nextSubtaskMax, string nextSubtaskTitle = null, string nextSubtaskDetails = null)
        {
            // temporarily stop the timer
            _taskTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // finish previous subtask and start a new one
            _subtask.Progress(100, 100, overallProgress, 100, subtaskFinishMessage);
            _subtask.Finish(subtaskFinishMessage);

            _syncedObjectsTotal += _syncedObjects;

            if (!string.IsNullOrEmpty(nextSubtaskTitle))
            {
                _subtask = new SnSubtask(nextSubtaskTitle, nextSubtaskDetails);
                _subtask.Start();

                // reset subtask values
                _syncedObjects = 0;
                _processedObjects = 0;
                _maxObjects = nextSubtaskMax;
                _overallProgress = overallProgress;

                _taskTimer.Change(TIMER_INTERVAL*1000, TIMER_INTERVAL*1000);
            }
        }
        private void FinishAdSyncTask()
        {
            Console.WriteLine("ResultData:" + JsonHelper.Serialize(new
            {
                SyncedObjects = _syncedObjectsTotal,
                ObjectErrorCount = AdLog.ObjectErrors,
                ErrorCount = AdLog.Errors,
                ElapsedTime = _stopWatch.Elapsed
            }));
        }
        private void TaskTimerElapsed(object state)
        {
            _subtask.Progress(_processedObjects, _maxObjects, _overallProgress, 100);
        }

        public async Task<Content> GetOrLoadContentAsync(IEnumerable<string> guidList, string guid)
        {
            if (guidList == null || !guidList.Contains(guid))
                return null;

            // check if it can be found in the cache
            var tempContent = _contentCache.Get(guid) as Content;
            if (tempContent != null)
                return tempContent;

            AdLog.Log("Content CACHE MISS: " + guid);

            // reload it from the portal and add it to the cache
            return await Common.LoadContentAsync(guid);
        }

        private string GetErrorMessage(Exception ex)
        {
            var messages = new List<string>();
            while (ex != null)
            {
                messages.Add(ex.Message);
                ex = ex.InnerException;
            }
            return string.Join(" / ", messages);
        }

        private bool LoadConfiguration()
        {
            AdLog.LogMain("Loading configuration...");

            // preload configuration
            var config = SyncConfiguration.Current;
            if (config != null && config.Enabled && config.Validate())
                return true;

            if (config == null)
                AdLog.Log("AD sync configuration is not available.");
            else if (!config.Enabled)
                AdLog.Log("AD sync is not enabled.");

            AdLog.EndLog();

            _stopWatch.Stop();

            FinishAdSyncTask();
            return false;
        }
    }
}
