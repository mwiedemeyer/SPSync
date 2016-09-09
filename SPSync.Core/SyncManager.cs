using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Threading;
using SPSync.Core.Metadata;
using SPSync.Core.Common;
using System.Threading.Tasks;
using System.Diagnostics;

namespace SPSync.Core
{
    public class SyncManager
    {
        private string _localFolder;
        private string _originalFolder;
        private SyncConfiguration _configuration;
        private SharePointManager _sharePointManager;
        private MetadataStore _metadataStore;

        public event EventHandler<SyncProgressEventArgs> SyncProgress;
        public event EventHandler<ItemProgressEventArgs> ItemProgress;
        public event EventHandler<ConflictEventArgs> ItemConflict;

        public string ConfigurationName => _configuration.Name;

        protected void OnSyncProgress(int percent, ProgressStatus status, string message = "", Exception innerException = null)
        {
            if (SyncProgress != null)
                SyncProgress(this, new SyncProgressEventArgs(_configuration, percent, status, message, innerException));
        }

        protected void OnItemProgress(int percent, ItemType type, ProgressStatus status, string message = "", Exception innerException = null)
        {
            if (ItemProgress != null)
                ItemProgress(this, new ItemProgressEventArgs(_configuration, percent, type, status, message, innerException));
        }

        protected ItemStatus OnItemConflict(MetadataItem item)
        {
            if (ItemConflict != null)
            {
                var arg = new ConflictEventArgs(_configuration, item.DeepClone());
                ItemConflict(this, arg);
                return arg.NewStatus;
            }
            return ItemStatus.Conflict;
        }

        public SyncManager(string localFolder)
        {
            _originalFolder = localFolder;
            _configuration = SyncConfiguration.FindConfiguration(localFolder);
            _localFolder = _configuration.LocalFolder;
            _sharePointManager = _configuration.GetSharePointManager();
            _metadataStore = new MetadataStore(_localFolder);
        }

        public MetadataItem[] SyncResults { get; private set; }

        public void DownloadFile(string syncFileName)
        {
            string fullSyncFile = Path.Combine(_originalFolder, syncFileName);
            string origFilename = Path.GetFileNameWithoutExtension(syncFileName);
            _sharePointManager.DownloadFile(origFilename, _originalFolder, File.GetLastWriteTimeUtc(fullSyncFile));
            File.Delete(fullSyncFile);
        }

        public async Task SynchronizeAsync(bool reviewOnly = false, bool rescanLocalFiles = true)
        {
            await Task.Run(() =>
            {
                lock (this)
                {
                    Synchronize(reviewOnly, rescanLocalFiles);
                }
            });
        }

        public int Synchronize(bool reviewOnly = false, bool rescanLocalFiles = true)
        {
            var countChanged = 0;
            bool moreChangesFound = false;
            lock (this)
            {
                OnSyncProgress(0, ProgressStatus.Analyzing);

                try
                {
                    countChanged = SyncMetadataStore(reviewOnly, _configuration.ConflictHandling, out moreChangesFound, rescanLocalFiles);

                    SyncResults = _metadataStore.GetResults();

                    OnSyncProgress(10, ProgressStatus.Analyzed, string.Format("Found {0} modified items", countChanged));

                    if (reviewOnly || countChanged < 1)
                    {
                        OnSyncProgress(100, ProgressStatus.Completed);
                        return countChanged;
                    }

                    SyncChanges();

                    OnSyncProgress(100, ProgressStatus.Completed);
                }
                catch (Exception ex)
                {
                    //todo:
                    if (_configuration.AuthenticationType == AuthenticationType.ADFS) //&& ex is webexception 403
                    {
                        Adfs.AdfsHelper.InValidateCookie();
                    }
                    OnSyncProgress(100, ProgressStatus.Error, "An error has occured: " + ex.Message, ex);
                    return -1;
                }
            }

            if (moreChangesFound)
                countChanged += Synchronize(reviewOnly, rescanLocalFiles);

            return countChanged;
        }

