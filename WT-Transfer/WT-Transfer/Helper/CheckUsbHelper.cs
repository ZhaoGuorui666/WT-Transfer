using MathNet.Numerics;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WT_Transfer.Helper
{
    //检测usb
    public class CheckUsbHelper : Page
    {
        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        Thread thread;
        private static CheckUsbHelper _instance;
        private static readonly object _lock = new object();

        public CheckUsbHelper() {
            thread = new Thread(CheckUsbThread);
        }

        public static CheckUsbHelper Instance
        {
            get
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new CheckUsbHelper();
                    }
                    return _instance;
                }
            }
        }

        public void startThread()
        {
            if (thread == null || thread.ThreadState != ThreadState.Running)
            {
                thread = new Thread(CheckUsbThread);
                thread.IsBackground = true;
                thread.Start();
            }
        }

        public void CheckUsbThread()
        {
            while(true)
            {
                //logHelper.Info(logger, "检测了一次USB状态");
                Thread.Sleep(2000);

                try
                {
                    List<AdvancedSharpAdbClient.DeviceData> deviceDatas = GuideWindow.client.GetDevices();
                    if (deviceDatas == null || deviceDatas.Count == 0)
                    {
                        DispatcherQueue.TryEnqueue(async () =>
                        {
                            await Task.Run(() =>
                            {
                                // 打开新窗口
                                GuideWindow.currentWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                                {
                                    if (GuideWindow.disconnected)
                                    {
                                        return;
                                    }
                                    GuideWindow.disconnected = true;

                                    ContentDialog appErrorDialog = new ContentDialog
                                    {
                                        Title = "Error",
                                        Content = "An error has occured, phone disconnected, please restart the software.",
                                        PrimaryButtonText = "OK",
                                    };
                                    appErrorDialog.XamlRoot = GuideWindow.currentWindow.Content.XamlRoot; ;
                                    ContentDialogResult re = await appErrorDialog.ShowAsync();

                                    GuideWindow guideWindow = new GuideWindow();
                                    //guideWindow.Activate();
                                    //GuideWindow.currentWindow.Close();

                                    GuideWindow.currentWindow = guideWindow;
                                    GuideWindow.isGuideWindow = true;
                                });
                            });
                        });
                    }
                }
                catch (Exception ex)
                {
                    logHelper.Info(logger, ex.ToString());
                    throw;
                }
            }
            
        }

        // unused
        public void CheckUsbOperation(object state)
        {
            try
            {
                Console.WriteLine("检测了一次USB状态");
                //logHelper.Info(logger, "检测了一次USB状态");
                //DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, () =>
                //{
                //    show_error("检测了一次USB状态");
                //});
                List<AdvancedSharpAdbClient.DeviceData> deviceDatas = GuideWindow.client.GetDevices();
                if (deviceDatas == null || deviceDatas.Count == 0)
                {
                    DispatcherQueue.TryEnqueue(async () =>
                    {
                        await Task.Run(() =>
                        {
                            // 打开新窗口
                            GuideWindow.currentWindow.DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Normal, async () =>
                            {
                                if (GuideWindow.isGuideWindow)
                                {
                                    return;
                                }
                                GuideWindow.isUsbConnected = false;

                                ContentDialog appErrorDialog = new ContentDialog
                                {
                                    Title = "Error",
                                    Content = "An error has occured, phone disconnected, please restart the software.",
                                    PrimaryButtonText = "OK",
                                };
                                appErrorDialog.XamlRoot = GuideWindow.currentWindow.Content.XamlRoot; ;
                                ContentDialogResult re = await appErrorDialog.ShowAsync();
                                

                                GuideWindow guideWindow = new GuideWindow();
                                //guideWindow.Activate();
                                //GuideWindow.currentWindow.Close();

                                GuideWindow.currentWindow = guideWindow;
                                GuideWindow.isGuideWindow = true;
                            });
                        });
                    });
                }
            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }
    }
}
