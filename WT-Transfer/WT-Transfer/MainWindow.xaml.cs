// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using MathNet.Numerics.RootFinding;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using WT_Transfer.Helper;
using WT_Transfer.Models;
using WT_Transfer.Pages;
using NLog;
using Microsoft.Extensions.Logging;
using LogLevel = NLog.LogLevel;
using System.Threading;
using Windows.ApplicationModel.Core;
using Windows.UI.Core;
using Windows.Media.Protection.PlayReady;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using System.Security;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml;
using Windows.UI.ViewManagement;
using Windows.UI;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public static IntPtr WindowHandle { get; private set; }
        public static string win = "";//版本号
        public static string savePath;  //保存路径
        public static string name;      //文件名
        public static string currentPage = "";
        public static string BackupPath;      //文件备份路径
        public static String Setting_BackupPath; // 配置文件中的存储 文件备份路径 的地址
        public static string ContactBackupPath;      //联系人备份路径
        public static String Setting_ContactBackupPath; // 配置文件中的存储 联系人备份路径 的地址
        public static string MusicBackupPath;      //音乐备份路径
        public static String Setting_MusicBackupPath; // 配置文件中的存储 音乐备份路径 的地址
        public static string PhotoBackupPath;      //照片备份路径
        public static String Setting_PhotoBackupPath; // 配置文件中的存储 照片备份路径 的地址v
        public static string SmsBackupPath;      //短信备份路径
        public static String Setting_SmsBackupPath; // 配置文件中的存储 短信备份路径 的地址
        public static string RootDirBackUpPath;      //根路径 备份路径
        public static String Setting_RootBackupPath; // 配置文件中的存储 根路径  的地址
        public static string AutoBackupPath =
            GuideWindow.generatePrefix().ToString() + "-" + "AutoBackupPath";     //短信备份路径
        public static bool AutoBackup; // 配置文件中的存储 短信备份路径 的地址

        // 配置文件中的存储 已选中文件Map 的地址
        public static String SelectedFileMap_BackupPath
            = GuideWindow.generatePrefix().ToString() + "-" + "SelectedFileMap";
        public static String SelectedFileSet_BackupPath
            = GuideWindow.generatePrefix().ToString() + "-" + "SelectedFileSet";

        // 备份图片的日期
        public static string PhotoBackUpDate_BackupPath
            = GuideWindow.generatePrefix().ToString() + "-" + "PhotoBackUpDate";

        // 备份的频率
        public static string BackupHour_Path
            = GuideWindow.generatePrefix().ToString() + "-" + "Backup_hour";
        public static string BackupMinute_Path
            = GuideWindow.generatePrefix().ToString() + "-" + "Backup_minute";
        public static int BackupHour = 0;
        public static int BackupMinute = 0;

        // 还原模式
        public static string RestoreMode
            = GuideWindow.generatePrefix().ToString() + "-" + "RestoreMode";

        // 日志文件
        public static FileTarget fileTarget;
        public static char[] Permissions = new char[] {
            '0',
            '0',
            '0',
            '0',
            '0'
        };

        // 联系人等页面的数据，存取到一块，之后用的时候直接用，不要每次进页面都要重新解析
        public static ICollection<PhoneNumberRecord> Calls { get; set; }
        public static ObservableCollection<ContactShow> Contacts { get; set; }
        public static ICollection<PhoneNumberSmsRecord> Smss { get; set; }
        public static ICollection<DateSmsRecord> SmssByDate { get; set; }
        public static ICollection<Calendar> Calendars { get; set; }
        public static Dictionary<DateTime, List<Calendar>> CalendarDic { get; set; }
        public static List<CalendarByDate> calendarByDates { get; set; }
        public static List<PhotoInfo> Photos { get; set; }
        public static List<PhotoInfo> PhotosSorted { get; set; }
        public static Dictionary<string, List<PhotoInfo>> PhotosInBucket { get; set; }
        public static HashSet<String> buckets { get; set; }
        public static ObservableCollection<MusicInfo> Musics { get; set; }
        public static ObservableCollection<MusicInfoGroup> MusicsByCreater
            = new ObservableCollection<MusicInfoGroup>();
        public static ObservableCollection<GroupInfoCollection<MusicInfo>> MusicsByAlbum
            = new ObservableCollection<GroupInfoCollection<MusicInfo>>();

        //正在备份的模块
        public static bool contact_isRuning = false;
        public static bool music_isRuning = false;
        public static bool calendar_isRuning = false;
        public static bool sms_isRuning = false;
        public static bool photo_isRuning = false;
        public static bool calllog_isRuning = false;

        public static Socket client_socket;

        AdbHelper adbHelper = new AdbHelper();
        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        SocketHelper socketHelper = new SocketHelper();
        CheckUsbHelper checkUsbHelper = GuideWindow.checkUsbHelper;

        public MainWindow()
        {
            try
            {
                checkUsbHelper.startThread();

                this.Activated += MainWindow_Activated;

                // 设置标题栏颜色
                // CustomizeTitleBar();

                // 启动窗口时，初始化页面，为了启动定时器
                //FileTransfer fileTransfer = new FileTransfer();

                //设置标题栏图标
                WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WindowId id = Win32Interop.GetWindowIdFromWindow(WindowHandle);
                AppWindow appWindow = AppWindow.GetFromWindowId(id);
                string iconpath = Path.Combine(Package.Current.InstalledLocation.Path, "app.ico");
                appWindow.SetIcon(iconpath);
                //设置标题栏文字
                Title = "    ALL Droid File Transfer Pro";

                this.InitializeComponent();



                WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
                Setting_BackupPath = GuideWindow.generatePrefix().ToString() + "-" + "BackupPath";

                //注册手机信息
                //ApplicationData.Current.LocalSettings.Values[GuideWindow.generatePrefix()] = null;
                object phoneInfo = ApplicationData.Current.LocalSettings.Values[GuideWindow.generatePrefix()];

                Setting_ContactBackupPath = GuideWindow.generatePrefix().ToString() + "-" + "ContactBackupPath";
                Setting_MusicBackupPath = GuideWindow.generatePrefix().ToString() + "-" + "MusicBackupPath";
                Setting_PhotoBackupPath = GuideWindow.generatePrefix().ToString() + "-" + "PhotoBackupPath";
                Setting_SmsBackupPath = GuideWindow.generatePrefix().ToString() + "-" + "SmsBackupPath";
                Setting_RootBackupPath = GuideWindow.generatePrefix().ToString() + "-" + "RootDirBackUpPath";

                //phoneInfo = null;
                // 第一次连接手机，加入配置
                if (phoneInfo == null)
                {
                    ApplicationData.Current.LocalSettings.Values[GuideWindow.generatePrefix()] = "true";
                    

                    string path = GuideWindow.localPath + @"\" + GuideWindow.generatePrefix();

                    // 创建目录
                    Directory.CreateDirectory(path + @"\file");
                    Directory.CreateDirectory(path + @"\music");
                    Directory.CreateDirectory(path + @"\contact");
                    Directory.CreateDirectory(path + @"\photo");
                    Directory.CreateDirectory(path + @"\sms");
                    //加入配置
                    ApplicationData.Current.LocalSettings.Values[Setting_RootBackupPath] = path;
                    ApplicationData.Current.LocalSettings.Values[Setting_BackupPath] = path + @"\file";
                    ApplicationData.Current.LocalSettings.Values[Setting_MusicBackupPath] = path + @"\music";
                    ApplicationData.Current.LocalSettings.Values[Setting_ContactBackupPath] = path + @"\contact";
                    ApplicationData.Current.LocalSettings.Values[Setting_PhotoBackupPath] = path + @"\photo";
                    ApplicationData.Current.LocalSettings.Values[Setting_SmsBackupPath] = path + @"\sms";
                    // 设置哪些根目录被占用，防止重复占用
                    ApplicationData.Current.LocalSettings.Values[path] = true;


                    ApplicationData.Current.LocalSettings.Values[AutoBackupPath] = false;
                    AutoBackup = false;

                    ApplicationData.Current.LocalSettings.Values[BackupHour_Path] = "0";
                    ApplicationData.Current.LocalSettings.Values[BackupMinute_Path] = "0";

                    //还原模式
                    ApplicationData.Current.LocalSettings.Values[SettingPage.FileRestoreModePath] = "Incre_restore";
                    ApplicationData.Current.LocalSettings.Values[SettingPage.ContactRestoreModePath] = "Incre_restore";
                    ApplicationData.Current.LocalSettings.Values[SettingPage.SmsRestoreModePath] = "Incre_restore";
                }

                //获取配置中的配置路径
                RootDirBackUpPath = (string)ApplicationData.Current.LocalSettings.Values[Setting_RootBackupPath];
                BackupPath = (string)ApplicationData.Current.LocalSettings.Values[Setting_BackupPath];
                ContactBackupPath = (string)ApplicationData.Current.LocalSettings.Values[Setting_ContactBackupPath];
                MusicBackupPath = (string)ApplicationData.Current.LocalSettings.Values[Setting_MusicBackupPath];
                PhotoBackupPath = (string)ApplicationData.Current.LocalSettings.Values[Setting_PhotoBackupPath];
                SmsBackupPath = (string)ApplicationData.Current.LocalSettings.Values[Setting_SmsBackupPath];
                AutoBackup = (bool)ApplicationData.Current.LocalSettings.Values[AutoBackupPath];

                //日志初始化
                fileTarget = (FileTarget)NLog.LogManager.Configuration.FindTargetByName("logfile");

                //socket初始化连接
                client_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress ipAddress = IPAddress.Parse("127.0.0.1");

                IPEndPoint ipEndpoint = new IPEndPoint(ipAddress, 8888);

                client_socket.Connect(ipEndpoint);

                //logHelper.Info(logger, "ceshi helper");

                // 查看手机权限
                SocketModels.Result result = socketHelper.getResult("permissions","query");
                Permissions = result.path.ToCharArray();
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        

        private void MainWindow_Activated(object sender, Microsoft.UI.Xaml.WindowActivatedEventArgs args)
        {
            // 仅在首次激活窗口时执行
            if (args.WindowActivationState != WindowActivationState.Deactivated)
            {
                var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
                var textBlock = new TextBlock { Text = GuideWindow.device.Model };
                
                stackPanel.Children.Add(textBlock);

                phoneViewItem.Content = stackPanel;
            }
        }



        private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            try
            {
                var selectedItem = (NavigationViewItem)args.SelectedItem;
                if ((string)selectedItem.Tag == "PhoneInfo") contentFrame.Navigate(typeof(PhoneInfo));
                else if ((string)selectedItem.Tag == "FileTransfer")
                {
                    contentFrame.Navigate(typeof(FileTransfer));
                    currentPage = "FileTransfer";
                }
                else if ((string)selectedItem.Tag == "Contact")
                {
                    contentFrame.Navigate(typeof(ContactPage));
                    currentPage = "Contact";
                }
                else if ((string)selectedItem.Tag == "CallLog") contentFrame.Navigate(typeof(CallLog));
                else if ((string)selectedItem.Tag == "Sms")
                {
                    contentFrame.Navigate(typeof(Sms));
                    currentPage = "Sms";
                }
                else if ((string)selectedItem.Tag == "Calendar") contentFrame.Navigate(typeof(CalendarPage));
                else if ((string)selectedItem.Tag == "OperationLog") contentFrame.Navigate(typeof(OperationLog));
                else if ((string)selectedItem.Tag == "SettingPage") contentFrame.Navigate(typeof(SettingPage));
                else if ((string)selectedItem.Tag == "Music") contentFrame.Navigate(typeof(MusicPage));
                else if ((string)selectedItem.Tag == "Photo") contentFrame.Navigate(typeof(PhotoPage));
                else if ((string)selectedItem.Tag == "Apps") contentFrame.Navigate(typeof(AppPage));
                else if ((string)selectedItem.Tag == "BackupSettings") contentFrame.Navigate(typeof(BackupSetting));
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
