// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PhotoPage : Page
    {
        SocketHelper socketHelper = new SocketHelper();
        AdbHelper adbHelper = new AdbHelper();

        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        public List<PhotoInfo> Photos = new List<PhotoInfo>();
        public List<PhotoInfo> PhotosSorted = new List<PhotoInfo>();
        public Dictionary<string, List<PhotoInfo>> PhotosInBucket { get; set; }
        HashSet<String> buckets = new HashSet<string>();
        string currentBucket = "";
        //当前操作模块，是目录还是图片
        //photo photoList bucket
        string currentModule = "bucket";
        List<PhotoInfo> currentPhotos = new List<PhotoInfo>();

        int bucketSize = 36;
        int pageSize = 36;
        int currentPage = 1;
        int totalPages = 0;


        public PhotoPage()
        {
            this.InitializeComponent();

            this.Loaded += LoadingPage_Loaded;
        }

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

        private void InitPage()
        {
            Photos = MainWindow.Photos;
            buckets = MainWindow.buckets;
            PhotosInBucket = MainWindow.PhotosInBucket;
            PhotosSorted = MainWindow.PhotosSorted;

            BucketGrid.ItemsSource = buckets.Take(bucketSize).ToList();
            totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // 总页数
            pageNum.Text = currentPage + " / " + totalPages;


            progressRing.Visibility = Visibility.Collapsed;
            BucketGrid.Visibility = Visibility.Visible;
        }

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
                            //读数据
                            JArray jArray = JArray.Parse(str);
                            // use LINQ query to get DisplayName and MobileNum
                            var resultArray = (from item in jArray
                                               select new
                                               {
                                                   Bucket = item["bucket"]?.ToString(),
                                                   Date = item["date"]?.ToString(),
                                                   Path = item["path"]?.ToString()
                                               })
                                            .ToArray();

                            //文件夹数量
                            foreach (var item in resultArray)
                            {
                                if (item.Bucket == null)
                                {
                                    buckets.Add("null");
                                    continue;
                                }
                                buckets.Add(item.Bucket);
                            }

                            //渲染进map
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
                                    // map中有，直接存入
                                    photos.Add(photoInfo);
                                }
                                else
                                {
                                    PhotosInBucket.Add(photoInfo.Bucket, new List<PhotoInfo> {
                        photoInfo
                    });
                                }
                            }

                            //Photos.OrderByDescending(item => item
                            //Todo 要在Photos找是哪张照片，这里不能排序
                            //Photos = Photos.OrderByDescending(item => item.Date).ToList();

                            DispatcherQueue.TryEnqueue(() => {
                                BucketGrid.ItemsSource = buckets.Take(bucketSize).ToList();
                                totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // 总页数
                                pageNum.Text = currentPage + " / " + totalPages;

                                progressRing.Visibility = Visibility.Collapsed;
                                BucketGrid.Visibility = Visibility.Visible;
                            });

                            PhotosSorted = new List<PhotoInfo>(Photos);
                            PhotosSorted = PhotosSorted.OrderByDescending(p => p.Date).ToList();

                            //缓存
                            MainWindow.buckets = buckets;
                            MainWindow.Photos = Photos;
                            MainWindow.PhotosSorted = PhotosSorted;
                            MainWindow.PhotosInBucket = PhotosInBucket;
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
                        // 修改不成功
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

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
        }

        //前一页
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (currentPage == 1)
                {
                    //在第一页
                }
                else
                {
                    if (currentModule.Equals("bucket"))
                    {
                        List<string> list = PreviousPage(currentModule).Select(s => (string)s).ToList();

                        BucketGrid.ItemsSource = list;
                    }
                    else if(currentModule.Equals("photo"))
                    {
                        List<PhotoInfo> list = PreviousPage(currentModule).Select(s => (PhotoInfo)s).ToList();
                        setPhotoImgPath(list);
                        PhotoGrid.ItemsSource = list;
                    }
                    else
                    {
                        List<PhotoInfo> list = PreviousPage(currentModule).Select(s => (PhotoInfo)s).ToList();
                        setPhotoImgPath(list);
                        PhotoListGrid.ItemsSource = list;
                    }
                    pageNum.Text = currentPage + " / " + totalPages;
                }
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }

        }

        //后一页
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentPage == totalPages)
                {
                    //是最后一页
                }
                else
                {
                    if (currentModule.Equals("bucket"))
                    {
                        List<string> list = NextPage(currentModule).Select(s => (string)s).ToList();

                        BucketGrid.ItemsSource = list;
                    }
                    else if (currentModule.Equals("photo"))
                    {
                        List<PhotoInfo> list = NextPage(currentModule).Select(s => (PhotoInfo)s).ToList();
                        setPhotoImgPath(list);
                        PhotoGrid.ItemsSource = list;
                    }
                    else
                    {
                        List<PhotoInfo> list = NextPage(currentModule).Select(s => (PhotoInfo)s).ToList();
                        setPhotoImgPath(list);
                        PhotoListGrid.ItemsSource = list;
                    }
                    pageNum.Text = currentPage + " / " + totalPages;
                }
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // 获取当前目录页的元素
        List<Object> GetCurrentPageItems(string module)
        {
            try
            {
                if (module.Equals("bucket"))
                {
                    //操作目录
                    int startIndex = (currentPage - 1) * bucketSize;
                    int endIndex = Math.Min(startIndex + bucketSize, buckets.Count);
                    return buckets.ToList().GetRange(startIndex, endIndex - startIndex)
                        .Select(s => (object)s).ToList();
                }
                else if (module.Equals("photo"))
                {
                    // 操作图片
                    int startIndex = (currentPage - 1) * pageSize;
                    int endIndex = Math.Min(startIndex + pageSize, PhotosInBucket[currentBucket].Count);
                    List<PhotoInfo> photoInfos = PhotosInBucket[currentBucket].ToList().GetRange(startIndex, endIndex - startIndex);
                    currentPhotos = photoInfos;

                    return photoInfos.Select(s => (object)s).ToList();
                }
                else
                {
                    // 操作图片List
                    int startIndex = (currentPage - 1) * pageSize;
                    int endIndex = Math.Min(startIndex + pageSize, PhotosSorted.Count);
                    List<PhotoInfo> photoInfos = PhotosSorted.ToList().GetRange(startIndex, endIndex - startIndex);
                    currentPhotos = photoInfos;

                    return photoInfos.Select(s => (object)s).ToList();
                }
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
            
        }

        // 前一页
        List<object> PreviousPage(string module)
        {
            ////返回图片前一页
            //if (currentPage > 1)
            //{
            //    currentPage--;
            //    return GetCurrentPageItems(module);
            //    // 执行前一页的操作
            //}

            if (module.Equals("bucket"))
            {
                //返回目录下一页
                int totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // 总页数
                if (currentPage > 1)
                {
                    currentPage--;
                    return GetCurrentPageItems(module);
                    // 执行后一页的操作
                }
                return null;
            }
            else if (module.Equals("photo"))
            {
                //返回图片下一页
                int totalPages = (PhotosInBucket[currentBucket].Count + bucketSize - 1) / bucketSize; // 总页数
                if (currentPage > 1)
                {
                    currentPage--;
                    return GetCurrentPageItems(module);
                    // 执行后一页的操作
                }
                return null;
            }
            else
            {
                //返回图片下一页
                int totalPages = (Photos.Count + pageSize - 1) / pageSize; // 总页数
                if (currentPage > 1)
                {
                    currentPage--;
                    return GetCurrentPageItems(module);
                    // 执行后一页的操作
                }
                return null;
            }


            return null;
        }

        // 后一页
        List<object> NextPage(string module)
        {
            try
            {

                if (module.Equals("bucket"))
                {
                    //返回目录下一页
                    int totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // 总页数
                    if (currentPage < totalPages)
                    {
                        currentPage++;
                        return GetCurrentPageItems(module);
                        // 执行后一页的操作
                    }
                    return null;
                }
                else if (module.Equals("photo"))
                {
                    //返回图片下一页
                    int totalPages = (PhotosInBucket[currentBucket].Count + bucketSize - 1) / bucketSize; // 总页数
                    if (currentPage < totalPages)
                    {
                        currentPage++;
                        return GetCurrentPageItems(module);
                        // 执行后一页的操作
                    }
                    return null;
                }
                else
                {
                    //返回图片下一页
                    int totalPages = (Photos.Count + pageSize - 1) / pageSize; // 总页数
                    if (currentPage < totalPages)
                    {
                        currentPage++;
                        return GetCurrentPageItems(module);
                        // 执行后一页的操作
                    }
                    return null;
                }
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }


        }

        //双击目录
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
                        // 拿到目录名称
                        string bucket = textBlock.Text;
                        currentBucket = bucket;
                        List<PhotoInfo> photos = new List<PhotoInfo>();
                        if (PhotosInBucket.TryGetValue(bucket, out photos))
                        {
                            //按照时间排序
                            photos = photos.OrderByDescending(item => item.Date).ToList();
                            PhotosInBucket[bucket] = photos;

                            //拿到图片了
                            currentModule = "photo";

                            //重置变量
                            currentPage = 1;
                            totalPages = (photos.Count + pageSize - 1) / pageSize; // 总页数
                            pageNum.Text = currentPage + " / " + totalPages;

                            //更改页面
                            BucketGrid.Visibility = Visibility.Collapsed;
                            PhotoGrid.Visibility = Visibility.Visible;

                            //查看Photo在原来list中的位置，同时渲染拉取缩率图，显示缩率图
                            setPhotoImgPath(photos.Take(pageSize).ToList());

                            //设置数据源
                            currentPhotos = photos.Take(pageSize).ToList();
                            PhotoGrid.ItemsSource = photos.Take(pageSize).ToList();
                        }
                        else
                        {
                            //找不到图片
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

        //设置对应图片的缩率图
        public void setPhotoImgPath(List<PhotoInfo> photos)
        {
            try
            {
                foreach (var photo in photos)
                {
                    int index = Photos.FindIndex(p => p.Title == photo.Title);
                    //photo.LocalPath = "/Images/pic/" + index + ".jpg";
                    //拉到Images目录下
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

        //同步选中的文件夹
        private async void SyncFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                //没有选中
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

        //保存单张图片
        private async void Save_Clicks(object sender, RoutedEventArgs e)
        {
            try
            {
                PhotoInfo selectedItem = new PhotoInfo();
                if (currentModule.Equals("photo"))
                    selectedItem = (PhotoInfo)PhotoGrid.SelectedItem;
                else if (currentModule.Equals("photoList"))
                    selectedItem = (PhotoInfo)PhotoListGrid.SelectedItem;
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

                if (storageFolder!=null)
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

        //设置为壁纸
        private async void SetPhotoToWall_Clicks(object sender, RoutedEventArgs e)
        {
            try
            {
                PhotoInfo selectedItem = new PhotoInfo();
                if (currentModule.Equals("photo"))
                    selectedItem = (PhotoInfo)PhotoGrid.SelectedItem;
                else if (currentModule.Equals("photoList"))
                    selectedItem = (PhotoInfo)PhotoListGrid.SelectedItem;
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

        //删除操作
        private async void Del_Clicks(object sender, RoutedEventArgs e)
        {
            try
            {
                PhotoInfo selectedItem = new PhotoInfo();
                if (currentModule.Equals("photo"))
                    selectedItem = (PhotoInfo)PhotoGrid.SelectedItem;
                else if (currentModule.Equals("photoList"))
                    selectedItem = (PhotoInfo)PhotoListGrid.SelectedItem;
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


                //询问
                ContentDialog aa = new ContentDialog
                {
                    Title = "Info",
                    Content = "Are you sure to delete it ?",
                    PrimaryButtonText = "OK",
                    SecondaryButtonText = "Cancel",
                };
                aa.XamlRoot = this.Content.XamlRoot;
                ContentDialogResult rr = await aa.ShowAsync();
                if (rr == ContentDialogResult.Primary)
                {
                    //删除操作
                    //1. 删除目录
                    if (!string.IsNullOrEmpty(selectedBucket) && currentModule.Equals("bucket"))
                    {
                        await Task.Run(async () => {
                            List<PhotoInfo> photoInfos = PhotosInBucket[selectedBucket];

                            foreach (var photo in photoInfos)
                            {
                                //图片路径
                                string path = photo.Path;
                                string res = adbHelper.cmdExecuteWithAdbExit("shell rm -r " + path);
                                //string op = "adb shell am broadcast " +
                                //"-a android.intent.action.MEDIA_SCANNER_SCAN_FILE -d \'" +
                                //"file://" + path + "\'";
                                //res = adbHelper.cmdExecuteWithAdbExit(op);
                                Result result = socketHelper.getResult("picture", "delete");
                            }

                            DispatcherQueue.TryEnqueue(() => {
                                //删除目录
                                buckets.Remove(selectedBucket);
                                PhotosInBucket.Remove(selectedBucket);
                                MainWindow.PhotosInBucket = PhotosInBucket;
                                MainWindow.buckets = buckets;

                                BucketGrid.ItemsSource = buckets.Take(bucketSize).ToList();
                                totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // 总页数
                                currentPage = 1;
                                pageNum.Text = currentPage + " / " + totalPages;
                            });
                        });
                    }
                    //删除单个图片
                    else
                    {
                        string path = selectedItem.Path;
                        string res = adbHelper.cmdExecuteWithAdbExit("shell rm " + path);
                        //string op = "adb shell am broadcast " +
                        //    "-a android.intent.action.MEDIA_SCANNER_SCAN_FILE -d \'" +
                        //    "file://" + path+"\'";
                        //res = adbHelper.cmdExecuteWithAdbExit(op);

                        Result result = new Result();
                        await Task.Run(() =>
                        {
                            result = socketHelper.getResult("picture", "delete");
                        });

                        if (result.status.Equals("00"))
                        {
                            //删除文件
                            if (currentModule.Equals("photo"))
                            {
                                List<PhotoInfo> photoInfos = PhotosInBucket[currentBucket];
                                photoInfos.Remove(selectedItem);
                                currentPhotos.Remove(selectedItem);
                                PhotosInBucket[currentBucket] = photoInfos;
                                PhotoGrid.ItemsSource = currentPhotos;
                                totalPages = (PhotosInBucket[currentBucket].Count + pageSize - 1) / pageSize; // 总页数
                                pageNum.Text = currentPage + " / " + totalPages;

                                MainWindow.PhotosInBucket = PhotosInBucket;
                            }
                            else
                            {
                                currentPhotos.Remove(selectedItem);
                                PhotosSorted.Remove(selectedItem);
                                //删除对应的照片
                                PhotoListGrid.ItemsSource = PhotosSorted.Take(pageSize).ToList();
                                //查看Photo在原来list中的位置，同时渲染拉取缩率图，显示缩率图
                                setPhotoImgPath(PhotosSorted.Take(pageSize).ToList());
                                totalPages = (PhotosSorted.Count + pageSize - 1) / pageSize; // 总页数
                                currentPage = 1;
                                pageNum.Text = currentPage + " / " + totalPages;

                                MainWindow.PhotosSorted = PhotosSorted;
                            }
                            
                        }
                        else if (result.status.Equals("101"))
                        {
                            // 不成功
                            DispatcherQueue.TryEnqueue(() => {
                                show_error(" No permissions granted.");
                            });
                        }
                        else
                        {
                            // 修改不成功
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

        //返回按钮
        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                BucketGrid.Visibility = Visibility.Visible;
                PhotoGrid.Visibility = Visibility.Collapsed;
                currentModule = "bucket";

                BucketGrid.ItemsSource = buckets.Take(bucketSize).ToList();
                totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // 总页数
                pageNum.Text = currentPage + " / " + totalPages;
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }


        }

        private async void show_info(string title,string content)
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

        private void ListButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PhotoListGrid.Visibility = Visibility.Visible;
                progressRing.Visibility = Visibility.Collapsed;
                BucketGrid.Visibility = Visibility.Collapsed;
                PhotoGrid.Visibility = Visibility.Collapsed;
                PhotoListGrid.ItemsSource = PhotosSorted.Take(pageSize).ToList();
                //查看Photo在原来list中的位置，同时渲染拉取缩率图，显示缩率图
                setPhotoImgPath(PhotosSorted.Take(pageSize).ToList());
                currentModule = "photoList";
                totalPages = (Photos.Count + pageSize - 1) / pageSize; // 总页数
                currentPage = 1;
                pageNum.Text = currentPage + " / " + totalPages;

                ListButton.FontWeight = FontWeights.Bold;
                DirectoryButton.FontWeight = FontWeights.Thin;
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
            
        }

        private void DirectoryButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                PhotoListGrid.Visibility = Visibility.Collapsed;
                progressRing.Visibility = Visibility.Collapsed;
                BucketGrid.Visibility = Visibility.Visible;
                PhotoGrid.Visibility = Visibility.Collapsed;
                currentModule = "bucket";

                totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // 总页数
                currentPage = 1;
                pageNum.Text = currentPage + " / " + totalPages;

                ListButton.FontWeight = FontWeights.Thin;
                DirectoryButton.FontWeight = FontWeights.Bold;
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }


        }

        //双击图片
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
                        // 拿到照片名称
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
