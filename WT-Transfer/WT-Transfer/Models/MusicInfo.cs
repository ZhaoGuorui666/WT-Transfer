using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Models
{
    public class MusicInfo : INotifyPropertyChanged
    {
        private string _album;
        private string _duration;
        private string _fileName;
        private string _fileUrl;
        private string _singer;
        private string _size;
        private string _title;
        private string _type;
        private string _year;
        private bool _isSelected;

        public string album
        {
            get => _album;
            set { _album = value; OnPropertyChanged(nameof(album)); }
        }

        public string duration
        {
            get => _duration;
            set { _duration = value; OnPropertyChanged(nameof(duration)); }
        }

        public string fileName
        {
            get => _fileName;
            set { _fileName = value; OnPropertyChanged(nameof(fileName)); }
        }

        public string fileUrl
        {
            get => _fileUrl;
            set { _fileUrl = value; OnPropertyChanged(nameof(fileUrl)); }
        }

        public string singer
        {
            get => _singer;
            set
            {
                _singer = value ?? "Unknown Artist";  // 设置默认值
                OnPropertyChanged(nameof(singer));
            }
        }

        public string size
        {
            get => _size;
            set { _size = value; OnPropertyChanged(nameof(size)); }
        }

        public string title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(nameof(title)); }
        }

        public string type
        {
            get => _type;
            set { _type = value; OnPropertyChanged(nameof(type)); }
        }

        public string year
        {
            get => _year;
            set { _year = value; OnPropertyChanged(nameof(year)); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

}
