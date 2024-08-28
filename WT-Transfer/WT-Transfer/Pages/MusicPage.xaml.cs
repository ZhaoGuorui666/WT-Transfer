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
using NPOI.HPSF;

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

        private enum ViewState
        {
            List,
            Artist,
            Album
        }

        private ViewState currentState = ViewState.List;


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
                this.SearchBox.TextChanged += SearchBox_TextChanged; // 添加这行

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


        private async void ExportAllMusic_Click(object sender, RoutedEventArgs e)
        {
            // 处理导出所有图片的逻辑
            ExportMusics(allMusics: true);
        }

        private async void ExportSelectedMusic_Click(object sender, RoutedEventArgs e)
        {
            if (musicListRepeater.Visibility == Visibility.Visible)
            {
                if(Musics.Where(music => music.IsSelected).ToList().Count == 0)
                {
                    await ShowMessageDialog("No music selected", "No music files have been selected for export.\r\nPlease select the music files you want to export and try again.");
                    return;
                }
                ExportMusics(allMusics: false, selectedMusics: Musics.Where(music => music.IsSelected).ToList());
            }
            else if (artistRepeater.Visibility == Visibility.Visible)
            {
                var selectedNodes = artistRepeater.SelectedNodes;
                var selectedMusics = selectedNodes.Where(node => !node.HasChildren)
                                                  .Select(node => node.Content as MusicInfo)
                                                  .ToList();
                if (selectedMusics.Count == 0)
                {
                    await ShowMessageDialog("No music selected", "No music files have been selected for export.\r\nPlease select the music files you want to export and try again.");
                    return;
                }
                ExportMusics(allMusics: false, selectedMusics: selectedMusics);
            }
            else if (albumRepeater.Visibility == Visibility.Visible)
            {
                var selectedNodes = albumRepeater.SelectedNodes;
                var selectedMusics = selectedNodes.Where(node => !node.HasChildren)
                                                  .Select(node => node.Content as MusicInfo)
                                                  .ToList();
                ExportMusics(allMusics: false, selectedMusics: selectedMusics);
            }
        }

        private async void ExportMusics(bool allMusics, List<MusicInfo> selectedMusics = null)
        {
            try
            {
                var filePicker = new FolderPicker();
                var hWnd = MainWindow.WindowHandle;
                InitializeWithWindow.Initialize(filePicker, hWnd);
                filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                filePicker.FileTypeFilter.Add("*");
                Windows.Storage.StorageFolder storageFolder = await filePicker.PickSingleFolderAsync();

                if (storageFolder != null)
                {
                    List<MusicInfo> musicsToExport;

                    if (allMusics)
                    {
                        // 导出所有音乐的逻辑
                        musicsToExport = Musics.ToList();
                    }
                    else
                    {
                        // 导出选中音乐的逻辑
                        musicsToExport = selectedMusics ?? new List<MusicInfo>();
                    }

                    // 创建并显示ContentDialog
                    var progressDialog = new ContentDialog
                    {
                        Title = "Exporting Music",
                        Content = new StackPanel
                        {
                            Children =
                    {
                        new ProgressBar
                        {
                            Name = "ExportProgressBar",
                            Minimum = 0,
                            Maximum = 100,
                            Width = 300,
                            Height = 20
                        },
                        new TextBlock
                        {
                            Name = "ExportProgressText",
                            Margin = new Thickness(0, 10, 0, 0)
                        }
                    }
                        },
                        CloseButtonText = "Cancel"
                    };
                    progressDialog.XamlRoot = this.Content.XamlRoot;
                    var progressBar = ((StackPanel)progressDialog.Content).Children[0] as ProgressBar;
                    var progressText = ((StackPanel)progressDialog.Content).Children[1] as TextBlock;

                    // 显示进度对话框
                    _ = progressDialog.ShowAsync();

                    await Task.Run(() =>
                    {
                        int totalMusics = musicsToExport.Count;
                        int exportedMusics = 0;

                        foreach (var music in musicsToExport)
                        {
                            string path = music.fileUrl;
                            string localPath = storageFolder.Path + "\\" + music.fileName;

                            adbHelper.saveFromPathWithBlank(path, localPath);

                            exportedMusics++;
                            double progress = (double)exportedMusics / totalMusics * 100;

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                progressBar.Value = progress;
                                progressText.Text = $"{progress:F1}%";
                            });
                        }
                    });

                    // 关闭ContentDialog
                    progressDialog.Hide();

                    ContentDialog exportDialog = new ContentDialog
                    {
                        Title = "Export Complete",
                        Content = "Your music has been successfully exported to the designated folder.",
                        PrimaryButtonText = "View Folder",
                        SecondaryButtonText = "OK",
                    };
                    exportDialog.XamlRoot = this.Content.XamlRoot;
                    // 打开文件夹的操作
                    exportDialog.PrimaryButtonClick += async (s, args) =>
                    {
                        await Windows.System.Launcher.LaunchFolderPathAsync(storageFolder.Path);
                    };
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        _ = exportDialog.ShowAsync();
                    });
                }
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        private async void ExportMusics(bool allMusics)
        {
            try
            {
                String MusicBackupPath = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_MusicBackupPath];
                var filePicker = new FolderPicker();
                var hWnd = MainWindow.WindowHandle;
                InitializeWithWindow.Initialize(filePicker, hWnd);
                filePicker.SuggestedStartLocation = PickerLocationId.MusicLibrary;
                filePicker.FileTypeFilter.Add("*");

                Windows.Storage.StorageFolder storageFolder = await filePicker.PickSingleFolderAsync();

                if (storageFolder != null)
                {
                    List<MusicInfo> musicsToExport;

                    if (allMusics)
                    {
                        // 导出所有音乐的逻辑
                        musicsToExport = Musics.ToList();
                    }
                    else
                    {
                        // 导出选中音乐的逻辑
                        musicsToExport = Musics.Where(music => music.IsSelected).ToList();
                    }
                    // 创建并显示ContentDialog
                    var progressDialog = new ContentDialog
                    {
                        Title = "Exporting Music",
                        Content = new StackPanel
                        {
                            Children =
                        {
                            new ProgressBar
                            {
                                Name = "ExportProgressBar",
                                Minimum = 0,
                                Maximum = 100,
                                Width = 300,
                                Height = 20
                            },
                            new TextBlock
                            {
                                Name = "ExportProgressText",
                                Margin = new Thickness(0, 10, 0, 0)
                            }
                            }
                        },
                        CloseButtonText = "Cancel"
                    };
                    progressDialog.XamlRoot = this.Content.XamlRoot;
                    var progressBar = ((StackPanel)progressDialog.Content).Children[0] as ProgressBar;
                    var progressText = ((StackPanel)progressDialog.Content).Children[1] as TextBlock;

                    // 显示进度对话框
                    _ = progressDialog.ShowAsync();



                    await Task.Run(() =>
                    {
                        int totalMusics = musicsToExport.Count;
                        int exportedMusics = 0;

                        foreach (var music in musicsToExport)
                        {
                            string path = music.fileUrl;
                            string localPath = storageFolder.Path+"\\"+music.fileName;

                            adbHelper.saveFromPathWithBlank(path, localPath);

                            exportedMusics++;
                            double progress = (double)exportedMusics / totalMusics * 100;

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                progressBar.Value = progress;
                                progressText.Text = $"{progress:F1}%";
                            });
                        }
                    });

                    // 关闭ContentDialog
                    progressDialog.Hide();

                    ContentDialog exportDialog = new ContentDialog
                    {
                        Title = "Export Complete",
                        Content = "Your music has been successfully exported to the designated folder.",
                        PrimaryButtonText = "View Folder",
                        SecondaryButtonText = "OK",
                        DefaultButton = ContentDialogButton.Secondary // 设置OK为默认按钮
                    };
                    exportDialog.XamlRoot = this.Content.XamlRoot;
                    // 打开文件夹的操作
                    exportDialog.PrimaryButtonClick += async (s, args) =>
                    {
                        await Windows.System.Launcher.LaunchFolderPathAsync(storageFolder.Path);
                    };
                    await exportDialog.ShowAsync();
                }
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


                List<String> paths = new List<string>();
                foreach (var file in fileNames)
                {
                    // 从路径中拿到文件名称
                    string fileName = Path.GetFileName(file);
                    string command = "push \"" + file + "\"" + " \"" + "/sdcard/Music/" + fileName + "\"";
                    string res = adbHelper.cmdExecuteWithAdbExit(command) + "\n";

                    paths.Add("/sdcard/Music/" + fileName);
                    
                }

                // 提醒手机 新增音乐，扫描
                Request request = new Request();
                request.command_id = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                request.module = "music";
                request.operation = "insert";
                request.info = new Data
                {
                    paths = paths,
                };

                string requestStr = JsonConvert.SerializeObject(request);
                SocketHelper helper = new SocketHelper();
                Result result = new Result();
                await Task.Run(() =>
                {
                    result = helper.ExecuteOp(requestStr);
                });


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
            currentState = ViewState.Album;
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
                currentState = ViewState.Artist;
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

            currentState = ViewState.List;
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
                await ShowMessageDialog("No music selected", "No music files have been selected for export.\r\nPlease select the music files you want to export and try again.");
                return;
            }

            // 确认删除
            bool isConfirmed = await ShowConfirmationDialog("Confirm Deletion", $"Are you sure you want to delete the selected music? This action cannot be undone.");
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
            request.operation = "delete";
            request.info = new Data
            {
                paths = selectedMusics.Select(file => file.fileUrl).ToList<string>(),
            };

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
                Content = "The selected music files have been successfully deleted.",
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
                SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - {totalSelectedSize:F2} MB of {totalSize:F2} MB";
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
                SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - {totalSelectedSize:F2} MB of {totalSize:F2} MB";
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
                SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - {totalSelectedSize:F2} MB of {totalSize:F2} MB";
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
            SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - 0 MB of {totalSize:F2} MB";
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
            //ListButton_Click(ListButton, new RoutedEventArgs()); // 模拟点击 ListButton
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

                // 清空并重新填充 artistRepeater 和 albumRepeater
                artistRepeater.RootNodes.Clear();
                albumRepeater.RootNodes.Clear();

                foreach (var group in MusicsByCreater)
                {
                    var singerNode = new TreeViewNode
                    {
                        Content = group.Key,
                        IsExpanded = false
                    };

                    foreach (var song in group.Items)
                    {
                        var songNode = new TreeViewNode
                        {
                            Content = song
                        };
                        singerNode.Children.Add(songNode);
                    }
                    artistRepeater.RootNodes.Add(singerNode);
                }

                foreach (var group in MusicsByAlbum)
                {
                    var albumNode = new TreeViewNode
                    {
                        Content = group.Key,
                        IsExpanded = false
                    };

                    foreach (var song in group.Items)
                    {
                        var songNode = new TreeViewNode
                        {
                            Content = song
                        };
                        albumNode.Children.Add(songNode);
                    }
                    albumRepeater.RootNodes.Add(albumNode);
                }

                // 保持当前视图状态
                switch (currentState)
                {
                    case ViewState.List:
                        musicListRepeater.ItemsSource = Musics;
                        albumRepeater.Visibility = Visibility.Collapsed;
                        artistRepeater.Visibility = Visibility.Collapsed;
                        musicListRepeater.Visibility = Visibility.Visible;
                        break;
                    case ViewState.Artist:
                        artistRepeater.Visibility = Visibility.Visible;
                        albumRepeater.Visibility = Visibility.Collapsed;
                        musicListRepeater.Visibility = Visibility.Collapsed;
                        break;
                    case ViewState.Album:
                        albumRepeater.Visibility = Visibility.Visible;
                        artistRepeater.Visibility = Visibility.Collapsed;
                        musicListRepeater.Visibility = Visibility.Collapsed;
                        break;
                }
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

        //选择文件夹，音乐导入
        private async void SelectFiles_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.FileTypeFilter.Add(".mp3");
            picker.FileTypeFilter.Add(".wav");
            picker.FileTypeFilter.Add(".flac");

            var hwnd = MainWindow.WindowHandle;
            InitializeWithWindow.Initialize(picker, hwnd);

            var files = await picker.PickMultipleFilesAsync();
            if (files != null)
            {
                await ImportFilesToAndroid(files);
            }
        }

        private async void SelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
            picker.FileTypeFilter.Add("*");

            var hwnd = MainWindow.WindowHandle;
            InitializeWithWindow.Initialize(picker, hwnd);

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                var files = await folder.GetFilesAsync();
                await ImportFilesToAndroid(files);
            }
        }

        private async Task ImportFilesToAndroid(IEnumerable<StorageFile> files)
        {
            if(!files.Any())
            {
                return;
            }


            var progressDialog = new ContentDialog
            {
                Title = "Importing Files",
                Content = new StackPanel
                {
                    Children =
            {
                new ProgressBar
                {
                    Name = "ImportProgressBar",
                    Minimum = 0,
                    Maximum = 100,
                    Width = 300,
                    Height = 20
                        },
                new TextBlock
                {
                    Name = "ImportProgressText",
                    Margin = new Thickness(0, 10, 0, 0)
                }
            }
                },
                CloseButtonText = "Cancel"
            };

            progressDialog.XamlRoot = this.Content.XamlRoot;
            var progressBar = ((StackPanel)progressDialog.Content).Children[0] as ProgressBar;
            var progressText = ((StackPanel)progressDialog.Content).Children[1] as TextBlock;

            // 显示进度对话框
            _ = progressDialog.ShowAsync();

            int totalFiles = files.Count();
            int importedFiles = 0;

            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    string localPath = file.Path;
                    string androidPath = $"/sdcard/Music/{file.Name}";

                    adbHelper.importFromPath(localPath, androidPath);

                    importedFiles++;
                    double progress = (double)importedFiles / totalFiles * 100;

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        progressBar.Value = progress;
                        progressText.Text = $"{progress:F1}%";
                    });
                }
            });

            List<String> paths = new List<string>();
            foreach (var file in files)
            {
                paths.Add("/sdcard/Music/" + file.Name);
            }

            // 提醒手机 新增音乐，扫描
            Request request = new Request();
            request.command_id = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            request.module = "music";
            request.operation = "insert";
            request.info = new Data
            {
                paths = paths,
            };

            string requestStr = JsonConvert.SerializeObject(request);
            SocketHelper helper = new SocketHelper();
            Result result = new Result();
            await Task.Run(() =>
            {
                result = helper.ExecuteOp(requestStr);
            });

            await RefreshMusicList();
            ListButton_Click(ListButton, new RoutedEventArgs()); // 模拟点击 ListButton

            // 隐藏进度对话框
            progressDialog.Hide();

            ContentDialog importDialog = new ContentDialog
            {
                Title = "Info",
                Content = "Your files have been successfully imported.",
                PrimaryButtonText = "OK",
            };
            importDialog.XamlRoot = this.Content.XamlRoot;
            await importDialog.ShowAsync();
        }

        //点击表头排序
        private bool _isSortedAscending = true;
        public ObservableCollection<MusicInfo> SortedMusics { get; set; } = new ObservableCollection<MusicInfo>();

        private void SortBy(string columnName)
        {

            if (musicListRepeater.Visibility == Visibility.Visible)
            {
                SortListView(columnName);
            }
            else if (artistRepeater.Visibility == Visibility.Visible)
            {
                SortGroupedItemsBy(columnName, "artist");
            }
            else if (albumRepeater.Visibility == Visibility.Visible)
            {
                SortGroupedItemsBy(columnName, "album");
            }

            _isSortedAscending = !_isSortedAscending;
        }


        private void ClearAllSortIcons()
        {
            NameSortIcon.Text = string.Empty;
            TimeSortIcon.Text = string.Empty;
            ArtistSortIcon.Text = string.Empty;
            AlbumSortIcon.Text = string.Empty;
            SizeSortIcon.Text = string.Empty;
        }


        private void SortListView(string columnName)
        {
            IEnumerable<MusicInfo> sortedList;

            // 先清空所有的排序图标
            ClearAllSortIcons();

            // 根据点击的列头确定要显示的图标
            string glyph = _isSortedAscending ? "\uEB11" : "\uEB0F"; // StockUp or StockDown


            switch (columnName)
            {
                case "Name":
                    NameSortIcon.Text = glyph;
                    sortedList = _isSortedAscending ? Musics.OrderBy(m => m.fileName) : Musics.OrderByDescending(m => m.fileName);
                    break;
                case "Time":
                    TimeSortIcon.Text = glyph;
                    sortedList = _isSortedAscending ? Musics.OrderBy(m => m.duration) : Musics.OrderByDescending(m => m.duration);
                    break;
                case "Artist":
                    ArtistSortIcon.Text = glyph;
                    sortedList = _isSortedAscending ? Musics.OrderBy(m => m.singer) : Musics.OrderByDescending(m => m.singer);
                    break;
                case "Album":
                    AlbumSortIcon.Text = glyph;
                    sortedList = _isSortedAscending ? Musics.OrderBy(m => m.album) : Musics.OrderByDescending(m => m.album);
                    break;
                case "Size":
                    SizeSortIcon.Text = glyph;
                    sortedList = _isSortedAscending ? Musics.OrderBy(m => m.size) : Musics.OrderByDescending(m => m.size);
                    break;
                default:
                    sortedList = Musics;
                    break;
            }

            musicListRepeater.ItemsSource = new ObservableCollection<MusicInfo>(sortedList);
        }

        private void SortGroupedItemsBy(string columnName, string viewType)
        {
            var expandedNodes = new Dictionary<string, bool>();

            


            if (viewType == "artist")
            {
                foreach (var group in MusicsByCreater)
                {
                    string groupKey = group.Key.ToString();
                    expandedNodes[groupKey] = GetTreeViewNodeByKey(artistRepeater.RootNodes, groupKey)?.IsExpanded ?? false;
                    SortGroup(group.Items, columnName);
                }
            }
            else if (viewType == "album")
            {
                foreach (var group in MusicsByAlbum)
                {
                    string groupKey = group.Key.ToString();
                    expandedNodes[groupKey] = GetTreeViewNodeByKey(albumRepeater.RootNodes, groupKey)?.IsExpanded ?? false;
                    SortGroup(group.Items, columnName);
                }
            }

            RefreshGroupedViews(viewType, expandedNodes);
        }

        private void SortGroup(ObservableCollection<MusicInfo> group, string columnName)
        {
            IEnumerable<MusicInfo> sortedList;
            // 先清空所有的排序图标
            ClearAllSortIcons();

            // 根据点击的列头确定要显示的图标
            string glyph = _isSortedAscending ? "\uEB11" : "\uEB0F"; // StockUp or StockDown


            switch (columnName)
            {
                case "Name":

                    NameSortIcon.Text = glyph;
                    sortedList = _isSortedAscending ? group.OrderBy(m => m.fileName) : group.OrderByDescending(m => m.fileName);
                    break;
                case "Time":
                    TimeSortIcon.Text = glyph;
                    sortedList = _isSortedAscending ? group.OrderBy(m => m.duration) : group.OrderByDescending(m => m.duration);
                    break;
                case "Artist":
                    ArtistSortIcon.Text = glyph;
                    sortedList = _isSortedAscending ? group.OrderBy(m => m.singer) : group.OrderByDescending(m => m.singer);
                    break;
                case "Album":
                    AlbumSortIcon.Text = glyph;
                    sortedList = _isSortedAscending ? group.OrderBy(m => m.album) : group.OrderByDescending(m => m.album);
                    break;
                case "Size":
                    SizeSortIcon.Text = glyph;
                    sortedList = _isSortedAscending ? group.OrderBy(m => m.size) : group.OrderByDescending(m => m.size);
                    break;
                default:
                    sortedList = group;
                    break;
            }

            var sortedGroup = new ObservableCollection<MusicInfo>(sortedList);
            group.Clear();
            foreach (var item in sortedGroup)
            {
                group.Add(item);
            }
        }

        private TreeViewNode GetTreeViewNodeByKey(IList<TreeViewNode> nodes, string key)
        {
            foreach (var node in nodes)
            {
                if (node.Content.ToString() == key)
                {
                    return node;
                }
            }
            return null;
        }

        private void RefreshGroupedViews(string viewType, Dictionary<string, bool> expandedNodes)
        {
            if (viewType == "artist")
            {
                artistRepeater.RootNodes.Clear();
                foreach (var group in MusicsByCreater)
                {
                    var singerNode = new TreeViewNode { Content = group.Key };
                    foreach (var song in group.Items)
                    {
                        singerNode.Children.Add(new TreeViewNode { Content = song });
                    }

                    string groupKey = group.Key.ToString();
                    singerNode.IsExpanded = expandedNodes.ContainsKey(groupKey) && expandedNodes[groupKey];
                    artistRepeater.RootNodes.Add(singerNode);
                }
            }
            else if (viewType == "album")
            {
                albumRepeater.RootNodes.Clear();
                foreach (var group in MusicsByAlbum)
                {
                    var albumNode = new TreeViewNode { Content = group.Key };
                    foreach (var song in group.Items)
                    {
                        albumNode.Children.Add(new TreeViewNode { Content = song });
                    }

                    string groupKey = group.Key.ToString();
                    albumNode.IsExpanded = expandedNodes.ContainsKey(groupKey) && expandedNodes[groupKey];
                    albumRepeater.RootNodes.Add(albumNode);
                }
            }
        }

        private void SortBy_Click(object sender, RoutedEventArgs e)
        {
            var textBlock = sender as TextBlock;
            if (textBlock != null)
            {
                SortBy(textBlock.Text);
            }
        }


        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchBox.Text))
            {
                // 如果搜索框为空，则显示所有音乐
                musicListRepeater.ItemsSource = Musics;
            }
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


