// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WT_Transfer.Helper;
using WT_Transfer.Models;
using static WT_Transfer.SocketModels.Request;
using WT_Transfer.SocketModels;
using Newtonsoft.Json.Linq;
using WT_Transfer.Pages;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Dialog
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SmsRestoreDialog : Page
    {
        public ICollection<PhoneNumberSmsRecord> Smss { get; set; }
        public List<PhoneNumberSmsRecord> SmssList { get; set; }


        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        CheckUsbHelper checkUsbHelper = GuideWindow.checkUsbHelper;

        public SmsRestoreDialog()
        {
            this.InitializeComponent();
            this.Loaded += SmsRestoreDialog_Loaded;
        }

        private async void SmsRestoreDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await Init();
                InitPage();
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }


        private void InitPage()
        {
            this.Smss = MainWindow.Smss;
            listDetailsView.ItemsSource = Smss.Take(100).ToList();
            progressRing.Visibility = Visibility.Collapsed;
            listDetailsView.Visibility = Visibility.Visible;
        }

        private async Task Init()
        {
            try
            {
                await Task.Run(async () => {
                    string winPath = MainWindow.SmsBackupPath + @"\WT.sms";
                    string callString = File.ReadAllText(winPath);

                    JObject jObject = JObject.Parse(callString);

                    // use LINQ query to get DisplayName and MobileNum
                    var resultArray = (from item in jObject.Properties()
                                       select new
                                       {
                                           Number = item.Value["address"]?.ToString(),
                                           Date = item.Value["date"]?.ToString(),
                                           Type = item.Value["type"]?.ToString(),
                                           Body = item.Value["body"]?.ToString()
                                       })
                                    .Where(item => !string.IsNullOrEmpty(item.Number))
                                    .ToArray();


                    Smss = new List<PhoneNumberSmsRecord>();
                    SmssList = new List<PhoneNumberSmsRecord>();
                    // output result
                    Dictionary<string, PhoneNumberSmsRecord> records = new Dictionary<string, PhoneNumberSmsRecord>();
                    //Dictionary<string, DateSmsRecord> recordsByDate = new Dictionary<string, DateSmsRecord>();
                    foreach (var item in resultArray)
                    {
                        // 一个是按照电话号码分类
                        // 一个是按照日期分类
                        // 分别赋值
                        PhoneNumberSmsRecord record;
                        //DateSmsRecord recordByDate;
                        if (records.TryGetValue(item.Number, out record))
                        {
                            // map中有，直接存入
                            record.AddCallRecord(new SmsRecord
                            {
                                Date = item.Date,
                                Type = item.Type == "1" ? "Receive" : "Send",
                                Body = item.Body,
                                Number = item.Number,
                            });
                        }
                        else
                        {
                            // map中没，新增
                            record = new PhoneNumberSmsRecord(item.Number);
                            SmsRecord callRecord = new SmsRecord
                            {
                                Date = item.Date,
                                Type = item.Type == "1" ? "Receive" : "Send",
                                Body = item.Body,
                                Number = item.Number,
                            };
                            record.AddCallRecord(callRecord);
                            records.Add(item.Number, record);
                        }
                    }

                    foreach (var item in records)
                    {
                        item.Value.sortByDate();
                        Smss.Add(item.Value);
                    }

                    Smss = Smss.OrderByDescending(phone => phone.Smss[0].Date).ToList();

                    MainWindow.Smss = this.Smss;
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        listDetailsView.ItemsSource = Smss.Take(100).ToList();
                        progressRing.Visibility = Visibility.Collapsed;
                        listDetailsView.Visibility = Visibility.Visible;
                    });
                });

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

        private void Button_Click(object sender, RoutedEventArgs e)
        {
        }

        private async void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {

                await Task.Run(async () =>
                {
                    string mode = SettingPage.SmsRestoreMode;
                    mode = string.IsNullOrEmpty(mode) ? "Incre_restore" : mode;

                    if (!string.IsNullOrEmpty(mode))
                    {
                        DispatcherQueue.TryEnqueue(async () =>
                        {
                            //SmsRestoreDialog.ShowAsync();

                            Result result = new Result();
                            if (mode.Equals("Overwrite_restore"))
                            {
                                result = await SendSyncOpTo("sync1");
                            }
                            else
                            {
                                result = await SendSyncOpTo("sync0");
                            }
                            //SmsRestoreDialog.Hide();
                            ContentDialog appInfoDialog = new ContentDialog
                            {
                                Title = "Info",
                                Content = "Successfully restored the sms.",
                                PrimaryButtonText = "OK",
                            };
                            appInfoDialog.XamlRoot = this.Content.XamlRoot;
                            await appInfoDialog.ShowAsync();
                        });


                    }
                });
            }
            catch (Exception ex)
            {

                throw;
            }
        }

        private static async Task<Result> SendSyncOpTo(String syncModel)
        {
            //adb把文件传输到指定目录
            string phonePath = @"/storage/emulated/0/Android/data/com.example.contacts/files/Download/" + "WT.sms";
            AdbHelper helper = new AdbHelper();
            string winPath = MainWindow.SmsBackupPath + @"\WT.sms";
            string res = helper.importFromPath(winPath, phonePath);

            Request request = new Request();
            request.command_id = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            request.module = "sms";
            request.operation = syncModel;
            request.info = new Data
            {
                path = phonePath,
            };
            string requestStr = JsonConvert.SerializeObject(request);

            SocketHelper socketHelper = new SocketHelper();

            Result result = new Result();
            await Task.Run(() =>
            {
                result = socketHelper.ExecuteOp(requestStr);
            });
            return result;
        }
    }
}
