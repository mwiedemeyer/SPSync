using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using System.Timers;
using System.Net;
using SPSync.Core;

namespace SPSync
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly MainViewModel viewModel = new MainViewModel();
        private Timer syncTimer;

        internal static MainViewModel MainViewModel
        {
            get
            {
                if (!viewModel.IsInitialized)
                    viewModel.Init();

                return viewModel;
            }
        }

        public App()
        {
            SquirrelSetup.HandleStartup();

            Logger.Log(string.Join("|", Environment.GetCommandLineArgs()));
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;

            //HACK: disable SSL certificate check
            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

            string[] args = Environment.GetCommandLineArgs();

            if (args.Length == 2)
            {
                if (args[1].EndsWith("spsync"))
                {
                    DownloadWindow dw = new DownloadWindow();
                    dw.ShowDialog();
                    return;
                }
            }

            base.OnStartup(e);

            int intervalMinutes = 20;
            if (!int.TryParse(ConfigurationManager.AppSettings["AutoSyncInterval"], out intervalMinutes))
                intervalMinutes = 20;
            
            syncTimer = new Timer();
            syncTimer.AutoReset = true;
            syncTimer.Elapsed += new ElapsedEventHandler(syncTimer_Elapsed);
            syncTimer.Interval = new TimeSpan(0, intervalMinutes, 0).TotalMilliseconds;
            syncTimer.Start();
        }

        void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Logger.Log("Unhandled Exception: {0}", e.Exception.Message);

            var innerEx = e.Exception.InnerException;
            while (innerEx != null)
            {
                Logger.Log("--->{0}", innerEx.Message);
                innerEx = innerEx.InnerException;
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
        }

        void syncTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            MainViewModel.SyncAll();
        }
    }
}
