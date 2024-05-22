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

namespace AngelicaArchiveManager
{
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
                tab.TabIndex = Archives.Items.Count;
                Archives.Items.Add(tab);
                Archives.SelectedIndex = tab.TabIndex;
                tab.Initialize();
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
        }

        private void PathEnter(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                Archives.Focus();
        }

        private void CloseTab(object tab) => Archives.Items.Remove(tab);

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
            tab.TabIndex = Archives.Items.Count;
            Archives.Items.Add(tab);
            Archives.SelectedIndex = tab.TabIndex;
            tab.Initialize();
        }
    }
}