        public void SynchronizeLocalFileChange(string fullPath, FileChangeType changeType, string oldFullPath)
        {
            lock (this)
            {
                Logger.LogDebug("SynchronizeLocalFileChange Path={0} ChangeType={1} OldPath={2}", fullPath, changeType, oldFullPath);

                OnSyncProgress(0, ProgressStatus.Analyzing);

                try
                {
                    var syncToRemote = (_configuration.Direction == SyncDirection.LocalToRemote || _configuration.Direction == SyncDirection.Both);
                    var syncToLocal = _configuration.Direction == SyncDirection.RemoteToLocal || _configuration.Direction == SyncDirection.Both;

                    if (!syncToRemote)
                    {
                        OnSyncProgress(100, ProgressStatus.Completed);
                        return;
                    }

                    if (!_configuration.ShouldFileSync(fullPath))
                    {
                        OnSyncProgress(100, ProgressStatus.Completed);
                        return;
                    }

                    var localExtension = Path.GetExtension(fullPath);
                    if (localExtension == ".spsync")
                    {
                        OnSyncProgress(100, ProgressStatus.Completed);
                        return;
                    }

                    var isDirectory = false;

                    if (changeType != FileChangeType.Deleted)
                    {
                        try
                        {
                            if (File.GetAttributes(fullPath).HasFlag(FileAttributes.Hidden))
                            {
                                OnSyncProgress(100, ProgressStatus.Completed);
                                return;
                            }

                            if (File.GetAttributes(fullPath).HasFlag(FileAttributes.Directory))
                                isDirectory = true;

                            if (isDirectory)
                            {
                                if (Path.GetDirectoryName(fullPath) == MetadataStore.STOREFOLDER)
                                {
                                    OnSyncProgress(100, ProgressStatus.Completed);
                                    return;
                                }
                            }
                            else
                            {
                                if (Directory.GetParent(fullPath).Name == MetadataStore.STOREFOLDER)
                                {
                                    OnSyncProgress(100, ProgressStatus.Completed);
                                    return;
                                }
                            }
                        }
                        catch
                        {
                            OnSyncProgress(100, ProgressStatus.Completed);
                            return;
                        }
                    }

                    MetadataItem item = null;

                    if (string.IsNullOrEmpty(oldFullPath))
                    {
                        item = _metadataStore.GetByFileName(fullPath);
                    }
                    else
                    {
                        item = _metadataStore.GetByFileName(oldFullPath);
                        if (item == null)
                        {
                            changeType = FileChangeType.Changed;
                            item = _metadataStore.GetByFileName(fullPath);
                        }
                    }

                    if (item == null)
                    {
                        if (changeType != FileChangeType.Deleted)
                        {
                            if (_metadataStore.GetByFileName(fullPath) == null)
                                _metadataStore.Add(new MetadataItem(fullPath, isDirectory ? ItemType.Folder : ItemType.File));
                        }
                    }
                    else
                    {
                        item.UpdateWithLocalInfo(_configuration.ConflictHandling, Guid.NewGuid());
                        if (item.Status == ItemStatus.Conflict)
                            item.Status = OnItemConflict(item);

                        if (changeType == FileChangeType.Renamed)
                        {
                            item.NewNameAfterRename = Path.GetFileName(fullPath); //works for directories as well
                            item.Status = ItemStatus.RenamedLocal;

                            if (isDirectory)
                            {
                                foreach (var itemInFolder in _metadataStore.Items.Where(p => p.LocalFile.Contains(item.LocalFile)))
                                {
                                    if (itemInFolder.Id == item.Id)
                                        continue;
                                    itemInFolder.LocalFolder = itemInFolder.LocalFolder.Replace(item.LocalFile, fullPath);
                                }
                            }
                        }
                    }

                    if (changeType == FileChangeType.Deleted && item != null)
                        item.Status = ItemStatus.DeletedLocal;

                    _metadataStore.Save();

                    SyncChanges();

                    OnSyncProgress(100, ProgressStatus.Completed);
                }
                catch (Exception ex)
                {
                    //todo:
                    if (_configuration.AuthenticationType == AuthenticationType.ADFS) //&& ex is webexception 403
                    {
                        Adfs.AdfsHelper.InValidateCookie();
                    }
                    OnSyncProgress(100, ProgressStatus.Error, "An error has occured: " + ex.Message, ex);
                    return;
                }
            }
        }

