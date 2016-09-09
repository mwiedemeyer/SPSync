using SPSync.Core;
using Squirrel;
using System;
using System.Threading.Tasks;

namespace SPSync
{
    internal class SquirrelSetup
    {
        private const string UPDATE_URL = "E:\\SetupTest";

        public static bool IsFirstStart = false;

        internal static void HandleStartup()
        {
            if (System.Diagnostics.Debugger.IsAttached)
                return;

            var rootDir = System.IO.Path.GetDirectoryName(new System.IO.DirectoryInfo(System.Reflection.Assembly.GetEntryAssembly().Location).Parent.Parent.FullName);
            using (var mgr = new UpdateManager(UPDATE_URL, null, rootDir))
            {
                SquirrelAwareApp.HandleEvents(
                  onInitialInstall: v =>
                  {
                      mgr.CreateShortcutsForExecutable("SPSync.exe", ShortcutLocation.Desktop, false);
                      mgr.CreateShortcutsForExecutable("SPSync.exe", ShortcutLocation.Startup, false);
                      Logger.Log("App installed");
                  },
                  onFirstRun: () =>
                  {
                      IsFirstStart = true;
                  },
                  onAppUpdate: v =>
                  {
                      mgr.CreateShortcutsForExecutable("SPSync.exe", ShortcutLocation.Desktop, false);
                      mgr.CreateShortcutsForExecutable("SPSync.exe", ShortcutLocation.Startup, false);
                      Logger.Log("App updated");
                  },
                  onAppUninstall: v =>
                  {
                      mgr.RemoveShortcutsForExecutable("SPSync.exe", ShortcutLocation.Desktop);
                      mgr.RemoveShortcutsForExecutable("SPSync.exe", ShortcutLocation.Startup);
                      Logger.Log("App uninstalled");
                  });
            }

            TryUpdateAsync();
        }

        internal static void TryUpdateAsync()
        {
            Task.Run(async () =>
            {
                using (var mgr = new UpdateManager(UPDATE_URL))
                {
                    Logger.Log("Looking for updates");
                    var r = await mgr.UpdateApp();
                    if (r != null)
                    {
                        Logger.Log("App updated to version {0}", r.Version.ToString(4));
                    }
                }
            });
        }
    }
}