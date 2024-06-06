// 版权所有 (c) Microsoft Corporation and Contributors.
// 根据 MIT 许可证获得许可。

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using NLog;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WT_Transfer.Helper;
using WT_Transfer.SocketModels;
using WT_Transfer.Models;
using Windows.Storage;
using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Text;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;
using NPOI.SS.Formula.Functions;
using Windows.ApplicationModel.Contacts;
using NLog.Fluent;
using MathNet.Numerics.RootFinding;
using Microsoft.UI.Xaml.Media.Imaging;
using Org.BouncyCastle.Asn1.X509;

namespace WT_Transfer.Pages
{
    /// <summary>
    /// 一个空白页面，可以单独使用或在 Frame 内导航到此页面。
    /// </summary>
    public sealed partial class PhotoPage : Page
    {
        // 辅助类用于各种功能
        SocketHelper socketHelper = new SocketHelper();
        AdbHelper adbHelper = new AdbHelper();

        // 日志记录实例
        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        // 用于存储照片信息的数据结构
        public List<PhotoInfo> Photos = new List<PhotoInfo>();
        public List<PhotoInfo> PhotosSorted = new List<PhotoInfo>();
        public Dictionary<string, List<PhotoInfo>> PhotosInBucket { get; set; }
        HashSet<String> buckets = new HashSet<string>();
        string currentBucket = "";

        // 当前操作的模块
        string currentModule = "bucket";
        List<PhotoInfo> currentPhotos = new List<PhotoInfo>();


        // 构造函数
        public PhotoPage()
        {
            this.InitializeComponent();

            // 页面加载时调用 LoadingPage_Loaded 方法
            this.Loaded += LoadingPage_Loaded;
        }

