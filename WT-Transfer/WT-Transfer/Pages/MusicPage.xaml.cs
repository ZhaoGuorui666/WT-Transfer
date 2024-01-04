// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using AdvancedSharpAdbClient;
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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MusicPage : Page
    {
        public List<MusicInfo> Musics { get; set; }
        ObservableCollection<GroupInfoCollection<MusicInfo>> MusicsByCreater
            = new ObservableCollection<GroupInfoCollection<MusicInfo>>();
        ObservableCollection<GroupInfoCollection<MusicInfo>> MusicsByAlbum
            = new ObservableCollection<GroupInfoCollection<MusicInfo>>();

        private AdbClient client = GuideWindow.client;
        private DeviceData device = GuideWindow.device;

        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();

        AdbHelper adbHelper = new AdbHelper(); 
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        public MusicPage()
        {
            this.InitializeComponent();

            this.Loaded += LoadingPage_Loaded;
        }

        private async void LoadingPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.Musics == null)
                {
                    if (MainWindow.Musics == null)
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
            dataGrid.Visibility = Visibility.Collapsed;
            musicList.ItemsSource = Musics;
            musicList.Visibility = Visibility.Visible;
        }

        private async Task Init()
        {
            try
            {
                CollectionViewSource groupedItems = new CollectionViewSource();

                if (MainWindow.Permissions[4]=='0')
                {
                    permission.XamlRoot = this.XamlRoot;
                    permission.ShowAsync();

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


                            Musics = new List<MusicInfo>(list);

                            //Implement grouping through LINQ queries
                            var query = from item in list
                                        group item by item.singer into g
                                        select new { GroupName = g.Key, Items = g };
                            var query2 = from item in list
                                         group item by item.album into g
                                         select new { GroupName = g.Key, Items = g };

                            foreach (var g in query)
                            {
                                GroupInfoCollection<MusicInfo> info = new GroupInfoCollection<MusicInfo>();
                                info.Key = g.GroupName;
                                foreach (var item in g.Items)
                                {
                                    info.Add(item);
                                }
                                MusicsByCreater.Add(info);
                            }
                            foreach (var g in query2)
                            {
                                GroupInfoCollection<MusicInfo> info = new GroupInfoCollection<MusicInfo>();
                                info.Key = g.GroupName;
                                foreach (var item in g.Items)
                                {
                                    info.Add(item);
                                }
                                MusicsByAlbum.Add(info);
                            }

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                groupedItems.IsSourceGrouped = true;
                                groupedItems.Source = MusicsByCreater;
                                dataGrid.ItemsSource = groupedItems.View;
                                progressRing.Visibility = Visibility.Collapsed;
                                dataGrid.Visibility = Visibility.Collapsed;
                                musicList.ItemsSource = Musics;
                                musicList.Visibility = Visibility.Visible;
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
                        // 不成功
                        show_error("Music query failed ,please check the phone connection.");
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

                //infoBar.Visibility = Visibility.Collapsed;


                SyncMessage.Text = "Music successfully backup.";

                //ContentDialog appInfoDialog = new ContentDialog
                //{
                //    Title = "Info",
                //    Content = "Musci successfully backup",
                //    PrimaryButtonText = "OK",
                //};
                //appInfoDialog.XamlRoot = this.Content.XamlRoot;
                //ContentDialogResult re = await appInfoDialog.ShowAsync();
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


                    MusicInfo musicInfo = new MusicInfo();
                    musicInfo.title = Path.GetFileName(file);
                    musicInfo.fileName = Path.GetFileName(file);
                    Musics.Add(musicInfo);
                }


                progressRing.Visibility = Visibility.Collapsed;
                dataGrid.Visibility = Visibility.Collapsed;
                musicList.ItemsSource = Musics;
                musicList.Visibility = Visibility.Visible;

                ContentDialog appInfoDialog = new ContentDialog
                {
                    Title = "Info",
                    Content = "Music successfully uploaded",
                    PrimaryButtonText = "OK",
                };
                appInfoDialog.XamlRoot = this.Content.XamlRoot;
                ContentDialogResult re = await appInfoDialog.ShowAsync();
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
            CollectionViewSource groupedItems2 = new CollectionViewSource();
            groupedItems2.IsSourceGrouped = true;
            groupedItems2.Source = MusicsByAlbum;

            dataGrid1.ItemsSource = groupedItems2.View;
            dataGrid.Visibility = Visibility.Collapsed;
            dataGrid1.Visibility = Visibility.Visible;
            musicList.Visibility = Visibility.Collapsed;

            AlbumButton.FontWeight = FontWeights.Bold;
            SingerButton.FontWeight = FontWeights.Thin;
            ListButton.FontWeight = FontWeights.Thin;
        }
        
        //Album分类
        private void dataGrid1_LoadingRowGroup(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridRowGroupHeaderEventArgs e)
        {

            ICollectionViewGroup group = e.RowGroupHeader.CollectionViewGroup;
            MusicInfo item = group.GroupItems[0] as MusicInfo;
            e.RowGroupHeader.PropertyValue = item.album;
        }

        //Singer按钮
        private void GroupBySinger_Click(object sender, RoutedEventArgs e)
        {
            CollectionViewSource groupedItems = new CollectionViewSource();
            groupedItems.IsSourceGrouped = true;
            groupedItems.Source = MusicsByCreater;

            dataGrid1.ItemsSource = groupedItems.View;
            dataGrid1.Visibility = Visibility.Collapsed;
            dataGrid.Visibility = Visibility.Visible;
            musicList.Visibility = Visibility.Collapsed;


            AlbumButton.FontWeight = FontWeights.Thin;
            SingerButton.FontWeight = FontWeights.Bold;
            ListButton.FontWeight = FontWeights.Thin;
        }

        //List按钮
        private void ListButton_Click(object sender, RoutedEventArgs e)
        {

            dataGrid1.Visibility = Visibility.Collapsed;
            dataGrid.Visibility = Visibility.Collapsed;
            musicList.Visibility = Visibility.Visible;
            musicList.ItemsSource = Musics;

            AlbumButton.FontWeight = FontWeights.Thin;
            SingerButton.FontWeight = FontWeights.Thin;
            ListButton.FontWeight = FontWeights.Bold;
        }

        private async void DelMusic_Click(object sender, RoutedEventArgs e)
        {
            //弹框显示

            // 开对话框，选择还原模式
            ContentDialog appErrorDialog = new ContentDialog
            {
                Title = "Info",
                Content = "DDo you want to delete the selected music ?",
                PrimaryButtonText = "Yes",
                SecondaryButtonText = "Cancel"
            };
            appErrorDialog.XamlRoot = this.Content.XamlRoot;
            ContentDialogResult re = await appErrorDialog.ShowAsync();

            if (re == ContentDialogResult.Primary)
            {
                MusicInfo musicInfo;
                if (musicList.Visibility == Visibility.Visible)
                {
                    musicInfo = (MusicInfo)musicList.SelectedItem;
                }
                else if (dataGrid.Visibility == Visibility.Visible)
                {
                    musicInfo = (MusicInfo)dataGrid.SelectedItem;
                }
                else
                {
                    musicInfo = (MusicInfo)dataGrid1.SelectedItem;
                }

                //执行删除操作
                string res = adbHelper.delFromPath(musicInfo.fileUrl);

                //弹框显示成功
                appErrorDialog = new ContentDialog
                {
                    Title = "Info",
                    Content = "Successfully deleted.",
                    PrimaryButtonText = "Yes"
                };
                appErrorDialog.XamlRoot = this.Content.XamlRoot;
                re = await appErrorDialog.ShowAsync();

                //刷新页面
                await Init();
            }



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
    }
}
