using AngelicaArchiveManager.Core;
using AngelicaArchiveManager.Core.ArchiveEngine;
using AngelicaArchiveManager.Interfaces;
using AngelicaArchiveManager.Previews;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Threading;
using static AngelicaArchiveManager.Core.Events;
using System.Runtime.InteropServices;
using Image = System.Drawing.Image;

namespace AngelicaArchiveManager.Controls
{
    public partial class ArchiveTab : TabItem, INotifyPropertyChanged
    {
        // Add Windows API interop for system icons
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        // Dictionary to cache file icons
        private Dictionary<string, Image> _iconCache = new Dictionary<string, Image>();

        private DataGridView Table { get; set; }
        public ArchiveManager Archive { get; set; }
        private FSWatcher Watcher { get; set; }

        private string _Path = "";
        public string Path
        {
            get => _Path;
            set
            {
                if (_Path != value)
                {
                    _Path = value.Replace("/", "\\");
                    OnPropertyChanged("Path");
                    LoadDataWin?.Invoke(0);
                    ReloadTable();
                }
            }
        }
        private Dictionary<string, HashSet<string>> _Folders { get; set; } = new Dictionary<string, HashSet<string>>();
        private Dictionary<string, HashSet<IArchiveEntry>> _Files { get; set; } = new Dictionary<string, HashSet<IArchiveEntry>>();
        private Rectangle? dragRect;

        public event LoadData LoadDataWin;
        public event CloseTab CloseTab;
        public delegate void FoldersUpdatedHandler(Dictionary<string, HashSet<string>> folders);
        public event FoldersUpdatedHandler FoldersUpdated;

        #region Progress
        private int _ProgressMax = 1;
        public int ProgressMax
        {
            get => _ProgressMax;
            set
            {
                if (_ProgressMax != value)
                {
                    _ProgressMax = value;
                    OnPropertyChanged("ProgressMax");
                }
            }
        }

        private int _ProgressValue = 0;
        public int ProgressValue
        {
            get => _ProgressValue;
            set
            {
                if (_ProgressValue != value)
                {
                    _ProgressValue = value;
                    OnPropertyChanged("ProgressValue");
                }
            }
        }

        private void SetProgressNext() => ++ProgressValue;
        private void SetProgressMax(int val) => ProgressMax = val;
        private void SetProgress(int val) => ProgressValue = val;
        #endregion

        public ArchiveTab(string path, ArchiveKey key)
        {
            InitializeComponent();
            DataContext = this;
            BuildTable();
            Host.Child = Table;
            Header = System.IO.Path.GetFileName(path);
            Archive = new ArchiveManager(path, key);
            Archive.SetProgress += SetProgress;
            Archive.SetProgressMax += SetProgressMax;
            Archive.SetProgressNext += SetProgressNext;
            Archive.LoadData += LoadData;
        }

        public ArchiveTab(string path)
        {
            InitializeComponent();
            DataContext = this;
            BuildTable();
            Host.Child = Table;
            Header = System.IO.Path.GetFileName(path);
            Archive = new ArchiveManager(path);
            Archive.SetProgress += SetProgress;
            Archive.SetProgressMax += SetProgressMax;
            Archive.SetProgressNext += SetProgressNext;
            Archive.LoadData += LoadData;
        }

