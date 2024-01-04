// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.ObjectModel;
using WT_Transfer.Models;
using AdvancedSharpAdbClient;
using System.Diagnostics;
using System.Text;
using Windows.Media.Protection.PlayReady;
using WT_Transfer.SocketModels;
using Windows.Storage.Pickers;
using WinRT.Interop;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using WT_Transfer.Helper;
using NLog;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class AppPage : Page
    {
        public ObservableCollection<string> Apps { get; set; }
        private AdbClient client = GuideWindow.client;
        private DeviceData device = GuideWindow.device;
        private Dictionary<string,string> packagePairs = new Dictionary<string,string>();

        AdbHelper adbHelper = new AdbHelper();
        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        public AppPage()
        {
            try
            {
                this.InitializeComponent();
                Init();
                packagePairsInit();
            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        private void Init()
        {
            try
            {
                if (Apps != null)
                {
                    Apps.Clear();
                }
                string res = adbHelper.cmdExecuteWithAdbExit("shell pm list packages -3");
                string[] strings = res.Split("\n");
                List<string> list = strings.ToList();
                Apps = new ObservableCollection<string>();

                foreach (string s in list)
                {
                    if (!string.IsNullOrEmpty(s))
                    {
                        string v = s.Substring(8, s.Length - 9);
                        Apps.Add(v);
                    }
                }

                dataGrid.ItemsSource = Apps;
            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
        }

        private void packagePairsInit()
        {
            packagePairs.Add("com.facebook.katana", "Facebook");
            packagePairs.Add("com.instagram.android", "Instagram");
            packagePairs.Add("com.twitter.android", "Twitter");
            packagePairs.Add("com.whatsapp", "WhatsApp");
            packagePairs.Add("com.google.android.youtube", "YouTube");
            packagePairs.Add("com.linkedin.android", "LinkedIn");
            packagePairs.Add("com.snapchat.android", "Snapchat");
            packagePairs.Add("com.netflix.mediaclient", "Netflix");
            packagePairs.Add("com.spotify.music", "Spotify");
            packagePairs.Add("com.amazon.mShop.android.shopping", "Amazon");
            packagePairs.Add("com.google.android.apps.maps", "Google Maps");
            packagePairs.Add("com.facebook.orca", "Facebook Messenger");
            packagePairs.Add("com.microsoft.office.word", "Microsoft Word");
            packagePairs.Add("com.google.android.gm", "Gmail");
            packagePairs.Add("com.android.chrome", "Google Chrome");
            packagePairs.Add("com.skype.raider", "Skype");
            packagePairs.Add("com.viber.voip", "Viber");
            packagePairs.Add("com.pinterest", "Pinterest");
            packagePairs.Add("com.evernote", "Evernote");
            packagePairs.Add("com.shazam.android", "Shazam");
            packagePairs.Add("com.google.android.apps.photos", "Google Photos");
            packagePairs.Add("com.dropbox.android", "Dropbox");
            packagePairs.Add("com.adobe.reader", "Adobe Acrobat Reader");
            packagePairs.Add("com.opera.browser", "Opera Browser");
            packagePairs.Add("com.microsoft.office.excel", "Microsoft Excel");
            packagePairs.Add("com.airbnb.android", "Airbnb");
            packagePairs.Add("com.booking", "Booking.com");
            packagePairs.Add("com.yelp.android", "Yelp");
            packagePairs.Add("com.zillow.android.zillowmap", "Zillow");
            packagePairs.Add("com.weather.Weather", "The Weather Channel");
            packagePairs.Add("com.google.android.apps.translate", "Google Translate");
            packagePairs.Add("com.zhihu.android", "知乎 (Zhihu)");
            packagePairs.Add("com.booking.now", "Booking Now");
            packagePairs.Add("com.microsoft.teams", "Microsoft Teams");
            packagePairs.Add("com.tiktok.music.ly", "TikTok");
            packagePairs.Add("com.messengerlite.android", "Messenger Lite");
            packagePairs.Add("com.tinder", "Tinder");
            packagePairs.Add("com.alibaba.aliexpresshd", "AliExpress");
            packagePairs.Add("com.airbnb.lottieplayer", "Lottie Player");
            packagePairs.Add("com.google.android.keep", "Google Keep");
        }

        private async void RemoveApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int index = dataGrid.SelectedIndex;

                string packageName = Apps[index];

                string res = adbHelper.cmdExecuteWithAdbExit("uninstall " + packageName);

                ContentDialog appInfoDialog = new ContentDialog
                {
                    Title = "Info",
                    Content = "App successfully uninstall",
                    PrimaryButtonText = "OK",
                };
                appInfoDialog.XamlRoot = this.Content.XamlRoot;
                ContentDialogResult re = await appInfoDialog.ShowAsync();

                Apps.Remove(packageName);
            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        private async void InstallApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                var hwnd = MainWindow.WindowHandle;
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.FileTypeFilter.Add("*");
                // 选择多个文件
                var file = await picker.PickSingleFileAsync();
                if (file == null)
                    return;
                // 获取文件路径
                string path = file.Path;

                await Task.Run(async () =>
                {

                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        ContentDialog appInfoDialog = new ContentDialog
                        {
                            Title = "Info",
                            Content = "The app is currently being installed, please wait",
                            PrimaryButtonText = "OK",
                        };
                        appInfoDialog.XamlRoot = this.Content.XamlRoot;
                        await appInfoDialog.ShowAsync();
                    });


                    //安装指令
                    string res = adbHelper.cmdExecuteWithAdbExit("install \"" + path + "\"");

                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        ContentDialog appInfoDialog = new ContentDialog
                        {
                            Title = "Info",
                            Content = "Successfully installed the app.",
                            PrimaryButtonText = "OK",
                        };
                        appInfoDialog.XamlRoot = this.Content.XamlRoot;
                        await appInfoDialog.ShowAsync();


                        Init();
                    });

                });
            }
            catch (Exception ex)
            {
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
