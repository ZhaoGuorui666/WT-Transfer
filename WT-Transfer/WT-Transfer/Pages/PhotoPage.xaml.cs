// ��Ȩ���� (c) Microsoft Corporation and Contributors.
// ���� MIT ���֤�����ɡ�

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

namespace WT_Transfer.Pages
{
    /// <summary>
    /// һ���հ�ҳ�棬���Ե���ʹ�û��� Frame �ڵ�������ҳ�档
    /// </summary>
    public sealed partial class PhotoPage : Page
    {
        // ���������ڸ��ֹ���
        SocketHelper socketHelper = new SocketHelper();
        AdbHelper adbHelper = new AdbHelper();

        // ��־��¼ʵ��
        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        // ���ڴ洢��Ƭ��Ϣ�����ݽṹ
        public List<PhotoInfo> Photos = new List<PhotoInfo>();
        public List<PhotoInfo> PhotosSorted = new List<PhotoInfo>();
        public Dictionary<string, List<PhotoInfo>> PhotosInBucket { get; set; }
        HashSet<String> buckets = new HashSet<string>();
        string currentBucket = "";

        // ��ǰ������ģ��
        string currentModule = "bucket";
        List<PhotoInfo> currentPhotos = new List<PhotoInfo>();

        //Alubm�б�����
        List<AlbumInfo> albumList = new List<AlbumInfo>();
        ObservableCollection<GroupInfoList> groupedData;

        //��ǰĿ¼
        private string currentDirectory = "/Pictures/";

        // ���캯��
        public PhotoPage()
        {
            try
            {
                this.InitializeComponent();

                CurrentDirectoryTextBox.Text = currentDirectory;
                // ҳ�����ʱ���� LoadingPage_Loaded ����
                this.Loaded += LoadingPage_Loaded;
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // ҳ�������ɺ�Ĵ�����
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

        // ��ʼ��ҳ��ķ���
        private void InitPage()
        {
            Photos = MainWindow.Photos;
            buckets = MainWindow.buckets;
            PhotosInBucket = MainWindow.PhotosInBucket;
            PhotosSorted = MainWindow.PhotosSorted;

            // ����Ŀ¼���������Դ�����·�ҳ��Ϣ
            BucketGrid.ItemsSource = PhotosInBucket;

            // ���ؽ��Ȼ�����ʾĿ¼����
            progressRing.Visibility = Visibility.Collapsed;
            BucketGrid.Visibility = Visibility.Visible;
        }

        // ��ʼ�����ݵķ���
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
                            // ��������
                            JArray jArray = JArray.Parse(str);
                            // ʹ�� LINQ ��ѯ��ȡ DisplayName �� MobileNum
                            var resultArray = (from item in jArray
                                               select new
                                               {
                                                   Bucket = item["bucket"]?.ToString(),
                                                   Date = item["date"]?.ToString(),
                                                   Path = item["path"]?.ToString()
                                               })
                                            .ToArray();

                            // ͳ���ļ�������
                            foreach (var item in resultArray)
                            {
                                if (item.Bucket == null)
                                {
                                    buckets.Add("null");
                                    continue;
                                }
                                buckets.Add(item.Bucket);
                            }

                            // �����ݴ洢���ֵ���
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
                                    // ����ֵ������иü���ֱ�������Ƭ��Ϣ
                                    photos.Add(photoInfo);
                                }
                                else
                                {
                                    PhotosInBucket.Add(photoInfo.Bucket, new List<PhotoInfo> {
                        photoInfo
                    });
                                }
                            }

                            // ʹ�� LINQ ��ѯ������Ƭ��Ϣ
                            PhotosSorted = new List<PhotoInfo>(Photos);
                            PhotosSorted = PhotosSorted.OrderByDescending(p => p.Date).ToList();

                            // ��������
                            MainWindow.buckets = buckets;
                            MainWindow.Photos = Photos;
                            MainWindow.PhotosSorted = PhotosSorted;
                            MainWindow.PhotosInBucket = PhotosInBucket;

                            //�첽�߳̽�����ͼ���䵽���Զ�
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
                                // ��������ͼ·��
                                string localPath =
                                    System.IO.Path.GetFullPath(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "./images/pic/" + count++ + ".jpg")); ;

