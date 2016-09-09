using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Microsoft.SharePoint.Client;
using System.IO;
using SPSync.Core.Common;
using SPSync.Core.Adfs;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Security;

namespace SPSync.Core
{
    public class SharePointManager
    {
        private SyncConfiguration _configuration;
        private bool? _disableKeepAlive = null;

        private SharePointManager() { }

        internal SharePointManager(SyncConfiguration configuration)
        {
            _configuration = configuration;
        }

        public static SyncConfiguration TryFindConfiguration(string url, string username, string password)
        {
            var syncConfig = new SyncConfiguration();
            var docLibUrl = "n/a";

            try
            {
                syncConfig.Username = username;
                syncConfig.Password = password;
                syncConfig.SiteUrl = url;

                if (username.Contains("\\"))
                {
                    var split = username.Split('\\');
                    syncConfig.Username = split[1];
                    syncConfig.Domain = split[0];
                    syncConfig.AuthenticationType = AuthenticationType.NTLM;
                }
                else
                {
                    if (url.ToLower().Contains(".sharepoint.com"))
                    {
                        syncConfig.AuthenticationType = AuthenticationType.Office365;
                    }
                }

                //https://mytenant.sharepoint.com/personal/user_mytenant_onmicrosoft_com/_layouts/15/start.aspx#/Documents/Forms/All.aspx
                var siteUrl = url.ToLower();
                var documentLibUrl = string.Empty;
                if (siteUrl.Contains("/_layouts/15/start.aspx"))
                {

                    documentLibUrl = siteUrl.Substring(siteUrl.IndexOf("#/") + 2);
                    documentLibUrl = documentLibUrl.Substring(0, documentLibUrl.IndexOf("/"));
                    siteUrl = siteUrl.Substring(0, siteUrl.IndexOf("/_layouts/15/start.aspx"));
                }
                else
                {
                    if (siteUrl.Contains("/forms/"))
                    {
                        siteUrl = siteUrl.Substring(0, siteUrl.IndexOf("/forms/"));
                        documentLibUrl = siteUrl.Substring(siteUrl.LastIndexOf("/") + 1);
                        siteUrl = siteUrl.Substring(0, siteUrl.LastIndexOf("/"));
                    }
                    else
                    {
                        var s = siteUrl;
                        siteUrl = siteUrl.Substring(0, siteUrl.LastIndexOf("/"));
                        documentLibUrl = s.Substring(s.LastIndexOf("/") + 1);
                    }
                }

                docLibUrl = documentLibUrl;

                syncConfig.SiteUrl = siteUrl;
                syncConfig.ConflictHandling = ConflictHandling.ManualConflictHandling;
                syncConfig.DownloadHeadersOnly = false;
                syncConfig.SelectedFolders = null;

                var spm = syncConfig.GetSharePointManager();
                using (var context = spm.GetClientContext())
                {
                    Logger.LogDebug("TryFindConfiguration ClientContext acquired");
                    var web = context.Web;
                    context.Load(web);
                    context.ExecuteQuery();

                    var listUrl = web.ServerRelativeUrl + "/" + documentLibUrl;
                    if (web.ServerRelativeUrl == "/")
                        listUrl = "/" + documentLibUrl;

                    Logger.LogDebug("TryFindConfiguration ClientContext web loaded. GetList={0}", listUrl);

                    var list = web.GetList(listUrl);
                    try
                    {
                        context.Load(list);
                        context.ExecuteQuery();

                        syncConfig.DocumentLibrary = list.Title;
                        Logger.LogDebug("TryFindConfiguration Found lib title={0}", list.Title);
                    }
                    catch
                    {
                        Logger.LogDebug("TryFindConfiguration Url does not contain a list. Trying to find sub web...");

                        var allSubWebs = web.Webs;
                        context.Load(allSubWebs, p => p.Include(x => x.ServerRelativeUrl));
                        context.ExecuteQuery();

                        var subWeb = allSubWebs.ToList().FirstOrDefault(p => p.ServerRelativeUrl.Contains(listUrl));

                        if (subWeb == null)
                        {
                            return null;
                        }

                        Logger.LogDebug("TryFindConfiguration Sub web found. Loading all lists...");

                        var allLists = subWeb.Lists;
                        context.Load(allLists);
                        context.ExecuteQuery();

                        var allDocLibs = allLists.ToList().Where(p => p.BaseType == BaseType.DocumentLibrary && !p.Hidden);

                        syncConfig.DocumentLibrary = string.Join("|", allDocLibs.Select(p => p.Title));
                        Logger.LogDebug("TryFindConfiguration All doc libs loaded: {0}", syncConfig.DocumentLibrary);
                    }
                }

                return syncConfig;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (IdcrlException)
            {
                throw;
            }
            catch (WebException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogDebug("TryFindConfiguration Error: Url={0} AuthType={1} SiteUrl={2} DocLibUrl={3} Username={4}", url, syncConfig.AuthenticationType, syncConfig.SiteUrl, docLibUrl, syncConfig.Username);
                Logger.Log("[{3}] Failed to get config automatically: {0}{1}{2}", ex.Message, Environment.NewLine, ex.StackTrace, DateTime.Now);

                return null;
            }
        }

        public static string[] GetAllFoldersFromConfig(SyncConfiguration configuration)
        {
            var folderList = new List<string>();

            var spm = configuration.GetSharePointManager();
            using (var context = spm.GetClientContext())
            {
                var web = context.Web;
                context.Load(web);
                var list = context.Web.Lists.GetByTitle(configuration.DocumentLibrary);
                context.Load(list, p => p.RootFolder.ServerRelativeUrl, p => p.RootFolder.Name, p => p.RootFolder.Folders);
                context.ExecuteQuery();

                var subFolderList = GetAllFoldersInternal(context, list.RootFolder);
                subFolderList.ForEach(f =>
                {
                    var folder = f;
                    if (web.ServerRelativeUrl != "/")
                        folder = f.Substring(web.ServerRelativeUrl.Length);
                    folder = folder.Substring(folder.IndexOf("/", 1));
                    folderList.Add(folder);
                });
            }

            return folderList.ToArray();

        }

        private static List<string> GetAllFoldersInternal(ClientContext context, Folder folder)
        {
            var folderList = new List<string>();

            foreach (var subFolder in folder.Folders)
            {
                if (subFolder.Name == "Forms")
                    continue;

                folderList.Add(subFolder.ServerRelativeUrl);

                context.Load(subFolder, p => p.Folders.Include(f => f.ServerRelativeUrl, f => f.Name));
                context.ExecuteQuery();

                var subFolderList = GetAllFoldersInternal(context, subFolder);
                folderList.AddRange(subFolderList);
            }

            return folderList;
        }

        internal void DeleteFile(int id)
        {
            using (var context = GetClientContext())
            {
                var list = context.Web.Lists.GetByTitle(_configuration.DocumentLibrary);
                var item = list.GetItemById(id);
                item.Recycle();
                context.ExecuteQuery();
            }
        }

        internal void RenameItem(int id, string newName)
        {
            using (var context = GetClientContext())
            {
                var list = context.Web.Lists.GetByTitle(_configuration.DocumentLibrary);
                var item = list.GetItemById(id);
                item["FileLeafRef"] = newName;
                item.Update();
                context.ExecuteQuery();
            }
        }

        internal List<SharePointItem> GetChangedFiles(Metadata.MetadataStore metadataStore, Action<int, string> progressHandler)
        {
            if (string.IsNullOrEmpty(metadataStore.ChangeToken))
            {
                Logger.LogDebug("No ChangeToken Found, downloading list of all files");
                progressHandler(0, null);
                var listOfAllFiles = DownloadFileList();

                using (var context = GetClientContext())
                {
                    var list = context.Web.Lists.GetByTitle(_configuration.DocumentLibrary);
                    context.Load(list, p => p.DefaultViewUrl);

                    var changeQuery = new ChangeQuery();
                    changeQuery.Item = true;
                    changeQuery.Add = true;
                    changeQuery.DeleteObject = true;
                    changeQuery.Move = true;
                    changeQuery.Rename = true;
                    changeQuery.Update = true;
                    var changes = list.GetChanges(changeQuery);
                    context.Load(changes);

                    context.ExecuteQuery();

                    if (changes.Count > 0)
                    {
                        var newChangeToken = changes.OrderBy(p => p.ChangeToken.StringValue).Last().ChangeToken.StringValue;
                        Logger.LogDebug("Set ChangeToken from empty to new {0}", newChangeToken);
                        metadataStore.ChangeToken = newChangeToken;
                    }
                    else
                    {
                        Logger.LogDebug("No initial changes found, so no ChangeToken set.");
                    }
                }

                progressHandler(100, null);

                return listOfAllFiles;
            }

            var fileList = new List<SharePointItem>();
            var tempFileList = new List<SharePointItem>();

            using (var context = GetClientContext())
            {
                var list = context.Web.Lists.GetByTitle(_configuration.DocumentLibrary);
                context.Load(list, p => p.DefaultViewUrl);

                var changeQuery = new ChangeQuery();
                changeQuery.Item = true;
                changeQuery.Add = true;
                changeQuery.DeleteObject = true;
                changeQuery.Move = true;
                changeQuery.Rename = true;
                changeQuery.Update = true;
                if (!string.IsNullOrEmpty(metadataStore.ChangeToken))
                    changeQuery.ChangeTokenStart = new ChangeToken() { StringValue = metadataStore.ChangeToken };
                var changes = list.GetChanges(changeQuery);
                context.Load(changes);

                try
                {
                    context.ExecuteQuery();
                }
                catch (ServerException ex)
                {
                    if (ex.ServerErrorTypeName == "Microsoft.SharePoint.SPInvalidChangeTokenException")
                    {
                        changeQuery.ChangeTokenStart = null;
                        changes = list.GetChanges(changeQuery);
                        context.Load(changes);
                        context.ExecuteQuery();
                    }
                    else
                    {
                        throw;
                    }
                }

                var spListItemCache = new Dictionary<int, ListItem>();

                var changeCount = changes.Count;
                var counter = 0;
                progressHandler(1, null);

                Logger.LogDebug("Found {0} remote changes {1}", changeCount, string.IsNullOrEmpty(metadataStore.ChangeToken) ? string.Empty : "with change token " + metadataStore.ChangeToken);

                foreach (var change in changes)
                {
                    var changedItem = (ChangeItem)change;

                    Logger.LogDebug("ChangeItem: {0} ChangeType: {1}", changedItem.ItemId, changedItem.ChangeType);

                    if (changedItem.ChangeType == ChangeType.DeleteObject)
                    {
                        tempFileList.Add(new SharePointItem(changedItem.ItemId, ItemType.File, changedItem.ChangeType, "Unknown", null, changedItem.Time, "."));
                    }
                    else
                    {
                        try
                        {
                            if (!spListItemCache.ContainsKey(changedItem.ItemId))
                            {
                                var tempItem = list.GetItemById(changedItem.ItemId);
                                context.Load(tempItem, p => p["Modified"], p => p.File, p => p.File.ServerRelativeUrl, p => p.Folder, p => p.Folder.ServerRelativeUrl);
                                context.ExecuteQuery();

                                spListItemCache.Add(changedItem.ItemId, tempItem);
                            }

                            var changedListItem = spListItemCache[changedItem.ItemId];

                            // File
                            if (changedListItem.File.IsPropertyAvailable("ServerRelativeUrl"))
                            {
                                var folder = changedListItem.File.ServerRelativeUrl;
                                folder = folder.Replace(changedListItem.File.Name, string.Empty);
                                folder = folder.Replace(list.DefaultViewUrl.Substring(0, list.DefaultViewUrl.IndexOf("/Forms/")), string.Empty);
                                folder = folder.Replace("/", "\\");
                                folder = folder.EndsWith("\\") ? folder : folder + "\\";

                                tempFileList.Add(new SharePointItem(changedItem.ItemId, ItemType.File, changedItem.ChangeType, changedListItem.File.Name, changedListItem.File.ETag, changedListItem.File.TimeLastModified, "." + folder + changedListItem.File.Name));
                            }
                            // Folder
                            else if (changedListItem.Folder.IsPropertyAvailable("ServerRelativeUrl"))
                            {
                                var folder = changedListItem.Folder.ServerRelativeUrl;
                                folder = folder.Replace(changedListItem.Folder.Name, string.Empty);
                                folder = folder.Replace(list.DefaultViewUrl.Substring(0, list.DefaultViewUrl.IndexOf("/Forms/")), string.Empty);
                                folder = folder.Replace("/", "\\");
                                folder = folder.EndsWith("\\") ? folder : folder + "\\";

                                tempFileList.Add(new SharePointItem(changedItem.ItemId, ItemType.Folder, changedItem.ChangeType, changedListItem.Folder.Name, null, (DateTime)changedListItem["Modified"], "." + folder + changedListItem.Folder.Name));
                            }
                        }
                        catch (ServerException ex)
                        {
                            Logger.LogDebug("Change for item id {0} was not added. Message: {1} ServerErrorTypeName: {2}", changedItem.ItemId, ex.Message, ex.ServerErrorTypeName);
                        }
                    }

                    counter++;
                    var percent = (int)(((double)counter / (double)changeCount) * 100);
                    progressHandler(percent, tempFileList.LastOrDefault()?.Name);
                }

                if (changes.Count > 0)
                {
                    var newChangeToken = changes.OrderBy(p => p.ChangeToken.StringValue).Last().ChangeToken.StringValue;
                    if (string.IsNullOrEmpty(metadataStore.ChangeToken))
                    {
                        Logger.LogDebug("Set ChangeToken from empty to new {0}", newChangeToken);
                        metadataStore.ChangeToken = newChangeToken;
                    }
                    else if (metadataStore.ChangeToken.CompareTo(newChangeToken) < 0)
                    {
                        Logger.LogDebug("Set ChangeToken from {0} to new {1}", metadataStore.ChangeToken, newChangeToken);
                        metadataStore.ChangeToken = newChangeToken;
                    }
                }

                foreach (var ti in tempFileList.GroupBy(p => p.Id))
                {
                    Logger.LogDebug("Found {0} changes for item id {1}", ti.Count(), ti.Key);
                    SharePointItem spitem = null;
                    if (ti.All(p => p.ChangeType == ChangeType.Update))
                        spitem = ti.FirstOrDefault(p => p.ChangeType == ChangeType.Update);
                    else if (ti.All(p => p.ChangeType == ChangeType.Add || p.ChangeType == ChangeType.Update))
                        spitem = ti.FirstOrDefault(p => p.ChangeType == ChangeType.Add);
                    else if (ti.Any(p => p.ChangeType == ChangeType.DeleteObject))
                        spitem = ti.FirstOrDefault(p => p.ChangeType == ChangeType.DeleteObject);
                    else if (ti.Any(p => p.ChangeType == ChangeType.Rename))
                        spitem = ti.FirstOrDefault(p => p.ChangeType == ChangeType.Rename);

                    fileList.Add(spitem);
                }

                progressHandler(100, null);
            }

            return fileList;
        }

        private List<SharePointItem> DownloadFileList()
        {
            var fileList = new List<SharePointItem>();

            using (var context = GetClientContext())
            {
                var list = context.Web.Lists.GetByTitle(_configuration.DocumentLibrary);
                context.Load(list, p => p.RootFolder.Files, p => p.RootFolder.Folders, p => p.RootFolder.Files.Include(f => f.ListItemAllFields.Id));
                context.ExecuteQuery();

                var subFileList = DownloadFileNameListInternal(context, list.RootFolder, ".\\");
                fileList.AddRange(subFileList);
            }

            return fileList;
        }

        private List<SharePointItem> DownloadFileNameListInternal(ClientContext context, Folder folder, string folderFullPath)
        {
            var fileList = new List<SharePointItem>();

            foreach (var item in folder.Files)
            {
                fileList.Add(new SharePointItem(item.ListItemAllFields.Id, ItemType.File, ChangeType.Add, item.Name, item.ETag, item.TimeLastModified, folderFullPath + item.Name));
            }

            //TODO: check if sub folder is in selected folders of config
            foreach (var subFolder in folder.Folders)
            {
                if (subFolder.Name == "Forms")
                    continue;

                context.Load(subFolder, p => p.Files, p => p.Folders, p => p.Files.Include(f => f.ListItemAllFields.Id), p => p.Folders.Include(g => g.Files.Include(x => x.ListItemAllFields.Id)));
                context.ExecuteQuery();
                string newFullPath = folderFullPath + subFolder.Name + "\\";

                var subFileList = DownloadFileNameListInternal(context, subFolder, newFullPath);
                fileList.AddRange(subFileList);
            }

            return fileList;
        }

        internal DateTime GetFileTimestamp(string relativeFile, out int eTag)
        {
            eTag = -1;
            try
            {
                using (var context = GetClientContext())
                {
                    var file = context.Web.GetFileByServerRelativeUrl(GetServerRelativeUrl(relativeFile));
                    context.Load(file);
                    context.ExecuteQuery();

                    if (!string.IsNullOrEmpty(file.ETag))
                        eTag = ParseETag(file.ETag);

                    return file.TimeLastModified;
                }
            }
            catch
            { return DateTime.MinValue; }
        }

        internal void DownloadFile(string filename, string targetDirectory, DateTime modifiedDate)
        {
            string serverRelativeUrl = GetServerRelativeUrl(targetDirectory, filename);


            using (var context = GetClientContext())
            {
                context.RequestTimeout = System.Threading.Timeout.Infinite;
                var file = context.Web.GetFileByServerRelativeUrl(serverRelativeUrl);
                var stream = file.OpenBinaryStream();
                context.ExecuteQuery();

                var localTargetFile = Path.Combine(targetDirectory, filename);
                using (var fs = new FileStream(localTargetFile, FileMode.Create))
                {
                    stream.Value.CopyTo(fs);
                }

                System.IO.File.SetLastWriteTimeUtc(localTargetFile, modifiedDate);
            }
        }

        private SharePointOnlineCredentials GetSharePointOnlineCredentials()
        {
            var securePassword = new SecureString();
            foreach (char c in _configuration.Password) securePassword.AppendChar(c);
            return new SharePointOnlineCredentials(_configuration.Username, securePassword);
        }

        private string GetServerRelativeUrl(string relativeFilename)
        {
            relativeFilename = relativeFilename.Replace("\\", "/");
            relativeFilename = relativeFilename.TrimStart('/');

            using (var context = GetClientContext())
            {
                var web = context.Web;
                context.Load(web);
                var list = web.Lists.GetByTitle(_configuration.DocumentLibrary);
                context.Load(list, p => p.DefaultViewUrl);
                context.ExecuteQuery();

                string url = list.DefaultViewUrl.Substring(0, list.DefaultViewUrl.IndexOf("/Forms/"));
                url = url.EndsWith("/") ? url + relativeFilename : url + "/" + relativeFilename;
                return url;
            }
        }

        private string GetServerRelativeUrl(string localDirectory, string filename)
        {
            var relativeFile = localDirectory.Replace(_configuration.LocalFolder, string.Empty);
            relativeFile = relativeFile.EndsWith("\\") ? relativeFile + filename : relativeFile + "\\" + filename;
            return GetServerRelativeUrl(relativeFile);
        }

        internal int CreateFolder(string relativePath, string folderName)
        {
            using (var context = GetClientContext())
            {
                var list = context.Web.Lists.GetByTitle(_configuration.DocumentLibrary);
                context.Load(list, l => l.DefaultViewUrl);
                context.ExecuteQuery();

                ListItemCreationInformation newFolder = new ListItemCreationInformation();
                newFolder.UnderlyingObjectType = FileSystemObjectType.Folder;
                newFolder.FolderUrl = list.DefaultViewUrl.Substring(0, list.DefaultViewUrl.IndexOf("/Forms/"));
                if (!relativePath.Equals(string.Empty))
                {
                    newFolder.FolderUrl = GetServerRelativeUrl(relativePath);
                }
                newFolder.LeafName = folderName;
                ListItem item = list.AddItem(newFolder);
                item.Update();
                try
                {
                    context.ExecuteQuery();
                }
                catch
                {
                    // Folder exists?
                    var fullRelFolderUrl = newFolder.FolderUrl + (!newFolder.FolderUrl.EndsWith("/") ? "/" : "") + folderName;
                    var existingFolder = context.Web.GetFolderByServerRelativeUrl(fullRelFolderUrl);
                    context.Load(existingFolder, p => p.ListItemAllFields.Id);
                    context.ExecuteQuery();
                    item = existingFolder.ListItemAllFields;
                }
                return item.Id;
            }
        }

        internal void DeleteFolder(string relativePath, string folderName)
        {
            using (var context = GetClientContext())
            {
                var folder = context.Web.GetFolderByServerRelativeUrl(GetServerRelativeUrl(relativePath + "/" + folderName));
                folder.Recycle();
                context.ExecuteQuery();
            }
        }

        internal int UploadFile(string relativeFile, string localFile)
        {
            try
            {
                var relativeUrl = GetServerRelativeUrl(relativeFile);
                var destinationUrl = new Uri(_configuration.SiteUrl).GetLeftPart(UriPartial.Authority);
                destinationUrl = destinationUrl + relativeUrl;

                var itemId = -1;

                using (var context = GetClientContext(preAuthenticate: true))
                using (Stream stream = new FileStream(localFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    var list = context.Web.Lists.GetByTitle(_configuration.DocumentLibrary);

                    var file = list.RootFolder.Files.Add(new FileCreationInformation()
                    {
                        Url = destinationUrl,
                        ContentStream = stream,
                        Overwrite = true
                    });

                    context.Load(file);

                    var listItem = file.ListItemAllFields;
                    context.Load(listItem);

                    context.ExecuteQuery();

                    itemId = listItem.Id;
                }

                return itemId;
            }
            catch (Exception ex)
            {
                Logger.Log("Uploading {0} failed: {1}{2}{3}", relativeFile, ex.Message, Environment.NewLine, ex.StackTrace);
                throw;
            }
        }

        internal static int ParseETag(string etag) => int.Parse(etag.Split(',')[1].Trim('\"').Trim());

        private ClientContext GetClientContext(string url = null, bool preAuthenticate = false)
        {
            if (string.IsNullOrEmpty(url))
                url = _configuration.SiteUrl;

            Logger.LogDebug("GetClientContext Url={0} AuthType={1}", url, _configuration.AuthenticationType);

            ClientContext context = new ClientContext(url);
            context.RequestTimeout = System.Threading.Timeout.Infinite;

            switch (_configuration.AuthenticationType)
            {
                case AuthenticationType.ADFS:
                    context.Credentials = CredentialCache.DefaultCredentials;
                    context.ExecutingWebRequest += new EventHandler<WebRequestEventArgs>(context_ExecutingWebRequestADFS);
                    break;
                case AuthenticationType.Office365:
                    context.Credentials = GetSharePointOnlineCredentials();
                    Logger.LogDebug("GetClientContext SharePointOnlineCredentials set");
                    break;
                case AuthenticationType.NTLM:
                default:

                    context.Credentials = new NetworkCredential(_configuration.Username, _configuration.Password, _configuration.Domain);

                    if (!_disableKeepAlive.HasValue)
                    {
                        Logger.LogDebug("GetClientContext DisableKeepAlive has no value");
                        EventHandler<WebRequestEventArgs> reqHandler = (sender, e) =>
                    {
                        e.WebRequestExecutor.WebRequest.ImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;
                        e.WebRequestExecutor.WebRequest.UnsafeAuthenticatedConnectionSharing = true;
                    };
                        context.RequestTimeout = 15000;
                        context.ExecutingWebRequest += reqHandler;
                        context.Load(context.Web);
                        try
                        {
                            context.ExecuteQuery();
                            _disableKeepAlive = false;
                        }
                        catch (Exception)
                        {
                            _disableKeepAlive = true;
                        }
                        Logger.LogDebug("GetClientContext DisableKeepAlive value set to: {0}", _disableKeepAlive);
                        context.ExecutingWebRequest -= reqHandler;
                        context.RequestTimeout = System.Threading.Timeout.Infinite;
                    }

                    context.ExecutingWebRequest += (sender, e) =>
                {
                    e.WebRequestExecutor.WebRequest.ImpersonationLevel = System.Security.Principal.TokenImpersonationLevel.Impersonation;
                    e.WebRequestExecutor.WebRequest.UnsafeAuthenticatedConnectionSharing = true;
                    if (preAuthenticate)
                        e.WebRequestExecutor.WebRequest.PreAuthenticate = true;
                    if (_disableKeepAlive.Value)
                        e.WebRequestExecutor.WebRequest.KeepAlive = false;
                };
                    break;
            }

            return context;
        }

        private void context_ExecutingWebRequestADFS(object sender, WebRequestEventArgs e)
        {
            var wtrealm = _configuration.AdfsRealm;
            var wctx = string.Format("{0}/_layouts/Authenticate.aspx?Source=%2F", _configuration.SiteUrl);
            var wreply = string.Format("{0}/_trust/", _configuration.SiteUrl);
            var stsUrl = string.Format("https://{0}/adfs/services/trust/2005/usernamemixed", _configuration.AdfsSTSUrl);

            try
            {
                e.WebRequestExecutor.WebRequest.CookieContainer = AdfsHelper.AttachCookie(_configuration.SiteUrl, wctx, wtrealm, wreply, stsUrl, _configuration.Username, _configuration.Password);
            }
            catch
            {
                AdfsHelper.InValidateCookie();
                throw;
            }

        }

        internal void CreateFoldersIfNotExists(string relFolder)
        {
            Logger.LogDebug(Guid.Empty, Guid.Empty, "(CreateFoldersIfNotExists) relFolder={0}", relFolder);
            using (var context = GetClientContext())
            {
                var list = context.Web.Lists.GetByTitle(_configuration.DocumentLibrary);
                context.Load(list.RootFolder.Folders);
                context.ExecuteQuery();

                var folders = relFolder.Split('\\');
                var searchFolderRoot = list.RootFolder.Folders;
                foreach (var folder in folders)
                {
                    Folder folderExists = null;
                    foreach (var item in searchFolderRoot)
                    {
                        if (item.Name == folder)
                        {
                            folderExists = item;
                            break;
                        }
                    }

                    if (folderExists == null)
                    {
                        Logger.LogDebug(Guid.Empty, Guid.Empty, "(CreateFoldersIfNotExists) Creating folder {0}", folder);
                        var f = searchFolderRoot.Add(folder);
                        context.Load(f, p => p.Folders);
                        context.ExecuteQuery();
                        searchFolderRoot = f.Folders;
                    }
                    else
                    {
                        searchFolderRoot = folderExists.Folders;
                        context.Load(searchFolderRoot);
                        context.ExecuteQuery();
                    }
                }
            }
        }
    }
}
