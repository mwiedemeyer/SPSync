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
using System.Windows.Shapes;
using SPSync.Core;
using SPSync.Core.Common;

namespace SPSync
{
    public partial class EditConfWindow : Window
    {
        private string _oldLibrary;
        private SyncViewModel _viewModel;
        private SyncDirection _oldDirection;
        private bool _isNewConfig;

        public EditConfWindow()
        {
            InitializeComponent();
        }

        internal bool? ShowDialog(SyncViewModel conf)
        {
            if (conf == null)
            {
                _viewModel = new SyncViewModel(new SyncConfiguration());
                _isNewConfig = true;
            }
            else
            {
                _isNewConfig = false;
                _viewModel = conf;
                _oldLibrary = _viewModel.DocumentLibrary;
                _oldDirection = _viewModel.Direction;
            }

            DataContext = _viewModel;
            textBoxPassword.Password = _viewModel.Password;

            return ShowDialog();
        }

        private void buttonSave_Click(object sender, RoutedEventArgs e)
        {
            if (Save())
            {
                DialogResult = true;
                Close();
            }
        }

        private bool Save()
        {
            if (string.IsNullOrEmpty(_viewModel.Name) || string.IsNullOrEmpty(_viewModel.LocalFolder)
                || string.IsNullOrEmpty(_viewModel.SiteUrl) || string.IsNullOrEmpty(_viewModel.DocumentLibrary)
                || string.IsNullOrEmpty(_viewModel.Username))
            {
                MessageBox.Show("Required fields are empty", "SPSync", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!string.IsNullOrEmpty(_oldLibrary) && _oldLibrary != _viewModel.DocumentLibrary)
            {
                if (MessageBox.Show("Warning: If you change the document library, all your local files might be deleted. Are your sure?", "SPSync", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    Core.Metadata.MetadataStore.DeleteStoreForFolder(_viewModel.LocalFolder);
                    _viewModel.DocumentLibrary = _oldLibrary;
                    return false;
                }
            }

            if (_oldDirection != _viewModel.Direction)
            {
                Core.Metadata.MetadataStore.DeleteStoreForFolder(_viewModel.LocalFolder);
            }

            _viewModel.Password = textBoxPassword.Password;
            _viewModel.Save();

            if (!_isNewConfig)
            {
                Core.Metadata.MetadataStore.DeleteStoreForFolder(_viewModel.LocalFolder);
            }

            return true;
        }

        private void buttonCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void buttonBrowse_Click(object sender, RoutedEventArgs e)
        {
            using (System.Windows.Forms.FolderBrowserDialog fd = new System.Windows.Forms.FolderBrowserDialog())
            {
                fd.Description = "Select the folder you want to sync with SharePoint";
                fd.ShowNewFolderButton = true;
                var result = fd.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    if (_isNewConfig)
                    {
                        if (System.IO.Directory.GetFiles(fd.SelectedPath, "*.*", System.IO.SearchOption.AllDirectories).Length > 0)
                        {
                            //var notEmpty=MessageBox.Show("The selected folder is not empty. You have choosen 
                        }
                    }
                    _viewModel.LocalFolder = fd.SelectedPath;
                }
            }
        }

        private void ButtonSelectFolders_Click(object sender, RoutedEventArgs e)
        {
            if (Save())
            {
                DialogResult = true;
                Close();
                new NewConfigFolderSelect(_viewModel.SyncConfiguration).ShowDialog();
            }
        }
    }
}
