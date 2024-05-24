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
        public static string win = "";//�汾��
        public static string savePath;  //����·��
        public static string name;      //�ļ���
        public static string currentPage = "";
        public static string BackupPath;      //�ļ�����·��
        public static String Setting_BackupPath; // �����ļ��еĴ洢 �ļ�����·�� �ĵ�ַ
        public static string ContactBackupPath;      //��ϵ�˱���·��
        public static String Setting_ContactBackupPath; // �����ļ��еĴ洢 ��ϵ�˱���·�� �ĵ�ַ
        public static string MusicBackupPath;      //���ֱ���·��
        public static String Setting_MusicBackupPath; // �����ļ��еĴ洢 ���ֱ���·�� �ĵ�ַ
        public static string PhotoBackupPath;      //��Ƭ����·��
        public static String Setting_PhotoBackupPath; // �����ļ��еĴ洢 ��Ƭ����·�� �ĵ�ַv
        public static string SmsBackupPath;      //���ű���·��
        public static String Setting_SmsBackupPath; // �����ļ��еĴ洢 ���ű���·�� �ĵ�ַ
        public static string RootDirBackUpPath;      //��·�� ����·��
        public static String Setting_RootBackupPath; // �����ļ��еĴ洢 ��·��  �ĵ�ַ
        public static string AutoBackupPath =
            GuideWindow.generatePrefix().ToString() + "-" + "AutoBackupPath";     //���ű���·��
        public static bool AutoBackup; // �����ļ��еĴ洢 ���ű���·�� �ĵ�ַ

        // �����ļ��еĴ洢 ��ѡ���ļ�Map �ĵ�ַ
        public static String SelectedFileMap_BackupPath
            = GuideWindow.generatePrefix().ToString() + "-" + "SelectedFileMap";
        public static String SelectedFileSet_BackupPath
            = GuideWindow.generatePrefix().ToString() + "-" + "SelectedFileSet";

        // ����ͼƬ������
        public static string PhotoBackUpDate_BackupPath
            = GuideWindow.generatePrefix().ToString() + "-" + "PhotoBackUpDate";

        // ���ݵ�Ƶ��
        public static string BackupHour_Path
            = GuideWindow.generatePrefix().ToString() + "-" + "Backup_hour";
        public static string BackupMinute_Path
            = GuideWindow.generatePrefix().ToString() + "-" + "Backup_minute";
        public static int BackupHour = 0;
        public static int BackupMinute = 0;

        // ��ԭģʽ
        public static string RestoreMode
            = GuideWindow.generatePrefix().ToString() + "-" + "RestoreMode";

        // ��־�ļ�
        public static FileTarget fileTarget;
        public static char[] Permissions = new char[] {
            '0',
            '0',
            '0',
            '0',
            '0'
        };

        // ��ϵ�˵�ҳ������ݣ���ȡ��һ�飬֮���õ�ʱ��ֱ���ã���Ҫÿ�ν�ҳ�涼Ҫ���½���
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

        //���ڱ��ݵ�ģ��
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

                // ���ñ�������ɫ
                // CustomizeTitleBar();

                // ��������ʱ����ʼ��ҳ�棬Ϊ��������ʱ��
                //FileTransfer fileTransfer = new FileTransfer();

                //���ñ�����ͼ��
                WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WindowId id = Win32Interop.GetWindowIdFromWindow(WindowHandle);
                AppWindow appWindow = AppWindow.GetFromWindowId(id);
                string iconpath = Path.Combine(Package.Current.InstalledLocation.Path, "app.ico");
                appWindow.SetIcon(iconpath);
                //���ñ���������
                Title = "    ALL Droid File Transfer Pro";

                this.InitializeComponent();



                WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
                Setting_BackupPath = GuideWindow.generatePrefix().ToString() + "-" + "BackupPath";

                //ע���ֻ���Ϣ
                //ApplicationData.Current.LocalSettings.Values[GuideWindow.generatePrefix()] = null;
                object phoneInfo = ApplicationData.Current.LocalSettings.Values[GuideWindow.generatePrefix()];

                Setting_ContactBackupPath = GuideWindow.generatePrefix().ToString() + "-" + "ContactBackupPath";
                Setting_MusicBackupPath = GuideWindow.generatePrefix().ToString() + "-" + "MusicBackupPath";
                Setting_PhotoBackupPath = GuideWindow.generatePrefix().ToString() + "-" + "PhotoBackupPath";
                Setting_SmsBackupPath = GuideWindow.generatePrefix().ToString() + "-" + "SmsBackupPath";
                Setting_RootBackupPath = GuideWindow.generatePrefix().ToString() + "-" + "RootDirBackUpPath";

                //phoneInfo = null;
                // ��һ�������ֻ�����������
                if (phoneInfo == null)
                {
                    ApplicationData.Current.LocalSettings.Values[GuideWindow.generatePrefix()] = "true";
                    

                    string path = GuideWindow.localPath + @"\" + GuideWindow.generatePrefix();

                    // ����Ŀ¼
                    Directory.CreateDirectory(path + @"\file");
                    Directory.CreateDirectory(path + @"\music");
                    Directory.CreateDirectory(path + @"\contact");
                    Directory.CreateDirectory(path + @"\photo");
                    Directory.CreateDirectory(path + @"\sms");
                    //��������
                    ApplicationData.Current.LocalSettings.Values[Setting_RootBackupPath] = path;
                    ApplicationData.Current.LocalSettings.Values[Setting_BackupPath] = path + @"\file";
                    ApplicationData.Current.LocalSettings.Values[Setting_MusicBackupPath] = path + @"\music";
                    ApplicationData.Current.LocalSettings.Values[Setting_ContactBackupPath] = path + @"\contact";
                    ApplicationData.Current.LocalSettings.Values[Setting_PhotoBackupPath] = path + @"\photo";
                    ApplicationData.Current.LocalSettings.Values[Setting_SmsBackupPath] = path + @"\sms";
                    // ������Щ��Ŀ¼��ռ�ã���ֹ�ظ�ռ��
                    ApplicationData.Current.LocalSettings.Values[path] = true;


                    ApplicationData.Current.LocalSettings.Values[AutoBackupPath] = false;
                    AutoBackup = false;

                    ApplicationData.Current.LocalSettings.Values[BackupHour_Path] = "0";
                    ApplicationData.Current.LocalSettings.Values[BackupMinute_Path] = "0";

                    //��ԭģʽ
                    ApplicationData.Current.LocalSettings.Values[SettingPage.FileRestoreModePath] = "Incre_restore";
                    ApplicationData.Current.LocalSettings.Values[SettingPage.ContactRestoreModePath] = "Incre_restore";
                    ApplicationData.Current.LocalSettings.Values[SettingPage.SmsRestoreModePath] = "Incre_restore";
                }

                //��ȡ�����е�����·��
                RootDirBackUpPath = (string)ApplicationData.Current.LocalSettings.Values[Setting_RootBackupPath];
                BackupPath = (string)ApplicationData.Current.LocalSettings.Values[Setting_BackupPath];
                ContactBackupPath = (string)ApplicationData.Current.LocalSettings.Values[Setting_ContactBackupPath];
                MusicBackupPath = (string)ApplicationData.Current.LocalSettings.Values[Setting_MusicBackupPath];
                PhotoBackupPath = (string)ApplicationData.Current.LocalSettings.Values[Setting_PhotoBackupPath];
                SmsBackupPath = (string)ApplicationData.Current.LocalSettings.Values[Setting_SmsBackupPath];
                AutoBackup = (bool)ApplicationData.Current.LocalSettings.Values[AutoBackupPath];

                //��־��ʼ��
                fileTarget = (FileTarget)NLog.LogManager.Configuration.FindTargetByName("logfile");

                //socket��ʼ������
                client_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress ipAddress = IPAddress.Parse("127.0.0.1");

                IPEndPoint ipEndpoint = new IPEndPoint(ipAddress, 8888);

                client_socket.Connect(ipEndpoint);

                //logHelper.Info(logger, "ceshi helper");

                // �鿴�ֻ�Ȩ��
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
            // �����״μ����ʱִ��
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
