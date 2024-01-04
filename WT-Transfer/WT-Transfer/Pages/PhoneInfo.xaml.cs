// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using AdvancedSharpAdbClient;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Core;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Media.Protection.PlayReady;
using WT_Transfer.Helper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PhoneInfo : Page
    {
        private String Model;
        private String Manufacturer;
        private String PhoneName;
        private String Release;
        private String Resolution;

        private DeviceData device = GuideWindow.device;
        private AdbClient client = GuideWindow.client;
        private IShellOutputReceiver receiver = new ConsoleOutputReceiver();


        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();

        AdbHelper adbHelper = new AdbHelper();
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        public PhoneInfo()
        {
            try
            {

                this.InitializeComponent();
                Model = device.Model;
                client.ExecuteRemoteCommand("getprop ro.product.manufacturer", device, receiver);
                Manufacturer = receiver.ToString().Replace("\n", "");
                Manufacturer = Manufacturer.Replace("\r", "");
                PhoneName = device.Name;
                client.ExecuteRemoteCommand("getprop ro.build.version.release", device, receiver);
                Release = receiver.ToString().Replace("\n", "").Replace("\r", "");

                string size = adbHelper.cmdExecuteWithAdb("shell wm size");
                // ����ð�ŵ�����λ��
                int colonIndex = size.IndexOf(':');
                // ��ȡð�ź���Ĳ��֣���ȥ��ǰ����β��ո�
                Resolution = size.Substring(colonIndex + 1).Trim();



            }
            catch (Exception ex)
            {
                logHelper.Error(logger, ex.ToString());
                throw;
            }

        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
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
                            GuideWindow.currentWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                            {
                                if(GuideWindow.isGuideWindow)
                                {
                                    return;
                                }
                                GuideWindow.isUsbConnected = false;


                                GuideWindow guideWindow = new GuideWindow();
                                guideWindow.Activate();
                                GuideWindow.currentWindow.Close();

                                GuideWindow.currentWindow = guideWindow;
                                GuideWindow.isGuideWindow = true;
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
    }
}
