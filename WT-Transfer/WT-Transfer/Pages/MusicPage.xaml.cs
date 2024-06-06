// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using AdvancedSharpAdbClient;
using CommunityToolkit.WinUI.UI.Controls;
using CommunityToolkit.WinUI.UI;
using MathNet.Numerics.RootFinding;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Newtonsoft.Json;
using NLog;
using NPOI.OpenXmlFormats.Spreadsheet;
using Org.BouncyCastle.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Contacts;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Protection.PlayReady;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WT_Transfer.Helper;
using WT_Transfer.Models;
using WT_Transfer.SocketModels;
using Path = System.IO.Path;
using static WT_Transfer.SocketModels.Request;
using Microsoft.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MusicPage : Page, INotifyPropertyChanged
    {
        public ObservableCollection<MusicInfo> Musics { get; set; } = new ObservableCollection<MusicInfo>();

        ObservableCollection<MusicInfoGroup> MusicsByCreater
            = new ObservableCollection<MusicInfoGroup>();
        ObservableCollection<MusicInfoGroup> MusicsByAlbum
            = new ObservableCollection<MusicInfoGroup>();

        private AdbClient client = GuideWindow.client;
        private DeviceData device = GuideWindow.device;

        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();

        AdbHelper adbHelper = new AdbHelper(); 
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        private List<Button> buttons = new List<Button>();

        private bool _isAllSelected;
        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (_isAllSelected != value)
                {
                    _isAllSelected = value;
                    OnPropertyChanged(nameof(IsAllSelected));
                    SelectAllMusic(_isAllSelected);
                }
            }
        }

        public MusicPage()
        {
            try {
                this.InitializeComponent();

                this.Loaded += LoadingPage_Loaded;

                buttons.Add(ListButton);
                buttons.Add(SingerButton);
                buttons.Add(AlbumButton);

                this.DataContext = this;
            
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        private async void LoadingPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ListButton_Click(ListButton, new RoutedEventArgs()); // 模拟点击 ListButton

                if (this.Musics == null || this.Musics.Count == 0)
                {
                    if (MainWindow.Musics == null || MainWindow.Musics.Count == 0)
                    {
                        // 进行初始化操作，例如解析数据并赋值给 calls
                        if (!MainWindow.music_isRuning)
                            await Init();
                        else
                        {
                            await Task.Run(() =>
                            {
                                while (MainWindow.Musics == null)
                                {
                                    Task.Delay(1000).Wait();
                                }
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    InitPage();
                                });
                            });
                        }
                    }
                    else
                    {
                        InitPage();
                    }
                }

                // 在加载逻辑之后初始化选中文件信息
                InitializeSelectedFilesInfo();
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
        }

        private void InitPage()
        {
            this.Musics = MainWindow.Musics;
            this.MusicsByAlbum = MainWindow.MusicsByAlbum;
            this.MusicsByCreater = MainWindow.MusicsByCreater;


            progressRing.Visibility = Visibility.Collapsed;
            albumRepeater.Visibility = Visibility.Collapsed;
            musicListRepeater.ItemsSource = Musics;
            musicListRepeater.Visibility = Visibility.Visible;
            artistRepeater.Visibility = Visibility.Collapsed; // 初始化时隐藏
        }

        private async Task Init()
        {
            try
            {
                CollectionViewSource groupedItems = new CollectionViewSource();

                if (MainWindow.Permissions[4]=='0')
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        permission.XamlRoot = this.XamlRoot;
                        permission.ShowAsync();
                    });

                    MainWindow.Permissions[4] = '1';
                }

                MainWindow.music_isRuning = true;   

                await Task.Run(async () => {
                    SocketHelper helper = new SocketHelper();
                    AdbHelper adbHelper = new AdbHelper();


                    Result result = new Result();
                    await Task.Run(() =>
                    {
                        result = helper.getResult("music", "query");
                    });

                    if (result.status.Equals("00"))
                    {
                        if (string.IsNullOrEmpty(result.path))
                        {
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                progressRing.Visibility = Visibility.Collapsed;
                                NoDataText.Visibility = Visibility.Visible;
                            });
                        }
                        else
                        {
                            string musicInfo = adbHelper.readFromPath(result.path, "music");

                            //string path = "C:\\Users\\Windows 10\\Desktop\\1688302257535.music";
                            //string musicInfo = File.ReadAllText(path);
                            List<MusicInfo> list = JsonConvert.DeserializeObject<List<MusicInfo>>(musicInfo);

                            Musics = new ObservableCollection<MusicInfo>(list);
                            var newMusicsByCreater = new ObservableCollection<MusicInfoGroup>();
                            var newMusicsByAlbum = new ObservableCollection<MusicInfoGroup>();

                            //Implement grouping through LINQ queries
                            var query = from item in list
                                        group item by item.singer into g
                                        select new { GroupName = g.Key ?? "Unknown Artist", Items = g };
                            var query2 = from item in list
                                         group item by item.album into g
                                         select new { GroupName = g.Key ?? "Unknown Artist", Items = g };

                            // 在添加新项之前先清空这些集合
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                MusicsByCreater.Clear();
                                MusicsByAlbum.Clear();
                            });

                            foreach (var g in query)
                            {
                                var info = new MusicInfoGroup
                                {
                                    Key = g.GroupName
                                };
                                foreach (var item in g.Items)
                                {
                                    info.Add(item);
                                }
                                newMusicsByCreater.Add(info);
                            }

                            foreach (var g in query2)
                            {
                                var info = new MusicInfoGroup
                                {
                                    Key = g.GroupName
                                };
                                foreach (var item in g.Items)
                                {
                                    info.Add(item);
                                }
                                newMusicsByAlbum.Add(info);
                            }

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                groupedItems.IsSourceGrouped = true;

                                //按照歌手分类，使用TreeView渲染
                                MusicsByCreater = newMusicsByCreater;
                                MusicsByAlbum = newMusicsByAlbum;
                                groupedItems.Source = MusicsByCreater;
                                foreach (var group in MusicsByCreater)
                                {
                                    var singerNode = new TreeViewNode();
                                    singerNode.Content = group.Key; // Artist name as the node content
                                    singerNode.IsExpanded = false;
                                    artistRepeater.RootNodes.Add(singerNode);

                                    foreach (var song in group.Items)
                                    {
                                        var songNode = new TreeViewNode();
                                        songNode.Content = song; // Song details as the node content
                                        singerNode.Children.Add(songNode);
                                    }
                                }
                                foreach (var group in MusicsByAlbum)
                                {
                                    var albumNode = new TreeViewNode();
                                    albumNode.Content = group.Key; // Artist name as the node content
                                    albumNode.IsExpanded = false;
                                    albumRepeater.RootNodes.Add(albumNode);

                                    foreach (var song in group.Items)
                                    {
                                        var songNode = new TreeViewNode();
                                        songNode.Content = song; // Song details as the node content
                                        albumNode.Children.Add(songNode);
                                    }
                                }


                                progressRing.Visibility = Visibility.Collapsed;
                                musicListRepeater.ItemsSource = Musics;
                                musicListRepeater.Visibility = Visibility.Visible;
                            });
                        }
                    }
                    else if (result.status.Equals("101"))
                    {
                        // 不成功
                        DispatcherQueue.TryEnqueue(() => {
                            permission.Hide();
                            show_error(" No permissions granted.");

                            MainWindow.Permissions[4] = '0';
                        });
                    }
                    else
                    {
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            // 不成功
                            show_error("Music query failed ,please check the phone connection.");
                        });
                    }
                });


                MainWindow.music_isRuning = false;

                
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }

        }


        //同步音乐
        private async void PullMusic_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string winPath =
                (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_MusicBackupPath];

                //infoBar.Visibility = Visibility.Visible;

                SyncFolder.XamlRoot = this.XamlRoot;
                SyncFolder.ShowAsync();

                //SyncMessage.Text = "Musci successfully backup.";

                await Task.Run(() =>
                {
                    Musics.ToList().ForEach(music =>
                    {
                        string filePath = music.fileUrl;

                        string command = "pull -a \"" + "/" + filePath + "\"" + " \"" + winPath + "/" + music.fileName + "\"";
                        string res = adbHelper.cmdExecuteWithAdbExit(command) + "\n";

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            //infoBar.Message = "Currently backing up: " + music.title;
                            SyncMessage.Text = "Currently backing up: " + music.title;
                        });

                    });
                });

                SyncMessage.Text = "Music successfully backup.";
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //往手机中传入音乐
        private async void PushMusic_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                var hwnd = MainWindow.WindowHandle;
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.FileTypeFilter.Add("*");
                // 选择多个文件
                var files = await picker.PickMultipleFilesAsync();
                // 获取文件路径
                List<string> fileNames = files.Select(file => file.Path).ToList<string>();

                if (fileNames.Count == 0)
                {
                    return;
                }
                foreach (var file in fileNames)
                {
                    // 从路径中拿到文件名称
                    string fileName = Path.GetFileName(file);
                    string command = "push \"" + file + "\"" + " \"" + "/sdcard/Music/" + fileName + "\"";
                    string res = adbHelper.cmdExecuteWithAdbExit(command) + "\n";


                    // 新增音乐
                    Request request = new Request();
                    request.command_id = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                    request.module = "music";
                    request.operation = "insert";

                    string requestStr = JsonConvert.SerializeObject(request);
                    SocketHelper helper = new SocketHelper();
                    Result result = new Result();
                    await Task.Run(() =>
                    {
                        result = helper.ExecuteOp(requestStr);
                    });


                }


                ContentDialog appInfoDialog = new ContentDialog
                {
                    Title = "Info",
                    Content = "Music successfully uploaded",
                    PrimaryButtonText = "OK",
                };
                appInfoDialog.XamlRoot = this.Content.XamlRoot;
                ContentDialogResult re = await appInfoDialog.ShowAsync();


                await RefreshMusicList();
                ListButton_Click(ListButton, new RoutedEventArgs()); // 模拟点击 ListButton
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //singer分类
        private void dataGrid_LoadingRowGroup(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridRowGroupHeaderEventArgs e)
        {
            ICollectionViewGroup group = e.RowGroupHeader.CollectionViewGroup;
            MusicInfo item = group.GroupItems[0] as MusicInfo;
            e.RowGroupHeader.PropertyValue = item.singer;
        }

        //Album按钮
        private void GroupByAlbum_Click(object sender, RoutedEventArgs e)
        {
            // 将所有按钮设置为未选中
            foreach (var btn in buttons)
            {
                VisualStateManager.GoToState(btn, "Unselected", true);
            }

            // 将点击的按钮设置为选中
            var button = (Button)sender;
            VisualStateManager.GoToState(button, "Selected", true);


            CollectionViewSource groupedItems2 = new CollectionViewSource();
            groupedItems2.IsSourceGrouped = true;
            groupedItems2.Source = MusicsByAlbum;

            artistRepeater.Visibility = Visibility.Collapsed;
            albumRepeater.Visibility = Visibility.Visible;
            musicListRepeater.Visibility = Visibility.Collapsed;

            IsAllSelected = false;
            UpdateSelectedFilesInfo();
        }
        

        //Singer按钮
        private void GroupBySinger_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 将所有按钮设置为未选中
                foreach (var btn in buttons)
                {
                    VisualStateManager.GoToState(btn, "Unselected", true);
                }

                // 将点击的按钮设置为选中
                var button = (Button)sender;
                VisualStateManager.GoToState(button, "Selected", true);

                
                albumRepeater.Visibility = Visibility.Collapsed;
                musicListRepeater.Visibility = Visibility.Collapsed;
                artistRepeater.Visibility = Visibility.Visible;
                IsAllSelected = false;
                UpdateSelectedFilesInfo();

            }
            catch (Exception)
            {

                throw;
            }
        }

        //List按钮
        private void ListButton_Click(object sender, RoutedEventArgs e)
        {
            // 将所有按钮设置为未选中
            foreach (var btn in buttons)
            {
                VisualStateManager.GoToState(btn, "Unselected", true);
            }

            // 将点击的按钮设置为选中
            var button = (Button)sender;
            VisualStateManager.GoToState(button, "Selected", true);

            albumRepeater.Visibility = Visibility.Collapsed;
            artistRepeater.Visibility = Visibility.Collapsed;
            musicListRepeater.Visibility = Visibility.Visible;
            musicListRepeater.ItemsSource = Musics;
            IsAllSelected = false;
            UpdateSelectedFilesInfo();
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否有选中的音乐项
            var selectedMusics = Musics.Where(m => m.IsSelected).ToList();
            if (!selectedMusics.Any())
            {
                await ShowMessageDialog("No music selected", "Please select at least one music item to delete.");
                return;
            }

            // 确认删除
            bool isConfirmed = await ShowConfirmationDialog("Confirm Deletion", $"Are you sure you want to delete {selectedMusics.Count} selected item(s)?");
            if (!isConfirmed)
            {
                return;
            }

            // 执行删除操作
            foreach (var music in selectedMusics)
            {
                // 删除音乐文件的示例逻辑，您需要根据具体情况实现删除逻辑
                adbHelper.delFromPath(music.fileUrl);
                Musics.Remove(music);
            }

            // 更新UI
            // 删除音乐
            Request request = new Request();
            request.command_id = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            request.module = "music";
            request.operation = "insert";

            string requestStr = JsonConvert.SerializeObject(request);
            SocketHelper helper = new SocketHelper();
            Result result = new Result();
            await Task.Run(() =>
            {
                result = helper.ExecuteOp(requestStr);
            });


            // 刷新选中状态
            //UpdateSelectedFilesInfo();

            ContentDialog appInfoDialog = new ContentDialog
            {
                Title = "Info",
                Content = "Music successfully deleted",
                PrimaryButtonText = "OK",
            };
            appInfoDialog.XamlRoot = this.Content.XamlRoot;
            ContentDialogResult re = await appInfoDialog.ShowAsync();


            await RefreshMusicList();
            ListButton_Click(ListButton, new RoutedEventArgs()); // 模拟点击 ListButton
        }

        private async Task<bool> ShowConfirmationDialog(string title, string content)
        {
            ContentDialog confirmationDialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = "Delete",
                SecondaryButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Secondary
            };

            confirmationDialog.XamlRoot = this.Content.XamlRoot;
            ContentDialogResult result = await confirmationDialog.ShowAsync();

            return result == ContentDialogResult.Primary;
        }

        private async Task ShowMessageDialog(string title, string content)
        {
            ContentDialog messageDialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK"
            };

            messageDialog.XamlRoot = this.Content.XamlRoot;
            await messageDialog.ShowAsync();
        }


        private async void show_error(string msg)
        {
            ContentDialog appErrorDialog = new ContentDialog
            {
                Title = "Error",
                Content = "An error has occured:" + msg,
                PrimaryButtonText = "OK",
            };
            appErrorDialog.XamlRoot = this.Content.XamlRoot;
            ContentDialogResult re = await appErrorDialog.ShowAsync();
            if (re == ContentDialogResult.Primary)
            {

            }
        }


        //更新选中的文件信息
        private void UpdateSelectedFilesInfo()
        {
            if(musicListRepeater.Visibility == Visibility)
            {
                int selectedCount = Musics.Count(m => m.IsSelected); // 假设你的MusicInfo类有一个IsSelected属性
                                                                     // 计算选中文件的总大小
                double totalSelectedSize = Musics
                    .Where(m => m.IsSelected && !string.IsNullOrEmpty(m.size))
                    .Sum(m => ExtractSizeInMB(m.size));

                double totalSize = Musics.Sum(m => ExtractSizeInMB(m.size));

                // 更新TextBlock的文本
                SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - {totalSelectedSize:F2}MB of {totalSize:F2}MB";
            }
            else if(artistRepeater.Visibility == Visibility)
            {
                // 获取新增选中的节点
                IList<TreeViewNode> selectedNodes = artistRepeater.SelectedNodes;
                IList<TreeViewNode> filteredNodes = selectedNodes.Where(node => !node.HasChildren).ToList();

                int selectedCount = filteredNodes.Count;
                double totalSelectedSize = filteredNodes
                    .Sum(m => ExtractSizeInMB(((MusicInfo)m.Content).size));


                double totalSize = Musics.Sum(m => ExtractSizeInMB(m.size));

                // 更新TextBlock的文本
                SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - {totalSelectedSize:F2}MB of {totalSize:F2}MB";
            }
            else if (albumRepeater.Visibility == Visibility)
            {
                // 获取新增选中的节点
                IList<TreeViewNode> selectedNodes = albumRepeater.SelectedNodes;
                IList<TreeViewNode> filteredNodes = selectedNodes.Where(node => !node.HasChildren).ToList();

                int selectedCount = filteredNodes.Count;
                double totalSelectedSize = filteredNodes
                    .Sum(m => ExtractSizeInMB(((MusicInfo)m.Content).size));


                double totalSize = Musics.Sum(m => ExtractSizeInMB(m.size));

                // 更新TextBlock的文本
                SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - {totalSelectedSize:F2}MB of {totalSize:F2}MB";
            }

        }

        //初始化选中文件信息
        private void InitializeSelectedFilesInfo()
        {
            // 初始化时假设没有文件被选中
            int selectedCount = 0;

            // 计算所有文件的总大小
            double totalSize = Musics
                .Where(m => !string.IsNullOrEmpty(m.size))
                .Sum(m => ExtractSizeInMB(m.size));

            // 更新TextBlock的文本
            SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - 0MB of {totalSize:F2}MB";
        }


        private double ExtractSizeInMB(string sizeString)
        {
            // 尝试从size字符串中移除"M"，然后转换剩余的部分为double
            var cleanSizeString = sizeString.TrimEnd('M');
            bool success = double.TryParse(cleanSizeString, out double sizeValue);
            return success ? sizeValue : 0; // 如果转换失败，返回0
        }

        private void MusicList_Sorting(object sender, DataGridColumnEventArgs e)
        {
            var column = e.Column;
            var header = column.Header.ToString();
            var direction = column.SortDirection;

            IEnumerable<MusicInfo> sortedItems = null;

            // 根据点击的列头确定排序的属性
            switch (header)
            {
                case "Name":
                    sortedItems = SortData(Musics, m => m.fileName, direction);
                    break;
                case "Artist":
                    sortedItems = SortData(Musics, m => m.singer, direction);
                    break;
                case "Album":
                    sortedItems = SortData(Musics, m => m.album, direction);
                    break;
                case "Time":
                    sortedItems = SortData(Musics, m => m.duration, direction);
                    break;
                case "Size":
                    sortedItems = SortData(Musics, m => m.size, direction);
                    break;
            }

            // 更新排序方向
            column.SortDirection = direction == null || direction == DataGridSortDirection.Descending
                ? DataGridSortDirection.Ascending
                : DataGridSortDirection.Descending;

            // 应用排序结果
            musicListRepeater.ItemsSource = sortedItems.ToList();

            // 清除其他列的排序方向
            /**foreach (var dgColumn in musicListRepeater.Columns.Where(c => c != column))
            {
                dgColumn.SortDirection = null;
            }*/
        }

        private IEnumerable<MusicInfo> SortData<T>(IEnumerable<MusicInfo> source, Func<MusicInfo, T> keySelector, DataGridSortDirection? direction)
        {
            if (direction == null || direction == DataGridSortDirection.Descending)
            {
                return source.OrderBy(keySelector);
            }
            else
            {
                return source.OrderByDescending(keySelector);
            }
        }

        //查找框 按钮
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void PerformSearch()
        {
            // 获取用户输入的搜索文本
            var searchText = SearchBox.Text.ToLower();

            // 过滤曲目列表
            var filteredMusics = Musics.Where(m =>
                (!string.IsNullOrEmpty(m.fileName) && m.fileName.ToLower().Contains(searchText)) ||
                (!string.IsNullOrEmpty(m.singer) && m.singer.ToLower().Contains(searchText)) ||
                (!string.IsNullOrEmpty(m.album) && m.album.ToLower().Contains(searchText))
            ).ToList();

            // 更新数据网格的显示内容
            musicListRepeater.ItemsSource = filteredMusics;

            // 重置排序状态
            /**foreach (var column in musicList.Columns)
            {
                column.SortDirection = null;
            }*/
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshMusicList();
            ListButton_Click(ListButton, new RoutedEventArgs()); // 模拟点击 ListButton
        }

        private async Task RefreshMusicList()
        {
            // 显示进度环提示正在加载
            DispatcherQueue.TryEnqueue(() => {
                // 显示进度环提示正在加载
                progressRing.Visibility = Visibility.Visible;
                _progressRing.IsActive = true;
            });

            try
            {
                // 重新初始化音乐列表
                await Init();

                // 更新音乐列表显示
                musicListRepeater.ItemsSource = Musics;
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() => {
                    show_error(ex.ToString());
                    logHelper.Info(logger, ex.ToString());
                });
            }
            finally
            {
                DispatcherQueue.TryEnqueue(() => {
                    // 隐藏进度环
                    _progressRing.IsActive = false;
                    progressRing.Visibility = Visibility.Collapsed;
                });
            }
        }

        private StackPanel _previousSelectedPanel;


        private void OnCheckBoxChecked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                var panel = checkBox.Parent as StackPanel;
                if (panel != null)
                {
                    panel.Background = new SolidColorBrush(Colors.LightBlue);
                }

                var fileUrl = checkBox.Tag as string;
                if (!string.IsNullOrEmpty(fileUrl))
                {
                    var music = Musics.FirstOrDefault(m => m.fileUrl == fileUrl);
                    if (music != null)
                    {
                        music.IsSelected = true;
                    }
                }
            }

            UpdateSelectedFilesInfo();
        }

        private void OnCheckBoxUnchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null)
            {
                var panel = checkBox.Parent as StackPanel;
                if (panel != null)
                {
                    panel.Background = new SolidColorBrush(Colors.Transparent);
                }

                var fileUrl = checkBox.Tag as string;
                if (!string.IsNullOrEmpty(fileUrl))
                {
                    var music = Musics.FirstOrDefault(m => m.fileUrl == fileUrl);
                    if (music != null)
                    {
                        music.IsSelected = false;
                    }
                }
            }

            UpdateSelectedFilesInfo();
        }

        private void SelectAllMusic(bool isSelected)
        {
            if (musicListRepeater.Visibility == Visibility)
            {
                foreach (var music in Musics)
                {
                    music.IsSelected = isSelected;
                }
            }
            else if (artistRepeater.Visibility == Visibility)
            {
                if (isSelected)
                {
                    artistRepeater.SelectAll();
                }
                else
                {
                    artistRepeater.SelectedNodes.Clear();
                }
            }
            else if (albumRepeater.Visibility == Visibility)
            {
                if (isSelected)
                {
                    albumRepeater.SelectAll();
                }
                else
                {
                    albumRepeater.SelectedNodes.Clear();
                }
            }

            UpdateSelectedFilesInfo();
        }

        // 事件处理方法
        private void SelectAllCheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            IsAllSelected = true;
        }

        private void SelectAllCheckBox_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            IsAllSelected = false;
        }

        // 实现 OnPropertyChanged 方法
        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private void Artist_SelectionChanged(TreeView treeViewNode, TreeViewSelectionChangedEventArgs args)
        {
            UpdateSelectedFilesInfo();
        }
        private void Album_SelectionChanged(TreeView treeViewNode, TreeViewSelectionChangedEventArgs args)
        {
            UpdateSelectedFilesInfo();
        }

    }

    public class SingerItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate SingerInfoTemplate { get; set; }
        public DataTemplate MusicInfoTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            var node = (TreeViewNode)item;

            if (node.Children.Count > 0)
            {
                // If the node has children, it's an artist folder
                return SingerInfoTemplate;
            }
            else
            {
                // If the node has no children, it's a song
                return MusicInfoTemplate;
            }
        }
    }

    public class AlbumItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate AlbumInfoTemplate { get; set; }
        public DataTemplate MusicInfoTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            var node = (TreeViewNode)item;

            if (node.Children.Count > 0)
            {
                // If the node has children, it's an artist folder
                return AlbumInfoTemplate;
            }
            else
            {
                // If the node has no children, it's a song
                return MusicInfoTemplate;
            }
        }
    }
}


