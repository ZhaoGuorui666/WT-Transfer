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
                this.SearchBox.TextChanged += SearchBox_TextChanged; // �������

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
                ListButton_Click(ListButton, new RoutedEventArgs()); // ģ���� ListButton

                if (this.Musics == null || this.Musics.Count == 0)
                {
                    if (MainWindow.Musics == null || MainWindow.Musics.Count == 0)
                    {
                        // ���г�ʼ������������������ݲ���ֵ�� calls
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

                // �ڼ����߼�֮���ʼ��ѡ���ļ���Ϣ
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
            artistRepeater.Visibility = Visibility.Collapsed; // ��ʼ��ʱ����
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

                            // ���������֮ǰ�������Щ����
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

                                //���ո��ַ��࣬ʹ��TreeView��Ⱦ
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
                        // ���ɹ�
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
                            // ���ɹ�
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


        //ͬ������
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
            // ����������ͼƬ���߼�
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
                        // �����������ֵ��߼�
                        musicsToExport = Musics.ToList();
                    }
                    else
                    {
                        // ����ѡ�����ֵ��߼�
                        musicsToExport = selectedMusics ?? new List<MusicInfo>();
                    }

                    // ��������ʾContentDialog
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

                    // ��ʾ���ȶԻ���
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

                    // �ر�ContentDialog
                    progressDialog.Hide();

                    ContentDialog exportDialog = new ContentDialog
                    {
                        Title = "Export Complete",
                        Content = "Your music has been successfully exported to the designated folder.",
                        PrimaryButtonText = "View Folder",
                        SecondaryButtonText = "OK",
                    };
                    exportDialog.XamlRoot = this.Content.XamlRoot;
                    // ���ļ��еĲ���
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
                        // �����������ֵ��߼�
                        musicsToExport = Musics.ToList();
                    }
                    else
                    {
                        // ����ѡ�����ֵ��߼�
                        musicsToExport = Musics.Where(music => music.IsSelected).ToList();
                    }
                    // ��������ʾContentDialog
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

                    // ��ʾ���ȶԻ���
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

                    // �ر�ContentDialog
                    progressDialog.Hide();

                    ContentDialog exportDialog = new ContentDialog
                    {
                        Title = "Export Complete",
                        Content = "Your music has been successfully exported to the designated folder.",
                        PrimaryButtonText = "View Folder",
                        SecondaryButtonText = "OK",
                        DefaultButton = ContentDialogButton.Secondary // ����OKΪĬ�ϰ�ť
                    };
                    exportDialog.XamlRoot = this.Content.XamlRoot;
                    // ���ļ��еĲ���
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


        //���ֻ��д�������
        private async void PushMusic_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                var hwnd = MainWindow.WindowHandle;
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.FileTypeFilter.Add("*");
                // ѡ�����ļ�
                var files = await picker.PickMultipleFilesAsync();
                // ��ȡ�ļ�·��
                List<string> fileNames = files.Select(file => file.Path).ToList<string>();

                if (fileNames.Count == 0)
                {
                    return;
                }


                List<String> paths = new List<string>();
                foreach (var file in fileNames)
                {
                    // ��·�����õ��ļ�����
                    string fileName = Path.GetFileName(file);
                    string command = "push \"" + file + "\"" + " \"" + "/sdcard/Music/" + fileName + "\"";
                    string res = adbHelper.cmdExecuteWithAdbExit(command) + "\n";

                    paths.Add("/sdcard/Music/" + fileName);
                    
                }

                // �����ֻ� �������֣�ɨ��
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
                ListButton_Click(ListButton, new RoutedEventArgs()); // ģ���� ListButton
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //singer����
        private void dataGrid_LoadingRowGroup(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridRowGroupHeaderEventArgs e)
        {
            ICollectionViewGroup group = e.RowGroupHeader.CollectionViewGroup;
            MusicInfo item = group.GroupItems[0] as MusicInfo;
            e.RowGroupHeader.PropertyValue = item.singer;
        }

        //Album��ť
        private void GroupByAlbum_Click(object sender, RoutedEventArgs e)
        {
            currentState = ViewState.Album;
            // �����а�ť����Ϊδѡ��
            foreach (var btn in buttons)
            {
                VisualStateManager.GoToState(btn, "Unselected", true);
            }

            // ������İ�ť����Ϊѡ��
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
        

        //Singer��ť
        private void GroupBySinger_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                currentState = ViewState.Artist;
                // �����а�ť����Ϊδѡ��
                foreach (var btn in buttons)
                {
                    VisualStateManager.GoToState(btn, "Unselected", true);
                }

                // ������İ�ť����Ϊѡ��
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

        //List��ť
        private void ListButton_Click(object sender, RoutedEventArgs e)
        {

            currentState = ViewState.List;
            // �����а�ť����Ϊδѡ��
            foreach (var btn in buttons)
            {
                VisualStateManager.GoToState(btn, "Unselected", true);
            }

            // ������İ�ť����Ϊѡ��
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
            // ����Ƿ���ѡ�е�������
            var selectedMusics = Musics.Where(m => m.IsSelected).ToList();
            if (!selectedMusics.Any())
            {
                await ShowMessageDialog("No music selected", "No music files have been selected for export.\r\nPlease select the music files you want to export and try again.");
                return;
            }

            // ȷ��ɾ��
            bool isConfirmed = await ShowConfirmationDialog("Confirm Deletion", $"Are you sure you want to delete the selected music? This action cannot be undone.");
            if (!isConfirmed)
            {
                return;
            }

            // ִ��ɾ������
            foreach (var music in selectedMusics)
            {
                // ɾ�������ļ���ʾ���߼�������Ҫ���ݾ������ʵ��ɾ���߼�
                adbHelper.delFromPath(music.fileUrl);
                Musics.Remove(music);
            }


            // ����UI
            // ɾ������
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

            
            // ˢ��ѡ��״̬
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
            ListButton_Click(ListButton, new RoutedEventArgs()); // ģ���� ListButton
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


        //����ѡ�е��ļ���Ϣ
        private void UpdateSelectedFilesInfo()
        {
            if(musicListRepeater.Visibility == Visibility)
            {
                int selectedCount = Musics.Count(m => m.IsSelected); // �������MusicInfo����һ��IsSelected����
                                                                     // ����ѡ���ļ����ܴ�С
                double totalSelectedSize = Musics
                    .Where(m => m.IsSelected && !string.IsNullOrEmpty(m.size))
                    .Sum(m => ExtractSizeInMB(m.size));

                double totalSize = Musics.Sum(m => ExtractSizeInMB(m.size));

                // ����TextBlock���ı�
                SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - {totalSelectedSize:F2} MB of {totalSize:F2} MB";
            }
            else if(artistRepeater.Visibility == Visibility)
            {
                // ��ȡ����ѡ�еĽڵ�
                IList<TreeViewNode> selectedNodes = artistRepeater.SelectedNodes;
                IList<TreeViewNode> filteredNodes = selectedNodes.Where(node => !node.HasChildren).ToList();

                int selectedCount = filteredNodes.Count;
                double totalSelectedSize = filteredNodes
                    .Sum(m => ExtractSizeInMB(((MusicInfo)m.Content).size));


                double totalSize = Musics.Sum(m => ExtractSizeInMB(m.size));

                // ����TextBlock���ı�
                SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - {totalSelectedSize:F2} MB of {totalSize:F2} MB";
            }
            else if (albumRepeater.Visibility == Visibility)
            {
                // ��ȡ����ѡ�еĽڵ�
                IList<TreeViewNode> selectedNodes = albumRepeater.SelectedNodes;
                IList<TreeViewNode> filteredNodes = selectedNodes.Where(node => !node.HasChildren).ToList();

                int selectedCount = filteredNodes.Count;
                double totalSelectedSize = filteredNodes
                    .Sum(m => ExtractSizeInMB(((MusicInfo)m.Content).size));


                double totalSize = Musics.Sum(m => ExtractSizeInMB(m.size));

                // ����TextBlock���ı�
                SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - {totalSelectedSize:F2} MB of {totalSize:F2} MB";
            }

        }

        //��ʼ��ѡ���ļ���Ϣ
        private void InitializeSelectedFilesInfo()
        {
            // ��ʼ��ʱ����û���ļ���ѡ��
            int selectedCount = 0;

            // ���������ļ����ܴ�С
            double totalSize = Musics
                .Where(m => !string.IsNullOrEmpty(m.size))
                .Sum(m => ExtractSizeInMB(m.size));

            // ����TextBlock���ı�
            SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - 0 MB of {totalSize:F2} MB";
        }


        private double ExtractSizeInMB(string sizeString)
        {
            // ���Դ�size�ַ������Ƴ�"M"��Ȼ��ת��ʣ��Ĳ���Ϊdouble
            var cleanSizeString = sizeString.TrimEnd('M');
            bool success = double.TryParse(cleanSizeString, out double sizeValue);
            return success ? sizeValue : 0; // ���ת��ʧ�ܣ�����0
        }

        private void MusicList_Sorting(object sender, DataGridColumnEventArgs e)
        {
            var column = e.Column;
            var header = column.Header.ToString();
            var direction = column.SortDirection;

            IEnumerable<MusicInfo> sortedItems = null;

            // ���ݵ������ͷȷ�����������
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

            // ����������
            column.SortDirection = direction == null || direction == DataGridSortDirection.Descending
                ? DataGridSortDirection.Ascending
                : DataGridSortDirection.Descending;

            // Ӧ��������
            musicListRepeater.ItemsSource = sortedItems.ToList();

            // ��������е�������
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

        //���ҿ� ��ť
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            PerformSearch();
        }

        private void PerformSearch()
        {
            // ��ȡ�û�����������ı�
            var searchText = SearchBox.Text.ToLower();

            // ������Ŀ�б�
            var filteredMusics = Musics.Where(m =>
                (!string.IsNullOrEmpty(m.fileName) && m.fileName.ToLower().Contains(searchText)) ||
                (!string.IsNullOrEmpty(m.singer) && m.singer.ToLower().Contains(searchText)) ||
                (!string.IsNullOrEmpty(m.album) && m.album.ToLower().Contains(searchText))
            ).ToList();

            // ���������������ʾ����
            musicListRepeater.ItemsSource = filteredMusics;

            // ��������״̬
            /**foreach (var column in musicList.Columns)
            {
                column.SortDirection = null;
            }*/
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshMusicList();
            //ListButton_Click(ListButton, new RoutedEventArgs()); // ģ���� ListButton
        }

        private async Task RefreshMusicList()
        {
            // ��ʾ���Ȼ���ʾ���ڼ���
            DispatcherQueue.TryEnqueue(() => {
                // ��ʾ���Ȼ���ʾ���ڼ���
                progressRing.Visibility = Visibility.Visible;
                _progressRing.IsActive = true;
            });

            try
            {
                // ���³�ʼ�������б�
                await Init();

                // ��ղ�������� artistRepeater �� albumRepeater
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

                // ���ֵ�ǰ��ͼ״̬
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
                    // ���ؽ��Ȼ�
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

        // �¼�������
        private void SelectAllCheckBox_Checked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            IsAllSelected = true;
        }

        private void SelectAllCheckBox_Unchecked(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            IsAllSelected = false;
        }

        // ʵ�� OnPropertyChanged ����
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

        //ѡ���ļ��У����ֵ���
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

            // ��ʾ���ȶԻ���
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

            // �����ֻ� �������֣�ɨ��
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
            ListButton_Click(ListButton, new RoutedEventArgs()); // ģ���� ListButton

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
        }

        //�����ͷ����
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

            // ��������е�����ͼ��
            ClearAllSortIcons();

            // ���ݵ������ͷȷ��Ҫ��ʾ��ͼ��
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
            // ��������е�����ͼ��
            ClearAllSortIcons();

            // ���ݵ������ͷȷ��Ҫ��ʾ��ͼ��
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
                // ���������Ϊ�գ�����ʾ��������
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


