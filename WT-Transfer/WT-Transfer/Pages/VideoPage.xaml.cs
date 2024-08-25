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
using Newtonsoft.Json;
using static WT_Transfer.SocketModels.Request;
using System.Threading;
using Microsoft.UI.Xaml.Shapes;
using System.Diagnostics;

namespace WT_Transfer.Pages
{
    /// <summary>
    /// һ���հ�ҳ�棬���Ե���ʹ�û��� Frame �ڵ�������ҳ�档
    /// </summary>
    public sealed partial class VideoPage : Page
    {
        // ���������ڸ��ֹ���
        SocketHelper socketHelper = new SocketHelper();
        AdbHelper adbHelper = new AdbHelper();

        // ��־��¼ʵ��
        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        // ���ڴ洢��Ƭ��Ϣ�����ݽṹ
        public List<VideoInfo> Videos = new List<VideoInfo>();
        public List<VideoInfo> VideosSorted = new List<VideoInfo>();
        public Dictionary<string, List<VideoInfo>> VideosInBucket { get; set; }
        HashSet<String> buckets = new HashSet<string>();
        string currentBucket = "";

        // ��ǰ������ģ��
        string currentModule = "bucket";
        List<VideoInfo> currentVideos = new List<VideoInfo>();

        //Alubm�б�����
        List<AlbumInfo> albumList = new List<AlbumInfo>();
        ObservableCollection<GroupInfoList_Video> groupedData;

        //��ǰĿ¼
        private string currentDirectory = "/Pictures/";

        // ���캯��
        public VideoPage()
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

