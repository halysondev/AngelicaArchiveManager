using AngelicaArchiveManager.Controls;
using AngelicaArchiveManager.Controls.CustomFileDialog;
using AngelicaArchiveManager.Core.ArchiveEngine;
using System;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AngelicaArchiveManager
{
    /// <summary>
    /// Main window for the application
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public string ArchivePath {
            get
            {
                if (Archives.SelectedItem != null)
                    return (Archives.SelectedItem as ArchiveTab).Path;
                else
                    return "";
            }
            set
            {
                if (Archives.SelectedItem != null)
                    (Archives.SelectedItem as ArchiveTab).Path = value;
                OnPropertyChanged("ArchivePath");
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            Settings.Load();
            DataContext = this;
            
            // Initially show the "No Archive Loaded" message
            UpdateArchiveVisibility();
        }

        private void OpenFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog<ArchiveDialog> ofd = new OpenFileDialog<ArchiveDialog>
            {
                FileDlgStartLocation = AddonWindowLocation.Bottom,
                InitialDirectory = new System.Windows.Forms.OpenFileDialog().InitialDirectory,
                FileDlgOkCaption = "&Открыть"
            };
            ofd.SetPlaces(new object[] { @"c:\", (int)Places.MyComputer, (int)Places.Favorites, (int)Places.All_Users_MyVideo, (int)Places.MyVideos });
            if (ofd.ShowDialog() == true)
            {
                var tab = new ArchiveTab(ofd.FileName, ofd.ChildWnd.Key);
                tab.LoadDataWin += LoadData;
                tab.CloseTab += CloseTab;
                tab.FoldersUpdated += PopulateFolderTree;
                tab.TabIndex = Archives.Items.Count;
                Archives.Items.Add(tab);
                Archives.SelectedIndex = tab.TabIndex;
                tab.Initialize();
                
                // Show archive content when a new tab is added
                UpdateArchiveVisibility();
            }
        }

        private void SettingsClick(object sender, RoutedEventArgs e)
        {
            new SettingsWin().Show();
        }

        private void Defrag(object sender, RoutedEventArgs e)
        {
            if (Archives.SelectedItem != null)
                (Archives.SelectedItem as ArchiveTab).Defrag();
        }

        public void LoadData(byte t)
        {
            switch (t)
            {
                case 0:
                    OnPropertyChanged("ArchivePath");
                    break;
            }
        }

        private void Archives_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OnPropertyChanged("ArchivePath");
            
            // Update status bar with current archive info
            if (Archives.SelectedItem != null)
            {
                var tab = Archives.SelectedItem as ArchiveTab;
                if (tab != null && tab.Archive != null)
                {
                    FileCount.Text = tab.Archive.Files.Count.ToString();
                    CurrentPath.Text = System.IO.Path.GetFileName(tab.Header.ToString());
                }
            }
        }

        private void PathEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Archives.Focus();
        }

        private void CloseTab(object tab)
        {
            Archives.Items.Remove(tab);
            
            // Update UI when all tabs are closed
            UpdateArchiveVisibility();
        }

        private void UpdateArchiveVisibility()
        {
            bool hasArchives = Archives.Items.Count > 0;
            
            // Toggle visibility of the two main content areas
            NoArchiveGrid.Visibility = hasArchives ? Visibility.Collapsed : Visibility.Visible;
            ArchiveContentGrid.Visibility = hasArchives ? Visibility.Visible : Visibility.Collapsed;
            
            // Update status bar
            if (!hasArchives)
            {
                FileCount.Text = "0";
                FragmentCount.Text = "0";
                CurrentPath.Text = "No archive loaded";
                DirectoryTree.Items.Clear();
            }
            // If we have archives but no tree view items yet, populate it if possible
            else if (DirectoryTree.Items.Count == 0 && Archives.SelectedItem != null)
            {
                var tab = Archives.SelectedItem as ArchiveTab;
                if (tab != null)
                {
                    // This will trigger folders to be populated from data that's already loaded
                    tab.RefreshFolders();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public ArchiveKey Keys
        {
            get => Settings.Keys[0];
        }
        
        private void Window_Drop(object sender, DragEventArgs e)
        {
            string path = ((string[])e.Data.GetData(DataFormats.FileDrop))[0];
            var tab = new ArchiveTab(path, Keys);
            tab.LoadDataWin += LoadData;
            tab.CloseTab += CloseTab;
            tab.FoldersUpdated += PopulateFolderTree;
            tab.TabIndex = Archives.Items.Count;
            Archives.Items.Add(tab);
            Archives.SelectedIndex = tab.TabIndex;
            tab.Initialize();
            
            // Show archive content when a file is dropped
            UpdateArchiveVisibility();
        }
        
        private void PopulateFolderTree(Dictionary<string, HashSet<string>> folders)
        {
            // Use Dispatcher to marshal the call to the UI thread
            this.Dispatcher.Invoke(() =>
            {
                // Exit if no active tab is selected
                if (Archives.SelectedItem == null)
                    return;
                    
                var archiveTab = Archives.SelectedItem as ArchiveTab;
                string archiveName = archiveTab.Header.ToString();
                    
                DirectoryTree.Items.Clear();
                
                // Root node for the archive
                TreeViewItem rootNode = new TreeViewItem();
                
                // Header with icon and text
                StackPanel rootHeader = new StackPanel { Orientation = Orientation.Horizontal };
                Image rootIcon = new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/folder.png")), Width = 16, Height = 16, Margin = new Thickness(0, 0, 5, 0) };
                TextBlock rootText = new TextBlock { Text = archiveName };
                rootHeader.Children.Add(rootIcon);
                rootHeader.Children.Add(rootText);
                rootNode.Header = rootHeader;
                
                // Build the folder tree recursively
                BuildFolderTree(rootNode, "\\", folders);
                
                // Add the root node to the tree
                DirectoryTree.Items.Add(rootNode);
                
                // Expand the root
                rootNode.IsExpanded = true;
            });
        }
        
        private void BuildFolderTree(TreeViewItem parentNode, string path, Dictionary<string, HashSet<string>> folders)
        {
            if (folders.ContainsKey(path))
            {
                foreach (string folder in folders[path])
                {
                    // Create node for this folder
                    TreeViewItem folderNode = new TreeViewItem();
                    
                    // Create header with icon and folder name
                    StackPanel header = new StackPanel { Orientation = Orientation.Horizontal };
                    Image icon = new Image { Source = new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/Resources/folder.png")), Width = 16, Height = 16, Margin = new Thickness(0, 0, 5, 0) };
                    TextBlock text = new TextBlock { Text = folder };
                    header.Children.Add(icon);
                    header.Children.Add(text);
                    folderNode.Header = header;
                    
                    // Add this node to parent
                    parentNode.Items.Add(folderNode);
                    
                    // Recursively build children
                    string nextPath = path + folder + "\\";
                    BuildFolderTree(folderNode, nextPath, folders);
                }
            }
        }

        private void DirectoryTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem selectedItem)
            {
                // Only process if we have a selected archive tab
                if (Archives.SelectedItem != null)
                {
                    var archiveTab = Archives.SelectedItem as ArchiveTab;
                    
                    // Build the path from the selected tree node to the root
                    string folderPath = BuildPathFromTreeNode(selectedItem);
                    
                    // Update the current path in the archive tab
                    archiveTab.Path = folderPath;
                }
            }
        }
        
        private string BuildPathFromTreeNode(TreeViewItem treeItem)
        {
            string path = "\\";
            
            // Skip the root item (which has the archive name)
            if (treeItem.Parent is TreeViewItem parentItem)
            {
                // Build the path by walking up the tree
                Stack<string> pathParts = new Stack<string>();
                
                // Collect folder names up the tree
                TreeViewItem currentItem = treeItem;
                while (currentItem.Parent is TreeViewItem)
                {
                    // Get the text part of the header (the folder name)
                    if (currentItem.Header is StackPanel header && 
                        header.Children.Count > 1 && 
                        header.Children[1] is TextBlock textBlock)
                    {
                        pathParts.Push(textBlock.Text);
                    }
                    
                    currentItem = currentItem.Parent as TreeViewItem;
                }
                
                // Build the path from collected parts
                while (pathParts.Count > 0)
                {
                    path += pathParts.Pop() + "\\";
                }
            }
            
            return path;
        }
    }
}
