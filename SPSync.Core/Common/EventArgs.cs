using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SPSync.Core.Common;
using SPSync.Core.Metadata;

namespace SPSync.Core
{
    public class SyncProgressEventArgs : EventArgs
    {
        public int Percent { get; set; }
        public ProgressStatus Status { get; set; }
        public string Message { get; set; }
        public SyncConfiguration Configuration { get; set; }
        public Exception InnerException { get; set; }

        public SyncProgressEventArgs(SyncConfiguration configuration, int percent, ProgressStatus status, string message = "", Exception innerException = null)
        {
            this.Configuration = configuration;
            this.Percent = percent;
            this.Status = status;
            this.Message = message;
            this.InnerException = innerException;
        }
    }

    public class ItemProgressEventArgs : EventArgs
    {
        public int Percent { get; set; }
        public ProgressStatus Status { get; set; }
        public string Message { get; set; }
        public ItemType ItemType { get; set; }
        public SyncConfiguration Configuration { get; set; }
        public Exception InnerException { get; set; }

        public ItemProgressEventArgs(SyncConfiguration configuration, int percent, ItemType type, ProgressStatus status, string message = "", Exception innerException = null)
        {
            this.Configuration = configuration;
            this.Percent = percent;
            this.Status = status;
            this.Message = message;
            this.ItemType = type;
            this.InnerException = innerException;
        }
    }

    public class ConflictEventArgs : EventArgs
    {
        public ItemStatus NewStatus { get; set; }
        public MetadataItem Item { get; set; }
        public SyncConfiguration Configuration { get; set; }

        public ConflictEventArgs(SyncConfiguration configuration, MetadataItem item)
        {
            this.Configuration = configuration;
            this.Item = item;
            this.NewStatus = ItemStatus.Conflict;
        }
    }
}