                                photo.LocalPath = localPath;
                            }


                            AddAlbumList();
                            // ���½���
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
                        // ��Ȩ��
                        DispatcherQueue.TryEnqueue(() =>
                        {
                            permission.Hide();
                            show_error(" No permissions granted.");

                            MainWindow.Permissions[4] = '0';
                        });
                    }
                    else
                    {
                        // ��ѯʧ��
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

        // ��ʼ��Ŀ¼ AlbumList
        private List<AlbumInfo> AddAlbumList()
        {
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
                        FirstPhotoPath = "/Images/folder.jpg", // Ĭ�Ϸ���ͼƬ
                        PhotoCount = 0
                    });
                }
            }

            return albumList;
        }

        // ˫��Ŀ¼���¼�������
        private async void StackPanel_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            try
            {
                StackPanel stackPanel = sender as StackPanel;
                foreach (var child in stackPanel.Children)
                {
                    // �����Ԫ���Ƿ�Ϊ TextBlock ����
                    if (child is TextBlock textBlock)
                    {
                        // ��ȡĿ¼����
                        string bucket = textBlock.Text;
                        List<PhotoInfo> photos = new List<PhotoInfo>();
                        if (PhotosInBucket.TryGetValue(bucket, out photos))
                        {
                            // ���µ�ǰĿ¼
                            currentBucket = bucket;
                            currentDirectory = $"/Pictures/{bucket}";
                            CurrentDirectoryTextBox.Text = currentDirectory;

                            //�������ڷ���
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


                            // ��ʱ������
                            //PhotosInBucket[bucket] = photos;

                            // ���µ�ǰģ��ͷ�ҳ��Ϣ
                            currentModule = "photo";

                            DispatcherQueue.TryEnqueue(() =>
                            {

                                // ��ʾ���ؽ�����
                                BucketGrid.Visibility = Visibility.Collapsed;
                                progressRing.Visibility = Visibility.Visible;
                            });


                            // ����Ƿ��Ѿ�����������ͼ·��
                            bool allPhotosHaveLocalPath = photos.Take(10).All(photo => !string.IsNullOrEmpty(photo.LocalPath) && File.Exists(photo.LocalPath));

                            if (!allPhotosHaveLocalPath)
                            {
                                // ������Ƭ������ͼ·��
                                //await Task.Run(() => setPhotoImgPath(photos.ToList()));
                            }


                            DispatcherQueue.TryEnqueue(() =>
                            {
                                // �л�����Ƭ������ͼ
                                PhotoGrid.Visibility = Visibility.Visible;
                                // ���ؼ��ؽ�����
                                progressRing.Visibility = Visibility.Collapsed;
                            });

                            // ��������Դ
                            //currentPhotos = photos.ToList();

                            //���ѡ��֮���˳����ٴν���֮�󣬰�֮ǰѡ�е�ͼƬ������ѡ��״̬

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
                            // �Ҳ���ͼƬ
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
            string thumbnailDirectory = Path.Combine(imageDirectory, "thumbnails");

            var groupedData = new ObservableCollection<GroupInfoList>();

            for (int i = 0; i < 1000; i += 50)
            {
                var group = new GroupInfoList() { Key = $"Group {i / 50 + 1}" };

                for (int j = 1; j <= 50; j++)
                {
                    string imageName = $"Image{j + i}.jpg";
                    string imagePath = Path.Combine(imageDirectory, imageName);
                    string thumbnailPath = Path.Combine(thumbnailDirectory, imageName);

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


        // ������Ƭ������ͼ·��
        public async Task setPhotoImgPath(List<PhotoInfo> photos)
        {
            try
            {
                foreach (var photo in photos)
                {
                    int index = Photos.FindIndex(p => p.Title == photo.Title);
                    // ��������ͼ·��
                    string phonePath =
                        "/storage/emulated/0/Android/data/com.example.contacts/files/Download/pic/" + index + ".jpg";

                    string localPath =
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "./images/pic/" + index + ".jpg")); ;
                    // �첽��������ͼ
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

        // ͬ��ѡ�е��ļ���
        private async void SyncFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // û��ѡ���ļ���
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

        // ���浥��ͼƬ
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

            // ��ʾ���ȶԻ���
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

            await Init();
            // ���ؽ��ȶԻ���
            progressDialog.Hide();

            ContentDialog importDialog = new ContentDialog
            {
                Title = "Info",
                Content = "Files import successful.",
                PrimaryButtonText = "OK",
            };
            importDialog.XamlRoot = this.Content.XamlRoot;
            await importDialog.ShowAsync();
        }



        // ����ͼƬΪ��ֽ
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

        // ɾ������
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

                // ѯ���û�ȷ��ɾ������
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
                    // ɾ������
                    // 1. ɾ��Ŀ¼
                    if (!string.IsNullOrEmpty(selectedBucket) && currentModule.Equals("bucket"))
                    {
                        await Task.Run(async () =>
                        {
                            List<PhotoInfo> photoInfos = PhotosInBucket[selectedBucket];

                            foreach (var photo in photoInfos)
                            {
                                // ɾ���豸�е�ͼƬ
                                string path = photo.Path;
                                string res = adbHelper.cmdExecuteWithAdbExit("shell rm -r " + path);
                                Result result = socketHelper.getResult("picture", "delete");
                            }

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                // ����UI��ɾ��Ŀ¼��Ϣ
                                buckets.Remove(selectedBucket);
                                PhotosInBucket.Remove(selectedBucket);
                                MainWindow.PhotosInBucket = PhotosInBucket;
                                MainWindow.buckets = buckets;

                                BucketGrid.ItemsSource = buckets.ToList();
                            });
                        });
                    }
                    // ɾ������ͼƬ
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
                            // ɾ���ļ�
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
                            // ��Ȩ��
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                show_error(" No permissions granted.");
                            });
                        }
                        else
                        {
                            // ɾ��ʧ��
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
                // ��鵱ǰģ��
                if (currentModule.Equals("bucket"))
                {
                    // ɾ��ѡ�е����
                    if (BucketGrid.SelectedItem != null)
                    {
                        string selectedBucket = BucketGrid.SelectedItem.ToString();

                        // ѯ���û�ȷ��ɾ������
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
                                    // ����UI��ɾ�������Ϣ
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
                    // ɾ��ѡ�е�ͼƬ
                    if (PhotoGrid.SelectedItem != null)
                    {
                        PhotoInfo selectedPhoto = (PhotoInfo)PhotoGrid.SelectedItem;

                        // ѯ���û�ȷ��ɾ������
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
                                    // ����UI��ɾ����Ƭ��Ϣ
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



        // ���ذ�ť����¼�������
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // �л���Ŀ¼��ͼ
                BucketGrid.Visibility = Visibility.Visible;
                PhotoGrid.Visibility = Visibility.Collapsed;
                currentModule = "bucket";

                // ���·�ҳ��Ϣ
                BucketGrid.ItemsSource = albumList.Count == 0 ? AddAlbumList() : albumList;

                // ���µ�ǰĿ¼��ʾ
                //CurrentDirectoryTextBlock.Text = "Current Bucket: " + currentBucket;
                currentDirectory = "/Pictures";
                CurrentDirectoryTextBox.Text = currentDirectory;

            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }


        // ˫��ͼƬ���¼�������
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
                        // ��ȡ��Ƭ·��
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

        // ��ʾ������Ϣ�Ի���
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

                }

                PhotoGrid.SelectedItems.Clear();
            }
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
        }

        //ѡ��ĳ��
        private void PhotoGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedItem = e.ClickedItem as PhotoInfo;
            if (clickedItem != null)
            {
                clickedItem.IsSelected = !clickedItem.IsSelected;

                // �ҵ�����������鲢����ѡ������
                var parentGroup = groupedData.FirstOrDefault(g => g.Contains(clickedItem));
                if (parentGroup != null)
                {
                    parentGroup.OnPropertyChanged(nameof(parentGroup.SelectedCount));
                }
            }
        }

        private void SortButton_Click(object sender, RoutedEventArgs e)
        {
            // Method intentionally left empty, as specific sort actions are handled by the individual event handlers
        }

        private void SortButton2_Click(object sender, RoutedEventArgs e)
        {
            // Method intentionally left empty, as specific sort actions are handled by the individual event handlers
        }

        //�������򣬵�ѡ
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

        private void SortByTimeCreated_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection(SortBySubItem, (ToggleMenuFlyoutItem)sender);
            SortCurrentGroupData(true);
        }

        private void SortByFileSize_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SetSingleSelection(SortBySubItem, (ToggleMenuFlyoutItem)sender);
                SortCurrentGroupDataByFileSize();
            }
            catch (Exception ex)
            {

            }
        }

        private void SortCurrentGroupDataByFileSize()
        {
            if (groupedData != null && groupedData.Any())
            {
                foreach (var group in groupedData)
                {
                    var sortedItems = group.OrderBy(photo => new FileInfo(photo.Path).Length).ToList();

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
        }

        private void SortDescending_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection(OrderSubItem, (ToggleMenuFlyoutItem)sender);
            SortCurrentGroupOrder(false);
        }

        private void SortCurrentGroupData(bool byDate)
        {
            if (groupedData != null && groupedData.Any())
            {
                var sortedGroups = byDate
                    ? groupedData.OrderBy(group => DateTime.Parse(group.Key)).ToList()
                    : groupedData.OrderBy(group => group.Sum(photo => new FileInfo(photo.Path).Length)).ToList();

                groupedData.Clear();
                foreach (var group in sortedGroups)
                {
                    groupedData.Add(group);
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
                var sortedGroups = ascending
                    ? groupedData.OrderBy(group => group.Key).ToList()
                    : groupedData.OrderByDescending(group => group.Key).ToList();

                groupedData.Clear();
                foreach (var group in sortedGroups)
                {
                    groupedData.Add(group);
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
            FilterPhotosByDateRange(DateTime.MinValue, DateTime.MaxValue);
        }

        private void FilterByThisWeek_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7).AddSeconds(-1);
            FilterPhotosByDateRange(startOfWeek, endOfWeek);
        }

        private void FilterByThisMonth_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);
            FilterPhotosByDateRange(startOfMonth, endOfMonth);
        }

        private void FilterByLastMonth_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfLastMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
            var endOfLastMonth = startOfLastMonth.AddMonths(1).AddSeconds(-1);
            FilterPhotosByDateRange(startOfLastMonth, endOfLastMonth);
        }

        private void FilterByLast3Months_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfLast3Months = DateTime.Today.AddMonths(-3);
            FilterPhotosByDateRange(startOfLast3Months, DateTime.Today);
        }

        private void FilterByLast6Months_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfLast6Months = DateTime.Today.AddMonths(-6);
            FilterPhotosByDateRange(startOfLast6Months, DateTime.Today);
        }

        private void FilterByThisYear_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfYear = new DateTime(DateTime.Today.Year, 1, 1);
            var endOfYear = startOfYear.AddYears(1).AddSeconds(-1);
            FilterPhotosByDateRange(startOfYear, endOfYear);
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

        //��������ͼƬ
        private void ExportAllPhotos_Click(object sender, RoutedEventArgs e)
        {
            // ����������ͼƬ���߼�
            ExportPhotos(allPhotos: true);
        }

        private void ExportSelectedPhotos_Click(object sender, RoutedEventArgs e)
        {
            // ������ѡ��ͼƬ���߼�
            ExportPhotos(allPhotos: false);
        }

        private async void ExportPhotos(bool allPhotos)
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
                    List<PhotoInfo> photosToExport;

                    if (allPhotos)
                    {
                        // ��������ͼƬ���߼�
                        photosToExport = Photos;
                    }
                    else
                    {
                        // ����ѡ��ͼƬ���߼�
                        photosToExport = Photos.Where(photo => photo.IsSelected).ToList();
                    }
                    // ��������ʾContentDialog
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

                    // ��ʾ���ȶԻ���
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

                    // �ر�ContentDialog
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
        public int PhotoCount => this.Count; // �������ԣ�������Ƭ����

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


    }
}
