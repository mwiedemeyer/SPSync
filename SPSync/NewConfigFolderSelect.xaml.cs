using SPSync.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SPSync
{
    public partial class NewConfigFolderSelect : Window
    {
        private SyncConfiguration _configuration;

        public NewConfigFolderSelect(SyncConfiguration configuration)
        {
            _configuration = configuration;
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            TreeViewFolders.Items.Add(new TreeViewItem() { Header = "Please wait... Loading folders" });

            if (_configuration.SelectedFolders != null)
            {
                RadioSelected.IsChecked = true;
            }

            Task.Run(() =>
            {
                try
                {
                    var allFolders = SharePointManager.GetAllFoldersFromConfig(_configuration);

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var rootTreeItem = new FolderViewModel() { Name = _configuration.DocumentLibrary, Folder = "\\" };

                        foreach (var folder in allFolders)
                        {
                            var split = folder.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);

                            var treeItem = new FolderViewModel();
                            treeItem.Name = split[split.Length - 1];
                            treeItem.Folder = (folder.TrimStart('/').Replace("/", "\\") + "\\").ToLowerInvariant();

                            if (split.Length == 1)
                            {
                                rootTreeItem.AddChildren(treeItem);
                            }
                            else
                            {
                                var splitFolder = string.Empty;
                                for (var i = 0; i < split.Length - 1; i++)
                                {
                                    splitFolder += split[i] + "\\";
                                }

                                FolderViewModel parent = FindTreeViewItemByFolder(rootTreeItem.Children, splitFolder);
                                // should never happen
                                if (parent == null)
                                    parent = rootTreeItem;

                                parent.AddChildren(treeItem);
                            }
                        }

                        TreeViewFolders.Items.Clear();
                        TreeViewFolders.Items.Add(rootTreeItem);

                        if (_configuration.SelectedFolders == null)
                            return;

                        foreach (var item in _configuration.SelectedFolders)
                        {
                            var fvm = FindTreeViewItemByFolder(rootTreeItem.Children, item);
                            if (fvm == null)
                                continue;

                            fvm.IsChecked = true;
                        }
                    }));
                }
                catch (Exception ex)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        var err1TreeItem = new TreeViewItem() { Header = "Error getting list of folders" };
                        var err2TreeItem = new TreeViewItem() { Header = ex.Message };
                        TreeViewFolders.Items.Clear();
                        TreeViewFolders.Items.Add(err1TreeItem);
                        TreeViewFolders.Items.Add(err2TreeItem);

                        Logger.Log("Error getting folder list: {0}{1}{2}", ex.Message, Environment.NewLine, ex.StackTrace);
                    }));
                }
            });
        }

        private FolderViewModel FindTreeViewItemByFolder(List<FolderViewModel> items, string folder)
        {
            folder = folder.ToLowerInvariant();

            foreach (var item in items)
            {
                if (item.Folder == folder)
                {
                    return item;
                }
                var ti = FindTreeViewItemByFolder(item.Children, folder);
                if (ti != null)
                    return ti;
            }
            return null;
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            if (RadioSelected.IsChecked.HasValue && RadioSelected.IsChecked.Value)
            {
                var root = TreeViewFolders.Items[0] as FolderViewModel;
                if (root.IsChecked.HasValue && root.IsChecked.Value)
                {
                    _configuration.SelectedFolders = null;
                }
                else
                {
                    var folders = GetCheckedFolders(root.Children);
                    _configuration.SelectedFolders = folders.ToArray();
                }
            }
            else
            {
                _configuration.SelectedFolders = null;
            }

            App.MainViewModel.AddOrUpdateConfiguration(_configuration);

            DialogResult = true;
            Close();
        }

        private List<string> GetCheckedFolders(List<FolderViewModel> root)
        {
            var list = new List<string>();

            foreach (var item in root)
            {
                if (item.IsChecked.HasValue && item.IsChecked.Value)
                {
                    list.Add(item.Folder);
                }
                else
                {
                    list.AddRange(GetCheckedFolders(item.Children));
                }
            }

            return list;
        }
    }
}