        private int SyncMetadataStore(bool doNotSave, ConflictHandling conflictHandling, out bool moreChangesFound, bool rescanLocalFiles = true)
        {
            var sumWatch = Stopwatch.StartNew();

            var correlationId = Guid.NewGuid();

            //reset item status for all items except the ones with errors
            _metadataStore.Items.Where(p => p.Status != ItemStatus.Conflict).ToList().ForEach(p => { p.Status = ItemStatus.Unchanged; p.HasError = false; });

            var watch = Stopwatch.StartNew();

            var remoteFileList = _sharePointManager.GetChangedFiles(_metadataStore, (percent, currentFile) =>
             {
                 OnSyncProgress(percent, ProgressStatus.Analyzing, $"Processing remote changes... '{currentFile}'");
             });

            watch.Stop();

            var syncToRemote = (_configuration.Direction == SyncDirection.LocalToRemote || _configuration.Direction == SyncDirection.Both);
            var syncToLocal = _configuration.Direction == SyncDirection.RemoteToLocal || _configuration.Direction == SyncDirection.Both;

            var searchOption = SearchOption.AllDirectories;

            if (rescanLocalFiles)
            {
                OnSyncProgress(0, ProgressStatus.Analyzing, "Processing local changes");

                #region Iterate local files/folders

                watch = Stopwatch.StartNew();

                Parallel.ForEach(Directory.EnumerateDirectories(_localFolder, "*", searchOption), localFolder =>
                {
                    if (!syncToRemote)
                        return;

                    if (!_configuration.ShouldFileSync(localFolder))
                        return;

                    if (File.GetAttributes(localFolder).HasFlag(FileAttributes.Hidden))
                        return;

                    if (Path.GetDirectoryName(localFolder) == MetadataStore.STOREFOLDER)
                        return;

                    var item = _metadataStore.GetByFileName(localFolder);
                    if (item == null)
                    {
                        _metadataStore.Add(new MetadataItem(localFolder, ItemType.Folder));
                    }
                    else
                    {
                        item.UpdateWithLocalInfo(conflictHandling, correlationId);
                        if (item.Status == ItemStatus.Conflict)
                            item.Status = OnItemConflict(item);
                    }
                });

                watch.Stop();
                watch = Stopwatch.StartNew();

                // update store for local files
                Parallel.ForEach(Directory.EnumerateFiles(_localFolder, "*.*", searchOption), localFile =>
                //foreach (var localFile in Directory.EnumerateFiles(_localFolder, "*.*", searchOption))
                {
                    if (!syncToRemote)
                        return;

                    if (!_configuration.ShouldFileSync(localFile))
                        return;

                    var localExtension = Path.GetExtension(localFile);
                    if (localExtension == ".spsync")
                        return;

                    if (File.GetAttributes(localFile).HasFlag(FileAttributes.Hidden))
                        return;

                    if (Directory.GetParent(localFile).Name == MetadataStore.STOREFOLDER)
                        return;

                    var item = _metadataStore.GetByFileName(localFile);
                    if (item == null)
                    {
                        _metadataStore.Add(new MetadataItem(localFile, ItemType.File));
                    }
                    else
                    {
                        item.UpdateWithLocalInfo(conflictHandling, correlationId);
                        if (item.Status == ItemStatus.Conflict)
                            item.Status = OnItemConflict(item);
                    }
                });

                watch.Stop();

                #endregion
            }

            #region Iterate remote files/folders

            // update store for remote files/folders
            foreach (var remoteItem in remoteFileList)
            {
                if (!syncToLocal)
                    continue;

                var localFile = new DirectoryInfo(Path.Combine(_localFolder, remoteItem.FullFileName)).FullName;

                if (!_configuration.ShouldFileSync(localFile))
                    continue;

                string fn = localFile;
                if (remoteItem.Type == ItemType.Folder)
                    fn = Path.Combine(localFile, remoteItem.Name);

                var item = _metadataStore.GetByItemId(remoteItem.Id);
                if (remoteItem.ChangeType == Microsoft.SharePoint.Client.ChangeType.Add)
                {
                    // new
                    if (item == null)
                    {
                        _metadataStore.Add(new MetadataItem(remoteItem.Id, remoteItem.ETag, remoteItem.Name, Path.GetDirectoryName(localFile), remoteItem.LastModified, remoteItem.Type));
                    }
                    // remote and local change
                    else
                    {
                        item.UpdateWithRemoteInfo(remoteItem.Id, remoteItem.ETag, remoteItem.LastModified, conflictHandling, correlationId);
                        if (item.Status == ItemStatus.Conflict)
                            item.Status = OnItemConflict(item);
                    }
                }
                if (remoteItem.ChangeType == Microsoft.SharePoint.Client.ChangeType.DeleteObject)
                {
                    if (item != null)
                        item.Status = ItemStatus.DeletedRemote;
                }
                if (remoteItem.ChangeType == Microsoft.SharePoint.Client.ChangeType.Rename)
                {
                    if (item == null)
                    {
                        _metadataStore.Add(new MetadataItem(remoteItem.Id, remoteItem.ETag, remoteItem.Name, Path.GetDirectoryName(localFile), remoteItem.LastModified, remoteItem.Type));
                    }
                    else
                    {
                        if (item.Name != remoteItem.Name)
                        {
                            item.NewNameAfterRename = remoteItem.Name;
                            item.Status = ItemStatus.RenamedRemote;

                            if (item.Type == ItemType.Folder)
                            {
                                foreach (var itemInFolder in _metadataStore.Items.Where(p => p.LocalFile.Contains(item.LocalFile)))
                                {
                                    if (itemInFolder.Id == item.Id)
                                        continue;
                                    var newFolder = _localFolder + remoteItem.FullFileName.Substring(1);
                                    itemInFolder.LocalFolder = itemInFolder.LocalFolder.Replace(item.LocalFile, newFolder);
                                    itemInFolder.HasError = true;
                                }
                            }
                        }
                        else
                        {
                            item.Status = ItemStatus.Unchanged;
                        }
                    }
                }
                if (remoteItem.ChangeType == Microsoft.SharePoint.Client.ChangeType.Update)
                {
                    // new
                    if (item == null)
                    {
                        _metadataStore.Add(new MetadataItem(remoteItem.Id, remoteItem.ETag, remoteItem.Name, Path.GetDirectoryName(localFile), remoteItem.LastModified, remoteItem.Type));
                    }
                    else
                    {
                        item.UpdateWithRemoteInfo(remoteItem.Id, remoteItem.ETag, remoteItem.LastModified, conflictHandling, correlationId);
                        if (item.Status == ItemStatus.Conflict)
                            item.Status = OnItemConflict(item);
                    }
                }
            }

            #endregion

            #region Check for deleted files/folders

            var itemsToDelete = new List<Guid>();

            // update store: files
            foreach (var item in _metadataStore.Items.Where(p => p.Status == ItemStatus.Unchanged && p.Type == ItemType.File && !p.HasError))
            {
                var path = item.LocalFile;

                if (!_configuration.ShouldFileSync(path))
                    continue;

                if (!File.Exists(path) && !File.Exists(path + ".spsync"))
                {
                    item.Status = ItemStatus.DeletedLocal;
                }
                //if (remoteFileList.Count(p => p.Id == item.SharePointId) < 1)
                //{
                //    if (item.Status == ItemStatus.DeletedLocal)
                //    {
                //        itemsToDelete.Add(item.Id);
                //    }
                //    item.Status = ItemStatus.DeletedRemote;
                //}
            }

            // update store: folders
            foreach (var item in _metadataStore.Items.Where(p => p.Status == ItemStatus.Unchanged && p.Type == ItemType.Folder && !p.HasError))
            {
                var relFile = item.LocalFile.Replace(_localFolder, string.Empty).TrimStart('.', '\\');
                var path = item.LocalFile;

                if (!_configuration.ShouldFileSync(path))
                    continue;

                if (!Directory.Exists(path))
                {
                    item.Status = ItemStatus.DeletedLocal;

                    // delete all dependend items
                    _metadataStore.Items.Where(p => p.LocalFolder == item.LocalFile).ToList().ForEach(p => { if (!itemsToDelete.Contains(p.Id)) itemsToDelete.Add(p.Id); });
                }
                //if (remoteFileList.Count(p => p.FullFileName.Replace(_localFolder, string.Empty).TrimStart('.', '\\') + p.Name == relFile) < 1)
                //{
                //    if (item.Status == ItemStatus.DeletedLocal)
                //    {
                //        if (!itemsToDelete.Contains(item.Id))
                //            itemsToDelete.Add(item.Id);
                //    }
                //    item.Status = ItemStatus.DeletedRemote;
                //}
            }

            #endregion

            itemsToDelete.ForEach(p => _metadataStore.Delete(p));

            var countChanged = _metadataStore.Items.Count(p => p.Status != ItemStatus.Unchanged);

            _metadataStore.Items.Where(p => p.Status != ItemStatus.Unchanged).ToList().ForEach(p =>
            {
                Logger.LogDebug(correlationId, p.Id, "(Result) Item Name={0}, Status={1}, HasError={2}, LastError={3}", p.Name, p.Status, p.HasError, p.LastError);
            });

            // reset error flag
            //_metadataStore.Items.Where(p => p.HasError && p.Status != ItemStatus.Unchanged).ToList().ForEach(p => p.HasError = false);

            if (!doNotSave)
                _metadataStore.Save();

            sumWatch.Stop();

            moreChangesFound = remoteFileList.Count > 0;

            return countChanged;
        }

