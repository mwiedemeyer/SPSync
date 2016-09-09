using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SPSync.Core;
using System.ComponentModel;
using SPSync.Core.Common;
using SPSync.Core.Metadata;

namespace SPSync
{
    internal class SyncViewModel : INotifyPropertyChanged
    {
        internal SyncConfiguration SyncConfiguration { get; set; }
        private int _percent;
        private ProgressStatus _status;
        private string _message;

        public string[] ConflictModes => Enum.GetNames(typeof(ConflictHandling));

        public string[] AuthenticationTypes => Enum.GetNames(typeof(AuthenticationType));

        public string[] Directions => Enum.GetNames(typeof(SyncDirection));

        internal SyncViewModel(SyncConfiguration configuration)
        {
            SyncConfiguration = configuration;            
        }

        #region Properties

        public IEnumerable<MetadataItem> ItemsWithErrors => new MetadataStore(SyncConfiguration.LocalFolder).Items.Where(p => p.HasError);

        public int Percent
        {
            get { return _percent; }
            set
            {
                if (_percent != value)
                {
                    _percent = value;
                    OnPropertyChanged("Percent");
                    OnPropertyChanged("IsIndeterminate");
                }
            }
        }

        public bool IsIndeterminate
        {
            get
            {
                if (_status == ProgressStatus.Completed || _status == ProgressStatus.Error || _status == ProgressStatus.Idle)
                    return false;

                return (_percent < 1);
            }
        }

        public ProgressStatus Status
        {
            get { return _status; }
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged("Status");
                    OnPropertyChanged("StatusLabel");
                    OnPropertyChanged("IsIndeterminate");
                }
            }
        }

        public string StatusLabel => "(" + Status.ToString() + ")";

        public string Message
        {
            get { return _message; }
            set
            {
                if (_message != value)
                {
                    _message = value;
                    OnPropertyChanged("Message");
                }
            }
        }

        public string LocalFolder
        {
            get { return SyncConfiguration.LocalFolder; }
            set
            {
                if (SyncConfiguration.LocalFolder != value)
                {
                    SyncConfiguration.LocalFolder = value.TrimEnd('\\');
                    OnPropertyChanged("LocalFolder");
                    OnPropertyChanged("DisplayName");
                }
            }
        }

        public string Name
        {
            get { return SyncConfiguration.Name; }
            set
            {
                if (SyncConfiguration.Name != value)
                {
                    SyncConfiguration.Name = value;
                    OnPropertyChanged("Name");
                    OnPropertyChanged("DisplayName");
                }
            }
        }

        public string DisplayName => Name + " (" + LocalFolder + ")";

        public SyncDirection Direction
        {
            get { return SyncConfiguration.Direction; }
            set
            {
                if (SyncConfiguration.Direction != value)
                {
                    SyncConfiguration.Direction = value;
                    OnPropertyChanged("Direction");
                }
            }
        }

        public ConflictHandling ConflictHandling
        {
            get { return SyncConfiguration.ConflictHandling; }
            set
            {
                if (SyncConfiguration.ConflictHandling != value)
                {
                    SyncConfiguration.ConflictHandling = value;
                    OnPropertyChanged("ConflictHandling");
                }
            }
        }

        public AuthenticationType AuthenticationType
        {
            get { return SyncConfiguration.AuthenticationType; }
            set
            {
                if (SyncConfiguration.AuthenticationType != value)
                {
                    SyncConfiguration.AuthenticationType = value;
                    OnPropertyChanged("AuthenticationType");
                    OnPropertyChanged("IsAdfsAuthentication");
                    OnPropertyChanged("IsDomainRequired");
                }
            }
        }

        public bool IsAdfsAuthentication => AuthenticationType == Core.AuthenticationType.ADFS;

        public bool IsO365Authentication => AuthenticationType == Core.AuthenticationType.Office365;

        public bool IsDomainRequired => AuthenticationType == Core.AuthenticationType.ADFS || AuthenticationType == Core.AuthenticationType.NTLM;

        public string AdfsSTSUrl
        {
            get { return SyncConfiguration.AdfsSTSUrl; }
            set
            {
                if (SyncConfiguration.AdfsSTSUrl != value)
                {
                    SyncConfiguration.AdfsSTSUrl = value;
                    OnPropertyChanged("AdfsSTSUrl");
                }
            }
        }

        public string AdfsRealm
        {
            get { return SyncConfiguration.AdfsRealm; }
            set
            {
                if (SyncConfiguration.AdfsRealm != value)
                {
                    SyncConfiguration.AdfsRealm = value;
                    OnPropertyChanged("AdfsRealm");
                }
            }
        }

        public string DocumentLibrary
        {
            get { return SyncConfiguration.DocumentLibrary; }
            set
            {
                if (SyncConfiguration.DocumentLibrary != value)
                {
                    SyncConfiguration.DocumentLibrary = value;
                    OnPropertyChanged("DocumentLibrary");
                }
            }
        }

        public string SiteUrl
        {
            get { return SyncConfiguration.SiteUrl; }
            set
            {
                if (SyncConfiguration.SiteUrl != value)
                {
                    SyncConfiguration.SiteUrl = value;
                    OnPropertyChanged("SiteUrl");
                }
            }
        }

        public string[] SelectedFolders
        {
            get { return SyncConfiguration.SelectedFolders; }
            set
            {
                if (SyncConfiguration.SelectedFolders != value)
                {
                    SyncConfiguration.SelectedFolders = value;
                    OnPropertyChanged("SelectedFolders");
                }
            }
        }

        public bool DownloadHeadersOnly
        {
            get { return SyncConfiguration.DownloadHeadersOnly; }
            set
            {
                if (SyncConfiguration.DownloadHeadersOnly != value)
                {
                    SyncConfiguration.DownloadHeadersOnly = value;
                    OnPropertyChanged("DownloadHeadersOnly");
                }
            }
        }

        public string Username
        {
            get { return SyncConfiguration.Username; }
            set
            {
                if (SyncConfiguration.Username != value)
                {
                    SyncConfiguration.Username = value;
                    OnPropertyChanged("Username");
                }
            }
        }

        public string Password
        {
            get { return SyncConfiguration.Password; }
            set
            {
                if (SyncConfiguration.Password != value)
                {
                    SyncConfiguration.Password = value;
                    OnPropertyChanged("Password");
                }
            }
        }

        public string Domain
        {
            get { return SyncConfiguration.Domain; }
            set
            {
                if (SyncConfiguration.Domain != value)
                {
                    SyncConfiguration.Domain = value;
                    OnPropertyChanged("Domain");
                }
            }
        }

        #endregion

        internal void Save()
        {
            App.MainViewModel.AddOrUpdateConfiguration(this);
        }

        internal void ResetErrorFlag(Guid? id = null)
        {
            var store = new MetadataStore(SyncConfiguration.LocalFolder);

            if (id == null)
            {
                foreach (var item in store.Items.Where(p => p.HasError).Select(p => p.Id).ToArray())
                {
                    store.Delete(item);
                }
            }
            else
            {
                store.Delete(id.Value);
            }

            store.Save();

            OnPropertyChanged(nameof(ItemsWithErrors));
        }

        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
