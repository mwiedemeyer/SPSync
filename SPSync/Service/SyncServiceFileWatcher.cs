using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SPSync.Core;
using SPSync.Core.Metadata;

namespace SPSync
{
    internal class SyncServiceFileWatcher : IDisposable
    {
        private SyncService _service;
        private SyncConfiguration _config;
        private Queue<FileChange> _fileChangeQueue = new Queue<FileChange>();
        private System.Timers.Timer _fileChangeDetectionTimer;
        private FileSystemWatcher _fs;

        public SyncServiceFileWatcher(SyncService service, SyncConfiguration syncConfig)
        {
            _service = service;
            _config = syncConfig;
            _fileChangeDetectionTimer = new System.Timers.Timer(5000);
            _fileChangeDetectionTimer.Elapsed += DetectChangeTimer;
            Init();
        }

        public void Dispose()
        {
            if (_fs != null)
                _fs.Dispose();
        }

        private void Init()
        {
            try
            {
                _fs = new FileSystemWatcher(_config.LocalFolder);
                _fs.IncludeSubdirectories = true;
                _fs.NotifyFilter = NotifyFilters.Attributes | NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size;
                _fs.Changed += new FileSystemEventHandler(fs_Changed);
                _fs.Created += new FileSystemEventHandler(fs_Changed);
                _fs.Deleted += new FileSystemEventHandler(fs_Changed);
                _fs.Renamed += new RenamedEventHandler(fs_Renamed);
                _fs.EnableRaisingEvents = true;
            }
            catch
            { }

        }

        private void fs_Renamed(object sender, RenamedEventArgs e)
        {
            DetectChange(new FileChange(e.FullPath, e.ChangeType, e.OldFullPath));
        }

        private void fs_Changed(object sender, FileSystemEventArgs e)
        {
            DetectChange(new FileChange(e.FullPath, e.ChangeType));
        }

        private void DetectChange(FileChange change)
        {
            if (change.FullPath.EndsWith(".spsync"))
                return;

            if (Directory.GetParent(change.FullPath).Name == MetadataStore.STOREFOLDER)
                return;

            lock (_fileChangeQueue)
            {
                if (_fileChangeQueue.Count > 0)
                {
                    var prevChange = _fileChangeQueue.Peek();
                    if (prevChange.FullPath == change.FullPath && prevChange.ChangeType == change.ChangeType)
                        return;
                }
                _fileChangeQueue.Enqueue(change);
                if (!_fileChangeDetectionTimer.Enabled)
                {
                    _fileChangeDetectionTimer.Interval = 5000;
                    _fileChangeDetectionTimer.Start();
                }
            }
        }

        private void DetectChangeTimer(object sender, System.Timers.ElapsedEventArgs e)
        {
            lock (_fileChangeQueue)
            {
                // already stopped on last access within the lock
                if (!_fileChangeDetectionTimer.Enabled)
                    return;

                if (_fileChangeQueue.Count < 1)
                {
                    _fileChangeDetectionTimer.Stop();
                    _service.Sync(_config);
                    return;
                }

                var change = _fileChangeQueue.Dequeue();

                _service.SyncLocalFileChange(_config, change.FullPath, change.ChangeType, change.OldFullPath);

                _fileChangeDetectionTimer.Interval = 500;

                if (_fileChangeQueue.Count < 1)
                    _fileChangeDetectionTimer.Stop();
            }
        }

        private class FileChange
        {
            public FileChange(string fullPath, WatcherChangeTypes changeType, string oldFullPath = null)
            {
                FullPath = fullPath;
                OldFullPath = oldFullPath;
                switch (changeType)
                {
                    case WatcherChangeTypes.Created:
                        ChangeType = FileChangeType.Created;
                        break;
                    case WatcherChangeTypes.Deleted:
                        ChangeType = FileChangeType.Deleted;
                        break;
                    case WatcherChangeTypes.Changed:
                        ChangeType = FileChangeType.Changed;
                        break;
                    case WatcherChangeTypes.Renamed:
                        ChangeType = FileChangeType.Renamed;
                        break;
                    default:
                        ChangeType = FileChangeType.Unknown;
                        break;
                }
            }

            public string FullPath { get; set; }
            public string OldFullPath { get; set; }
            public FileChangeType ChangeType { get; set; }
        }
    }
}