        private void SyncChanges()
        {
            var allFolders = from p in _metadataStore.Items
                             where p.Status != ItemStatus.Unchanged && p.Type == ItemType.Folder && !p.HasError
                             select p;

            var allFiles = from p in _metadataStore.Items
                           where p.Status != ItemStatus.Unchanged && p.Type == ItemType.File && !p.HasError
                           select p;

            List<Guid> itemsToDelete = new List<Guid>();

            var syncToRemote = (_configuration.Direction == SyncDirection.LocalToRemote || _configuration.Direction == SyncDirection.Both);
            var syncToLocal = _configuration.Direction == SyncDirection.RemoteToLocal || _configuration.Direction == SyncDirection.Both;

            #region Sync Folder changes

            int countFolders = 0;
            int countAllFolders = allFolders.Count();

            OnSyncProgress(20, ProgressStatus.Running, string.Format("Syncing {0} folders...", countAllFolders));

            foreach (var item in allFolders)
            {
                countFolders++;

                var relFolder = item.LocalFolder.Replace(_localFolder, string.Empty).TrimStart('.', '\\');
                if (item.Status == ItemStatus.UpdatedLocal && syncToRemote)
                {
                    OnItemProgress((int)(((double)countFolders / (double)countAllFolders) * 100), ItemType.Folder, ProgressStatus.Running, string.Format("Updating remote folder {0}...", item.Name));

                    try
                    {
                        item.SharePointId = _sharePointManager.CreateFolder(relFolder, item.Name);
                        item.LastModified = Directory.GetLastWriteTimeUtc(item.LocalFile);
                        item.Status = ItemStatus.Unchanged;
                    }
                    catch (Exception ex)
                    {
                        OnItemProgress((int)(((double)countFolders / (double)countAllFolders) * 100), ItemType.Folder, ProgressStatus.Warning, "Remote folder could not be created. Ignoring...", ex);
                    }
                }
                else if (item.Status == ItemStatus.UpdatedRemote && syncToLocal)
                {
                    OnItemProgress((int)(((double)countFolders / (double)countAllFolders) * 100), ItemType.Folder, ProgressStatus.Running, string.Format("Updating local folder {0}...", item.Name));

                    if (!Directory.Exists(item.LocalFile))
                        Directory.CreateDirectory(item.LocalFile);

                    item.LastModified = Directory.GetLastWriteTimeUtc(item.LocalFile);
                    item.Status = ItemStatus.Unchanged;
                }
                else if (item.Status == ItemStatus.DeletedLocal && syncToRemote)
                {
                    OnItemProgress((int)(((double)countFolders / (double)countAllFolders) * 100), ItemType.Folder, ProgressStatus.Running, string.Format("Deleting remote folder {0}...", item.Name));

                    try
                    {
                        _sharePointManager.DeleteFolder(relFolder, item.Name);
                    }
                    catch (Exception ex)
                    {
                        OnItemProgress((int)(((double)countFolders / (double)countAllFolders) * 100), ItemType.Folder, ProgressStatus.Warning, "Remote folder could not be delete. Ignoring...", ex);
                    }
                    itemsToDelete.Add(item.Id);
                }
                else if (item.Status == ItemStatus.DeletedRemote && syncToLocal)
                {
                    OnItemProgress((int)(((double)countFolders / (double)countAllFolders) * 100), ItemType.Folder, ProgressStatus.Running, string.Format("Deleting local folder {0}...", item.Name));

                    if (Directory.Exists(item.LocalFile))
                        FileHelper.RecycleDirectory(item.LocalFile);

                    itemsToDelete.Add(item.Id);
                }
                else if (item.Status == ItemStatus.RenamedRemote)
                {
                    OnItemProgress((int)(((double)countFolders / (double)countAllFolders) * 100), ItemType.Folder, ProgressStatus.Running, string.Format("Renaming local folder {0} to {1}...", item.Name, item.NewNameAfterRename));

                    try
                    {
                        var newFilePath = Path.Combine(item.LocalFolder, item.NewNameAfterRename);
                        if (Directory.Exists(item.LocalFile))
                        {
                            Directory.Move(item.LocalFile, newFilePath);
                            item.LastModified = Directory.GetLastWriteTimeUtc(newFilePath);
                        }

                        item.Name = item.NewNameAfterRename;
                        item.Status = ItemStatus.Unchanged;
                    }
                    catch (IOException ex)
                    {
                        item.Status = ItemStatus.Unchanged;

                        OnItemProgress((int)(((double)countFolders / (double)countAllFolders) * 100), ItemType.Folder, ProgressStatus.Warning, "Folder locked. Trying again later...", ex);
                    }
                }
                else if (item.Status == ItemStatus.RenamedLocal)
                {
                    OnItemProgress((int)(((double)countFolders / (double)countAllFolders) * 100), ItemType.Folder, ProgressStatus.Running, string.Format("Renaming remote folder {0} to {1}...", item.Name, item.NewNameAfterRename));

                    try
                    {
                        _sharePointManager.RenameItem(item.SharePointId, item.NewNameAfterRename);

                        item.Name = item.NewNameAfterRename;
                        item.Status = ItemStatus.Unchanged;
                    }
                    catch (IOException ex)
                    {
                        item.Status = ItemStatus.Unchanged;

                        OnItemProgress((int)(((double)countFolders / (double)countAllFolders) * 100), ItemType.Folder, ProgressStatus.Warning, "Folder locked. Trying again later...", ex);
                    }
                }
                else if (item.Status == ItemStatus.Conflict)
                {
                    OnItemProgress((int)(((double)countFolders / (double)countAllFolders) * 100), ItemType.Folder, ProgressStatus.Conflict, string.Format("Folder conflict: {0}. No changes have been made.", item.Name));
                }
            }

            #endregion

            #region Sync File changes

            int countFiles = 0;
            int countAllFiles = allFiles.Count();

            OnSyncProgress(60, ProgressStatus.Running, string.Format("Syncing {0} files...", countAllFiles));

            //Parallel.ForEach(allFiles, (item) =>
            //{
            foreach (var item in allFiles)
            {
                countFiles++;

                try
                {
                    var relFolder = item.LocalFolder.Replace(_localFolder, string.Empty).TrimStart('.', '\\');
                    var relFile = item.LocalFile.Replace(_localFolder, string.Empty).TrimStart('.', '\\');
                    if (item.Status == ItemStatus.UpdatedLocal && syncToRemote)
                    {
                        OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Running, string.Format("Updating remote file {0}...", item.Name));

                        // if not exists anymore (deleted between metadata and real sync)
                        if (!File.Exists(item.LocalFile))
                        {
                            item.Status = ItemStatus.DeletedLocal;
                            return;
                        }

                        try
                        {
                            _sharePointManager.CreateFoldersIfNotExists(relFolder);
                            int id = _sharePointManager.UploadFile(relFile, item.LocalFile);
                            Logger.LogDebug("File uploaded with id: {0}", id);

                            int etag;
                            var remoteTimestamp = _sharePointManager.GetFileTimestamp(relFile, out etag);
                            item.ETag = etag;
                            Logger.LogDebug("Got ETag from remote file: {0}", etag);

                            item.SharePointId = id;
                            item.LastModified = File.GetLastWriteTimeUtc(item.LocalFile);
                            item.Status = ItemStatus.Unchanged;
                        }
                        catch (IOException ex)
                        {
                            item.LastModified = item.LastModified - new TimeSpan(365, 0, 0, 0);
                            item.Status = ItemStatus.Unchanged;

                            OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Warning, "File locked. Trying again later...", ex);
                        }
                    }
                    else if (item.Status == ItemStatus.UpdatedRemote && syncToLocal)
                    {
                        OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Running, string.Format("Updating local file {0}...", item.Name));

                        string fullNameNotSynchronized = item.LocalFile + ".spsync";

                        int etag;
                        var remoteTimestamp = _sharePointManager.GetFileTimestamp(relFile, out etag);
                        item.ETag = etag;
                        item.LastModified = remoteTimestamp;

                        try
                        {
                            if (File.Exists(item.LocalFile))
                            {
                                _sharePointManager.DownloadFile(Path.GetFileName(item.LocalFile), Path.GetDirectoryName(item.LocalFile), remoteTimestamp);
                                item.Status = ItemStatus.Unchanged;
                                return;
                            }

                            if (File.Exists(fullNameNotSynchronized))
                            {
                                item.Status = ItemStatus.Unchanged;
                                return;
                            }

                            CreateFoldersIfNotExists(item.LocalFolder);

                            if (_configuration.DownloadHeadersOnly)
                            {
                                File.Create(fullNameNotSynchronized).Close();
                                File.SetLastWriteTimeUtc(fullNameNotSynchronized, remoteTimestamp);
                            }
                            else
                            {
                                _sharePointManager.DownloadFile(Path.GetFileName(item.LocalFile), Path.GetDirectoryName(item.LocalFile), remoteTimestamp);
                            }

                            item.Status = ItemStatus.Unchanged;
                        }
                        catch (System.Net.WebException ex)
                        {
                            if (ex.InnerException != null && ex.InnerException is IOException)
                            {
                                //item.Status = ItemStatus.Unchanged;

                                OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Warning, "File locked. Trying again later...", ex);
                            }
                            else
                            {
                                item.HasError = true;
                                item.LastError = ex.Message;

                                OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Error, "WebException", ex);
                                throw;
                            }
                        }
                    }
                    else if (item.Status == ItemStatus.DeletedLocal && syncToRemote)
                    {
                        OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Running, string.Format("Deleting remote file {0}...", item.Name));

                        try
                        {
                            _sharePointManager.DeleteFile(item.SharePointId);
                        }
                        catch (Exception ex)
                        {
                            OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Error, "Deleting from SharePoint failed. Ignoring...", ex);
                        }
                        itemsToDelete.Add(item.Id);
                    }
                    else if (item.Status == ItemStatus.DeletedRemote && syncToLocal)
                    {
                        OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Running, string.Format("Deleting local file {0}...", item.Name));

                        try
                        {
                            if (File.Exists(item.LocalFile))
                                FileHelper.RecycleFile(item.LocalFile);
                            if (File.Exists(item.LocalFile + ".spsync"))
                                FileHelper.RecycleFile(item.LocalFile + ".spsync");

                            itemsToDelete.Add(item.Id);
                        }
                        catch (IOException ex)
                        {
                            item.Status = ItemStatus.Unchanged;

                            OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Warning, "File locked. Trying again later...", ex);
                        }
                    }
                    else if (item.Status == ItemStatus.RenamedRemote)
                    {
                        OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Running, string.Format("Renaming local file {0} to {1}...", item.Name, item.NewNameAfterRename));

                        try
                        {
                            var newFilePath = Path.Combine(item.LocalFolder, item.NewNameAfterRename);
                            if (File.Exists(item.LocalFile))
                            {
                                File.Move(item.LocalFile, newFilePath);
                                item.LastModified = File.GetLastWriteTimeUtc(newFilePath);
                            }
                            if (File.Exists(item.LocalFile + ".spsync"))
                            {
                                File.Move(item.LocalFile + ".spsync", newFilePath + ".spsync");
                                item.LastModified = File.GetLastWriteTimeUtc(newFilePath + ".spsync");
                            }

                            item.Name = item.NewNameAfterRename;
                            item.Status = ItemStatus.Unchanged;
                        }
                        catch (IOException ex)
                        {
                            item.Status = ItemStatus.Unchanged;

                            OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Warning, "File locked. Trying again later...", ex);
                        }
                    }
                    else if (item.Status == ItemStatus.RenamedLocal)
                    {
                        OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Running, string.Format("Renaming remote file {0} to {1}...", item.Name, item.NewNameAfterRename));

                        try
                        {
                            _sharePointManager.RenameItem(item.SharePointId, item.NewNameAfterRename);

                            item.Name = item.NewNameAfterRename;
                            item.Status = ItemStatus.Unchanged;
                        }
                        catch (IOException ex)
                        {
                            item.Status = ItemStatus.Unchanged;

                            OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Warning, "File locked. Trying again later...", ex);
                        }
                    }
                    else if (item.Status == ItemStatus.Conflict)
                    {
                        try
                        {
                            // to keep both files, rename the local one
                            var newFileCopy = Path.Combine(item.LocalFolder, Path.GetFileNameWithoutExtension(item.LocalFile) + "_" + Environment.MachineName + "_" + DateTime.Now.Ticks.ToString() + Path.GetExtension(item.LocalFile));
                            File.Copy(item.LocalFile, newFileCopy);
                            File.SetLastWriteTimeUtc(item.LocalFile, item.LastModified.AddHours(-1));
                            item.Status = ItemStatus.Unchanged;
                            item.ETag = 0;
                            OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Conflict, string.Format("File conflict: {0}. Renamed local file.", item.Name));
                        }
                        catch (Exception ex)
                        {
                            OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Conflict, string.Format("File conflict: {0}. No changes have been made.", item.Name), ex);
                        }

                    }
                }
                catch (Exception ex)
                {
                    item.HasError = true;
                    item.LastError = ex.Message;
                    //if (ex.GetType() == Type.GetType("Microsoft.SharePoint.SoapServer.SoapServerException"))
                    var soapEx = ex as System.Web.Services.Protocols.SoapException;
                    if (soapEx != null)
                    {
                        var detail = soapEx.Detail != null ? soapEx.Detail.OuterXml : "n/a";
                        OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Error, string.Format("Error updating file {0}... - {1}\nDetails: {2}", item.Name, soapEx.Message, detail), soapEx);
                    }
                    else
                        OnItemProgress((int)(((double)countFiles / (double)countAllFiles) * 100), ItemType.File, ProgressStatus.Error, string.Format("Error updating file {0}... - {1}", item.Name, ex.Message), ex);
                }
            }

            #endregion

            OnSyncProgress(90, ProgressStatus.Running, "Finalizing...");

            itemsToDelete.ForEach(p => _metadataStore.Delete(p));

            _metadataStore.Save();
        }

        private void CreateFoldersIfNotExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            Directory.CreateDirectory(path);
        }
    }
}
