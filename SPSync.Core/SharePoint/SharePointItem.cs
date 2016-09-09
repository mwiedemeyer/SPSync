using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.SharePoint.Client;
using SPSync.Core.Common;

namespace SPSync.Core
{
    internal class SharePointItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int ETag { get; set; }
        public DateTime LastModified { get; set; }
        public string FullFileName { get; set; }
        public ItemType Type { get; set; }
        public ChangeType ChangeType { get; set; }

        public SharePointItem(int id, ItemType type, ChangeType changeType, string name, string etag, DateTime lastModified, string fullFileName)
        {
            Id = id;
            ChangeType = changeType;
            Name = name;
            if (!string.IsNullOrEmpty(etag))
                ETag = SharePointManager.ParseETag(etag);
            LastModified = lastModified;
            FullFileName = fullFileName;
            Type = type;
        }        
    }
}
