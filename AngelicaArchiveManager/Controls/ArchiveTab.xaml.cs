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

namespace AngelicaArchiveManager.Controls
{
    public partial class ArchiveTab : TabItem, INotifyPropertyChanged
    {
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
            Table.Rows[0].SetValues(Properties.Resources.folder, "...", "", "");
            int i = 1;
            if (count1 > 0)
            {
                foreach (var f in _Folders[Path])
                {
                    Table.Rows[i].SetValues(Properties.Resources.folder, f, "", "");
                    ++i;
                }
            }
            if (count2 > 0)
            {
                foreach (var f in _Files[Path])
                {
                    Table.Rows[i].SetValues(Properties.Resources.file,
                        System.IO.Path.GetFileName(f.Path),
                        f.Size,
                        f.CSize);
                    ++i;
                }
            }
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
            var menu = new System.Windows.Forms.ContextMenu(
                new System.Windows.Forms.MenuItem[] { new System.Windows.Forms.MenuItem("Open As",
                new System.Windows.Forms.MenuItem[] { new System.Windows.Forms.MenuItem("As .ski model", new EventHandler(AsModel)) }
            )});
            menu.Show(Table, e.Location);
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
                AllowUserToResizeColumns = false,
                AllowUserToResizeRows = false,
                AllowDrop = true,
            };
            Table.RowTemplate.Height = 24;
            Table.RowTemplate.Resizable = DataGridViewTriState.False;
            Table.Columns.Add(new DataGridViewImageColumn()
            {
                HeaderText = "",
                Width = 24,
                Resizable = DataGridViewTriState.False
            });
            Table.Columns.Add(new DataGridViewTextBoxColumn()
            {
                HeaderText = "File name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                Resizable = DataGridViewTriState.False
            });
            Table.Columns.Add(new DataGridViewTextBoxColumn()
            {
                HeaderText = "Size",
                Width = 120,
                Resizable = DataGridViewTriState.False
            });
            Table.Columns.Add(new DataGridViewTextBoxColumn()
            {
                HeaderText = "Compressed size",
                Width = 120,
                Resizable = DataGridViewTriState.False,
            });
            foreach (var column in Table.Columns)
            {
                if (column is DataGridViewTextBoxColumn)
                {
                    (column as DataGridViewTextBoxColumn).HeaderCell.Style.BackColor = Color.White;
                    //(column as DataGridViewTextBoxColumn).CellTemplate.Style.SelectionBackColor = System.Drawing.Color.LightYellow;
                    (column as DataGridViewTextBoxColumn).Resizable = DataGridViewTriState.False;
                }
                if (column is DataGridViewImageColumn)
                {
                    (column as DataGridViewImageColumn).HeaderCell.Style.BackColor = Color.White;
                    (column as DataGridViewImageColumn).CellTemplate.Style.SelectionBackColor = Color.Transparent;
                    (column as DataGridViewImageColumn).Resizable = DataGridViewTriState.False;
                }
            }
            Table.CellDoubleClick += CellDoubleClick;
            Table.DragEnter += DragEnter;
            Table.DragDrop += DragDrop;
            Table.MouseUp += MouseUp;
            Table.MouseDown += MouseDown;
            Table.MouseMove += MouseMove;
            Table.MouseClick += MouseClick;
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
    }
}
