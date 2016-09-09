using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hardcodet.Wpf.TaskbarNotification;
using SPSync.Core;
using System.Collections.ObjectModel;
using System.ComponentModel;
using SPSync.Core.Common;
using SPSync.Core.Metadata;
using System.Net;
using System.Windows;
using System.Threading.Tasks;

namespace SPSync
{
    internal class MainViewModel : INotifyPropertyChanged
    {
        private SyncService syncService;
        internal bool IsInitialized { get; private set; }
        private ObservableCollection<SyncViewModel> syncModels;
        internal Dictionary<string, ItemStatus> AllNextConflictsCache { get; private set; }

        public event EventHandler<NotifyStatusEventArgs> NotifyStatus;
        public event EventHandler<NotifyInvalidCredentialsEventArgs> NotifyInvalidCredentials;

        private void OnNotifyStatus(string message, BalloonIcon icon)
        {
            if (NotifyStatus != null)
                NotifyStatus(this, new NotifyStatusEventArgs(message, icon));
        }

        private void OnNotifyInvalidCredentials(SyncViewModel conf)
        {
            if (NotifyInvalidCredentials != null)
                NotifyInvalidCredentials(this, new NotifyInvalidCredentialsEventArgs(conf));
        }

        internal void Init()
        {
            AllNextConflictsCache = new Dictionary<string, ItemStatus>();

            syncService = new SyncService();
            syncService.Progress += new EventHandler<SyncServiceProgressEventArgs>(syncService_Progress);
            syncService.Conflict += new EventHandler<SyncServiceConflictEventArgs>(syncService_Conflict);
            syncService.Init();

            syncModels = new ObservableCollection<SyncViewModel>();
            UpdateSyncModels();

            IsInitialized = true;
        }

        internal void AddOrUpdateConfiguration(SyncConfiguration configuration)
        {
            syncService.AddConfig(configuration);

            SyncConfiguration.AllConfigurations[configuration.LocalFolder] = configuration;
            SyncConfiguration.SaveAllConfigurations();

            UpdateSyncModels();
        }

        internal void AddOrUpdateConfiguration(SyncViewModel configuration)
        {
            AddOrUpdateConfiguration(configuration.SyncConfiguration);
        }

        internal void DeleteConfiguration(string localFolder)
        {
            syncService.DeleteConfig(localFolder);

            SyncConfiguration.AllConfigurations.Remove(localFolder);
            SyncConfiguration.SaveAllConfigurations();

            UpdateSyncModels();
        }

        private void UpdateSyncModels()
        {
            syncModels.Clear();
            SyncConfiguration.AllConfigurations.ToList().ForEach(p => syncModels.Add(new SyncViewModel(p.Value)));
            NotifyPropertyChanged("SyncModels");
        }

        internal async Task SyncAsync(string localFolder)
        {
            await syncService.SyncAsync(SyncConfiguration.FindConfiguration(localFolder));
        }

        internal void SyncAll()
        {
            syncService.SyncAll();
        }

        public ObservableCollection<SyncViewModel> SyncModels => syncModels;

        void syncService_Conflict(object sender, SyncServiceConflictEventArgs e)
        {
            // check if user selected to handle all subsequent conflicts in the same way
            if (AllNextConflictsCache.ContainsKey(e.Configuration.LocalFolder))
            {
                e.NewStatus = AllNextConflictsCache[e.Configuration.LocalFolder];
                return;
            }

            App.Current.Dispatcher.Invoke(new Action(delegate
            {
                ConflictWindow cf = new ConflictWindow();
                cf.ShowDialog(e.Configuration, e.Item);
                e.NewStatus = cf.ItemStatus;
            }));
        }

        void syncService_Progress(object sender, SyncServiceProgressEventArgs e)
        {
            var sm = SyncModels.FirstOrDefault(p => p.LocalFolder == e.Configuration.LocalFolder);
            sm.Percent = e.Percent;
            sm.Status = e.Status;
            sm.Message = e.Message;

            if (e.Status == Core.ProgressStatus.Error)
            {
                var webEx = e.InnerException as WebException;
                if (webEx != null)
                {
                    var resp = webEx.Response as HttpWebResponse;
                    if (resp != null && resp.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        MessageBox.Show("Your sync configuration has invalid credentials. Please update them and try again.", "SPSync", MessageBoxButton.OK, MessageBoxImage.Warning);
                        OnNotifyInvalidCredentials(sm);
                    }
                }

                if (AllNextConflictsCache.ContainsKey(e.Configuration.LocalFolder))
                    AllNextConflictsCache.Remove(e.Configuration.LocalFolder);

                sm.Percent = 0;

                OnNotifyStatus("An error occured during sync process: " + Environment.NewLine + e.Message, BalloonIcon.Error);
                return;
            }

            sm.Percent = e.Percent;
            sm.Status = e.Status;
            sm.Message = e.Message;

            switch (e.EventType)
            {
                case SyncEventType.File:
                    break;
                case SyncEventType.Folder:
                    break;
                case SyncEventType.Overall:
                    if (e.Status == ProgressStatus.Completed)
                    {
                        sm.Percent = 0;
                        // reset conflict cache
                        if (AllNextConflictsCache.ContainsKey(e.Configuration.LocalFolder))
                            AllNextConflictsCache.Remove(e.Configuration.LocalFolder);
                    }
                    break;
                case SyncEventType.Unknown:
                default:
                    break;
            }
        }

        private void NotifyPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        internal SyncViewModel GetSyncConfiguration(string localFolder) => SyncModels.FirstOrDefault(p => p.LocalFolder.ToUpper() == localFolder.ToUpper());
    }
}
