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
using System.Linq;

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
            
            // Add SizeChanged event handler for window resizing
            this.SizeChanged += MainWindow_SizeChanged;
            
            // Initialize menu tabs
            InitializeMenuTabs();
        }

        /// <summary>
        /// Initialize menu tab handling
        /// </summary>
        private void InitializeMenuTabs()
        {
            // Set default tab
            MainMenuTabs.SelectedIndex = 0;
            
            // Ensure tab content is visible on selection
            MainMenuTabs.SelectionChanged += (sender, e) => 
            {
                // Make sure the selected tab content is visible
                if (MainMenuTabs.SelectedItem is TabItem selectedTab)
                {
                    selectedTab.IsSelected = true;
                }
            };
        }

        private void OpenFile(object sender, RoutedEventArgs e)
        {
            OpenFileDialog<ArchiveDialog> ofd = new OpenFileDialog<ArchiveDialog>
            {
                FileDlgStartLocation = AddonWindowLocation.Bottom,
                InitialDirectory = new System.Windows.Forms.OpenFileDialog().InitialDirectory,
                FileDlgOkCaption = "&OK"
            };
            ofd.SetPlaces(new object[] { @"c:\", (int)Places.MyComputer, (int)Places.Favorites, (int)Places.All_Users_MyVideo, (int)Places.MyVideos });
            if (ofd.ShowDialog() == true)
            {
                // O usuário pode escolher uma chave específica ou tentar todas
                if (ofd.ChildWnd.UseSpecificKey)
                {
                    var tab = new ArchiveTab(ofd.FileName, ofd.ChildWnd.Key);
                    tab.LoadDataWin += LoadData;
                    tab.CloseTab += CloseTab;
                    tab.FoldersUpdated += PopulateFolderTree;
                    tab.TabIndex = Archives.Items.Count;
                    Archives.Items.Add(tab);
                    Archives.SelectedIndex = tab.TabIndex;
                    tab.Initialize();
                }
                else
                {
                    // Tenta abrir com todas as chaves disponíveis
                    var tab = new ArchiveTab(ofd.FileName);
                    tab.LoadDataWin += LoadData;
                    tab.CloseTab += CloseTab;
                    tab.FoldersUpdated += PopulateFolderTree;
                    tab.TabIndex = Archives.Items.Count;
                    Archives.Items.Add(tab);
                    Archives.SelectedIndex = tab.TabIndex;
                    tab.Initialize();
                }
                
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
            // Tenta abrir com todas as chaves disponíveis
            var tab = new ArchiveTab(path);
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

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Notify any visible ArchiveTab to update column sizing
            if (Archives.SelectedItem != null && Archives.SelectedItem is ArchiveTab tab)
            {
                tab.HandleResize();
            }
        }
        
        /// <summary>
        /// Creates a new PCK file from a folder without requiring UI interaction
        /// </summary>
        /// <param name="sourceFolderPath">Path to the source folder</param>
        /// <param name="destPckPath">Path where the PCK file will be created</param>
        /// <param name="keyIndex">Index of the key to use (from Settings.Keys)</param>
        /// <param name="version">Version of PCK file (2 or 3)</param>
        /// <param name="compressionLevel">Compression level (0-9)</param>
        /// <returns>A task that completes when the PCK creation is finished</returns>
        public static async Task<bool> CreatePckFromFolder(
            string sourceFolderPath,
            string destPckPath,
            int keyIndex = 0,
            int version = 3,
            int compressionLevel = 1)
        {
            try
            {
                if (!Directory.Exists(sourceFolderPath))
                {
                    throw new DirectoryNotFoundException($"Source folder not found: {sourceFolderPath}");
                }
                
                if (keyIndex < 0 || keyIndex >= Settings.Keys.Count)
                {
                    throw new ArgumentOutOfRangeException(nameof(keyIndex), "Invalid key index");
                }
                
                if (version != 2 && version != 3)
                {
                    throw new ArgumentOutOfRangeException(nameof(version), "Version must be 2 or 3");
                }
                
                if (compressionLevel < 0 || compressionLevel > 9)
                {
                    throw new ArgumentOutOfRangeException(nameof(compressionLevel), "Compression level must be between 0 and 9");
                }
                
                // Ensure destination path has .pck extension
                if (!destPckPath.EndsWith(".pck", StringComparison.OrdinalIgnoreCase))
                {
                    destPckPath += ".pck";
                }
                
                // Create a new archive manager with the selected version
                ArchiveVersion selectedVersion = version == 2 ? ArchiveVersion.V2 : ArchiveVersion.V3;
                var archiveManager = new Core.ArchiveEngine.ArchiveManager(destPckPath, Settings.Keys[keyIndex], false)
                {
                    Version = selectedVersion
                };
                
                // Initialize the PCK file
                archiveManager.InitializePck(Settings.Keys[keyIndex].FSIG_1, Settings.Keys[keyIndex].FSIG_2);
                
                // Get all files in the source directory and subdirectories
                List<string> files = Directory.GetFiles(sourceFolderPath, "*", SearchOption.AllDirectories).ToList();
                
                // Set compression level temporarily for this operation
                int originalCompressionLevel = Settings.CompressionLevel;
                Settings.CompressionLevel = compressionLevel;
                
                try
                {
                    // Add all files to the archive, preserving folder structure
                    // Get the parent directory to preserve the selected folder in the PCK
                    string parentDir = Directory.GetParent(sourceFolderPath).FullName;
                    await archiveManager.AddFilesAsync(files, parentDir, "\\");
                    
                    // Ensure the file has the correct version markers
                    archiveManager.SaveFileTable();
                    
                    return true;
                }
                finally
                {
                    // Restore original compression level
                    Settings.CompressionLevel = originalCompressionLevel;
                }
            }
            catch (Exception)
            {
                // Re-throw the exception to be handled by the caller
                throw;
            }
        }
        
        private void CreateNewPckFromFolder(object sender, RoutedEventArgs e)
        {
            // Create folder browser dialog
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a folder to create a PCK from"
            };
            
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                // Prompt for PCK name and key
                var inputDialog = new Controls.CustomFileDialog.InputDialog();
                inputDialog.Title = "Create New PCK";
                
                // Add PCK name field
                inputDialog.AddTextBox("PckName", "PCK Name:", System.IO.Path.GetFileName(dialog.SelectedPath) + ".pck");
                
                // Add key selection combo box
                var keyCombo = inputDialog.AddComboBox("ArchiveKey", "Select Archive Key:", Settings.Keys.Select(k => k.Name).ToList());
                keyCombo.SelectedIndex = 0;
                
                // Add version selection
                var versionCombo = inputDialog.AddComboBox("Version", "PCK Version:", new List<string> { "Version 2", "Version 3" });
                versionCombo.SelectedIndex = 1; // Default to Version 3
                
                // Add compression level selection
                var compressionLevels = new List<string>();
                for (int i = 0; i <= 9; i++)
                {
                    compressionLevels.Add(i == 0 ? "0 - No Compression" : i == 9 ? "9 - Maximum Compression" : i.ToString());
                }
                var compressionCombo = inputDialog.AddComboBox("CompressionLevel", "Compression Level:", compressionLevels);
                compressionCombo.SelectedIndex = Settings.CompressionLevel; // Default to current setting
                
                if (inputDialog.ShowDialog() == true)
                {
                    string pckName = inputDialog.GetTextBoxValue("PckName");
                    int keyIndex = inputDialog.GetComboBoxSelectedIndex("ArchiveKey");
                    int versionIndex = inputDialog.GetComboBoxSelectedIndex("Version");
                    int compressionLevel = inputDialog.GetComboBoxSelectedIndex("CompressionLevel");
                    
                    if (string.IsNullOrEmpty(pckName))
                    {
                        MessageBox.Show("PCK name cannot be empty.");
                        return;
                    }
                    
                    if (!pckName.EndsWith(".pck", StringComparison.OrdinalIgnoreCase))
                    {
                        pckName += ".pck";
                    }
                    
                    // Get the full destination path
                    string destPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(dialog.SelectedPath), pckName);
                    
                    Task.Run(async () =>
                    {
                        try
                        {
                            // Use the static method to create the PCK file
                            bool success = await CreatePckFromFolder(
                                dialog.SelectedPath,
                                destPath,
                                keyIndex,
                                versionIndex == 0 ? 2 : 3,
                                compressionLevel
                            );
                            
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Successfully created {pckName} with {Directory.GetFiles(dialog.SelectedPath, "*", SearchOption.AllDirectories).Count()} files.");
                                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{destPath}\"");
                            });
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Error creating PCK file: {ex.Message}");
                            });
                        }
                    });
                }
            }
        }
    }
}
