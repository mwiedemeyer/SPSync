using SPSync.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SPSyncCli
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("SPSync Command-line interface v{0}", Assembly.GetExecutingAssembly().GetName().Version);
            Console.WriteLine("(C) 2016 Marco Wiedemeyer");
            Console.WriteLine("http://spsync.net");
            Console.WriteLine("======================================");
            Console.WriteLine();

            if (args.Length == 0)
            {
                var exeName = Assembly.GetExecutingAssembly().GetName().Name + ".exe";
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("Usage:");
                Console.WriteLine("{0} FOLDER_CONFIGURED_FOR_SYNC [PREVIEW]", exeName);
                Console.WriteLine();
                Console.WriteLine("Example:");
                Console.WriteLine("{0} \"C:\\My\\FolderToSync\"\t\t--\tSync the specified folder", exeName);
                Console.WriteLine("{0} \"C:\\My\\FolderToSync\" true\t\t--\tDo not sync. Just display the detected changes", exeName);
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("NOTE: The folder you want to sync must be configured within the full SPSync client.");
                Console.WriteLine();
                Console.ResetColor();
                Environment.ExitCode = 500;
                return;
            }

            //HACK: disable SSL certificate check
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            var folder = args[0];
            var preview = false;
            if (args.Length > 1)
                preview = bool.Parse(args[1]);

            SyncManager sync = null;

            try
            {
                sync = new SyncManager(folder);
                Console.WriteLine("Configuration '{0}' loaded. Starting... {1}", sync.ConfigurationName, preview ? "PREVIEW ONLY" : string.Empty);
                Console.WriteLine();
            }
            catch
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error getting sync configuration for {0}", folder);
                Console.WriteLine("Make sure that the folder is configured within SPSync and the current user account has access to the config file in C:\\Users\\{0}\\AppData\\Local\\SPSync\\Config.xml", Environment.ExpandEnvironmentVariables("%username%"));
                Logger.Log("Error getting sync configuration for {0}. Make sure that the folder is configured within SPSync and the current user account has access to the config file in C:\\Users\\{1}\\AppData\\Local\\SPSync\\Config.xml", folder, Environment.ExpandEnvironmentVariables("%username%"));
                Console.ResetColor();
                Environment.ExitCode = 501;
                return;
            }

            sync.SyncProgress += Sync_SyncProgress;
            sync.ItemProgress += Sync_ItemProgress;

            sync.Synchronize(preview);

            var results = sync.SyncResults;

            Console.WriteLine();
            Console.WriteLine();

            Console.ResetColor();

            if (results != null)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("Results");
                Console.WriteLine("-------");
                foreach (var item in results)
                {
                    Console.WriteLine("{0}\t{1}\t{2}\t{3}", item.Name, item.Type, item.Status, item.LastModified);
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine();
            Console.WriteLine("Completed");
            Console.ResetColor();
        }

        private static void Sync_ItemProgress(object sender, ItemProgressEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("[{4}] [{5}] {2} Item ({3}): {1}% - {0}", e.Message, e.Percent, e.Status, e.ItemType, DateTime.Now, e.Configuration.Name);
            Console.ResetColor();
        }

        private static void Sync_SyncProgress(object sender, SyncProgressEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("[{3}] [{4}] {2} Sync: {1}% - {0}", e.Message, e.Percent, e.Status, DateTime.Now, e.Configuration.Name);
            Console.ResetColor();
        }
    }
}
