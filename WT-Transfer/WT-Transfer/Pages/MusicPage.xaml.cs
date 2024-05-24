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
            this.InitializeComponent();

            this.Loaded += LoadingPage_Loaded;

            buttons.Add(ListButton);
            buttons.Add(SingerButton);
            buttons.Add(AlbumButton);

            this.DataContext = this;
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
            dataGrid.Visibility = Visibility.Collapsed;
            musicListRepeater.ItemsSource = Musics;
            musicListRepeater.Visibility = Visibility.Visible;
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

                            //Implement grouping through LINQ queries
                            var query = from item in list
                                        group item by item.singer into g
                                        select new { GroupName = g.Key, Items = g };
                            var query2 = from item in list
                                         group item by item.album into g
                                         select new { GroupName = g.Key, Items = g };

                            // ���������֮ǰ�������Щ����
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                MusicsByCreater.Clear();
                                MusicsByAlbum.Clear();
                            });

                            foreach (var g in query)
                            {
                                GroupInfoCollection<MusicInfo> info = new GroupInfoCollection<MusicInfo>();
                                info.Key = g.GroupName;
                                foreach (var item in g.Items)
                                {
                                    info.Add(item);
                                }
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    MusicsByCreater.Add(info);
                                });
                            }
                            foreach (var g in query2)
                            {
                                GroupInfoCollection<MusicInfo> info = new GroupInfoCollection<MusicInfo>();
                                info.Key = g.GroupName;
                                foreach (var item in g.Items)
                                {
                                    info.Add(item);
                                }
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    MusicsByAlbum.Add(info);
                                });
                            }

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                groupedItems.IsSourceGrouped = true;
                                groupedItems.Source = MusicsByCreater;
                                dataGrid.ItemsSource = groupedItems.View;
                                progressRing.Visibility = Visibility.Collapsed;
                                dataGrid.Visibility = Visibility.Collapsed;
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
                foreach (var file in fileNames)
                {
                    // ��·�����õ��ļ�����
                    string fileName = Path.GetFileName(file);
                    string command = "push \"" + file + "\"" + " \"" + "/sdcard/Music/" + fileName + "\"";
                    string res = adbHelper.cmdExecuteWithAdbExit(command) + "\n";


                    /**
                     * 
                     * 
                    MusicInfo musicInfo = new MusicInfo();
                    musicInfo.title = Path.GetFileName(file);
                    musicInfo.fileName = Path.GetFileName(file);

                    Musics.Add(musicInfo);


                    progressRing.Visibility = Visibility.Collapsed;
                    dataGrid.Visibility = Visibility.Collapsed;
                    musicList.ItemsSource = Musics;
                    musicList.Visibility = Visibility.Visible;
                     */


                    // ��������
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

            dataGrid1.ItemsSource = groupedItems2.View;
            dataGrid.Visibility = Visibility.Collapsed;
            dataGrid1.Visibility = Visibility.Visible;
            musicListRepeater.Visibility = Visibility.Collapsed;
        }
        
        //Album����
        private void dataGrid1_LoadingRowGroup(object sender, CommunityToolkit.WinUI.UI.Controls.DataGridRowGroupHeaderEventArgs e)
        {

            ICollectionViewGroup group = e.RowGroupHeader.CollectionViewGroup;
            MusicInfo item = group.GroupItems[0] as MusicInfo;
            e.RowGroupHeader.PropertyValue = item.album;
        }

        //Singer��ť
        private void GroupBySinger_Click(object sender, RoutedEventArgs e)
        {
            // �����а�ť����Ϊδѡ��
            foreach (var btn in buttons)
            {
                VisualStateManager.GoToState(btn, "Unselected", true);
            }

            // ������İ�ť����Ϊѡ��
            var button = (Button)sender;
            VisualStateManager.GoToState(button, "Selected", true);



            CollectionViewSource groupedItems = new CollectionViewSource();
            groupedItems.IsSourceGrouped = true;
            groupedItems.Source = MusicsByCreater;


            dataGrid1.ItemsSource = groupedItems.View;
            dataGrid1.Visibility = Visibility.Collapsed;
            dataGrid.Visibility = Visibility.Visible;
            musicListRepeater.Visibility = Visibility.Collapsed;


        }

        //List��ť
        private void ListButton_Click(object sender, RoutedEventArgs e)
        {
            // �����а�ť����Ϊδѡ��
            foreach (var btn in buttons)
            {
                VisualStateManager.GoToState(btn, "Unselected", true);
            }

            // ������İ�ť����Ϊѡ��
            var button = (Button)sender;
            VisualStateManager.GoToState(button, "Selected", true);

            dataGrid1.Visibility = Visibility.Collapsed;
            dataGrid.Visibility = Visibility.Collapsed;
            musicListRepeater.Visibility = Visibility.Visible;
            musicListRepeater.ItemsSource = Musics;
        }

        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            // ����Ƿ���ѡ�е�������
            var selectedMusics = Musics.Where(m => m.IsSelected).ToList();
            if (!selectedMusics.Any())
            {
                await ShowMessageDialog("No music selected", "Please select at least one music item to delete.");
                return;
            }

            // ȷ��ɾ��
            bool isConfirmed = await ShowConfirmationDialog("Confirm Deletion", $"Are you sure you want to delete {selectedMusics.Count} selected item(s)?");
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
            request.operation = "insert";

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
                Content = "Music successfully deleted",
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

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            UpdateSelectedFilesInfo();
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            UpdateSelectedFilesInfo();
        }

        //����ѡ�е��ļ���Ϣ
        private void UpdateSelectedFilesInfo()
        {
            int selectedCount = Musics.Count(m => m.IsSelected); // �������MusicInfo����һ��IsSelected����
                                                                 // ����ѡ���ļ����ܴ�С
            double totalSelectedSize = Musics
                .Where(m => m.IsSelected && !string.IsNullOrEmpty(m.size))
                .Sum(m => ExtractSizeInMB(m.size));

            double totalSize = Musics.Sum(m => ExtractSizeInMB(m.size));

            // ����TextBlock���ı�
            SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - {totalSelectedSize:F2}MB of {totalSize:F2}MB";
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
            SelectedFilesInfo.Text = $"{selectedCount} of {Musics.Count} Item(s) Selected - 0MB of {totalSize:F2}MB";
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
            ListButton_Click(ListButton, new RoutedEventArgs()); // ģ���� ListButton
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

                // ���������б���ʾ
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
                    // ���ؽ��Ȼ�
                    _progressRing.IsActive = false;
                    progressRing.Visibility = Visibility.Collapsed;
                });
            }
        }

        private StackPanel _previousSelectedPanel;

        private void OnItemClicked(object sender, PointerRoutedEventArgs e)
        {
            var panel = sender as StackPanel;

            if (_previousSelectedPanel != null)
            {
                _previousSelectedPanel.ClearValue(StackPanel.StyleProperty);
            }

            if (panel != null)
            {
                panel.Style = (Style)Application.Current.Resources["SelectedItemStyle"];
                _previousSelectedPanel = panel;
            }
        }

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
            foreach (var music in Musics)
            {
                music.IsSelected = isSelected;
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


    }
}
