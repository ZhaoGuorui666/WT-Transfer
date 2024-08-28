// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using AdvancedSharpAdbClient;
using Microsoft.UI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using NLog;
using NPOI.OpenXml4Net.OPC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Popups;
using WinRT.Interop;
using WT_Transfer.Dialog;
using WT_Transfer.Helper;
using WT_Transfer.Models;
using WT_Transfer.Pages;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class GuideWindow : Window
    {
        public static AdbServer server;
        public static DeviceData device;
        public static string DeviceName;
        public static AdbClient client;
        public MainWindow mainWindow;

        public static string serialno;
        public static string Serial;

        public static List<DeviceData> devices;
        public List<DeviceData> Devices { get; set; }
        public static string PackageName = "com.example.contacts";
        public static string localPath = ApplicationData.Current.LocalFolder.Path;

        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        AdbHelper adbHelper = new AdbHelper();
        public static CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        public static bool isCheckUsbRunning = false;
        public static bool isUsbConnected = true;

        public static Thread thread;
        public static Window currentWindow;

        public static bool isGuideWindow = true;

        public static int ApkVersion = 7;
        public static string SoftVersion = "1.0";

        public static bool disconnected = false;
        public static IntPtr WindowHandle { get; private set; }


        public DeviceData SelectedDevice { get; private set; }

        public GuideWindow()
        {

            try
            {
                this.InitializeComponent();

                AdbInit();

                //���ñ�����ͼ��
                WindowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WindowId id = Win32Interop.GetWindowIdFromWindow(WindowHandle);
                AppWindow appWindow = AppWindow.GetFromWindowId(id);
                string iconpath = Path.Combine(Package.Current.InstalledLocation.Path, "app.ico");
                appWindow.SetIcon(iconpath);
                //���ñ���������
                Title = "    ALL Droid File Transfer Pro";

                string appFolderPath = AppDomain.CurrentDomain.BaseDirectory;
                string DefaultPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(appFolderPath, "../images/test.png"));

                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.UriSource =
                    new Uri(DefaultPath, UriKind.RelativeOrAbsolute);

                //codeImg.Source = bitmapImage;

                //����汾
                //SoftwareVersion.Text = "Software version: "+ SoftVersion;
                //AppVersion.Text = "App version: 1."+ (ApkVersion - 1);
            }
            catch (Exception ex)
            {
                logHelper.Error(logger,ex.ToString());
                throw;
            }
        }

        
        private AppWindow GetAppWindowForCurrentWindow()
        {
            IntPtr hWnd = WindowNative.GetWindowHandle(this);
            WindowId myWndId = Win32Interop.GetWindowIdFromWindow(hWnd);
            return AppWindow.GetFromWindowId(myWndId);
        }

        private async void StartMainPage(object sender, RoutedEventArgs e) {
            try
            {
                //��ʼ���豸
                await initDevice();
                if (devices != null &&  devices.Count != 0 && device != null)
                {

                    isGuideWindow = false;
                    isUsbConnected = true;
                    string command, res;
                    Task<bool> task = CheckApp();
                    bool result = await task;
                    if (!result)
                    {
                        //֮ǰû��װ��app����ʾ�û�����װ��֮���ٴε��
                        return;
                    }

                    //adb forward tcp:8888 tcp:9999
                    command = "forward tcp:8888 tcp:9999";
                    res = adbHelper.cmdExecuteWithAdb(command);

                    InitApp();

                    //�����̼߳���������Ƿ�����
                    //if (!GuideWindow.isCheckUsbRunning)
                    //{
                    //    thread = new Thread(CheckUsbThread);
                    //    thread.Start();
                    //    GuideWindow.isCheckUsbRunning = true;
                    //}


                    // �����´���
                    mainWindow = new MainWindow();
                    currentWindow = mainWindow;
                    mainWindow.Activate();
                    this.Close();

                }
                else
                {
                    show_error("Please check if the device is connected or if USB debugging is enabled.");
                }
            }
            catch(Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger,ex.ToString());
                throw;
            }
        }

        //����app
        //����ǰ�ȹر�app
        private void InitApp()
        {
            try
            {
                string command = "shell am force-stop " + PackageName;
                string res = adbHelper.cmdExecuteWithAdb(command);

                command = "shell am start -n  com.example.contacts/.Communication";
                res = adbHelper.cmdExecuteWithAdb(command);
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //�豸�Ѿ����ӣ����App�Ƿ����
        private async Task<bool> CheckApp()
        {
            try
            {
                //string command = string.Format("shell pm list packages | find \"{0}\"", PackageName);
                string command = "shell pm list packages";
                string res = adbHelper.cmdExecuteWithAdb(command);

                if (!res.Contains(PackageName))
                {

                    // adbִ��installָ��
                    await Task.Run(async () =>
                    {

                        string appFolderPath = AppDomain.CurrentDomain.BaseDirectory;
                        string apkPath = System.IO.Path.GetFullPath(
                            System.IO.Path.Combine(appFolderPath, "../Apk/Contacts.apk"));

                        DispatcherQueue.TryEnqueue(async() =>{
                            AppInstall.ShowAsync();
                        });

                        //��װָ��
                        string res = adbHelper.cmdExecuteWithAdb("install \"" + apkPath + "\"");

                        DispatcherQueue.TryEnqueue(async () =>
                        {
                            AppInstall.Hide();
                            AppInstallDialog.ShowAsync();
                        });
                    });

                    command = "shell pm list packages";
                    res = adbHelper.cmdExecuteWithAdb(command);
                    bool v = res.Contains(PackageName);

                    return false;
                }
                else
                {
                    // �жϰ汾�Ƿ����ϰ汾

                    // 1. ��ȡ�ֻ��ϰ汾
                    command = string.Format("shell dumpsys package {0} | grep versionCode", PackageName);
                    string str = adbHelper.cmdExecuteWithAdb(command);

                    // ʹ�ÿո�ָ��ַ���
                    string[] splitString = str.Split(' ');

                    string phoneVersion = "";
                    // �����ָ����ַ����б��ҵ����� versionCode ���ַ���
                    foreach (string s in splitString)
                    {
                        if (s.Contains("versionCode"))
                        {
                            // ʹ��������ʽ���ַ�������ȡ����
                            phoneVersion = Regex.Match(s, @"\d+").Value;
                            break;
                        }
                    }
                    
                    if(int.Parse(phoneVersion) < ApkVersion)
                    {
                        await Task.Run(() =>
                        {
                            DispatcherQueue.TryEnqueue(async () => {
                                AppInstall.ShowAsync();
                            });

                            //��ɾ���ֻ���app
                            res = adbHelper.cmdExecuteWithAdbExit("uninstall " + PackageName);
                            //��װ�ֻ�app
                            string appFolderPath = AppDomain.CurrentDomain.BaseDirectory;
                            string apkPath = System.IO.Path.GetFullPath(
                                System.IO.Path.Combine(appFolderPath, "../Apk/Contacts.apk"));

                            res = adbHelper.cmdExecuteWithAdb("install \"" + apkPath + "\"");

                            //DispatcherQueue.TryEnqueue(() =>
                            //{
                            //    AppInstall.Hide();
                            //    ContentDialog appInfoDialog = new ContentDialog
                            //    {
                            //        Title = "Info",
                            //        Content = "Successfully installed the app.",
                            //        PrimaryButtonText = "OK",
                            //    };
                            //    appInfoDialog.XamlRoot = this.Content.XamlRoot;
                            //    appInfoDialog.ShowAsync();
                            //});
                        });
                    }
                    return true;
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
            try
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
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //����ǰ׺,��ʼ��
        public static String generatePrefix() {
            IShellOutputReceiver receiver = new ConsoleOutputReceiver();
            client.ExecuteRemoteCommand("getprop ro.product.manufacturer", device, receiver);
            String Manufacturer = receiver.ToString().Replace("\n", "").Replace("\r", "");
            client.ExecuteRemoteCommand("getprop ro.build.version.release", device, receiver);
            String Release = receiver.ToString().Replace("\n", "").Replace("\r", "");
            client.ExecuteRemoteCommand("getprop vendor.serialno", device, receiver);
            serialno = receiver.ToString().Replace("\n", "").Replace("\r", "");
            return Manufacturer + "-" + Release + "-" + serialno;
        }

        //Adb��ʼ��
        private static void AdbInit()
        {
            // ����adb
            bool isRunning = AdbServer.Instance.GetStatus().IsRunning;
            if (!AdbServer.Instance.GetStatus().IsRunning)
            {
                server = new AdbServer();
                string appFolderPath = AppDomain.CurrentDomain.BaseDirectory;
                string DefaultPath = Path.GetFullPath(Path.Combine(appFolderPath, "../platform-tools/adb.exe"));
                StartServerResult result = server.StartServer(DefaultPath, false);
                if (result != StartServerResult.Started)
                {
                    Console.WriteLine("Can't start adb server");
                }
            }
            Console.WriteLine(AdbServer.Instance.GetStatus().ToString());
            client = new AdbClient();
        }

        //��ֵdevice
        public async Task initDevice() {
            try
            {
                devices = client.GetDevices();
                this.Devices = devices;
                DeviceComboBox.ItemsSource = Devices;
                // ������豸��Ĭ��ѡ�е�һ��
                if (Devices != null && Devices.Count > 0)
                {
                    DeviceComboBox.SelectedIndex = 0;
                }
                await AndroidDeviceSelectionDialog.ShowAsync();
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        public void CheckUsbThread()
        {
            System.Threading.Timer timer = new System.Threading.Timer
                (CheckUsbOperation, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        }

        public void CheckUsbOperation(object state)
        {
            try
            {
                logHelper.Info(logger, "�����һ��USB״̬");
                //DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                //{
                //    show_error("�����һ��USB״̬");
                //});
                List<AdvancedSharpAdbClient.DeviceData> deviceDatas = GuideWindow.client.GetDevices();
                if (deviceDatas == null || deviceDatas.Count == 0)
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Run(() =>
                        {
                            // ���´���
                            currentWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                            {
                                isUsbConnected = false;

                                
                                GuideWindow guideWindow = new GuideWindow();
                                guideWindow.Activate();
                                currentWindow.Close();

                                currentWindow = guideWindow;
                            });
                        });
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

        private void HyperlinkButton_Click(object sender, RoutedEventArgs e)
        {
            USBDebuggWindow uSBDebuggWindow = new USBDebuggWindow();
            uSBDebuggWindow.Activate();
        }

        private void CloseAppInstallDialog(object sender, RoutedEventArgs e)
        {
            AppInstallDialog.Hide();
            AppInstall.Hide();
        }

        private void AndroidDeviceSelectionDialog_PrimaryButtonClick(object sender, RoutedEventArgs e)
        {
            SelectedDevice = DeviceComboBox.SelectedItem as DeviceData;
            if (SelectedDevice != null)
            {
                GuideWindow.device = SelectedDevice;
                GuideWindow.Serial = SelectedDevice.Serial;
                GuideWindow.DeviceName = SelectedDevice.Name.ToString();

                // ȷ������ȷ�رջ����ضԻ���
                AndroidDeviceSelectionDialog.Hide();
            }
            else
            {
                // ���û��ѡ���豸����ʾһ��������Ϣ��������Ӧ����
                show_error("No device selected. Please select a device.");
            }
        }


    }


}
