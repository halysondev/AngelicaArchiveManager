using AngelicaArchiveManager.Core.ArchiveEngine;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

namespace AngelicaArchiveManager
{
    public partial class SettingsWin : Window, INotifyPropertyChanged
    {
        private ObservableCollection<ArchiveKey> _Keys = new ObservableCollection<ArchiveKey>();
        public ObservableCollection<ArchiveKey> Keys
        {
            get => _Keys;
            set
            {
                if (_Keys != value)
                {
                    _Keys = value;
                    OnPropertyChanged("Keys");
                }
            }
        }
        private int _Index = -1;
        public int Index
        {
            get => _Index;
            set
            {
                if (_Index != value)
                {
                    _Index = value;
                    OnPropertyChanged("Index");
                    OnPropertyChanged("Key");
                }
            }
        }
        public ArchiveKey Key
        {
            get
            {
                if (Index > -1)
                    return Keys[Index];
                return null;
            }
            set
            {
                if (Index < 0)
                    return;
                Keys[Index] = value;
                OnPropertyChanged("Key");
            }
        }

        public SettingsWin()
        {
            InitializeComponent();
            DataContext = this;
            foreach (var k in Settings.Keys)
                Keys.Add(k);
            Compression.SelectedIndex = Settings.CompressionLevel;
        }

        private void AddClick(object sender, RoutedEventArgs e)
        {
            Keys.Add(new ArchiveKey() { Name = "New Key" });
        }

        private void RemoveClick(object sender, RoutedEventArgs e)
        {
            if (Index > -1)
                Keys.RemoveAt(Index);
        }

        private void SaveClick(object sender, RoutedEventArgs e)
        {
            Settings.CompressionLevel = Compression.SelectedIndex;
            Settings.Language = LanguageSelector.SelectedIndex + 1;
            Settings.Keys.Clear();
            Settings.Keys.AddRange(Keys);
            Settings.Save();
            Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
