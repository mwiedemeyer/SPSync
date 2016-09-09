using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SPSync.Core;
using System.IO;
using SPSync.Core.Common;
using System.Security.Cryptography;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace SPSyncTest
{
    class Program
    { 
        static void Main(string[] args)
        {
            bool preview = true;

            Console.Write("(P)review or (S)ync: ");
            var c = Console.ReadKey();
            if (c.KeyChar == 'p')
                preview = true;
            else
                preview = false;

            Console.WriteLine();

            while (true)
            {
                SyncConfiguration.RevertConfigurationChanges();

                SyncManager sync = new SyncManager(@"E:\SyncTest");

                sync.SyncProgress += new EventHandler<SyncProgressEventArgs>(sync_SyncProgress);
                sync.ItemProgress += new EventHandler<ItemProgressEventArgs>(sync_ItemProgress);
                sync.ItemConflict += new EventHandler<ConflictEventArgs>(sync_ItemConflict);

                sync.Synchronize(preview);

                var results = sync.SyncResults;

                Console.WriteLine();
                Console.WriteLine();

                if (results != null)
                {
                    foreach (var item in results)
                    {
                        Console.WriteLine("{0}\t{1}\t{2}\t{3}", item.Name, item.Type, item.Status, item.LastModified);
                    }
                }

                Console.Write("(P)review or (S)ync: ");
                c = Console.ReadKey();
                if (c.KeyChar == 'p')
                    preview = true;
                else
                    preview = false;

                Console.WriteLine();
            }
        }
        
        static void sync_ItemConflict(object sender, ConflictEventArgs e)
        {
            Console.WriteLine("CONFLICT: {0}", e.Item.Name);
        }

        static void sync_ItemProgress(object sender, ItemProgressEventArgs e)
        {
            Console.WriteLine("{0} Progress: {1} Status: {2}. {3}", e.ItemType, e.Percent, e.Status, e.Message);
            if (e.InnerException != null)
                Console.WriteLine(e.InnerException);
        }

        static void sync_SyncProgress(object sender, SyncProgressEventArgs e)
        {
            Console.WriteLine("Sync Progress: {0} Status: {1}. {2}", e.Percent, e.Status, e.Message);
        }
    }

}
