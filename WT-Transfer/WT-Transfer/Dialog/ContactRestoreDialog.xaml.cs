// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Dispatching;
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
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using WT_Transfer.Helper;
using WT_Transfer.Models;
using WT_Transfer.Pages;
using WT_Transfer.SocketModels;
using static WT_Transfer.SocketModels.Request;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Dialog
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ContactRestoreDialog : Page
    {
        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        public ObservableCollection<ContactShow> Contacts { get; set; }
        public ObservableCollection<ContactShow> ContactsBackUps { get; set; }
        bool finshed = false;

        public ContactRestoreDialog()
        {
            this.InitializeComponent();
            this.Loaded += ContactRestoreDialog_Loaded;
        }

        private async void ContactRestoreDialog_Loaded(object sender, RoutedEventArgs e)
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


        private async Task Init()
        {

            try
            {
                await Task.Run(async () =>
                {
                    string winPath = MainWindow.ContactBackupPath + @"\WT.contact";
                    string contactString = File.ReadAllText(winPath);


                    //string contactString = File.ReadAllText("C:\\Users\\Windows 10\\Desktop\\1688264737059.contact");
                    Dictionary<string, Contact> dictionary =
                        JsonConvert.DeserializeObject<Dictionary<string, Contact>>(contactString);

                    Contacts = new ObservableCollection<ContactShow>();

                    foreach (KeyValuePair<string, Contact> pair in dictionary)
                    {
                        Contact contact = pair.Value;
                        if (contact.structuredName.Count == 0)
                        {
                            continue;
                        }
                        ContactShow contactShow = new ContactShow(contact);
                        Contacts.Add(contactShow);
                        //ContactsDic.Add(contact.structuredName.FirstOrDefault(), contactShow);
                    }

                    finshed = DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
                    {
                        // 备份，置为空时恢复原来数据
                        ContactsBackUps = new ObservableCollection<ContactShow>(Contacts);

                        ContactList.ItemsSource = Contacts;
                        progressRing.Visibility = Visibility.Collapsed;
                        ContactList.Visibility = Visibility.Visible;

                        MainWindow.Contacts = new ObservableCollection<ContactShow>(this.Contacts);
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

        private void InitPage()
        {
            this.Contacts = MainWindow.Contacts;
            ContactList.ItemsSource = Contacts;
            progressRing.Visibility = Visibility.Collapsed;
            ContactList.Visibility = Visibility.Visible;
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

        // 确认按钮
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string mode =
                    SettingPage.ContactRestoreMode;

                mode = string.IsNullOrEmpty(mode) ? "Incre_restore" : mode;
                if (!string.IsNullOrEmpty(mode))
                {
                    //ContactRestoreDialog.ShowAsync();
                    if (mode.Equals("Overwrite_restore"))
                    {
                        await SendSyncOpTo("sync1");
                    }
                    else
                    {
                        await SendSyncOpTo("sync0");
                    }
                    //ContactRestoreDialog.Hide();

                    ContentDialog appInfoDialog = new ContentDialog
                    {
                        Title = "Info",
                        Content = "Successfully restored the contact.",
                        PrimaryButtonText = "OK",
                    };
                    appInfoDialog.XamlRoot = this.Content.XamlRoot;
                    ContentDialogResult re = await appInfoDialog.ShowAsync();
                    if (re == ContentDialogResult.Primary)
                    {

                    }
                }
            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //还原
        private static async Task<Result> SendSyncOpTo(String syncModel)
        {
            //adb把文件传输到指定目录
            string phonePath = @"/storage/emulated/0/Android/data/com.example.contacts/files/Download/" + "WT.contact";
            AdbHelper helper = new AdbHelper();
            string winPath = MainWindow.ContactBackupPath + @"\WT.contact";
            string res = helper.importFromPath(winPath, phonePath);

            Request request = new Request();
            request.command_id = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
            request.module = "contacts";
            request.operation = syncModel;
            request.info = new Data
            {
                path = phonePath,
            };
            string requestStr = JsonConvert.SerializeObject(request);

            SocketHelper socketHelper = new SocketHelper();

            Result result = new Result();
            //await Task.Run(() =>
            //{
            //    result = socketHelper.ExecuteOp(requestStr);
            //});
            result = socketHelper.ExecuteOp(requestStr);
            return result;
        }

        //取消按钮
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
            }
            catch (Exception ex)
            {

                throw;
            }
        }


    }
}
