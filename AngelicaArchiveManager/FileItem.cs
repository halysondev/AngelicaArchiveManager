using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AngelicaArchiveManager
{
    public class FileItem : INotifyPropertyChanged
    {
        private string _fileName;
        private string _type;
        private string _compressed;
        private string _decompressed;

        public string FileName
        {
            get { return _fileName; }
            set
            {
                if (_fileName != value)
                {
                    _fileName = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Type
        {
            get { return _type; }
            set
            {
                if (_type != value)
                {
                    _type = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Compressed
        {
            get { return _compressed; }
            set
            {
                if (_compressed != value)
                {
                    _compressed = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Decompressed
        {
            get { return _decompressed; }
            set
            {
                if (_decompressed != value)
                {
                    _decompressed = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 