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
        //��ǰ����ģ�飬��Ŀ¼����ͼƬ
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
                        // ���г�ʼ������������������ݲ���ֵ�� calls
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
            totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // ��ҳ��
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
                            //������
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

                            //�ļ�������
                            foreach (var item in resultArray)
                            {
                                if (item.Bucket == null)
                                {
                                    buckets.Add("null");
                                    continue;
                                }
                                buckets.Add(item.Bucket);
                            }

                            //��Ⱦ��map
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
                                    // map���У�ֱ�Ӵ���
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
                            //Todo Ҫ��Photos����������Ƭ�����ﲻ������
                            //Photos = Photos.OrderByDescending(item => item.Date).ToList();

                            DispatcherQueue.TryEnqueue(() => {
                                BucketGrid.ItemsSource = buckets.Take(bucketSize).ToList();
                                totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // ��ҳ��
                                pageNum.Text = currentPage + " / " + totalPages;

                                progressRing.Visibility = Visibility.Collapsed;
                                BucketGrid.Visibility = Visibility.Visible;
                            });

                            PhotosSorted = new List<PhotoInfo>(Photos);
                            PhotosSorted = PhotosSorted.OrderByDescending(p => p.Date).ToList();

                            //����
                            MainWindow.buckets = buckets;
                            MainWindow.Photos = Photos;
                            MainWindow.PhotosSorted = PhotosSorted;
                            MainWindow.PhotosInBucket = PhotosInBucket;
                        }
                    }
                    else if (result.status.Equals("101"))
                    {
                        // ���ɹ�
                        DispatcherQueue.TryEnqueue(() => {
                            permission.Hide();
                            show_error(" No permissions granted.");

                            MainWindow.Permissions[4] = '0';
                        });
                    }
                    else
                    {
                        // �޸Ĳ��ɹ�
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

        //ǰһҳ
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (currentPage == 1)
                {
                    //�ڵ�һҳ
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

        //��һҳ
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                if (currentPage == totalPages)
                {
                    //�����һҳ
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

        // ��ȡ��ǰĿ¼ҳ��Ԫ��
        List<Object> GetCurrentPageItems(string module)
        {
            try
            {
                if (module.Equals("bucket"))
                {
                    //����Ŀ¼
                    int startIndex = (currentPage - 1) * bucketSize;
                    int endIndex = Math.Min(startIndex + bucketSize, buckets.Count);
                    return buckets.ToList().GetRange(startIndex, endIndex - startIndex)
                        .Select(s => (object)s).ToList();
                }
                else if (module.Equals("photo"))
                {
                    // ����ͼƬ
                    int startIndex = (currentPage - 1) * pageSize;
                    int endIndex = Math.Min(startIndex + pageSize, PhotosInBucket[currentBucket].Count);
                    List<PhotoInfo> photoInfos = PhotosInBucket[currentBucket].ToList().GetRange(startIndex, endIndex - startIndex);
                    currentPhotos = photoInfos;

                    return photoInfos.Select(s => (object)s).ToList();
                }
                else
                {
                    // ����ͼƬList
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

        // ǰһҳ
        List<object> PreviousPage(string module)
        {
            ////����ͼƬǰһҳ
            //if (currentPage > 1)
            //{
            //    currentPage--;
            //    return GetCurrentPageItems(module);
            //    // ִ��ǰһҳ�Ĳ���
            //}

            if (module.Equals("bucket"))
            {
                //����Ŀ¼��һҳ
                int totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // ��ҳ��
                if (currentPage > 1)
                {
                    currentPage--;
                    return GetCurrentPageItems(module);
                    // ִ�к�һҳ�Ĳ���
                }
                return null;
            }
            else if (module.Equals("photo"))
            {
                //����ͼƬ��һҳ
                int totalPages = (PhotosInBucket[currentBucket].Count + bucketSize - 1) / bucketSize; // ��ҳ��
                if (currentPage > 1)
                {
                    currentPage--;
                    return GetCurrentPageItems(module);
                    // ִ�к�һҳ�Ĳ���
                }
                return null;
            }
            else
            {
                //����ͼƬ��һҳ
                int totalPages = (Photos.Count + pageSize - 1) / pageSize; // ��ҳ��
                if (currentPage > 1)
                {
                    currentPage--;
                    return GetCurrentPageItems(module);
                    // ִ�к�һҳ�Ĳ���
                }
                return null;
            }


            return null;
        }

        // ��һҳ
        List<object> NextPage(string module)
        {
            try
            {

                if (module.Equals("bucket"))
                {
                    //����Ŀ¼��һҳ
                    int totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // ��ҳ��
                    if (currentPage < totalPages)
                    {
                        currentPage++;
                        return GetCurrentPageItems(module);
                        // ִ�к�һҳ�Ĳ���
                    }
                    return null;
                }
                else if (module.Equals("photo"))
                {
                    //����ͼƬ��һҳ
                    int totalPages = (PhotosInBucket[currentBucket].Count + bucketSize - 1) / bucketSize; // ��ҳ��
                    if (currentPage < totalPages)
                    {
                        currentPage++;
                        return GetCurrentPageItems(module);
                        // ִ�к�һҳ�Ĳ���
                    }
                    return null;
                }
                else
                {
                    //����ͼƬ��һҳ
                    int totalPages = (Photos.Count + pageSize - 1) / pageSize; // ��ҳ��
                    if (currentPage < totalPages)
                    {
                        currentPage++;
                        return GetCurrentPageItems(module);
                        // ִ�к�һҳ�Ĳ���
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

        //˫��Ŀ¼
        private void StackPanel_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            try
            {
                StackPanel stackPanel = sender as StackPanel;
                foreach (var child in stackPanel.Children)
                {
                    // �����Ԫ���Ƿ�Ϊ TextBlock ����
                    if (child is TextBlock textBlock)
                    {
                        // �õ�Ŀ¼����
                        string bucket = textBlock.Text;
                        currentBucket = bucket;
                        List<PhotoInfo> photos = new List<PhotoInfo>();
                        if (PhotosInBucket.TryGetValue(bucket, out photos))
                        {
                            //����ʱ������
                            photos = photos.OrderByDescending(item => item.Date).ToList();
                            PhotosInBucket[bucket] = photos;

                            //�õ�ͼƬ��
                            currentModule = "photo";

                            //���ñ���
                            currentPage = 1;
                            totalPages = (photos.Count + pageSize - 1) / pageSize; // ��ҳ��
                            pageNum.Text = currentPage + " / " + totalPages;

                            //����ҳ��
                            BucketGrid.Visibility = Visibility.Collapsed;
                            PhotoGrid.Visibility = Visibility.Visible;

                            //�鿴Photo��ԭ��list�е�λ�ã�ͬʱ��Ⱦ��ȡ����ͼ����ʾ����ͼ
                            setPhotoImgPath(photos.Take(pageSize).ToList());

                            //��������Դ
                            currentPhotos = photos.Take(pageSize).ToList();
                            PhotoGrid.ItemsSource = photos.Take(pageSize).ToList();
                        }
                        else
                        {
                            //�Ҳ���ͼƬ
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

        //���ö�ӦͼƬ������ͼ
        public void setPhotoImgPath(List<PhotoInfo> photos)
        {
            try
            {
                foreach (var photo in photos)
                {
                    int index = Photos.FindIndex(p => p.Title == photo.Title);
                    //photo.LocalPath = "/Images/pic/" + index + ".jpg";
                    //����ImagesĿ¼��
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

        //ͬ��ѡ�е��ļ���
        private async void SyncFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                //û��ѡ��
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

        //���浥��ͼƬ
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

        //����Ϊ��ֽ
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

        //ɾ������
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


                //ѯ��
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
                    //ɾ������
                    //1. ɾ��Ŀ¼
                    if (!string.IsNullOrEmpty(selectedBucket) && currentModule.Equals("bucket"))
                    {
                        await Task.Run(async () => {
                            List<PhotoInfo> photoInfos = PhotosInBucket[selectedBucket];

                            foreach (var photo in photoInfos)
                            {
                                //ͼƬ·��
                                string path = photo.Path;
                                string res = adbHelper.cmdExecuteWithAdbExit("shell rm -r " + path);
                                //string op = "adb shell am broadcast " +
                                //"-a android.intent.action.MEDIA_SCANNER_SCAN_FILE -d \'" +
                                //"file://" + path + "\'";
                                //res = adbHelper.cmdExecuteWithAdbExit(op);
                                Result result = socketHelper.getResult("picture", "delete");
                            }

                            DispatcherQueue.TryEnqueue(() => {
                                //ɾ��Ŀ¼
                                buckets.Remove(selectedBucket);
                                PhotosInBucket.Remove(selectedBucket);
                                MainWindow.PhotosInBucket = PhotosInBucket;
                                MainWindow.buckets = buckets;

                                BucketGrid.ItemsSource = buckets.Take(bucketSize).ToList();
                                totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // ��ҳ��
                                currentPage = 1;
                                pageNum.Text = currentPage + " / " + totalPages;
                            });
                        });
                    }
                    //ɾ������ͼƬ
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
                            //ɾ���ļ�
                            if (currentModule.Equals("photo"))
                            {
                                List<PhotoInfo> photoInfos = PhotosInBucket[currentBucket];
                                photoInfos.Remove(selectedItem);
                                currentPhotos.Remove(selectedItem);
                                PhotosInBucket[currentBucket] = photoInfos;
                                PhotoGrid.ItemsSource = currentPhotos;
                                totalPages = (PhotosInBucket[currentBucket].Count + pageSize - 1) / pageSize; // ��ҳ��
                                pageNum.Text = currentPage + " / " + totalPages;

                                MainWindow.PhotosInBucket = PhotosInBucket;
                            }
                            else
                            {
                                currentPhotos.Remove(selectedItem);
                                PhotosSorted.Remove(selectedItem);
                                //ɾ����Ӧ����Ƭ
                                PhotoListGrid.ItemsSource = PhotosSorted.Take(pageSize).ToList();
                                //�鿴Photo��ԭ��list�е�λ�ã�ͬʱ��Ⱦ��ȡ����ͼ����ʾ����ͼ
                                setPhotoImgPath(PhotosSorted.Take(pageSize).ToList());
                                totalPages = (PhotosSorted.Count + pageSize - 1) / pageSize; // ��ҳ��
                                currentPage = 1;
                                pageNum.Text = currentPage + " / " + totalPages;

                                MainWindow.PhotosSorted = PhotosSorted;
                            }
                            
                        }
                        else if (result.status.Equals("101"))
                        {
                            // ���ɹ�
                            DispatcherQueue.TryEnqueue(() => {
                                show_error(" No permissions granted.");
                            });
                        }
                        else
                        {
                            // �޸Ĳ��ɹ�
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

        //���ذ�ť
        private async void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                BucketGrid.Visibility = Visibility.Visible;
                PhotoGrid.Visibility = Visibility.Collapsed;
                currentModule = "bucket";

                BucketGrid.ItemsSource = buckets.Take(bucketSize).ToList();
                totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // ��ҳ��
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
                //�鿴Photo��ԭ��list�е�λ�ã�ͬʱ��Ⱦ��ȡ����ͼ����ʾ����ͼ
                setPhotoImgPath(PhotosSorted.Take(pageSize).ToList());
                currentModule = "photoList";
                totalPages = (Photos.Count + pageSize - 1) / pageSize; // ��ҳ��
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

                totalPages = (buckets.Count + bucketSize - 1) / bucketSize; // ��ҳ��
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

        //˫��ͼƬ
        private async void StackPanel_DoubleTapped_1(object sender, DoubleTappedRoutedEventArgs e)
        {
            try
            {

                StackPanel stackPanel = sender as StackPanel;
                foreach (var child in stackPanel.Children)
                {
                    // �����Ԫ���Ƿ�Ϊ TextBlock ����
                    if (child is TextBlock textBlock)
                    {
                        // �õ���Ƭ����
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
