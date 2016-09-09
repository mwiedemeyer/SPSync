using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SPSync.Core.Metadata;
using SPSync.Core.Common;
using SPSync.Core;

namespace SPSync
{
    internal class SyncServiceConflictEventArgs : EventArgs
    {
        public SyncConfiguration Configuration { get; set; }
        public MetadataItem Item { get; }
        public ItemStatus NewStatus { get; set; }

        public SyncServiceConflictEventArgs(SyncConfiguration configuration, MetadataItem item, ItemStatus status)
        {
            Configuration = configuration;
            Item = item;
            NewStatus = status;
        }
    }
}