        // 页面加载完成后的处理方法
        private async void LoadingPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.Photos == null || this.buckets == null || this.PhotosInBucket == null
                    || Photos.Count == 0 || buckets.Count == 0 || PhotosInBucket.Count == 0)
                {
                    if (MainWindow.Photos == null || MainWindow.buckets == null || MainWindow.PhotosInBucket == null)
                    {
                        // 进行初始化操作，例如解析数据并赋值给 calls
                        if (!MainWindow.sms_isRuning)
                            await Init();
                        else
                        {
                            await Task.Run(() =>
                            {
                                while (MainWindow.Photos == null)
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

        // 初始化页面的方法
        private void InitPage()
        {
            Photos = MainWindow.Photos;
            buckets = MainWindow.buckets;
            PhotosInBucket = MainWindow.PhotosInBucket;
            PhotosSorted = MainWindow.PhotosSorted;

            // 设置目录网格的数据源并更新分页信息
            BucketGrid.ItemsSource = buckets.ToList();

            // 隐藏进度环并显示目录网格
            progressRing.Visibility = Visibility.Collapsed;
            BucketGrid.Visibility = Visibility.Visible;
        }

        // 初始化数据的方法
        private async Task Init()
        {
            try
            {
                if (MainWindow.Permissions[4] == '0')
                {
                    permission.XamlRoot = this.XamlRoot;
                    permission.ShowAsync();

                    MainWindow.Permissions[4] = '1';
                }

                await Task.Run(async () => {
                    PhotosInBucket = new Dictionary<string, List<PhotoInfo>>();

                    SocketHelper helper = new SocketHelper();
                    AdbHelper adbHelper = new AdbHelper();

                    Result result = new Result();
                    await Task.Run(() =>
                    {
                        result = helper.getResult("picture", "query");
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
                            string str = adbHelper.readFromPath(result.path, "picture");
                            // 解析数据
                            JArray jArray = JArray.Parse(str);
                            // 使用 LINQ 查询获取 DisplayName 和 MobileNum
                            var resultArray = (from item in jArray
                                               select new
                                               {
                                                   Bucket = item["bucket"]?.ToString(),
                                                   Date = item["date"]?.ToString(),
                                                   Path = item["path"]?.ToString()
                                               })
                                            .ToArray();

                            // 统计文件夹数量
                            foreach (var item in resultArray)
                            {
                                if (item.Bucket == null)
                                {
                                    buckets.Add("null");
                                    continue;
                                }
                                buckets.Add(item.Bucket);
                            }

                            // 将数据存储到字典中
                            foreach (var item in resultArray)
                            {
                                PhotoInfo photoInfo = new PhotoInfo();
                                photoInfo.Bucket = item.Bucket;
                                photoInfo.Date = item.Date;
                                photoInfo.Path = item.Path;
                                photoInfo.getTitle();

                                if (item.Bucket == null)
                                {
                                    photoInfo.Bucket = "null";
                                }
                                Photos.Add(photoInfo);

                                List<PhotoInfo> photos = new List<PhotoInfo>();
                                if (PhotosInBucket.TryGetValue(photoInfo.Bucket, out photos))
                                {
                                    // 如果字典中已有该键，直接添加照片信息
                                    photos.Add(photoInfo);
                                }
                                else
                                {
                                    PhotosInBucket.Add(photoInfo.Bucket, new List<PhotoInfo> {
                        photoInfo
                    });
                                }
                            }

                            // 使用 LINQ 查询排序照片信息
                            PhotosSorted = new List<PhotoInfo>(Photos);
                            PhotosSorted = PhotosSorted.OrderByDescending(p => p.Date).ToList();

                            // 缓存数据
                            MainWindow.buckets = buckets;
                            MainWindow.Photos = Photos;
                            MainWindow.PhotosSorted = PhotosSorted;
                            MainWindow.PhotosInBucket = PhotosInBucket;

                            // 更新界面
                            DispatcherQueue.TryEnqueue(() => {
                                BucketGrid.ItemsSource = buckets.ToList();

                                progressRing.Visibility = Visibility.Collapsed;
                                BucketGrid.Visibility = Visibility.Visible;
                            });
                        }
                    }
                    else if (result.status.Equals("101"))
                    {
                        // 无权限
                        DispatcherQueue.TryEnqueue(() => {
                            permission.Hide();
                            show_error(" No permissions granted.");

                            MainWindow.Permissions[4] = '0';
                        });
                    }
                    else
                    {
                        // 查询失败
                        show_error("Photo query failed ,please check the phone connection.");
                    }
                });

            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // 双击目录的事件处理方法
        private void StackPanel_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            try
            {
                StackPanel stackPanel = sender as StackPanel;
                foreach (var child in stackPanel.Children)
                {
                    // 检查子元素是否为 TextBlock 类型
                    if (child is TextBlock textBlock)
                    {
                        // 获取目录名称
                        string bucket = textBlock.Text;
                        currentBucket = bucket;
                        List<PhotoInfo> photos = new List<PhotoInfo>();
                        if (PhotosInBucket.TryGetValue(bucket, out photos))
                        {
                            // 按时间排序
                            photos = photos.OrderByDescending(item => item.Date).ToList();
                            PhotosInBucket[bucket] = photos;

                            // 更新当前模块和分页信息
                            currentModule = "photo";

                            // 切换到照片网格视图
                            BucketGrid.Visibility = Visibility.Collapsed;
                            PhotoGrid.Visibility = Visibility.Visible;

                            // 设置照片的缩略图路径
                            setPhotoImgPath(photos.ToList());

                            // 设置数据源
                            currentPhotos = photos.ToList();
                            PhotoGrid.ItemsSource = photos.ToList();
                        }
                        else
                        {
                            // 找不到图片
                        }
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

        // 设置照片的缩略图路径
        public void setPhotoImgPath(List<PhotoInfo> photos)
        {
            try
            {
                foreach (var photo in photos)
                {
                    int index = Photos.FindIndex(p => p.Title == photo.Title);
                    // 设置缩略图路径
                    string phonePath =
                        "/storage/emulated/0/Android/data/com.example.contacts/files/Download/pic/" + index + ".jpg";

                    string localPath =
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "./images/pic/" + index + ".jpg")); ;
                    string str = adbHelper.saveFromPath(phonePath, localPath);
                    if (File.Exists(localPath))
                    {
                        photo.LocalPath = localPath;
                    }
                    else
                    {
                        photo.LocalPath = "/Images/noImg.png";
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

        // 同步选中的文件夹
        private async void SyncFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 没有选中文件夹
                if (BucketGrid.SelectedItem == null)
                {
                    ContentDialog a = new ContentDialog
                    {
                        Title = "Error",
                        Content = "Please select a folder.",
                        PrimaryButtonText = "OK",
                    };
                    a.XamlRoot = this.Content.XamlRoot;
                    ContentDialogResult r = await a.ShowAsync();
                    return;
                }

                string selectedItem = BucketGrid.SelectedItem.ToString();
                List<PhotoInfo> photoInfos = PhotosInBucket[selectedItem];

                ContentDialog appInfoDialog = new ContentDialog
                {
                    Title = "Info",
                    Content = "Start backing up images.",
                    PrimaryButtonText = "OK",
                    SecondaryButtonText = "Cancel",
                };
                appInfoDialog.XamlRoot = this.Content.XamlRoot;
                ContentDialogResult re = await appInfoDialog.ShowAsync();

                if (re == ContentDialogResult.Primary)
                {
                    appInfoDialog.Hide();

                    SyncPhoto.XamlRoot = this.XamlRoot;
                    SyncPhoto.ShowAsync();

                    await Task.Run(async () =>
                    {
                        foreach (var photo in photoInfos)
                        {
                            string path = photo.Path;
                            string localPath = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_PhotoBackupPath];
                            string winPath = localPath + "\\" + selectedItem + "\\" + photo.Title;

                            adbHelper.saveFromPath(path, winPath);
                            DispatcherQueue.TryEnqueue(() => {
                                //infoBar.Message = "Currently backing up: " + photo.Title;
                                SyncMessage.Text = "Currently backing up: " + photo.Title;
                            });
                        }
                    });

                    SyncMessage.Text = "Image backup successful.";
                }
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // 保存单张图片
        private async void Save_Clicks(object sender, RoutedEventArgs e)
        {
            try
            {
                PhotoInfo selectedItem = new PhotoInfo();
                if (currentModule.Equals("photo"))
                    selectedItem = (PhotoInfo)PhotoGrid.SelectedItem;
                else
                    selectedItem = null;

                if (selectedItem == null)
                {
                    ContentDialog a = new ContentDialog
                    {
                        Title = "Error",
                        Content = "Please select a picture.",
                        PrimaryButtonText = "OK",
                    };
                    a.XamlRoot = this.Content.XamlRoot;
                    ContentDialogResult r = await a.ShowAsync();
                    return;
                }

                string musicPath = selectedItem.Path;

                var filePicker = new FolderPicker();
                var hWnd = MainWindow.WindowHandle;
                InitializeWithWindow.Initialize(filePicker, hWnd);
                filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                filePicker.FileTypeFilter.Add("*");
                Windows.Storage.StorageFolder storageFolder = await filePicker.PickSingleFolderAsync();

                if (storageFolder != null)
                {
                    string winPath = storageFolder.Path + "\\" + Path.GetFileName(selectedItem.Title);

                    adbHelper.saveFromPath(musicPath, winPath);
                    ContentDialog appInfoDialog = new ContentDialog
                    {
                        Title = "Info",
                        Content = "Image save successful.",
                        PrimaryButtonText = "OK",
                    };
                    appInfoDialog.XamlRoot = this.Content.XamlRoot;
                    ContentDialogResult re = await appInfoDialog.ShowAsync();
                }

            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // 设置图片为壁纸
        private async void SetPhotoToWall_Clicks(object sender, RoutedEventArgs e)
        {
            try
            {
                PhotoInfo selectedItem = new PhotoInfo();
                if (currentModule.Equals("photo"))
                    selectedItem = (PhotoInfo)PhotoGrid.SelectedItem;
                else
                    selectedItem = null;

                if (selectedItem == null || selectedItem.Title.Contains(".mp4"))
                {
                    ContentDialog a = new ContentDialog
                    {
                        Title = "Error",
                        Content = "Please select a picture.",
                        PrimaryButtonText = "OK",
                    };
                    a.XamlRoot = this.Content.XamlRoot;
                    ContentDialogResult rr = await a.ShowAsync();
                    return;
                }

                string photoPath = selectedItem.Path;
                string localPath =
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path,
                        "./images/" + "wallpaper.jpg")); ;
                adbHelper.saveFromPath(photoPath, localPath);
                WallpaperChanger.SetWallpaper(localPath);

                ContentDialog aa = new ContentDialog
                {
                    Title = "Info",
                    Content = "Wallpaper set successfully.",
                    PrimaryButtonText = "OK",
                };
                aa.XamlRoot = this.Content.XamlRoot;
                ContentDialogResult r = await aa.ShowAsync();
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // 删除操作
        private async void Del_Clicks(object sender, RoutedEventArgs e)
        {
            try
            {
                PhotoInfo selectedItem = new PhotoInfo();
                if (currentModule.Equals("photo"))
                    selectedItem = (PhotoInfo)PhotoGrid.SelectedItem;
                else
                    selectedItem = null;

                if ((selectedItem == null && currentModule.Equals("photo"))
                    || (BucketGrid.SelectedItem == null && currentModule.Equals("bucket")))
                {
                    ContentDialog a = new ContentDialog
                    {
                        Title = "Error",
                        Content = "Please select a picture or folder.",
                        PrimaryButtonText = "OK",
                    };
                    a.XamlRoot = this.Content.XamlRoot;
                    ContentDialogResult r = await a.ShowAsync();
                }

                string selectedBucket = "";
                if (BucketGrid.SelectedItem != null)
                    selectedBucket = BucketGrid.SelectedItem.ToString();

                // 询问用户确认删除操作
                ContentDialog aa = new ContentDialog
                {
                    Title = "Info",
                    Content = "Are you sure to delete it?",
                    PrimaryButtonText = "OK",
                    SecondaryButtonText = "Cancel",
                };
                aa.XamlRoot = this.Content.XamlRoot;
                ContentDialogResult rr = await aa.ShowAsync();
                if (rr == ContentDialogResult.Primary)
                {
                    // 删除操作
                    // 1. 删除目录
                    if (!string.IsNullOrEmpty(selectedBucket) && currentModule.Equals("bucket"))
                    {
                        await Task.Run(async () => {
                            List<PhotoInfo> photoInfos = PhotosInBucket[selectedBucket];

                            foreach (var photo in photoInfos)
                            {
                                // 删除设备中的图片
                                string path = photo.Path;
                                string res = adbHelper.cmdExecuteWithAdbExit("shell rm -r " + path);
                                Result result = socketHelper.getResult("picture", "delete");
                            }

                            DispatcherQueue.TryEnqueue(() => {
                                // 更新UI，删除目录信息
                                buckets.Remove(selectedBucket);
                                PhotosInBucket.Remove(selectedBucket);
                                MainWindow.PhotosInBucket = PhotosInBucket;
                                MainWindow.buckets = buckets;

                                BucketGrid.ItemsSource = buckets.ToList();
                            });
                        });
                    }
                    // 删除单张图片
                    else
                    {
                        string path = selectedItem.Path;
                        string res = adbHelper.cmdExecuteWithAdbExit("shell rm " + path);
                        Result result = new Result();
                        await Task.Run(() =>
                        {
                            result = socketHelper.getResult("picture", "delete");
                        });

                        if (result.status.Equals("00"))
                        {
                            // 删除文件
                            if (currentModule.Equals("photo"))
                            {
                                List<PhotoInfo> photoInfos = PhotosInBucket[currentBucket];
                                photoInfos.Remove(selectedItem);
                                currentPhotos.Remove(selectedItem);
                                PhotosInBucket[currentBucket] = photoInfos;
                                PhotoGrid.ItemsSource = currentPhotos;

                                MainWindow.PhotosInBucket = PhotosInBucket;
                            }
                        }
                        else if (result.status.Equals("101"))
                        {
                            // 无权限
                            DispatcherQueue.TryEnqueue(() => {
                                show_error(" No permissions granted.");
                            });
                        }
                        else
                        {
                            // 删除失败
                            show_error("Please check the phone connection and restart the software.");
                        }
                    }

                    ContentDialog a = new ContentDialog
                    {
                        Title = "Info",
                        Content = "Successfully deleted.",
                        PrimaryButtonText = "OK",
                    };
                    a.XamlRoot = this.Content.XamlRoot;
                    ContentDialogResult r = await a.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // 返回按钮点击事件处理方法
        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 切换到目录视图
                BucketGrid.Visibility = Visibility.Visible;
                PhotoGrid.Visibility = Visibility.Collapsed;
                currentModule = "bucket";

                // 更新分页信息
                BucketGrid.ItemsSource = buckets.ToList();
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // 显示信息对话框
        private async void show_info(string title, string content)
        {
            ContentDialog appErrorDialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = "OK",
            };
            appErrorDialog.XamlRoot = this.Content.XamlRoot;
            ContentDialogResult re = await appErrorDialog.ShowAsync();
            if (re == ContentDialogResult.Primary)
            {
            }
        }


        // 目录按钮点击事件处理方法
        private void DirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 切换到目录视图
                progressRing.Visibility = Visibility.Collapsed;
                BucketGrid.Visibility = Visibility.Visible;
                PhotoGrid.Visibility = Visibility.Collapsed;
                currentModule = "bucket";

            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // 双击图片的事件处理方法
        private async void StackPanel_DoubleTapped_1(object sender, DoubleTappedRoutedEventArgs e)
        {
            try
            {
                StackPanel stackPanel = sender as StackPanel;
                foreach (var child in stackPanel.Children)
                {
                    // 检查子元素是否为 TextBlock 类型
                    if (child is TextBlock textBlock)
                    {
                        // 获取照片路径
                        string path = textBlock.Tag.ToString();
                        string localPath = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_PhotoBackupPath];
                        string winPath = localPath + "\\image" + "\\" + textBlock.Text;

                        adbHelper.saveFromPath(path, winPath);

                        ShowPhoto.XamlRoot = this.XamlRoot;
                        BitmapImage imageSource = new BitmapImage(new Uri(winPath));
                        photoImage.Source = imageSource;
                        await ShowPhoto.ShowAsync();
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

        // 显示错误信息对话框
        private async void show_error(string msg)
        {
            ContentDialog appErrorDialog = new ContentDialog
            {
                Title = "Error",
                Content = "An error has occurred: " + msg,
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
