using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Hardcodet.Wpf.TaskbarNotification;
using SPSync.Core;

namespace SPSync
{
    public partial class MainWindow : Window
    {
        private TaskbarIcon taskbarIcon;
        private bool shutdownInitiatedFromApplication;
        private MainViewModel viewModel;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            viewModel = App.MainViewModel;
            viewModel.NotifyStatus += new EventHandler<NotifyStatusEventArgs>(viewModel_NotifyStatus);
            viewModel.PropertyChanged += new System.ComponentModel.PropertyChangedEventHandler(viewModel_PropertyChanged);
            viewModel.NotifyInvalidCredentials += viewModel_NotifyInvalidCredentials;
            DataContext = viewModel;

            taskbarIcon = new TaskbarIcon();
            taskbarIcon.IconSource = Icon;
            taskbarIcon.ToolTipText = "SPSync";
            taskbarIcon.TrayBalloonTipClicked += new RoutedEventHandler(taskbarIcon_TrayClicked);
            taskbarIcon.TrayMouseDoubleClick += new RoutedEventHandler(taskbarIcon_TrayClicked);
            UpdateContextMenu();

            HideWindow();

            if (SquirrelSetup.IsFirstStart)
            {
                MessageBox.Show("SPSync was installed successfully. To add a new sync connection right-click on the SPSync icon in the task tray.", "SPSync", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
        }

        private void viewModel_NotifyInvalidCredentials(object sender, NotifyInvalidCredentialsEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                EditConfWindow wind = new EditConfWindow();
                wind.ShowDialog(e.SyncConfiguration);
            }));
        }

        void viewModel_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            UpdateContextMenu();
        }

        void viewModel_NotifyStatus(object sender, NotifyStatusEventArgs e)
        {
            SetStatus(e.Message, e.Icon);
        }

        #region ContextMenu

        private void UpdateContextMenu()
        {
            var contextMenu = new ContextMenu();

            // Show Window
            var showMenu = new MenuItem() { Header = "Open SPSync", Tag = "SHOW", FontWeight = FontWeights.Bold };
            showMenu.Click += new RoutedEventHandler(contextMenu_Click);
            contextMenu.Items.Add(showMenu);

            // Separator
            contextMenu.Items.Add(new Separator());

            // Sync all
            var sync = new MenuItem() { Header = "Sync all now", Tag = "SYNCALL" };
            sync.Click += new RoutedEventHandler(contextMenu_Click);
            contextMenu.Items.Add(sync);

            // Add new Sync Conf
            var newSync = new MenuItem() { Header = "New sync configuration...", Tag = "NEWSYNC" };
            newSync.Click += new RoutedEventHandler(contextMenu_Click);
            contextMenu.Items.Add(newSync);

            // Separator
            contextMenu.Items.Add(new Separator());

            // Sync Configurations
            foreach (var sm in viewModel.SyncModels)
            {
                var syncConf = new MenuItem() { Header = sm.Name };

                var syncNow = new MenuItem() { Header = "Sync now", Tag = "SYNC|" + sm.LocalFolder };
                syncNow.Click += new RoutedEventHandler(contextMenu_Click);
                var editConf = new MenuItem() { Header = "Edit...", Tag = "EDIT|" + sm.LocalFolder };
                editConf.Click += new RoutedEventHandler(contextMenu_Click);
                var deleteConf = new MenuItem() { Header = "Delete...", Tag = "DELETE|" + sm.LocalFolder };
                deleteConf.Click += new RoutedEventHandler(contextMenu_Click);
                var showErrors= new MenuItem() { Header = "Show errors...", Tag = "ERROR|" + sm.LocalFolder };
                showErrors.Click += new RoutedEventHandler(contextMenu_Click);
                
                syncConf.Items.Add(syncNow);
                syncConf.Items.Add(editConf);
                syncConf.Items.Add(deleteConf);
                syncConf.Items.Add(showErrors);

                contextMenu.Items.Add(syncConf);
            }

            // Separator
            contextMenu.Items.Add(new Separator());

            //// Settings
            //var settingsMenu = new System.Windows.Controls.MenuItem() { Header = "Settings", Tag = "SETTINGS" };
            //settingsMenu.Click += new RoutedEventHandler(contextMenu_Click);
            //contextMenu.Items.Add(settingsMenu);

            // Info
            var infoMenu = new MenuItem() { Header = "Info", Tag = "INFO" };
            infoMenu.Click += new RoutedEventHandler(contextMenu_Click);
            contextMenu.Items.Add(infoMenu);

            // Exit
            var exitMenu = new MenuItem() { Header = "Exit", Tag = "EXIT" };
            exitMenu.Click += new RoutedEventHandler(contextMenu_Click);
            contextMenu.Items.Add(exitMenu);

            taskbarIcon.ContextMenu = contextMenu;
        }

        private void contextMenu_Click(object sender, RoutedEventArgs e)
        {
            var item = e.Source as MenuItem;
            if (item == null)
                return;

            var tag = item.Tag.ToString();
            switch (tag)
            {
                case "SHOW":
                    ShowWindow();
                    Focus();
                    break;
                case "SYNCALL":
                    viewModel.SyncAll();
                    break;
                case "NEWSYNC":
                    {
                        NewConfig wind = new NewConfig();
                        wind.ShowNewConfigDialog();
                        break;
                    }
                case "SETTINGS":
                    {
                        //SettingsWindow setWin = new SettingsWindow();
                        //setWin.ShowDialog(null);
                        break;
                    }
                case "INFO":
                    SquirrelSetup.TryUpdateAsync();
                    var version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version.ToString(4);
                    MessageBox.Show(this, "SPSync - Version " + version + " BETA\n(C) 2016 Marco Wiedemeyer\nMore on http://spsync.net", "SPSync", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                case "EXIT":
                    shutdownInitiatedFromApplication = true;
                    Application.Current.Shutdown();
                    break;
                default:
                    {
                        var localFolder = tag.Split('|')[1];
                        var conf = viewModel.GetSyncConfiguration(localFolder);
                        if (tag.StartsWith("SYNC|"))
                        {
                            var t = viewModel.SyncAsync(localFolder);
                        }
                        if (tag.StartsWith("EDIT|"))
                        {
                            EditConfWindow wind = new EditConfWindow();
                            wind.ShowDialog(conf);
                        }
                        if (tag.StartsWith("DELETE|"))
                        {
                            var result = MessageBox.Show(this, "Are you sure you want to delete the configuration '" + conf.Name + "'?", "SPSync", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.Yes);
                            if (result == MessageBoxResult.Yes)
                                viewModel.DeleteConfiguration(localFolder);
                        }
                        if (tag.StartsWith("ERROR|"))
                        {
                            ErrorReport wind = new ErrorReport();
                            wind.ShowDialog(conf);
                        }
                        break;
                    }
            }
        }

        #endregion

        void taskbarIcon_TrayClicked(object sender, RoutedEventArgs e)
        {
            ShowWindow();
        }

        private void SetStatus(string message, BalloonIcon icon)
        {
            bool displayNotifications = false;
            bool.TryParse(System.Configuration.ConfigurationManager.AppSettings["DisplayNotifications"], out displayNotifications);
            if (displayNotifications)
                taskbarIcon.ShowBalloonTip("SPSync", message, icon);
        }

        #region Window Events

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (shutdownInitiatedFromApplication)
                return;

            // Prevent from quit the application. Instead minimize it.
            HideWindow();
            e.Cancel = true;
        }

        private void ShowWindow()
        {
            Show();
        }

        private void HideWindow()
        {
            Hide();
        }

        #endregion
    }
}
