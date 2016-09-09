using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace SPSync.Core.Metadata
{
    public class MetadataStore
    {
        public const string STOREFOLDER = ".spsync";
        private const string CHANGE_TOKEN_FILE = "ChangeToken.dat";

        private List<MetadataItem> items = new List<MetadataItem>();
        public string ChangeToken { get; set; }
        private string localFolder;

        public MetadataStore(string localFolder)
        {
            this.localFolder = localFolder;
            Load();
        }

        private void Load()
        {
            var storeFolder = Path.Combine(localFolder, STOREFOLDER);
            EnsureStoreFolder(storeFolder);

            try
            {
                var hash = Convert.ToBase64String(System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.Default.GetBytes(localFolder))).Replace('/', '-').Replace('\\', '-');
                using (FileStream fs = new FileStream(Path.Combine(storeFolder, hash + ".xml"), FileMode.Open))
                {
                    XmlSerializer ser = new XmlSerializer(typeof(List<MetadataItem>));
                    items = ser.Deserialize(fs) as List<MetadataItem>;
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error loading metadata store for {0} {1}{2}{3}", localFolder, ex.Message, Environment.NewLine, ex.StackTrace);
            }

            try
            {
                ChangeToken = File.ReadAllText(Path.Combine(storeFolder, CHANGE_TOKEN_FILE));
            }
            catch (Exception ex)
            {
                Logger.Log("Error loading ChangeToken for {0} {1}", localFolder, ex.Message);
            }
        }

        public void Save()
        {
            var storeFolder = Path.Combine(localFolder, STOREFOLDER);
            EnsureStoreFolder(storeFolder);

            try
            {
                var hash = Convert.ToBase64String(System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.Default.GetBytes(localFolder))).Replace('/', '-').Replace('\\', '-');
                using (FileStream fs = new FileStream(Path.Combine(storeFolder, hash + ".xml"), FileMode.Create))
                {
                    XmlSerializer ser = new XmlSerializer(typeof(List<MetadataItem>));
                    ser.Serialize(fs, items);
                }
            }
            catch (Exception ex)
            {
                Logger.Log("Error saving metadata store for {0} {1}{2}{3}", localFolder, ex.Message, Environment.NewLine, ex.StackTrace);
            }

            try
            {
                File.WriteAllText(Path.Combine(storeFolder, CHANGE_TOKEN_FILE), ChangeToken);
            }
            catch { }
        }

        public List<MetadataItem> Items => items;

        public MetadataItem GetById(Guid id) => items.FirstOrDefault(p => p.Id == id);

        public void Delete(Guid id)
        {
            var i = items.FirstOrDefault(p => p.Id == id);
            if (i == null)
                return;

            items.Remove(i);
        }

        public void Update(MetadataItem item)
        {
            var i = items.FirstOrDefault(p => p.Id == item.Id);
            items.Remove(i);
            items.Add(item);
        }

        public void Add(MetadataItem item)
        {
            lock (items)
            {
                items.Add(item);
            }
        }

        public MetadataItem GetByFileName(string file)
        {
            lock (items)
            {
                return items.FirstOrDefault(p => new DirectoryInfo(Path.Combine(p.LocalFolder, p.Name)).FullName == new DirectoryInfo(file).FullName);
            }
        }

        public MetadataItem GetByItemId(int sharePointId) => items.FirstOrDefault(p => p.SharePointId == sharePointId);

        public MetadataItem[] GetResults()
        {
            List<MetadataItem> results = new List<MetadataItem>();

            foreach (var item in Items)
            {
                results.Add(item.DeepClone());
            }

            return results.ToArray();
        }

        public static void DeleteStoreForFolder(string localFolder)
        {
            try
            {
                var storeFolder = Path.Combine(localFolder, STOREFOLDER);
                EnsureStoreFolder(storeFolder);

                var hash = Convert.ToBase64String(System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.Default.GetBytes(localFolder))).Replace('/', '-').Replace('\\', '-');
                var path = Path.Combine(storeFolder, hash + ".xml");
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            { }
        }

        public static void DeleteChangeTokenForFolder(string localFolder)
        {
            try
            {
                var storeFolder = Path.Combine(localFolder, STOREFOLDER);

                var ctFile = Path.Combine(storeFolder, CHANGE_TOKEN_FILE);
                if (File.Exists(ctFile))
                {
                    File.Delete(ctFile);
                }
            }
            catch
            { }
        }

        private static void EnsureStoreFolder(string storeFolder)
        {
            if (Directory.Exists(storeFolder))
                return;

            try
            {
                var folder = Directory.CreateDirectory(storeFolder);
                folder.Attributes |= FileAttributes.Hidden;
            }
            catch (Exception ex)
            {
                Logger.Log("Storefolder {0} cannot be created: {1}", storeFolder, ex.Message);
                Logger.LogDebug("Storefolder cannot be created: {0}{1}{2}", ex.Message, Environment.NewLine, ex.StackTrace);
                throw new ApplicationException("Metadatastore folder (" + storeFolder + ") cannot be created. Make sure you have write access to the folder you want to sync.", ex);
            }
        }
    }
}
