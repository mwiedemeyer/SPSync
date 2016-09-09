using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using SPSync.Core;
using System.Threading;
using SPSync.Core.Common;
using SPSync.Core.Metadata;
using System.Threading.Tasks;

namespace SPSync
{
    internal class SyncService
    {
        #region Events

        internal event EventHandler<SyncServiceProgressEventArgs> Progress;
        internal event EventHandler<SyncServiceConflictEventArgs> Conflict;

        private void OnProgress(SyncConfiguration configuration, ItemType type, ProgressStatus status, int percent, string message, Exception innerException = null)
        {
            if (Progress != null)
            {
                SyncEventType t = SyncEventType.Unknown;
                if (type == ItemType.File)
                    t = SyncEventType.File;
                if (type == ItemType.Folder)
                    t = SyncEventType.Folder;
                Progress(this, new SyncServiceProgressEventArgs(configuration, t, status, percent, message, innerException));
            }
        }

        private void OnProgress(SyncConfiguration configuration, ProgressStatus status, int percent, string message, Exception innerException = null)
        {
            if (Progress != null)
                Progress(this, new SyncServiceProgressEventArgs(configuration, SyncEventType.Overall, status, percent, message, innerException));
        }

        private ItemStatus OnConflict(SyncConfiguration configuration, MetadataItem item, ItemStatus status)
        {
            var stat = status;
            if (Conflict != null)
            {
                var args = new SyncServiceConflictEventArgs(configuration, item, status);
                Conflict(this, args);
                stat = args.NewStatus;
            }
            return stat;
        }

        #endregion

        private Dictionary<string, SyncServiceFileWatcher> _watchers = new Dictionary<string, SyncServiceFileWatcher>();
        private Dictionary<string, SyncManager> _syncManagers = new Dictionary<string, SyncManager>();
        private Dictionary<string, bool> _initialSyncCache = new Dictionary<string, bool>();

        internal SyncService()
        {
        }

        internal void Init()
        {
            foreach (var confItem in SyncConfiguration.AllConfigurations)
            {
                _watchers.Add(confItem.Value.LocalFolder, new SyncServiceFileWatcher(this, confItem.Value));
            }
        }

        internal void DeleteConfig(string localFolder)
        {
            var fs = _watchers[localFolder];
            fs.Dispose();
            _watchers.Remove(localFolder);

            MetadataStore.DeleteStoreForFolder(localFolder);
        }

        internal void AddConfig(SyncConfiguration conf)
        {
            if (!_watchers.ContainsKey(conf.LocalFolder))
                _watchers.Add(conf.LocalFolder, new SyncServiceFileWatcher(this, conf));

            MetadataStore.DeleteChangeTokenForFolder(conf.LocalFolder);

            if (!_syncManagers.ContainsKey(conf.LocalFolder))
                return;

            _syncManagers.Remove(conf.LocalFolder);
        }

        internal async Task SyncAsync(SyncConfiguration conf)
        {
            var manager = GetSyncManager(conf);

            var rescanLocalFiles = true;
            if (_initialSyncCache.ContainsKey(conf.LocalFolder))
                rescanLocalFiles = _initialSyncCache[conf.LocalFolder];

            await manager.SynchronizeAsync(rescanLocalFiles: rescanLocalFiles);

            _initialSyncCache[conf.LocalFolder] = false;
        }

        internal int Sync(SyncConfiguration conf)
        {
            var manager = GetSyncManager(conf);

            var rescanLocalFiles = true;
            if (_initialSyncCache.ContainsKey(conf.LocalFolder))
                rescanLocalFiles = _initialSyncCache[conf.LocalFolder];

            var changeCount = manager.Synchronize(rescanLocalFiles: rescanLocalFiles);

            _initialSyncCache[conf.LocalFolder] = false;

            return changeCount;
        }

        internal void SyncLocalFileChange(SyncConfiguration conf, string fullPath, FileChangeType changeType, string oldFullPath = null)
        {
            var manager = GetSyncManager(conf);

            manager.SynchronizeLocalFileChange(fullPath, changeType, oldFullPath);
        }

        private SyncManager GetSyncManager(SyncConfiguration conf)
        {
            if (!_syncManagers.ContainsKey(conf.LocalFolder))
            {
                lock (_syncManagers)
                {
                    if (!_syncManagers.ContainsKey(conf.LocalFolder))
                    {
                        SyncManager manager = new SyncManager(conf.LocalFolder);
                        manager.SyncProgress += new EventHandler<SyncProgressEventArgs>(manager_SyncProgress);
                        manager.ItemProgress += new EventHandler<ItemProgressEventArgs>(manager_ItemProgress);
                        manager.ItemConflict += new EventHandler<ConflictEventArgs>(manager_ItemConflict);
                        _syncManagers.Add(conf.LocalFolder, manager);
                    }
                }
            }

            return _syncManagers[conf.LocalFolder];
        }

        internal void SyncAll()
        {
            foreach (var confItem in SyncConfiguration.AllConfigurations)
            {
                var t = SyncAsync(confItem.Value);
            }
        }

        private void manager_ItemProgress(object sender, ItemProgressEventArgs e)
        {
            OnProgress(e.Configuration, e.ItemType, e.Status, e.Percent, e.Message, e.InnerException);

            Logger.Log("[{4}] [{5}] {2} Item ({3}): {1}% - {0}", e.Message, e.Percent, e.Status, e.ItemType, DateTime.Now, e.Configuration.Name);
            if (e.InnerException != null)
                Logger.Log("[{0}] [{1}] {2}{3}{4}", DateTime.Now, e.Configuration.Name, e.InnerException.Message, Environment.NewLine, e.InnerException.StackTrace);
        }

        private void manager_SyncProgress(object sender, SyncProgressEventArgs e)
        {
            OnProgress(e.Configuration, e.Status, e.Percent, e.Message, e.InnerException);

            Logger.Log("[{3}] [{4}] {2} Sync: {1}% - {0}", e.Message, e.Percent, e.Status, DateTime.Now, e.Configuration.Name);
            if (e.InnerException != null)
            {
                var ie = e.InnerException;
                while (ie != null)
                {
                    Logger.Log("[{0}] [{1}] {2}{3}{4}", DateTime.Now, e.Configuration.Name, ie.Message, Environment.NewLine, ie.StackTrace);
                    ie = ie.InnerException;
                }
            }
        }

        private void manager_ItemConflict(object sender, ConflictEventArgs e)
        {
            e.NewStatus = OnConflict(e.Configuration, e.Item, e.NewStatus);

            Logger.Log("[{0}] [{3}] Conflict {1} - New Status: {2}", DateTime.Now, e.Item.Name, e.NewStatus, e.Configuration.Name);
        }
    }
}
