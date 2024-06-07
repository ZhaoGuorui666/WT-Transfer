using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Models
{
    public class PhotoInfo
    {
        public string Bucket { get; set; }
        public string Date { get; set; }
        public string Path { get; set; }
        public string Title { get; set; }
        public string LocalPath { get; set; }

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

        public PhotoInfo()
        {
        }

    }
}
