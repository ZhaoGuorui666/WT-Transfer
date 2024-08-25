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
using System.ComponentModel;
using static NPOI.HSSF.Util.HSSFColor;
using Newtonsoft.Json;
using static WT_Transfer.SocketModels.Request;
using System.Threading;
using Microsoft.UI.Xaml.Shapes;
using System.Diagnostics;

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

        //Alubm列表数据
        List<AlbumInfo> albumList = new List<AlbumInfo>();
        ObservableCollection<GroupInfoList> groupedData;

        //当前目录
        private string currentDirectory = "/Pictures/";

        // 构造函数
        public PhotoPage()
        {
            try
            {
                this.InitializeComponent();

                CurrentDirectoryTextBox.Text = currentDirectory;
                // 页面加载时调用 LoadingPage_Loaded 方法
                this.Loaded += LoadingPage_Loaded;
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
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

                // 初始化选择状态文本
                UpdateSelectedFilesInfo();
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
            BucketGrid.ItemsSource = PhotosInBucket;

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

                await Task.Run(async () =>
                {
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
                                                   Path = item["path"]?.ToString(),
                                                   Size = item["size"]?.ToString(),
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

                                // 将大小从字节转换为MB
                                if (double.TryParse(item.Size, out double sizeInBytes))
                                {
                                    double sizeInMB = sizeInBytes / (1024 * 1024);
                                    photoInfo.Size = sizeInMB.ToString("0.## MB"); // 格式化为保留两位小数
                                }
                                else
                                {
                                    photoInfo.Size = "Unknown Size"; // 如果无法解析，设置为未知
                                }

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

                            //异步线程将缩率图传输到电脑端
                            Task.Run(() =>
                            {
                                string phonePath =
                        "/storage/emulated/0/Android/data/com.example.contacts/files/Download/pic";

                                string localPath =
                                    System.IO.Path.GetFullPath(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "./images"));

                                adbHelper.savePathFromPath(phonePath, localPath);
                            });
                            int count = 0;
                            foreach (var photo in Photos)
                            {
                                // 设置缩略图路径
                                string localPath =
                                    System.IO.Path.GetFullPath(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "./images/pic/" + count++ + ".jpg")); ;

                                photo.LocalPath = localPath;
                            }


                            AddAlbumList();
                            // 更新界面
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                try
                                {
                                    BucketGrid.ItemsSource = albumList;
                                    progressRing.Visibility = Visibility.Collapsed;
                                    BucketGrid.Visibility = Visibility.Visible;
                                }
                                catch (Exception ex)
                                {
                                    show_error("Error updating UI: " + ex.Message);
                                    logHelper.Info(logger, ex.ToString());
                                }
                            });
                        }
                    }
                    else if (result.status.Equals("101"))
                    {
                        // 无权限
                        DispatcherQueue.TryEnqueue(() =>
                        {
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

        // 初始化目录 AlbumList
        private List<AlbumInfo> AddAlbumList()
        {
            albumList = new List<AlbumInfo>();
            foreach (var kv in PhotosInBucket)
            {
                List<PhotoInfo> photos = new List<PhotoInfo>();
                photos.Add(kv.Value.First());
                setPhotoImgPath(photos);
                if (kv.Value.Count > 0)
                {
                    var firstPhotoPath = kv.Value.First().LocalPath;
                    albumList.Add(new AlbumInfo
                    {
                        Name = kv.Key,
                        FirstPhotoPath = firstPhotoPath,
                        PhotoCount = kv.Value.Count
                    });
                }
                else
                {
                    albumList.Add(new AlbumInfo
                    {
                        Name = kv.Key,
                        FirstPhotoPath = "/Images/folder.jpg", // 默认封面图片
                        PhotoCount = 0
                    });
                }
            }

            return albumList;
        }

        // 更新选择状态信息
        private void UpdateSelectedFilesInfo()
        {
            int selectedCount = Photos.Count(photo => photo.IsSelected);
            double selectedSizeMB = Photos.Where(photo => photo.IsSelected).Sum(photo => ParseSizeInMB(photo.Size));

            double totalSizeMB = Photos.Sum(photo => ParseSizeInMB(photo.Size));
            string info = $"{selectedCount} of {Photos.Count} Item(s) Selected - {selectedSizeMB:0.##} MB of {totalSizeMB:0.##} MB";
            SelectedFilesInfo.Text = info;
        }

        // 解析大小字符串为MB
        private double ParseSizeInMB(string sizeString)
        {
            string str = sizeString.Replace(" MB", "").Trim();
            if (double.TryParse(str, out double size))
            {
                return size;
            }
            return 0;
        }
        // 双击目录的事件处理方法
        private async void StackPanel_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
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
                        List<PhotoInfo> photos = new List<PhotoInfo>();
                        if (PhotosInBucket.TryGetValue(bucket, out photos))
                        {
                            // 更新当前目录
                            currentBucket = bucket;
                            currentDirectory = $"/Pictures/{bucket}";
                            CurrentDirectoryTextBox.Text = currentDirectory;

                            //按照日期分组
                            groupedData = new ObservableCollection<GroupInfoList>();

                            var groupedPhotos = photos
                                .GroupBy(p => DateTime.Parse(p.Date).ToString("yyyy-MM-dd"))
                                .OrderByDescending(g => g.Key);

                            foreach (var group in groupedPhotos)
                            {
                                var groupInfoList = new GroupInfoList { Key = group.Key };
                                groupInfoList.AddRange(group);
                                groupedData.Add(groupInfoList);
                            }


                            // 按时间排序
                            //PhotosInBucket[bucket] = photos;

                            // 更新当前模块和分页信息
                            currentModule = "photo";

                            DispatcherQueue.TryEnqueue(() =>
                            {

                                // 显示加载进度条
                                BucketGrid.Visibility = Visibility.Collapsed;
                                progressRing.Visibility = Visibility.Visible;
                            });


                            DispatcherQueue.TryEnqueue(() =>
                            {
                                // 切换到照片网格视图
                                PhotoGrid.Visibility = Visibility.Visible;
                                // 隐藏加载进度条
                                progressRing.Visibility = Visibility.Collapsed;
                            });

                            // 设置数据源
                            //currentPhotos = photos.ToList();

                            //如果选中之后退出，再次进入之后，把之前选中的图片给设置选中状态
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                foreach (var group in groupedData)
                                {
                                    foreach (PhotoInfo p in group)
                                    {
                                        if (p.IsSelected == true)
                                        {
                                            PhotoGrid.SelectedItems.Add(p);
                                        }
                                    }
                                }
                            });

                            var cvs = new CollectionViewSource
                            {
                                IsSourceGrouped = true,
                                Source = groupedData
                            };
                            PhotoGrid.ItemsSource = cvs.View;
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

        private ObservableCollection<GroupInfoList> GenerateGroupedData()
        {
            string imageDirectory = @"D:\BaiduNetdiskDownload\val2017";
            string thumbnailDirectory = System.IO.Path.Combine(imageDirectory, "thumbnails");

            var groupedData = new ObservableCollection<GroupInfoList>();

            for (int i = 0; i < 1000; i += 50)
            {
                var group = new GroupInfoList() { Key = $"Group {i / 50 + 1}" };

                for (int j = 1; j <= 50; j++)
                {
                    string imageName = $"Image{j + i}.jpg";
                    string imagePath = System.IO.Path.Combine(imageDirectory, imageName);
                    string thumbnailPath = System.IO.Path.Combine(thumbnailDirectory, imageName);

                    PhotoInfo image = new PhotoInfo
                    {
                        Title = imagePath,
                        LocalPath = thumbnailPath
                    };

                    group.Add(image);
                }

                groupedData.Add(group);
            }

            return groupedData;
        }


        // 设置照片的缩略图路径
        public async Task setPhotoImgPath(List<PhotoInfo> photos)
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
                    // 异步保存缩略图
                    await Task.Run(() => adbHelper.saveFromPath(phonePath, localPath));
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
                            DispatcherQueue.TryEnqueue(() =>
                            {
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
                    string winPath = storageFolder.Path + "\\" + System.IO.Path.GetFileName(selectedItem.Title);

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

        private async void ImportPhotos_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hWnd = MainWindow.WindowHandle;
            InitializeWithWindow.Initialize(picker, hWnd);
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".bmp");

            var files = await picker.PickMultipleFilesAsync();
            if (files != null)
            {
                await ImportFilesToAndroid(files);
            }
        }

        private async void ImportFolder_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FolderPicker();
            var hWnd = MainWindow.WindowHandle;
            InitializeWithWindow.Initialize(picker, hWnd);
            picker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                var files = await folder.GetFilesAsync();
                await ImportFilesToAndroid(files);
            }
        }

        private async Task ImportFilesToAndroid(IEnumerable<StorageFile> files)
        {
            if (!files.Any())
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
                    string androidPath = $"/sdcard/Pictures/transfer/{file.Name}";

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

            //await Init();
            // 更新 transfer 组数据
            UpdateTransferGroup(files);

            // 隐藏进度对话框
            progressDialog.Hide();

            ContentDialog importDialog = new ContentDialog
            {
                Title = "Info",
                Content = "Files import successful.",
                PrimaryButtonText = "OK",
            };
            importDialog.XamlRoot = this.Content.XamlRoot;
            await importDialog.ShowAsync();

            RefreshButton_Click(null,null);
        }

        private async void UpdateTransferGroup(IEnumerable<StorageFile> files)
        {
            // 查找或创建 transfer 组
            if (!PhotosInBucket.TryGetValue("transfer", out var transferPhotos))
            {
                transferPhotos = new List<PhotoInfo>();
                PhotosInBucket["transfer"] = transferPhotos;
            }


            List<String> paths = new List<string>();
            foreach (var file in files)
            {
                var photoInfo = new PhotoInfo
                {
                    Title = System.IO.Path.GetFileName(file.Name),
                    Path = $"/sdcard/Pictures/transfer/{file.Name}",
                    LocalPath = file.Path,
                    Date = File.GetCreationTime(file.Path).ToString("yyyy-MM-dd")
                };
                transferPhotos.Add(photoInfo);
                Photos.Add(photoInfo);
                paths.Add(photoInfo.Path);
            }

            // 提醒手机 新增音乐，扫描
            Request request = new Request();
            request.command_id = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            request.module = "picture";
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
        private async void Del_Clicks2(object sender, RoutedEventArgs e)
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
                        await Task.Run(async () =>
                        {
                            List<PhotoInfo> photoInfos = PhotosInBucket[selectedBucket];

                            foreach (var photo in photoInfos)
                            {
                                // 删除设备中的图片
                                string path = photo.Path;
                                string res = adbHelper.cmdExecuteWithAdbExit("shell rm -r " + path);
                                Result result = socketHelper.getResult("picture", "delete");
                            }

                            DispatcherQueue.TryEnqueue(() =>
                            {
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
                            DispatcherQueue.TryEnqueue(() =>
                            {
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


        private async void Del_Clicks(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查当前模块
                if (currentModule.Equals("bucket"))
                {
                    // 删除选中的相册
                    if (BucketGrid.SelectedItem != null)
                    {
                        string selectedBucket = BucketGrid.SelectedItem.ToString();

                        // 询问用户确认删除操作
                        ContentDialog deleteDialog = new ContentDialog
                        {
                            Title = "Delete Album",
                            Content = "Are you sure you want to delete this album?",
                            PrimaryButtonText = "Delete",
                            SecondaryButtonText = "Cancel"
                        };
                        deleteDialog.XamlRoot = this.Content.XamlRoot;
                        ContentDialogResult result = await deleteDialog.ShowAsync();

                        if (result == ContentDialogResult.Primary)
                        {
                            await Task.Run(() =>
                            {
                                List<PhotoInfo> photosToDelete = PhotosInBucket[selectedBucket];

                                foreach (var photo in photosToDelete)
                                {
                                    string path = photo.Path;
                                    adbHelper.cmdExecuteWithAdbExit($"shell rm \"{path}\"");
                                }

                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    // 更新UI，删除相册信息
                                    PhotosInBucket.Remove(selectedBucket);
                                    albumList.Remove(albumList.First(a => a.Name == selectedBucket));
                                    BucketGrid.ItemsSource = albumList;
                                });
                            });

                            ContentDialog successDialog = new ContentDialog
                            {
                                Title = "Success",
                                Content = "Album deleted successfully.",
                                PrimaryButtonText = "OK"
                            };
                            successDialog.XamlRoot = this.Content.XamlRoot;
                            await successDialog.ShowAsync();
                        }
                    }
                    else
                    {
                        ContentDialog errorDialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = "Please select an album to delete.",
                            PrimaryButtonText = "OK"
                        };
                        errorDialog.XamlRoot = this.Content.XamlRoot;
                        await errorDialog.ShowAsync();
                    }
                }
                else if (currentModule.Equals("photo"))
                {
                    // 删除选中的图片
                    if (PhotoGrid.SelectedItem != null)
                    {
                        PhotoInfo selectedPhoto = (PhotoInfo)PhotoGrid.SelectedItem;

                        // 询问用户确认删除操作
                        ContentDialog deleteDialog = new ContentDialog
                        {
                            Title = "Delete Photo",
                            Content = "Are you sure you want to delete this photo?",
                            PrimaryButtonText = "Delete",
                            SecondaryButtonText = "Cancel"
                        };
                        deleteDialog.XamlRoot = this.Content.XamlRoot;
                        ContentDialogResult result = await deleteDialog.ShowAsync();

                        if (result == ContentDialogResult.Primary)
                        {
                            await Task.Run(() =>
                            {
                                string path = selectedPhoto.Path;
                                adbHelper.cmdExecuteWithAdbExit($"shell rm \"{path}\"");

                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    // 更新UI，删除照片信息
                                    var group = groupedData.FirstOrDefault(g => g.Contains(selectedPhoto));
                                    if (group != null)
                                    {
                                        group.Remove(selectedPhoto);
                                        if (group.Count == 0)
                                        {
                                            groupedData.Remove(group);
                                        }
                                    }

                                    PhotoGrid.ItemsSource = null;
                                    PhotoGrid.ItemsSource = groupedData;
                                });
                            });

                            ContentDialog successDialog = new ContentDialog
                            {
                                Title = "Success",
                                Content = "Photo deleted successfully.",
                                PrimaryButtonText = "OK"
                            };
                            successDialog.XamlRoot = this.Content.XamlRoot;
                            await successDialog.ShowAsync();
                        }
                    }
                    else
                    {
                        ContentDialog errorDialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = "Please select a photo to delete.",
                            PrimaryButtonText = "OK"
                        };
                        errorDialog.XamlRoot = this.Content.XamlRoot;
                        await errorDialog.ShowAsync();
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



        // 返回按钮点击事件处理方法
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 切换到目录视图
                BucketGrid.Visibility = Visibility.Visible;
                PhotoGrid.Visibility = Visibility.Collapsed;
                currentModule = "bucket";

                // 更新分页信息
                BucketGrid.ItemsSource = albumList.Count == 0 ? AddAlbumList() : albumList;

                // 更新当前目录显示
                //CurrentDirectoryTextBlock.Text = "Current Bucket: " + currentBucket;
                currentDirectory = "/Pictures";
                CurrentDirectoryTextBox.Text = currentDirectory;

                // 重置时间范围选择为 "All Time"
                ResetTimeRangeToAllTime();
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // 新增方法以重置时间范围到 "All Time"
        private void ResetTimeRangeToAllTime()
        {

            StartDatePicker.Date = null; // All Time 通常表示无界限
            EndDatePicker.Date = null;

            AllTime.IsChecked = true;
            ThisWeek.IsChecked = false;
            ThisMonth.IsChecked = false;
            LastMonth.IsChecked = false;
            Last3Months.IsChecked = false;
            Last6Months.IsChecked = false;
            ThisYear.IsChecked = false;
            CustomRange.IsChecked = false;
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


        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is GroupInfoList groupInfo)
            {
                foreach (var photo in groupInfo)
                {
                    photo.IsSelected = false;

                    // 在 PhotoGrid.SelectedItems 中找到对应的项并取消选中
                    if (PhotoGrid.SelectedItems.Contains(photo))
                    {
                        PhotoGrid.SelectedItems.Remove(photo);
                    }
                }

                //PhotoGrid.SelectedItems.Clear();
            }


            // 更新选择状态文本
            UpdateSelectedFilesInfo();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is GroupInfoList groupInfo)
            {
                foreach (var photo in groupInfo)
                {
                    photo.IsSelected = true;
                    PhotoGrid.SelectedItems.Add(photo);
                }
            }


            // 更新选择状态文本
            UpdateSelectedFilesInfo();
        }

        //选中某项
        private void PhotoGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedItem = e.ClickedItem as PhotoInfo;
            if (clickedItem != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    clickedItem.IsSelected = !clickedItem.IsSelected;
                    if(clickedItem.IsSelected == true)
                    {
                        PhotoGrid.SelectedItems.Add(clickedItem);
                    }
                    else
                    {
                        if (PhotoGrid.SelectedItems.Contains(clickedItem)){
                            PhotoGrid.SelectedItems.Remove(clickedItem);
                        }
                    }
                });

                var parentGroup = groupedData.FirstOrDefault(g => g.Contains(clickedItem));
                if (parentGroup != null)
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        UpdateCheckBoxIndeterminateState(parentGroup.Key);
                    });
                }
            }

            DispatcherQueue.TryEnqueue(() =>
            {
                // 更新选择状态文本
                UpdateSelectedFilesInfo();
            });
        }

        // 方法用于查找具有特定标签的复选框
        private CheckBox FindCheckBoxByTag(DependencyObject parent, string tag)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                // 如果子对象是 CheckBox 并且其标签匹配，则返回该复选框
                if (child is CheckBox checkBox && checkBox.Tag as string == tag)
                {
                    return checkBox;
                }

                // 递归查找子对象的子对象
                CheckBox result = FindCheckBoxByTag(child, tag);
                if (result != null)
                {
                    return result;
                }
            }
            return null;
        }


        private void UpdateCheckBoxIndeterminateState(string groupKey)
        {
            // 寻找与组键关联的CheckBox
            var checkBox =
                    FindCheckBoxByTag(this.Content as DependencyObject, groupKey);
            if (checkBox != null)
            {
                // 获取该组中的所有照片
                var group = groupedData.FirstOrDefault(g => g.Key == groupKey);
                if (group != null)
                {
                    bool allSelected = group.All(photo => photo.IsSelected);
                    bool anySelected = group.Any(photo => photo.IsSelected);

                    if (!allSelected && anySelected)
                    {
                        // 设置为中间状态
                        checkBox.IsChecked = null;
                    }else if (allSelected)
                    {
                        checkBox.IsChecked = true;
                    }
                    else if(!allSelected)
                    {
                        checkBox.IsChecked = false;
                    }
                }
            }
        }


        private void CheckBox_IndeterminateHandler(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is GroupInfoList groupInfo)
            {
                if (groupInfo.CheckAllSelected())
                {
                    // 如果全部选中，取消选中所有
                    groupInfo.SetAllSelected(false);
                    checkBox.IsChecked = false;
                }
            }
        }


        private void Image_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // 显示与图像关联的Flyout
            FlyoutBase.ShowAttachedFlyout((FrameworkElement)sender);
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            // Method intentionally left empty, as specific sort actions are handled by the individual event handlers
        }

        private void SortButton2_Click(object sender, RoutedEventArgs e)
        {
            // Method intentionally left empty, as specific sort actions are handled by the individual event handlers
        }

        //设置排序，单选
        private void SetSingleSelection(MenuFlyoutSubItem parent, ToggleMenuFlyoutItem selectedItem)
        {
            foreach (var item in parent.Items)
            {
                if (item is ToggleMenuFlyoutItem toggleItem)
                {
                    toggleItem.IsChecked = toggleItem == selectedItem;
                }
            }
        }

        public enum SortType
        {
            TimeCreated,
            FileSize
        }
        private SortType currentSortType = SortType.TimeCreated;

        private void SortByTimeCreated_Click(object sender, RoutedEventArgs e)
        {
            if (currentSortType == SortType.TimeCreated)
            {
                UpdateSortType(SortType.FileSize);
            }
            else
            {
                UpdateSortType(SortType.TimeCreated);
            }

            SetSingleSelection(SortBySubItem, (ToggleMenuFlyoutItem)sender);
            currentSortType = SortType.TimeCreated;
            SortCurrentGroupData();
        }

        private void SortByFileSize_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentSortType == SortType.TimeCreated)
                {
                    UpdateSortType(SortType.FileSize);
                }
                else
                {
                    UpdateSortType(SortType.TimeCreated);
                }
                SetSingleSelection(SortBySubItem, (ToggleMenuFlyoutItem)sender);
                currentSortType = SortType.FileSize;
                SortCurrentGroupDataByFileSize();
            }
            catch (Exception ex)
            {

            }
        }

        private void UpdateSortType(SortType newSortType)
        {
            currentSortType = newSortType;
            if (currentSortType == SortType.TimeCreated)
            {
                SortButton2.Content = "Time Created";
                SortCurrentGroupData();
            }
            else if (currentSortType == SortType.FileSize)
            {
                SortButton2.Content = "File Size";
                SortCurrentGroupData();
            }
        }

        private void SortCurrentGroupDataByFileSize()
        {
            if (groupedData != null && groupedData.Any())
            {
                foreach (var group in groupedData)
                {
                    var sortedItems = group.OrderBy(photo=>photo.Size).ToList();

                    group.Clear();
                    foreach (var item in sortedItems)
                    {
                        group.Add(item);
                    }
                }

                var cvs = new CollectionViewSource
                {
                    IsSourceGrouped = true,
                    Source = groupedData
                };
                PhotoGrid.ItemsSource = cvs.View;
            }
        }


        private void SortAscending_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection(OrderSubItem, (ToggleMenuFlyoutItem)sender);
            SortCurrentGroupOrder(true);
            SortAscending2.IsChecked = true;
            SortDescending2.IsChecked = false;
            SortAscending.IsChecked = true;
            SortDescending.IsChecked = false;
        }

        private void SortDescending_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection(OrderSubItem, (ToggleMenuFlyoutItem)sender);
            SortCurrentGroupOrder(false);
            SortDescending2.IsChecked = true;
            SortDescending.IsChecked = true;
            SortAscending2.IsChecked = false;
            SortAscending.IsChecked = false;
        }

        private void SortCurrentGroupData()
        {
            if (groupedData != null && groupedData.Any())
            {
                if (currentSortType == SortType.TimeCreated)
                {
                    foreach (var group in groupedData)
                    {
                        var sortedItems = group.OrderBy(photo => DateTime.Parse(photo.Date)).ToList();

                        group.Clear();
                        foreach (var item in sortedItems)
                        {
                            group.Add(item);
                        }
                    }
                }
                else if (currentSortType == SortType.FileSize)
                {
                    foreach (var group in groupedData)
                    {
                        var sortedItems = group.OrderBy(photo => photo.Size).ToList();

                        group.Clear();
                        foreach (var item in sortedItems)
                        {
                            group.Add(item);
                        }
                    }
                }

                var cvs = new CollectionViewSource
                {
                    IsSourceGrouped = true,
                    Source = groupedData
                };
                PhotoGrid.ItemsSource = cvs.View;
            }
        }

        private void SortCurrentGroupOrder(bool ascending)
        {
            if (groupedData != null && groupedData.Any())
            {
                if (currentSortType == SortType.TimeCreated)
                {
                    var sortedGroups = ascending
                        ? groupedData.OrderBy(group => DateTime.Parse(group.Key)).ToList()
                        : groupedData.OrderByDescending(group => DateTime.Parse(group.Key)).ToList();

                    groupedData.Clear();
                    foreach (var group in sortedGroups)
                    {
                        groupedData.Add(group);
                    }
                }
                else if (currentSortType == SortType.FileSize)
                {
                    var sortedGroups = ascending
                        ? groupedData.OrderBy(group => group.Sum(photo => int.TryParse(photo.Size, out int size) ? size : 0)).ToList()
                        : groupedData.OrderByDescending(group => group.Sum(photo => int.TryParse(photo.Size, out int size) ? size : 0)).ToList();

                    groupedData.Clear();
                    foreach (var group in sortedGroups)
                    {
                        groupedData.Add(group);
                    }
                }

                var cvs = new CollectionViewSource
                {
                    IsSourceGrouped = true,
                    Source = groupedData
                };
                PhotoGrid.ItemsSource = cvs.View;
            }
        }


        private void UpdatePhotoGrid(string directoryName)
        {
            if (currentModule == "photo" && PhotosInBucket.TryGetValue(directoryName, out var photosInDirectory))
            {
                var groupedData = new ObservableCollection<GroupInfoList>();

                var groupedPhotos = photosInDirectory
                    .GroupBy(p => DateTime.Parse(p.Date).ToString("yyyy-MM-dd"))
                    .OrderByDescending(g => g.Key);

                foreach (var group in groupedPhotos)
                {
                    var groupInfoList = new GroupInfoList { Key = group.Key };
                    groupInfoList.AddRange(group);
                    groupedData.Add(groupInfoList);
                }

                var cvs = new CollectionViewSource
                {
                    IsSourceGrouped = true,
                    Source = groupedData
                };

                PhotoGrid.ItemsSource = cvs.View;
            }
        }

        private void SetSingleSelection(ToggleMenuFlyoutItem selectedItem)
        {
            // Uncheck all ToggleMenuFlyoutItems
            AllTime.IsChecked = false;
            ThisWeek.IsChecked = false;
            ThisMonth.IsChecked = false;
            LastMonth.IsChecked = false;
            Last3Months.IsChecked = false;
            Last6Months.IsChecked = false;
            ThisYear.IsChecked = false;

            // Check the selected item
            selectedItem.IsChecked = true;
        }

        private void FilterByAllTime_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            StartDatePicker.Date = null; // All Time 通常表示无界限
            EndDatePicker.Date = null;
            FilterPhotosByDateRange(DateTime.MinValue, DateTime.MaxValue);
        }

        // 更新其他方法，以同步日期选择器的值
        private void FilterByThisWeek_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7).AddSeconds(-1);
            StartDatePicker.Date = startOfWeek;
            EndDatePicker.Date = endOfWeek;
            FilterPhotosByDateRange(startOfWeek, endOfWeek);
        }

        private void FilterByThisMonth_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);
            StartDatePicker.Date = startOfMonth;
            EndDatePicker.Date = endOfMonth;
            FilterPhotosByDateRange(startOfMonth, endOfMonth);
        }

        private void FilterByLastMonth_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfLastMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
            var endOfLastMonth = startOfLastMonth.AddMonths(1).AddSeconds(-1);
            StartDatePicker.Date = startOfLastMonth;
            EndDatePicker.Date = endOfLastMonth;
            FilterPhotosByDateRange(startOfLastMonth, endOfLastMonth);
        }

        private void FilterByLast3Months_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfLast3Months = DateTime.Today.AddMonths(-3);
            var endOfLast3Months = DateTime.Today;
            StartDatePicker.Date = startOfLast3Months;
            EndDatePicker.Date = endOfLast3Months;
            FilterPhotosByDateRange(startOfLast3Months, endOfLast3Months);
        }

        private void FilterByLast6Months_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfLast6Months = DateTime.Today.AddMonths(-6);
            var endOfLast6Months = DateTime.Today;
            StartDatePicker.Date = startOfLast6Months;
            EndDatePicker.Date = endOfLast6Months;
            FilterPhotosByDateRange(startOfLast6Months, endOfLast6Months);
        }

        private void FilterByThisYear_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfYear = new DateTime(DateTime.Today.Year, 1, 1);
            var endOfYear = startOfYear.AddYears(1).AddSeconds(-1);
            StartDatePicker.Date = startOfYear;
            EndDatePicker.Date = endOfYear;
            FilterPhotosByDateRange(startOfYear, endOfYear);
        }

        private void FilterByCustomRange_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            if (StartDatePicker.Date.HasValue && EndDatePicker.Date.HasValue)
            {
                DateTime startDate = StartDatePicker.Date.Value.DateTime;
                DateTime endDate = EndDatePicker.Date.Value.DateTime;
                FilterPhotosByDateRange(startDate, endDate);
            }
        }


        private void FilterPhotosByDateRange(DateTime startDate, DateTime endDate)
        {
            var filteredGroups = new ObservableCollection<GroupInfoList>();

            foreach (var group in groupedData)
            {
                var filteredItems = group.Where(photo => DateTime.Parse(photo.Date) >= startDate && DateTime.Parse(photo.Date) <= endDate).ToList();

                if (filteredItems.Any())
                {
                    var newGroup = new GroupInfoList { Key = group.Key };
                    newGroup.AddRange(filteredItems);
                    filteredGroups.Add(newGroup);
                }
            }

            var cvs = new CollectionViewSource
            {
                IsSourceGrouped = true,
                Source = filteredGroups
            };

            PhotoGrid.ItemsSource = cvs.View;
        }

        private void DatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            FilterPhotosBySelectedDateRange();
        }

        private void FilterPhotosBySelectedDateRange()
        {
            if (StartDatePicker.Date.HasValue && EndDatePicker.Date.HasValue)
            {
                DateTime startDate = StartDatePicker.Date.Value.DateTime;
                DateTime endDate = EndDatePicker.Date.Value.DateTime;

                var filteredGroups = new ObservableCollection<GroupInfoList>();

                foreach (var group in groupedData)
                {
                    var filteredItems = group.Where(photo => DateTime.Parse(photo.Date) >= startDate && DateTime.Parse(photo.Date) <= endDate).ToList();

                    if (filteredItems.Any())
                    {
                        var newGroup = new GroupInfoList { Key = group.Key };
                        newGroup.AddRange(filteredItems);
                        filteredGroups.Add(newGroup);
                    }
                }

                var cvs = new CollectionViewSource
                {
                    IsSourceGrouped = true,
                    Source = filteredGroups
                };

                PhotoGrid.ItemsSource = cvs.View;
            }
        }

        //导出所有图片
        private void ExportAllPhotos_Click(object sender, RoutedEventArgs e)
        {
            // 处理导出所有图片的逻辑
            ExportPhotos(allPhotos: true);
        }

        private void ExportSelectedPhotos_Click(object sender, RoutedEventArgs e)
        {
            // 处理导出选中图片的逻辑
            ExportPhotos(allPhotos: false);
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

        private async void ExportPhotos(bool allPhotos)
        {
            try
            {
                List<PhotoInfo> photosToExport;

                if (allPhotos)
                {
                    // 导出所有图片的逻辑
                    photosToExport = Photos;
                }
                else
                {
                    // 导出选中图片的逻辑
                    photosToExport = Photos.Where(photo => photo.IsSelected).ToList();

                    // 如果没有选择照片，提示用户
                    if (photosToExport.Count == 0)
                    {
                        await ShowMessageDialog("No photo selected", "Please select at least one photo item to export.");
                        return;
                    }
                }

                var filePicker = new FolderPicker();
                var hWnd = MainWindow.WindowHandle;
                InitializeWithWindow.Initialize(filePicker, hWnd);
                filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                filePicker.FileTypeFilter.Add("*");
                Windows.Storage.StorageFolder storageFolder = await filePicker.PickSingleFolderAsync();

                if (storageFolder != null)
                {
                    // 创建并显示ContentDialog
                    var progressDialog = new ContentDialog
                    {
                        Title = "Exporting Photos",
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
                        int totalPhotos = photosToExport.Count;
                        int exportedPhotos = 0;

                        foreach (var photo in photosToExport)
                        {
                            string path = photo.Path;
                            string localPath = storageFolder.Path;

                            adbHelper.saveFromPath(path, localPath);

                            exportedPhotos++;
                            double progress = (double)exportedPhotos / totalPhotos * 100;

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
                        Title = "Info",
                        Content = "Photos export successful.",
                        PrimaryButtonText = "OK",
                    };
                    exportDialog.XamlRoot = this.Content.XamlRoot;
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

        private async void DeleteSelectedPhotosButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 检查当前模块
                if (currentModule.Equals("photo"))
                {
                    var selectedPhotos = PhotoGrid.SelectedItems.Cast<PhotoInfo>().ToList();
                    if (selectedPhotos.Any())
                    {
                        // 询问用户确认删除操作
                        ContentDialog deleteDialog = new ContentDialog
                        {
                            Title = "Delete Photos",
                            Content = "Are you sure you want to delete the selected photos?",
                            PrimaryButtonText = "Delete",
                            SecondaryButtonText = "Cancel"
                        };
                        deleteDialog.XamlRoot = this.Content.XamlRoot;
                        ContentDialogResult result = await deleteDialog.ShowAsync();

                        if (result == ContentDialogResult.Primary)
                        {
                            await Task.Run(() =>
                            {
                                foreach (var photo in selectedPhotos)
                                {
                                    string path = photo.Path;
                                    adbHelper.cmdExecuteWithAdbExit("shell rm " + path);

                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        var group = groupedData.FirstOrDefault(g => g.Contains(photo));
                                        if (group != null)
                                        {
                                            group.Remove(photo);
                                            if (group.Count == 0)
                                            {
                                                groupedData.Remove(group);
                                            }
                                        }

                                        // 更新相应的 AlbumInfo
                                        var album = albumList.FirstOrDefault(a => a.Name == photo.Bucket);
                                        if (album != null)
                                        {
                                            album.PhotoCount--;
                                            if (album.PhotoCount == 0)
                                            {
                                                albumList.Remove(album);
                                            }
                                        }
                                    });

                                }
                            });

                            Request request = new Request();
                            request.command_id = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                            request.module = "picture";
                            request.operation = "delete";
                            request.info = new Data
                            {
                                paths = selectedPhotos.Select(file => file.Path).ToList<string>(),
                            };

                            string requestStr = JsonConvert.SerializeObject(request);
                            SocketHelper helper = new SocketHelper();
                            Result result2 = new Result();
                            await Task.Run(() =>
                            {
                                result2 = helper.ExecuteOp(requestStr);
                            });


                            ContentDialog successDialog = new ContentDialog
                            {
                                Title = "Success",
                                Content = "Selected photos deleted successfully.",
                                PrimaryButtonText = "OK"
                            };
                            successDialog.XamlRoot = this.Content.XamlRoot;
                            await successDialog.ShowAsync();


                            var cvs = new CollectionViewSource
                            {
                                IsSourceGrouped = true,
                                Source = groupedData
                            };
                            PhotoGrid.ItemsSource = cvs.View;
                        }
                    }
                    else
                    {
                        ContentDialog errorDialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = "Please select photos to delete.",
                            PrimaryButtonText = "OK"
                        };
                        errorDialog.XamlRoot = this.Content.XamlRoot;
                        await errorDialog.ShowAsync();
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

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    // 显示加载进度条
                    BucketGrid.Visibility = Visibility.Collapsed;
                    PhotoGrid.Visibility = Visibility.Collapsed;
                    progressRing.Visibility = Visibility.Visible;
                });



                // 更新UI
                if (currentModule == "bucket")
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            List<AlbumInfo> albumInfos = albumList;
                            albumInfos = AddAlbumList();
                            BucketGrid.ItemsSource = albumInfos;
                            BucketGrid.Visibility = Visibility.Visible;
                        }
                        catch (Exception ex)
                        {
                            show_error("Error updating UI: " + ex.Message);
                            logHelper.Info(logger, ex.ToString());
                        }
                    });

                }
                else if (currentModule == "photo")
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            UpdatePhotoGrid(currentBucket);
                            PhotoGrid.Visibility = Visibility.Visible;
                        }
                        catch (Exception ex)
                        {
                            show_error("Error updating UI: " + ex.Message);
                            logHelper.Info(logger, ex.ToString());
                        }

                    });
                }

                // 隐藏加载进度条
                DispatcherQueue.TryEnqueue(() =>
                {
                    progressRing.Visibility = Visibility.Collapsed;
                });
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

    }

    public class AlbumInfo
    {
        public string Name { get; set; }
        public string FirstPhotoPath { get; set; }
        public int PhotoCount { get; set; }
    }

    public class GroupInfoList : List<PhotoInfo>, INotifyPropertyChanged
    {
        public string Key { get; set; }

        public List<PhotoInfo> PhotoInfos { get; set; }
        public int PhotoCount => this.Count; // 新增属性，返回照片总数

        public int SelectedCount => this.Count(item => item.IsSelected);
        public event PropertyChangedEventHandler PropertyChanged;


        public GroupInfoList()
        {
            PhotoInfos = new List<PhotoInfo>();
        }
        public void AddRange(IEnumerable<PhotoInfo> collection)
        {
            foreach (var item in collection)
            {
                item.PropertyChanged += Item_PropertyChanged;
                base.Add(item);
            }
            OnPropertyChanged(nameof(PhotoCount));
            OnPropertyChanged(nameof(SelectedCount));
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PhotoInfo.IsSelected))
            {
                OnPropertyChanged(nameof(SelectedCount));
            }
        }

        public void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public bool CheckAllSelected()
        {
            return this.All(photo => photo.IsSelected);
        }

        public void SetAllSelected(bool selected)
        {
            foreach (var photo in this)
            {
                photo.IsSelected = selected;
            }
        }
    }
}
