// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WT_Transfer.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    public class GroupInfoListTest : List<CustomDataObject>, INotifyPropertyChanged
    {
        public string Key { get; set; }
        public int PhotoCount => this.Count; // 新增属性，返回照片总数public int SelectedCount => this.Count(item => item.IsSelected);

        public int SelectedCount => this.Count(item => item.IsSelected);
        public event PropertyChangedEventHandler PropertyChanged;

        public GroupInfoListTest() : base()
        {
        }
        public new void Add(CustomDataObject item)
        {
            item.PropertyChanged += Item_PropertyChanged;
            base.Add(item);
            OnPropertyChanged(nameof(PhotoCount));
            OnPropertyChanged(nameof(SelectedCount));
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CustomDataObject.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedCount));
            }
        }

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }



    public sealed partial class TestPage : Page
    {
        public ObservableCollection<GroupInfoListTest> GroupedItems { get; set; }

        public TestPage()
        {
            this.InitializeComponent();
            this.DataContext = this;
            this.Loaded += ItemsViewPage_Loaded;
        }

        private ObservableCollection<GroupInfoListTest> GenerateGroupedData()
        {
            string imageDirectory = @"D:\BaiduNetdiskDownload\val2017";
            string thumbnailDirectory = Path.Combine(imageDirectory, "thumbnails");

            var groupedData = new ObservableCollection<GroupInfoListTest>();

            for (int i = 0; i < 4000; i += 20)
            {
                var group = new GroupInfoListTest() { Key = $"Group {i / 20 + 1}" };

                for (int j = 1; j <= 20; j++)
                {
                    string imageName = $"Image{j + i}.jpg";
                    string imagePath = Path.Combine(imageDirectory, imageName);
                    string thumbnailPath = Path.Combine(thumbnailDirectory, imageName);

                    CustomDataObject image = new CustomDataObject
                    {
                        Title = imagePath,
                        ImageLocation = thumbnailPath,
                        Views = j.ToString(),
                        Likes = j.ToString(),
                        Description = j.ToString(),
                    };

                    group.Add(image);
                }

                groupedData.Add(group);
            }

            return groupedData;
        }


        private void ItemsViewPage_Loaded(object sender, RoutedEventArgs e)
        {
            GroupedItems = GenerateGroupedData();
            var cvs = new CollectionViewSource
            {
                IsSourceGrouped = true,
                Source = GroupedItems
            };

            SwappableSelectionModesItemsView.ItemsSource = cvs.View;

            // 显示每个组的照片总数
        }

        private void SwappableSelectionModesItemsView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedItem = e.ClickedItem as CustomDataObject;
            if (clickedItem != null)
            {
                clickedItem.IsSelected = !clickedItem.IsSelected;

                // 找到包含该项的组并更新选中数量
                var parentGroup = GroupedItems.FirstOrDefault(g => g.Contains(clickedItem));
                if (parentGroup != null)
                {
                    parentGroup.OnPropertyChanged(nameof(parentGroup.SelectedCount));
                }
            }
        }
    }

}
