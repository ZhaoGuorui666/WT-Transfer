using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Models
{
    public class VideoInfo : INotifyPropertyChanged
    {
        public string Bucket { get; set; }
        public string Date { get; set; }
        public string Path { get; set; }
        public string Title { get; set; }
        public string Size { get; set; }
        private string localPath;
        public string LocalPath
        {
            get { return localPath; }
            set
            {
                if (localPath != value)
                {
                    localPath = value;
                    OnPropertyChanged(nameof(LocalPath));
                }
            }
        }
        private bool isVideo;
        public bool IsVideo
        {
            get { return isVideo; }
            set
            {
                if (isVideo != value)
                {
                    isVideo = value;
                    OnPropertyChanged(nameof(IsVideo));
                }
            }
        }


        public void getTitle()
        {
            Title = System.IO.Path.GetFileNameWithoutExtension(Path);
        }

        private bool isSelected;
        public bool IsSelected
        {
            get => isSelected;
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public VideoInfo()
        {
        }

    }
}