                if (this.Videos == null || this.buckets == null || this.VideosInBucket == null
                    || Videos.Count == 0 || buckets.Count == 0 || VideosInBucket.Count == 0)
                {
                    if (MainWindow.Videos == null || MainWindow.buckets == null || MainWindow.VideosInBucket == null)
                    {
                        // ���г�ʼ������������������ݲ���ֵ�� calls
                        if (!MainWindow.sms_isRuning)
                            await Init();
                        else
                        {
                            await Task.Run(() =>
                            {
                                while (MainWindow.Videos == null)
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

                // ��ʼ��ѡ��״̬�ı�
                UpdateSelectedFilesInfo();
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
            Videos = MainWindow.Videos;
            buckets = MainWindow.buckets;
            VideosInBucket = MainWindow.VideosInBucket;
            VideosSorted = MainWindow.VideosSorted;

            // ����Ŀ¼���������Դ�����·�ҳ��Ϣ
            BucketGrid.ItemsSource = VideosInBucket;

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
                    VideosInBucket = new Dictionary<string, List<VideoInfo>>();

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
                                               let extension = System.IO.Path.GetExtension(item["path"]?.ToString()).ToLower()
                                               where extension == ".mp4" || extension == ".avi" || extension == ".mov" || extension == ".mkv" || extension == ".flv" || extension == ".wmv"
                                               select new
                                               {
                                                   Bucket = item["bucket"]?.ToString(),
                                                   Date = item["date"]?.ToString(),
                                                   Path = item["path"]?.ToString(),
                                                   Size = item["size"]?.ToString(),
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
                                VideoInfo VideoInfo = new VideoInfo();
                                VideoInfo.Bucket = item.Bucket;
                                VideoInfo.Date = item.Date;
                                VideoInfo.Path = item.Path;

                                // ����С���ֽ�ת��ΪMB
                                if (double.TryParse(item.Size, out double sizeInBytes))
                                {
                                    double sizeInMB = sizeInBytes / (1024 * 1024);
                                    VideoInfo.Size = sizeInMB.ToString("0.## MB"); // ��ʽ��Ϊ������λС��
                                }
                                else
                                {
                                    VideoInfo.Size = "Unknown Size"; // ����޷�����������Ϊδ֪
                                }

                                VideoInfo.getTitle();

                                if (item.Bucket == null)
                                {
                                    VideoInfo.Bucket = "null";
                                }
                                Videos.Add(VideoInfo);

                                List<VideoInfo> videos = new List<VideoInfo>();
                                if (VideosInBucket.TryGetValue(VideoInfo.Bucket, out videos))
                                {
                                    // ����ֵ������иü���ֱ�������Ƭ��Ϣ
                                    videos.Add(VideoInfo);
                                }
                                else
                                {
                                    VideosInBucket.Add(VideoInfo.Bucket, new List<VideoInfo> {
                        VideoInfo
                    });
                                }
                            }

                            // ʹ�� LINQ ��ѯ������Ƭ��Ϣ
                            VideosSorted = new List<VideoInfo>(Videos);
                            VideosSorted = VideosSorted.OrderByDescending(p => p.Date).ToList();

                            // ��������
                            MainWindow.buckets = buckets;
                            MainWindow.Videos = Videos;
                            MainWindow.VideosSorted = VideosSorted;
                            MainWindow.VideosInBucket = VideosInBucket;

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
                            foreach (var Video in Videos)
                            {
                                // ��������ͼ·��
                                string localPath =
                                    System.IO.Path.GetFullPath(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "./images/pic/" + count++ + ".jpg")); ;

                                Video.LocalPath = localPath;
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
                        show_error("Video query failed ,please check the phone connection.");
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
            albumList = new List<AlbumInfo>();
            foreach (var kv in VideosInBucket)
            {
                List<VideoInfo> Videos = new List<VideoInfo>();
                Videos.Add(kv.Value.First());
                setVideoImgPath(Videos);
                if (kv.Value.Count > 0)
                {
                    var firstVideoPath = kv.Value.First().LocalPath;
                    albumList.Add(new AlbumInfo
                    {
                        Name = kv.Key,
                        FirstPhotoPath = firstVideoPath,
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

        // ����ѡ��״̬��Ϣ
        private void UpdateSelectedFilesInfo()
        {
            int selectedCount = Videos.Count(Video => Video.IsSelected);
            double selectedSizeMB = Videos.Where(Video => Video.IsSelected).Sum(Video => ParseSizeInMB(Video.Size));

            double totalSizeMB = Videos.Sum(Video => ParseSizeInMB(Video.Size));
            string info = $"{selectedCount} of {Videos.Count} Item(s) Selected - {selectedSizeMB:0.##} MB of {totalSizeMB:0.##} MB";
            SelectedFilesInfo.Text = info;
        }

        // ������С�ַ���ΪMB
        private double ParseSizeInMB(string sizeString)
        {
            string str = sizeString.Replace(" MB", "").Trim();
            if (double.TryParse(str, out double size))
            {
                return size;
            }
            return 0;
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
                        List<VideoInfo> Videos = new List<VideoInfo>();
                        if (VideosInBucket.TryGetValue(bucket, out Videos))
                        {
                            // ���µ�ǰĿ¼
                            currentBucket = bucket;
                            currentDirectory = $"/Pictures/{bucket}";
                            CurrentDirectoryTextBox.Text = currentDirectory;

                            //�������ڷ���
                            groupedData = new ObservableCollection<GroupInfoList_Video>();

                            var groupedVideos = Videos
                                .GroupBy(p => DateTime.Parse(p.Date).ToString("yyyy-MM-dd"))
                                .OrderByDescending(g => g.Key);

                            foreach (var group in groupedVideos)
                            {
                                var groupInfoList = new GroupInfoList_Video { Key = group.Key };
                                groupInfoList.AddRange(group);
                                groupedData.Add(groupInfoList);
                            }


                            // ��ʱ������
                            //VideosInBucket[bucket] = Videos;

                            // ���µ�ǰģ��ͷ�ҳ��Ϣ
                            currentModule = "Video";

                            DispatcherQueue.TryEnqueue(() =>
                            {

                                // ��ʾ���ؽ�����
                                BucketGrid.Visibility = Visibility.Collapsed;
                                progressRing.Visibility = Visibility.Visible;
                            });


                            DispatcherQueue.TryEnqueue(() =>
                            {
                                // �л�����Ƭ������ͼ
                                VideoGrid.Visibility = Visibility.Visible;
                                // ���ؼ��ؽ�����
                                progressRing.Visibility = Visibility.Collapsed;
                            });

                            // ��������Դ
                            //currentVideos = Videos.ToList();

                            //���ѡ��֮���˳����ٴν���֮�󣬰�֮ǰѡ�е�ͼƬ������ѡ��״̬
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                foreach (var group in groupedData)
                                {
                                    foreach (VideoInfo p in group)
                                    {
                                        if (p.IsSelected == true)
                                        {
                                            VideoGrid.SelectedItems.Add(p);
                                        }
                                    }
                                }
                            });

                            var cvs = new CollectionViewSource
                            {
                                IsSourceGrouped = true,
                                Source = groupedData
                            };
                            VideoGrid.ItemsSource = cvs.View;
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

        private ObservableCollection<GroupInfoList_Video> GenerateGroupedData()
        {
            string imageDirectory = @"D:\BaiduNetdiskDownload\val2017";
            string thumbnailDirectory = System.IO.Path.Combine(imageDirectory, "thumbnails");

            var groupedData = new ObservableCollection<GroupInfoList_Video>();

            for (int i = 0; i < 1000; i += 50)
            {
                var group = new GroupInfoList_Video() { Key = $"Group {i / 50 + 1}" };

                for (int j = 1; j <= 50; j++)
                {
                    string imageName = $"Image{j + i}.jpg";
                    string imagePath = System.IO.Path.Combine(imageDirectory, imageName);
                    string thumbnailPath = System.IO.Path.Combine(thumbnailDirectory, imageName);

                    VideoInfo image = new VideoInfo
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
        public async Task setVideoImgPath(List<VideoInfo> Videos)
        {
            try
            {
                foreach (var Video in Videos)
                {
                    int index = Videos.FindIndex(p => p.Title == Video.Title);
                    // ��������ͼ·��
                    string phonePath =
                        "/storage/emulated/0/Android/data/com.example.contacts/files/Download/pic/" + index + ".jpg";

                    string localPath =
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path, "./images/pic/" + index + ".jpg")); ;
                    // �첽��������ͼ
                    await Task.Run(() => adbHelper.saveFromPath(phonePath, localPath));
                    if (File.Exists(localPath))
                    {
                        Video.LocalPath = localPath;
                    }
                    else
                    {
                        Video.LocalPath = "/Images/noImg.png";
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
                List<VideoInfo> VideoInfos = VideosInBucket[selectedItem];

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

                    SyncVideo.XamlRoot = this.XamlRoot;
                    SyncVideo.ShowAsync();

                    await Task.Run(async () =>
                    {
                        foreach (var Video in VideoInfos)
                        {
                            string path = Video.Path;
                            string localPath = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_VideoBackupPath];
                            string winPath = localPath + "\\" + selectedItem + "\\" + Video.Title;

                            adbHelper.saveFromPath(path, winPath);
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                //infoBar.Message = "Currently backing up: " + Video.Title;
                                SyncMessage.Text = "Currently backing up: " + Video.Title;
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
                VideoInfo selectedItem = new VideoInfo();
                if (currentModule.Equals("Video"))
                    selectedItem = (VideoInfo)VideoGrid.SelectedItem;
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

        private async void ImportVideos_Click(object sender, RoutedEventArgs e)
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

            //await Init();
            // ���� transfer ������
            UpdateTransferGroup(files);

            // ���ؽ��ȶԻ���
            progressDialog.Hide();

            ContentDialog importDialog = new ContentDialog
            {
                Title = "Info",
                Content = "Your files have been successfully imported.",
                PrimaryButtonText = "OK",
            };
            importDialog.XamlRoot = this.Content.XamlRoot;
            await importDialog.ShowAsync();

            RefreshButton_Click(null, null);
        }

        private async void UpdateTransferGroup(IEnumerable<StorageFile> files)
        {
            // ���һ򴴽� transfer ��
            if (!VideosInBucket.TryGetValue("transfer", out var transferVideos))
            {
                transferVideos = new List<VideoInfo>();
                VideosInBucket["transfer"] = transferVideos;
            }


            List<String> paths = new List<string>();
            foreach (var file in files)
            {
                var VideoInfo = new VideoInfo
                {
                    Title = System.IO.Path.GetFileName(file.Name),
                    Path = $"/sdcard/Pictures/transfer/{file.Name}",
                    LocalPath = file.Path,
                    Date = File.GetCreationTime(file.Path).ToString("yyyy-MM-dd")
                };
                transferVideos.Add(VideoInfo);
                Videos.Add(VideoInfo);
                paths.Add(VideoInfo.Path);
            }

            // �����ֻ� �������֣�ɨ��
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

        // ����ͼƬΪ��ֽ
        private async void SetVideoToWall_Clicks(object sender, RoutedEventArgs e)
        {
            try
            {
                VideoInfo selectedItem = new VideoInfo();
                if (currentModule.Equals("Video"))
                    selectedItem = (VideoInfo)VideoGrid.SelectedItem;
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

                string VideoPath = selectedItem.Path;
                string localPath =
                        System.IO.Path.GetFullPath(System.IO.Path.Combine(ApplicationData.Current.LocalFolder.Path,
                        "./images/" + "wallpaper.jpg")); ;
                adbHelper.saveFromPath(VideoPath, localPath);
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
                VideoInfo selectedItem = new VideoInfo();
                if (currentModule.Equals("Video"))
                    selectedItem = (VideoInfo)VideoGrid.SelectedItem;
                else
                    selectedItem = null;

                if ((selectedItem == null && currentModule.Equals("Video"))
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
                            List<VideoInfo> VideoInfos = VideosInBucket[selectedBucket];

                            foreach (var Video in VideoInfos)
                            {
                                // ɾ���豸�е�ͼƬ
                                string path = Video.Path;
                                string res = adbHelper.cmdExecuteWithAdbExit("shell rm -r " + path);
                                Result result = socketHelper.getResult("picture", "delete");
                            }

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                // ����UI��ɾ��Ŀ¼��Ϣ
                                buckets.Remove(selectedBucket);
                                VideosInBucket.Remove(selectedBucket);
                                MainWindow.VideosInBucket = VideosInBucket;
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
                            if (currentModule.Equals("Video"))
                            {
                                List<VideoInfo> VideoInfos = VideosInBucket[currentBucket];
                                VideoInfos.Remove(selectedItem);
                                currentVideos.Remove(selectedItem);
                                VideosInBucket[currentBucket] = VideoInfos;
                                VideoGrid.ItemsSource = currentVideos;

                                MainWindow.VideosInBucket = VideosInBucket;
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
                                List<VideoInfo> VideosToDelete = VideosInBucket[selectedBucket];

                                foreach (var Video in VideosToDelete)
                                {
                                    string path = Video.Path;
                                    adbHelper.cmdExecuteWithAdbExit($"shell rm \"{path}\"");
                                }

                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    // ����UI��ɾ�������Ϣ
                                    VideosInBucket.Remove(selectedBucket);
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
                else if (currentModule.Equals("Video"))
                {
                    // ɾ��ѡ�е�ͼƬ
                    if (VideoGrid.SelectedItem != null)
                    {
                        VideoInfo selectedVideo = (VideoInfo)VideoGrid.SelectedItem;

                        // ѯ���û�ȷ��ɾ������
                        ContentDialog deleteDialog = new ContentDialog
                        {
                            Title = "Delete Video",
                            Content = "Are you sure you want to delete the selected video(s)? This action cannot be undone?",
                            PrimaryButtonText = "Delete",
                            SecondaryButtonText = "Cancel"
                        };
                        deleteDialog.XamlRoot = this.Content.XamlRoot;
                        ContentDialogResult result = await deleteDialog.ShowAsync();

                        if (result == ContentDialogResult.Primary)
                        {
                            await Task.Run(() =>
                            {
                                string path = selectedVideo.Path;
                                adbHelper.cmdExecuteWithAdbExit($"shell rm \"{path}\"");

                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    // ����UI��ɾ����Ƭ��Ϣ
                                    var group = groupedData.FirstOrDefault(g => g.Contains(selectedVideo));
                                    if (group != null)
                                    {
                                        group.Remove(selectedVideo);
                                        if (group.Count == 0)
                                        {
                                            groupedData.Remove(group);
                                        }
                                    }

                                    VideoGrid.ItemsSource = null;
                                    VideoGrid.ItemsSource = groupedData;
                                });
                            });

                            ContentDialog successDialog = new ContentDialog
                            {
                                Title = "Success",
                                Content = "The selected videos have been successfully deleted.",
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
                            Content = "Please select a Video to delete.",
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
                VideoGrid.Visibility = Visibility.Collapsed;
                currentModule = "bucket";

                // ���·�ҳ��Ϣ
                BucketGrid.ItemsSource = albumList.Count == 0 ? AddAlbumList() : albumList;

                // ���µ�ǰĿ¼��ʾ
                //CurrentDirectoryTextBlock.Text = "Current Bucket: " + currentBucket;
                currentDirectory = "/Pictures";
                CurrentDirectoryTextBox.Text = currentDirectory;

                // ����ʱ�䷶Χѡ��Ϊ "All Time"
                ResetTimeRangeToAllTime();
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // ��������������ʱ�䷶Χ�� "All Time"
        private void ResetTimeRangeToAllTime()
        {

            StartDatePicker.Date = null; // All Time ͨ����ʾ�޽���
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
                        string localPath = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_VideoBackupPath];
                        string winPath = localPath + "\\image" + "\\" + textBlock.Text;

                        adbHelper.saveFromPath(path, winPath);

                        ShowVideo.XamlRoot = this.XamlRoot;
                        BitmapImage imageSource = new BitmapImage(new Uri(winPath));
                        VideoImage.Source = imageSource;
                        await ShowVideo.ShowAsync();
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
            if (sender is CheckBox checkBox && checkBox.DataContext is GroupInfoList_Video groupInfo)
            {
                foreach (var Video in groupInfo)
                {
                    Video.IsSelected = false;

                    // �� VideoGrid.SelectedItems ���ҵ���Ӧ���ȡ��ѡ��
                    if (VideoGrid.SelectedItems.Contains(Video))
                    {
                        VideoGrid.SelectedItems.Remove(Video);
                    }
                }

                //VideoGrid.SelectedItems.Clear();
            }


            // ����ѡ��״̬�ı�
            UpdateSelectedFilesInfo();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is GroupInfoList_Video groupInfo)
            {
                foreach (var Video in groupInfo)
                {
                    Video.IsSelected = true;
                    VideoGrid.SelectedItems.Add(Video);
                }
            }


            // ����ѡ��״̬�ı�
            UpdateSelectedFilesInfo();
        }

        //ѡ��ĳ��
        private void VideoGrid_ItemClick(object sender, ItemClickEventArgs e)
        {
            var clickedItem = e.ClickedItem as VideoInfo;
            if (clickedItem != null)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    clickedItem.IsSelected = !clickedItem.IsSelected;
                    if (clickedItem.IsSelected == true)
                    {
                        VideoGrid.SelectedItems.Add(clickedItem);
                    }
                    else
                    {
                        if (VideoGrid.SelectedItems.Contains(clickedItem))
                        {
                            VideoGrid.SelectedItems.Remove(clickedItem);
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
                // ����ѡ��״̬�ı�
                UpdateSelectedFilesInfo();
            });
        }

        // �������ڲ��Ҿ����ض���ǩ�ĸ�ѡ��
        private CheckBox FindCheckBoxByTag(DependencyObject parent, string tag)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                // ����Ӷ����� CheckBox �������ǩƥ�䣬�򷵻ظø�ѡ��
                if (child is CheckBox checkBox && checkBox.Tag as string == tag)
                {
                    return checkBox;
                }

                // �ݹ�����Ӷ�����Ӷ���
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
            // Ѱ�������������CheckBox
            var checkBox =
                    FindCheckBoxByTag(this.Content as DependencyObject, groupKey);
            if (checkBox != null)
            {
                // ��ȡ�����е�������Ƭ
                var group = groupedData.FirstOrDefault(g => g.Key == groupKey);
                if (group != null)
                {
                    bool allSelected = group.All(Video => Video.IsSelected);
                    bool anySelected = group.Any(Video => Video.IsSelected);

                    if (!allSelected && anySelected)
                    {
                        // ����Ϊ�м�״̬
                        checkBox.IsChecked = null;
                    }
                    else if (allSelected)
                    {
                        checkBox.IsChecked = true;
                    }
                    else if (!allSelected)
                    {
                        checkBox.IsChecked = false;
                    }
                }
            }
        }


        private void CheckBox_IndeterminateHandler(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is GroupInfoList_Video groupInfo)
            {
                if (groupInfo.CheckAllSelected())
                {
                    // ���ȫ��ѡ�У�ȡ��ѡ������
                    groupInfo.SetAllSelected(false);
                    checkBox.IsChecked = false;
                }
            }
        }


        private void Image_Tapped(object sender, TappedRoutedEventArgs e)
        {
            // ��ʾ��ͼ�������Flyout
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
                    var sortedItems = group.OrderBy(Video => Video.Size).ToList();

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
                VideoGrid.ItemsSource = cvs.View;
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
                        var sortedItems = group.OrderBy(Video => DateTime.Parse(Video.Date)).ToList();

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
                        var sortedItems = group.OrderBy(Video => Video.Size).ToList();

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
                VideoGrid.ItemsSource = cvs.View;
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
                        ? groupedData.OrderBy(group => group.Sum(Video => int.TryParse(Video.Size, out int size) ? size : 0)).ToList()
                        : groupedData.OrderByDescending(group => group.Sum(Video => int.TryParse(Video.Size, out int size) ? size : 0)).ToList();

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
                VideoGrid.ItemsSource = cvs.View;
            }
        }


        private void UpdateVideoGrid(string directoryName)
        {
            if (currentModule == "Video" && VideosInBucket.TryGetValue(directoryName, out var VideosInDirectory))
            {
                var groupedData = new ObservableCollection<GroupInfoList_Video>();

                var groupedVideos = VideosInDirectory
                    .GroupBy(p => DateTime.Parse(p.Date).ToString("yyyy-MM-dd"))
                    .OrderByDescending(g => g.Key);

                foreach (var group in groupedVideos)
                {
                    var GroupInfoList_Video = new GroupInfoList_Video { Key = group.Key };
                    GroupInfoList_Video.AddRange(group);
                    groupedData.Add(GroupInfoList_Video);
                }

                var cvs = new CollectionViewSource
                {
                    IsSourceGrouped = true,
                    Source = groupedData
                };

                VideoGrid.ItemsSource = cvs.View;
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
            StartDatePicker.Date = null; // All Time ͨ����ʾ�޽���
            EndDatePicker.Date = null;
            FilterVideosByDateRange(DateTime.MinValue, DateTime.MaxValue);
        }

        // ����������������ͬ������ѡ������ֵ
        private void FilterByThisWeek_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
            var endOfWeek = startOfWeek.AddDays(7).AddSeconds(-1);
            StartDatePicker.Date = startOfWeek;
            EndDatePicker.Date = endOfWeek;
            FilterVideosByDateRange(startOfWeek, endOfWeek);
        }

        private void FilterByThisMonth_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var endOfMonth = startOfMonth.AddMonths(1).AddSeconds(-1);
            StartDatePicker.Date = startOfMonth;
            EndDatePicker.Date = endOfMonth;
            FilterVideosByDateRange(startOfMonth, endOfMonth);
        }

        private void FilterByLastMonth_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfLastMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-1);
            var endOfLastMonth = startOfLastMonth.AddMonths(1).AddSeconds(-1);
            StartDatePicker.Date = startOfLastMonth;
            EndDatePicker.Date = endOfLastMonth;
            FilterVideosByDateRange(startOfLastMonth, endOfLastMonth);
        }

        private void FilterByLast3Months_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfLast3Months = DateTime.Today.AddMonths(-3);
            var endOfLast3Months = DateTime.Today;
            StartDatePicker.Date = startOfLast3Months;
            EndDatePicker.Date = endOfLast3Months;
            FilterVideosByDateRange(startOfLast3Months, endOfLast3Months);
        }

        private void FilterByLast6Months_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfLast6Months = DateTime.Today.AddMonths(-6);
            var endOfLast6Months = DateTime.Today;
            StartDatePicker.Date = startOfLast6Months;
            EndDatePicker.Date = endOfLast6Months;
            FilterVideosByDateRange(startOfLast6Months, endOfLast6Months);
        }

        private void FilterByThisYear_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            var startOfYear = new DateTime(DateTime.Today.Year, 1, 1);
            var endOfYear = startOfYear.AddYears(1).AddSeconds(-1);
            StartDatePicker.Date = startOfYear;
            EndDatePicker.Date = endOfYear;
            FilterVideosByDateRange(startOfYear, endOfYear);
        }

        private void FilterByCustomRange_Click(object sender, RoutedEventArgs e)
        {
            SetSingleSelection((ToggleMenuFlyoutItem)sender);
            if (StartDatePicker.Date.HasValue && EndDatePicker.Date.HasValue)
            {
                DateTime startDate = StartDatePicker.Date.Value.DateTime;
                DateTime endDate = EndDatePicker.Date.Value.DateTime;
                FilterVideosByDateRange(startDate, endDate);
            }
        }


        private void FilterVideosByDateRange(DateTime startDate, DateTime endDate)
        {
            var filteredGroups = new ObservableCollection<GroupInfoList_Video>();

            foreach (var group in groupedData)
            {
                var filteredItems = group.Where(Video => DateTime.Parse(Video.Date) >= startDate && DateTime.Parse(Video.Date) <= endDate).ToList();

                if (filteredItems.Any())
                {
                    var newGroup = new GroupInfoList_Video { Key = group.Key };
                    newGroup.AddRange(filteredItems);
                    filteredGroups.Add(newGroup);
                }
            }

            var cvs = new CollectionViewSource
            {
                IsSourceGrouped = true,
                Source = filteredGroups
            };

            VideoGrid.ItemsSource = cvs.View;
        }

        private void DatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            FilterVideosBySelectedDateRange();
        }

        private void FilterVideosBySelectedDateRange()
        {
            if (StartDatePicker.Date.HasValue && EndDatePicker.Date.HasValue)
            {
                DateTime startDate = StartDatePicker.Date.Value.DateTime;
                DateTime endDate = EndDatePicker.Date.Value.DateTime;

                var filteredGroups = new ObservableCollection<GroupInfoList_Video>();

                foreach (var group in groupedData)
                {
                    var filteredItems = group.Where(Video => DateTime.Parse(Video.Date) >= startDate && DateTime.Parse(Video.Date) <= endDate).ToList();

                    if (filteredItems.Any())
                    {
                        var newGroup = new GroupInfoList_Video { Key = group.Key };
                        newGroup.AddRange(filteredItems);
                        filteredGroups.Add(newGroup);
                    }
                }

                var cvs = new CollectionViewSource
                {
                    IsSourceGrouped = true,
                    Source = filteredGroups
                };

                VideoGrid.ItemsSource = cvs.View;
            }
        }

        //��������ͼƬ
        private void ExportAllVideos_Click(object sender, RoutedEventArgs e)
        {
            // ����������ͼƬ���߼�
            ExportVideos(allVideos: true);
        }

        private void ExportSelectedVideos_Click(object sender, RoutedEventArgs e)
        {
            // ������ѡ��ͼƬ���߼�
            ExportVideos(allVideos: false);
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

        private async void ExportVideos(bool allVideos)
        {
            try
            {
                List<VideoInfo> VideosToExport;

                if (allVideos)
                {
                    // ��������ͼƬ���߼�
                    VideosToExport = Videos;
                }
                else
                {
                    // ����ѡ��ͼƬ���߼�
                    VideosToExport = Videos.Where(Video => Video.IsSelected).ToList();

                    // ���û��ѡ����Ƭ����ʾ�û�
                    if (VideosToExport.Count == 0)
                    {
                        await ShowMessageDialog("No Video selected", "No videos have been selected for export.\r\nPlease select the videos you want to export and try again.");
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
                    // ��������ʾContentDialog
                    var progressDialog = new ContentDialog
                    {
                        Title = "Exporting Videos",
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
                        int totalVideos = VideosToExport.Count;
                        int exportedVideos = 0;

                        foreach (var Video in VideosToExport)
                        {
                            string path = Video.Path;
                            string localPath = storageFolder.Path;

                            adbHelper.saveFromPath(path, localPath);

                            exportedVideos++;
                            double progress = (double)exportedVideos / totalVideos * 100;

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
                        Content = "Your videos has been successfully exported to the designated folder.",
                        PrimaryButtonText = "View Folder",
                        SecondaryButtonText = "OK",
                        DefaultButton = ContentDialogButton.Secondary // ����OKΪĬ�ϰ�ť
                    };
                    exportDialog.XamlRoot = this.Content.XamlRoot;


                    exportDialog.SecondaryButtonClick += async (s, args) =>
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

        private async void DeleteSelectedVideosButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // ��鵱ǰģ��
                if (currentModule.Equals("Video"))
                {
                    var selectedVideos = VideoGrid.SelectedItems.Cast<VideoInfo>().ToList();
                    if (selectedVideos.Any())
                    {
                        // ѯ���û�ȷ��ɾ������
                        ContentDialog deleteDialog = new ContentDialog
                        {
                            Title = "Delete Videos",
                            Content = "Are you sure you want to delete the selected Videos?",
                            PrimaryButtonText = "Delete",
                            SecondaryButtonText = "Cancel"
                        };
                        deleteDialog.XamlRoot = this.Content.XamlRoot;
                        ContentDialogResult result = await deleteDialog.ShowAsync();

                        if (result == ContentDialogResult.Primary)
                        {
                            await Task.Run(() =>
                            {
                                foreach (var Video in selectedVideos)
                                {
                                    string path = Video.Path;
                                    adbHelper.cmdExecuteWithAdbExit("shell rm " + path);

                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        var group = groupedData.FirstOrDefault(g => g.Contains(Video));
                                        if (group != null)
                                        {
                                            group.Remove(Video);
                                            if (group.Count == 0)
                                            {
                                                groupedData.Remove(group);
                                            }
                                        }

                                        // ������Ӧ�� AlbumInfo
                                        var album = albumList.FirstOrDefault(a => a.Name == Video.Bucket);
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
                                paths = selectedVideos.Select(file => file.Path).ToList<string>(),
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
                                Content = "Selected Videos deleted successfully.",
                                PrimaryButtonText = "OK"
                            };
                            successDialog.XamlRoot = this.Content.XamlRoot;
                            await successDialog.ShowAsync();


                            var cvs = new CollectionViewSource
                            {
                                IsSourceGrouped = true,
                                Source = groupedData
                            };
                            VideoGrid.ItemsSource = cvs.View;
                        }
                    }
                    else
                    {
                        ContentDialog errorDialog = new ContentDialog
                        {
                            Title = "Error",
                            Content = "Please select Videos to delete.",
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
                    // ��ʾ���ؽ�����
                    BucketGrid.Visibility = Visibility.Collapsed;
                    VideoGrid.Visibility = Visibility.Collapsed;
                    progressRing.Visibility = Visibility.Visible;
                });



                // ����UI
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
                else if (currentModule == "Video")
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        try
                        {
                            UpdateVideoGrid(currentBucket);
                            VideoGrid.Visibility = Visibility.Visible;
                        }
                        catch (Exception ex)
                        {
                            show_error("Error updating UI: " + ex.Message);
                            logHelper.Info(logger, ex.ToString());
                        }

                    });
                }

                // ���ؼ��ؽ�����
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


    public class GroupInfoList_Video : List<VideoInfo>, INotifyPropertyChanged
    {
        public string Key { get; set; }

        public List<VideoInfo> VideoInfos { get; set; }
        public int VideoCount => this.Count; // �������ԣ�������Ƭ����

        public int SelectedCount => this.Count(item => item.IsSelected);
        public event PropertyChangedEventHandler PropertyChanged;


        public GroupInfoList_Video()
        {
            VideoInfos = new List<VideoInfo>();
        }
        public void AddRange(IEnumerable<VideoInfo> collection)
        {
            foreach (var item in collection)
            {
                item.PropertyChanged += Item_PropertyChanged;
                base.Add(item);
            }
            OnPropertyChanged(nameof(VideoCount));
            OnPropertyChanged(nameof(SelectedCount));
        }

        private void Item_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(VideoInfo.IsSelected))
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
            return this.All(Video => Video.IsSelected);
        }

        public void SetAllSelected(bool selected)
        {
            foreach (var Video in this)
            {
                Video.IsSelected = selected;
            }
        }
    }
}