        public void Initialize()
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    await Archive.ReadFileTableAsync();
                    Table.Invoke((MethodInvoker)delegate
                    {
                        Path = "\\";
                    });
                }
                catch (Exception e)
                {
                    Table.Invoke((MethodInvoker)delegate
                    {
                        MessageBox.Show($"{e.Message}\n{e.Source}\n{e.StackTrace}");
                    });
                }
            });
        }


        private void LoadData(byte type)
        {
            switch (type)
            {
                case 0:
                    foreach (IArchiveEntry file in Archive.Files)
                    {
                        List<string> parts = file.Path.Split('\\').ToList();
                        string fpath =  $"\\{file.Path.Replace(parts.Last(), "")}";
                        if (!_Files.ContainsKey(fpath))
                            _Files.Add(fpath, new HashSet<IArchiveEntry>() { file });
                        else
                            _Files[fpath].Add(file);
                        parts.Remove(parts.Last());
                        string path = "\\";
                        foreach (string part in parts)
                        {
                            if (!_Folders.ContainsKey(path))
                                _Folders.Add(path, new HashSet<string>() { part });
                            else
                                _Folders[path].Add(part);
                            path += $"{part}\\";
                        }
                    }
                    
                    // Use Dispatcher to ensure the event is triggered on the UI thread
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        FoldersUpdated?.Invoke(_Folders);
                    }));
                    break;
                case 1:
                    ReloadTable();
                    break;
            }
        }

        public void ReloadTable()
        {
            Table.Rows.Clear();
            int count1 = _Folders.ContainsKey(Path) ? _Folders[Path].Count : 0;
            int count2 = _Files.ContainsKey(Path) ? _Files[Path].Count : 0;
            Table.Rows.Add(count1 + count2 + 1);
            
            // Set the first row as the parent folder
            Table.Rows[0].Cells[0].Value = Properties.Resources.folder;
            Table.Rows[0].Cells[1].Value = "...";
            Table.Rows[0].Cells[2].Value = "";
            Table.Rows[0].Cells[3].Value = "";
            
            int i = 1;
            if (count1 > 0)
            {
                foreach (var f in _Folders[Path])
                {
                    Table.Rows[i].Cells[0].Value = Properties.Resources.folder;
                    Table.Rows[i].Cells[1].Value = f;
                    Table.Rows[i].Cells[2].Value = "";
                    Table.Rows[i].Cells[3].Value = "";
                    ++i;
                }
            }
            if (count2 > 0)
            {
                foreach (var f in _Files[Path])
                {
                    string fileName = System.IO.Path.GetFileName(f.Path);
                    string extension = System.IO.Path.GetExtension(fileName).ToLower();
                    
                    // Get appropriate icon for the file type
                    Image icon = GetFileIcon(extension);
                    
                    Table.Rows[i].Cells[0].Value = icon;
                    Table.Rows[i].Cells[1].Value = fileName;
                    Table.Rows[i].Cells[2].Value = FormatFileSize(f.Size);
                    Table.Rows[i].Cells[3].Value = FormatFileSize(f.CSize);
                    ++i;
                }
            }
            
            // Resize columns after loading data
            ResizeColumns();
        }
        
        private Image GetFileIcon(string extension)
        {
            // Check if icon is already cached
            if (_iconCache.ContainsKey(extension))
                return _iconCache[extension];
                
            try
            {
                // Create a temporary file with the extension to get its icon
                string tempFile = System.IO.Path.GetTempPath() + "temp" + extension;
                
                // If we don't want to create an actual file, we can use SHGetFileInfo with SHGFI_USEFILEATTRIBUTES
                SHFILEINFO shfi = new SHFILEINFO();
                IntPtr hImgSmall = SHGetFileInfo(tempFile, FILE_ATTRIBUTE_NORMAL, ref shfi, (uint)Marshal.SizeOf(shfi), 
                                            SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES);
                
                // Get the icon from the handle
                if (shfi.hIcon != IntPtr.Zero)
                {
                    Icon icon = Icon.FromHandle(shfi.hIcon);
                    Image img = icon.ToBitmap();
                    
                    // Free the icon handle
                    User32.DestroyIcon(shfi.hIcon);
                    
                    // Cache the icon
                    _iconCache[extension] = img;
                    return img;
                }
            }
            catch (Exception)
            {
                // Fall back to custom extension-based icons
                return GetCustomFileIcon(extension);
            }
            
            // If shell icon extraction fails, use custom icons
            return GetCustomFileIcon(extension);
        }

        private Image GetCustomFileIcon(string extension)
        {
            // Simplified version that uses custom icons based on file extension
            switch (extension.ToLower())
            {
                case ".txt":
                case ".str":
                case ".cmd":
                case ".err":
                case ".desc":
                case ".prop":
                    return Properties.Resources.file; // Use text file icon if available
                    
                case ".cfg":
                case ".ini":
                    return Properties.Resources.file; // Use config file icon if available
                    
                case ".data":
                    return Properties.Resources.file; // Use data file icon if available
                    
                default:
                    return Properties.Resources.file; // Default file icon
            }
        }

        // Add User32 P/Invoke definition for cleanup
        private static class User32
        {
            [DllImport("user32.dll")]
            public static extern bool DestroyIcon(IntPtr handle);
        }
        
        // Helper method to format file sizes to match the reference application
        private string FormatFileSize(long size)
        {
            if (size < 1024)
                return size.ToString();
            else if (size < 1024 * 1024)
                return $"{size / 1024} {size % 1024:D3}";
            else
                return $"{size / (1024 * 1024)} {(size % (1024 * 1024)) / 1024:D3} {size % 1024:D3}";
        }

        public void Defrag()
        {
            Task.Factory.StartNew(async () =>
            {
                try
                {
                    await Archive.DefragAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"{ex.Message}\n{ex.Source}\n{ex.StackTrace}");
                }
            });
        }

        private void CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            string path = Path.Clone().ToString();
            if (e.RowIndex == 0)
            {
                var s = path.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries);
                if (s.Length > 0)
                    path = path.Replace($"{s.Last()}\\", "");
            }
            else if (IsDirectory(e.RowIndex))
            {
                path += $"{Table.Rows[e.RowIndex].Cells[1].Value.ToString()}\\";
            }
            if (path.Length < 3)
                path = "\\";
            Path = path;
        }

        private bool IsDirectory(int row) => Table.Rows[row].Cells[2].Value.ToString().Length < 1;

        private new void DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false) == true)
            {
                e.Effect = DragDropEffects.All;
            }
        }

        private void DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            List<string> added_files = new List<string>();
            string base_dir = "";
            foreach (string file in files)
            {
                if (file.Contains(System.IO.Path.GetTempPath()))
                    break;
                if (Utils.IsFile(file))
                {
                    added_files.Add(file);
                    base_dir = System.IO.Path.GetDirectoryName(file);
                }
                else
                {
                    string[] _files = Directory.GetFiles(file, "*", SearchOption.AllDirectories);
                    base_dir = System.IO.Path.GetDirectoryName(file);
                    added_files.AddRange(_files);
                }
            }
            if (base_dir.Length > 1)
                Archive.AddFiles(added_files, base_dir, Path);
        }

        private new void MouseDown(object sender, MouseEventArgs e)
        {
            dragRect = null;
            if (e.Button == MouseButtons.Left)
            {
                dragRect = new Rectangle(
                    e.X - SystemInformation.DragSize.Width / 2, e.Y - SystemInformation.DragSize.Height / 2,
                    SystemInformation.DragSize.Width, SystemInformation.DragSize.Height);
            }
        }

        private new void MouseMove(object sender, MouseEventArgs e)
        {
            if (Table.SelectedRows.Count > 0 && dragRect.HasValue && !dragRect.Value.Contains(e.Location))
            {
                string tmp = System.IO.Path.GetTempFileName();
                Watcher = new FSWatcher(tmp);
                Watcher.FileWatcherCreated += FileWatcherCreated;
                IDataObject obj = new DataObject(DataFormats.FileDrop, new string[] { tmp });
                DragDropEffects result = Table.DoDragDrop(obj, DragDropEffects.Move);
                dragRect = null;
            }
        }

        private void FileWatcherCreated(object sender, FileSystemEventArgs e)
        {
            Watcher = null;
            if (!e.FullPath.Contains(System.IO.Path.GetTempPath()))
            {
                Task.Factory.StartNew(async () =>
                {
                    string dir = System.IO.Path.GetDirectoryName(e.FullPath);
                    
                    // Add retry logic with delay to handle file lock issues
                    bool deleted = false;
                    int maxRetries = 5;
                    int retryCount = 0;
                    
                    while (!deleted && retryCount < maxRetries)
                    {
                        try
                        {
                            File.Delete(e.FullPath);
                            deleted = true;
                        }
                        catch (IOException)
                        {
                            // Wait before retrying
                            await Task.Delay(100 * (retryCount + 1));
                            retryCount++;
                            
                            // If we've reached max retries, just continue without deleting
                            if (retryCount >= maxRetries)
                                break;
                        }
                    }
                    
                    List<IArchiveEntry> files = new List<IArchiveEntry>();
                    foreach (DataGridViewRow row in Table.SelectedRows)
                    {
                        if (IsDirectory(row.Index))
                        {
                            string path = System.IO.Path.Combine(Path, row.Cells[1].Value.ToString()).RemoveFirstSeparator();
                            files.AddRange(Archive.Files.Where(x => x.Path.StartsWith(path + "\\")));
                        }
                        else
                        {
                            files.Add(Archive.Files.Where(x => x.Path == (Path + row.Cells[1].Value.ToString()).RemoveFirstSeparator()).First());
                        }
                    }
                    if (files.Count > 0)
                        await Archive.UnpackFilesAsync(Path, files, dir);
                });
            }
        }

        // New method to extract files asynchronously
        private async void ExtractFilesAsync(string dir)
        {
            List<IArchiveEntry> files = new List<IArchiveEntry>();
            foreach (DataGridViewRow row in Table.SelectedRows)
            {
                if (IsDirectory(row.Index))
                {
                    string path = System.IO.Path.Combine(Path, row.Cells[1].Value.ToString()).RemoveFirstSeparator();
                    files.AddRange(Archive.Files.Where(x => x.Path.StartsWith(path + "\\")));
                }
                else
                {
                    files.Add(Archive.Files.Where(x => x.Path == (Path + row.Cells[1].Value.ToString()).RemoveFirstSeparator()).First());
                }
            }
            if (files.Count > 0)
                await Archive.UnpackFilesAsync(Path, files, dir);
        }

        private new void MouseUp(object sender, MouseEventArgs e)
        {
            dragRect = null;
        }

        private void MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
                return;
            
            // Create a WPF ContextMenu instead of WinForms ContextMenu
            var menu = new System.Windows.Controls.ContextMenu();
            var openAsMenuItem = new System.Windows.Controls.MenuItem { Header = "Open As" };
            var asModelMenuItem = new System.Windows.Controls.MenuItem { Header = "As .ski model" };
            asModelMenuItem.Click += new System.Windows.RoutedEventHandler((s, args) => AsModel(s, args));
            
            openAsMenuItem.Items.Add(asModelMenuItem);
            menu.Items.Add(openAsMenuItem);
            
            // Convert mouse position to screen coordinates
            System.Drawing.Point screenPoint = Table.PointToScreen(e.Location);
            
            // Convert screen coordinates to WPF coordinates
            System.Windows.Point wpfPoint = Host.PointFromScreen(new System.Windows.Point(screenPoint.X, screenPoint.Y));
            
            // Open context menu
            menu.PlacementTarget = Host;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.RelativePoint;
            menu.HorizontalOffset = wpfPoint.X;
            menu.VerticalOffset = wpfPoint.Y;
            menu.IsOpen = true;
        }

        #region Table
        private void BuildTable()
        {
            Table = new DataGridView
            {
                MultiSelect = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = false,
                ReadOnly = true,
                BackgroundColor = Color.White,
                RowHeadersWidth = 32,
                RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeColumns = true,
                AllowUserToResizeRows = false,
                GridColor = Color.LightGray,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                
                // Make the DataGridView fill its container
                Dock = DockStyle.Fill,
                
                // Enable scrolling
                AutoGenerateColumns = false
            };
            
            // Enable double buffering for smoother scrolling (requires reflection)
            typeof(DataGridView).GetProperty("DoubleBuffered", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(Table, true, null);
            
            // Configure the selection colors to match the reference application
            Table.DefaultCellStyle.SelectionBackColor = Color.FromArgb(204, 232, 255);
            Table.DefaultCellStyle.SelectionForeColor = Color.Black;
            
            var column1 = new DataGridViewImageColumn
            {
                Name = "Icon",
                Width = 24,
                HeaderText = "",
                ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
                MinimumWidth = 24
            };
            var column2 = new DataGridViewTextBoxColumn
            {
                Name = "Filename",
                Width = 250,
                HeaderText = "File name",
                ReadOnly = true,
                SortMode = DataGridViewColumnSortMode.Automatic,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                MinimumWidth = 150,
                FillWeight = 70
            };
            var column3 = new DataGridViewTextBoxColumn
            {
                Name = "Size",
                Width = 100,
                HeaderText = "Size",
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight },
                SortMode = DataGridViewColumnSortMode.Automatic,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 80,
                FillWeight = 15
            };
            var column4 = new DataGridViewTextBoxColumn
            {
                Name = "Compressed",
                Width = 120,
                HeaderText = "Compressed size",
                ReadOnly = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight },
                SortMode = DataGridViewColumnSortMode.Automatic,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
                MinimumWidth = 100,
                FillWeight = 15
            };

            Table.Columns.Add(column1);
            Table.Columns.Add(column2);
            Table.Columns.Add(column3);
            Table.Columns.Add(column4);

            // Add resize handler to adjust columns when parent is resized
            Table.ClientSizeChanged += Table_ClientSizeChanged;

            Table.CellDoubleClick += CellDoubleClick;
            Table.MouseDown += MouseDown;
            Table.MouseMove += MouseMove;
            Table.MouseUp += MouseUp;
            Table.MouseClick += MouseClick;
            Table.DragEnter += DragEnter;
            Table.DragDrop += DragDrop;
            Table.AllowDrop = true;
        }

        private void Table_ClientSizeChanged(object sender, EventArgs e)
        {
            ResizeColumns();
        }

        private void ResizeColumns()
        {
            if (Table.Columns.Count < 4) return;
            
            // Get table width excluding scrollbar width
            int scrollWidth = SystemInformation.VerticalScrollBarWidth;
            int tableWidth = Table.ClientSize.Width - scrollWidth;
            
            // Set fixed widths for the icon, name and size columns
            int iconWidth = 24;
            int nameWidth = (int)(tableWidth * 0.6); // 60% for name
            int sizeWidth = 80;
            
            // Calculate the remaining width for the date column
            int dateWidth = tableWidth - iconWidth - nameWidth - sizeWidth;
            
            // Apply the calculated widths
            Table.Columns[0].Width = iconWidth;              // Icon column
            Table.Columns[1].Width = nameWidth;              // Name column
            Table.Columns[2].Width = sizeWidth;              // Size column
            Table.Columns[3].Width = Math.Max(100, dateWidth); // Date column, minimum 100px
        }
        #endregion

        private void CloseBtn(object sender, System.Windows.RoutedEventArgs e) => CloseTab?.Invoke(this);

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void AsModel(object sender, EventArgs e)
        {
            if (Table.SelectedRows.Count < 1 || IsDirectory(Table.SelectedRows[0].Index))
                return;
            OpenPreview(Archive.Files.Where(x => x.Path.StartsWith(Path.RemoveFirstSeparator()) && x.Path.EndsWith(Table.Rows[Table.SelectedRows[0].Index].Cells[1].Value.ToString())).First(), PreviewType.SkiModel);
        }
        
        private void AsModel(object sender, System.Windows.RoutedEventArgs e)
        {
            if (Table.SelectedRows.Count < 1 || IsDirectory(Table.SelectedRows[0].Index))
                return;
            OpenPreview(Archive.Files.Where(x => x.Path.StartsWith(Path.RemoveFirstSeparator()) && x.Path.EndsWith(Table.Rows[Table.SelectedRows[0].Index].Cells[1].Value.ToString())).First(), PreviewType.SkiModel);
        }

        public void OpenPreview(IArchiveEntry entry, PreviewType type)
        {
            IPreviewWin viewer = null;
            switch (type)
            {
                case PreviewType.SkiModel:
                    viewer = new SkiViewer();
                    break;
            }
            viewer.Manager = Archive;
            viewer.Path = Path;
            viewer.File = entry;
            viewer.Prepare();
            (viewer as System.Windows.Window).Show();
        }

        public void RefreshFolders()
        {
            // Trigger folder update event with current folders
            if (_Folders.Count > 0)
            {
                // Use Dispatcher to ensure the event is triggered on the UI thread
                System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    FoldersUpdated?.Invoke(_Folders);
                }));
            }
        }

        /// <summary>
        /// Called from MainWindow when the window is resized
        /// </summary>
        public void HandleResize()
        {
            // Call ResizeColumns on the DataGridView to adjust column widths
            if (Table != null && Table.Columns.Count >= 4)
            {
                ResizeColumns();
            }
        }
    }
}
