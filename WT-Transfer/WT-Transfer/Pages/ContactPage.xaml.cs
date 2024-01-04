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
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.Json.Serialization;
using Windows.Foundation;
using Windows.Foundation.Collections;
using NPOI.HSSF.UserModel; // 处理xls格式文件
using NPOI.XSSF.UserModel; // 处理xlsx格式文件
using NPOI.SS.UserModel; // 提供了一些公共接口
using Windows.Storage.Pickers;
using WinRT.Interop;
using CommunityToolkit.WinUI.UI.Controls;
using System.Collections.ObjectModel;
using WT_Transfer.Models;
using System.Net.Sockets;
using System.Net;
using System.Text;
using WT_Transfer.SocketModels;
using static WT_Transfer.SocketModels.Request;
using Windows.ApplicationModel;
using System.Diagnostics;
using WT_Transfer.Helper;
using System.Threading.Tasks;
using NLog;
using NPOI.SS.Formula.Functions;
using Windows.ApplicationModel.Contacts;
using Contact = WT_Transfer.Models.Contact;
using Windows.Storage;
using System.Text.RegularExpressions;
using WT_Transfer.Dialog;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ContactPage : Page
    {
        public ObservableCollection<ContactShow> Contacts { get; set; }
        private Dictionary<string, ContactShow> ContactsDic = new Dictionary<string, ContactShow>();

        ContactShow selectedContact;

        HashSet<string> phoneNumsSet;
        HashSet<TextBox> phoneTextBoxSet;
        HashSet<string> addressSet;
        HashSet<TextBox> addressTextBoxSet;
        HashSet<string> emailSet;
        HashSet<TextBox> emailTextBoxSet;


        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();


        public ContactPage()
        {
            try
            {
                this.InitializeComponent();

                this.Loaded += LoadingPage_Loaded;
            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        private async void LoadingPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (MainWindow.Permissions[0] == '0')
                {
                    permission.XamlRoot = this.XamlRoot;
                    permission.ShowAsync();

                    MainWindow.Permissions[0] = '1';
                }

                if (this.Contacts == null)
                {
                    if (MainWindow.Contacts == null)
                    {
                        // 进行初始化操作，例如解析数据并赋值
                        if(!MainWindow.contact_isRuning)
                            await Init();
                        else
                        {
                            await Task.Run(() =>
                            {
                                while (MainWindow.Contacts == null)
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
            }
            catch (Exception ex)
            {
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

        private async Task Init()
        {

            try
            {
                MainWindow.contact_isRuning = true;

                await Task.Run(async () => {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (Contacts != null)
                            Contacts.Clear();
                        if (ContactsDic != null)
                            ContactsDic.Clear();
                    });

                    string filePath = "";
                    // 通过socket拿到文件Path
                    SocketHelper helper = new SocketHelper();
                    AdbHelper adbHelper = new AdbHelper();

                    Result result = new Result();
                    result = helper.getResult("contacts", "query");

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
                            string contactString = adbHelper.readFromPath(result.path, "contacts");

                            string winPath = MainWindow.ContactBackupPath + @"\WT.contact";
                            //检查备份文件是否存在，如果不存在，把文件保存一份，之后用于还原
                            if (!File.Exists(winPath))
                                adbHelper.saveFromPath(result.path, winPath);

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

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                // 备份，置为空时恢复原来数据
                                ContactList.ItemsSource = Contacts;
                                progressRing.Visibility = Visibility.Collapsed;
                                ContactList.Visibility = Visibility.Visible;
                                AddContactPage.Visibility = Visibility.Collapsed;

                                MainWindow.Contacts = new ObservableCollection<ContactShow>(this.Contacts);
                            });
                        }
                    }
                    else if (result.status.Equals("101"))
                    {
                        // 不成功
                        DispatcherQueue.TryEnqueue(() => {
                            permission.Hide();
                            show_error(" No permissions granted.");

                            MainWindow.Permissions[0] = '0';
                        });
                    }
                    else
                    {
                        // 修改不成功
                        show_error("Contact query failed ,please check the phone connection.");
                    }
                });

                MainWindow.contact_isRuning = false;
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

        public void OnXamlRendered(FrameworkElement control)
        {
            // Transfer Data Context so we can access Emails Collection.
            control.DataContext = this;
        }

        //导出
        private async void export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileSavePicker savePicker = new Windows.Storage.Pickers.FileSavePicker();
                var hWnd = MainWindow.WindowHandle;
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                // Dropdown of file types the user can save the file as
                savePicker.FileTypeChoices.Add(".Xls", new List<string>() { ".Xls" });
                savePicker.FileTypeChoices.Add(".Csv", new List<string>() { ".Csv" });
                savePicker.FileTypeChoices.Add(".html", new List<string>() { ".html" });

                savePicker.SuggestedFileName = "contact";

                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file == null)
                {
                    return;
                }

                if (file.FileType.Equals(".Xls"))
                {
                    String path = file.Path;

                    IWorkbook workbook = null;
                    workbook = new HSSFWorkbook(); // 处理xls格式文件

                    ISheet sheet = workbook.CreateSheet("contact"); // 操作第一个Sheet.

                    int rowIndex = 0;
                    IRow row = sheet.CreateRow(rowIndex);
                    row.CreateCell(0).SetCellValue("Name");
                    row.CreateCell(1).SetCellValue("MobileNum");
                    row.CreateCell(2).SetCellValue("Email");
                    row.CreateCell(3).SetCellValue("Address");

                    // 插入 通讯录记录
                    foreach (ContactShow contact in Contacts)
                    {
                        rowIndex++;
                        row = sheet.CreateRow(rowIndex);

                        int nowIndex = rowIndex;
                        int phoneNumSize = 0;
                        int emailSize = 0;
                        int AddressSize = 0;



                        row.CreateCell(0).SetCellValue(contact.structuredName[0]);
                        // 填入手机号码，邮件，地址数量
                        foreach (KeyValuePairModel kv in contact.phoneNumbers)
                        {
                            List<string> strs = kv.Value;
                            foreach (string str in strs)
                            {
                                row.CreateCell(1).SetCellValue(str);

                                rowIndex++;
                                row = sheet.CreateRow(rowIndex);
                                phoneNumSize++;
                            }
                        }
                        rowIndex = nowIndex;

                        foreach (KeyValuePairModel kv in contact.emails)
                        {
                            List<string> strs = kv.Value;
                            foreach (string str in strs)
                            {
                                row.CreateCell(2).SetCellValue(str);

                                rowIndex++;
                                row = sheet.CreateRow(rowIndex);
                                emailSize++;
                            }
                        }
                        rowIndex = nowIndex;

                        foreach (KeyValuePairModel kv in contact.addresses)
                        {
                            List<string> strs = kv.Value;
                            foreach (string str in strs)
                            {
                                row.CreateCell(3).SetCellValue(str);

                                rowIndex++;
                                row = sheet.CreateRow(rowIndex);
                                AddressSize++;
                            }
                        }
                        rowIndex = nowIndex + Math.Max(AddressSize,
                            Math.Max(emailSize, phoneNumSize)) - 1;
                    }

                    // 保存 Excel 文件
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        workbook.Write(fs, false);
                    }

                    ContentDialog appInfoDialog = new ContentDialog
                    {
                        Title = "Info",
                        Content = "File exported successfully, path is \"" + path + "\"",
                        PrimaryButtonText = "OK",
                    };
                    appInfoDialog.XamlRoot = this.Content.XamlRoot;
                    await appInfoDialog.ShowAsync();
                }
                else if (file.FileType.Equals(".Csv"))
                {
                    String path = file.Path;

                    IWorkbook workbook = null;
                    workbook = new HSSFWorkbook(); // 处理xls格式文件

                    ISheet sheet = workbook.CreateSheet("contact"); // 操作第一个Sheet.

                    int rowIndex = 0;
                    IRow row = sheet.CreateRow(rowIndex);
                    row.CreateCell(0).SetCellValue("Name");
                    row.CreateCell(1).SetCellValue("MobileNum");
                    row.CreateCell(2).SetCellValue("Email");
                    row.CreateCell(3).SetCellValue("Address");

                    // 插入 通讯录记录
                    foreach (ContactShow contact in Contacts)
                    {
                        rowIndex++;
                        row = sheet.CreateRow(rowIndex);

                        int nowIndex = rowIndex;
                        int phoneNumSize = 0;
                        int emailSize = 0;
                        int AddressSize = 0;



                        row.CreateCell(0).SetCellValue(contact.structuredName[0]);
                        // 填入手机号码，邮件，地址数量
                        foreach (KeyValuePairModel kv in contact.phoneNumbers)
                        {
                            List<string> strs = kv.Value;
                            foreach (string str in strs)
                            {
                                row.CreateCell(1).SetCellValue(str);

                                rowIndex++;
                                row = sheet.CreateRow(rowIndex);
                                phoneNumSize++;
                            }
                        }
                        rowIndex = nowIndex;

                        foreach (KeyValuePairModel kv in contact.emails)
                        {
                            List<string> strs = kv.Value;
                            foreach (string str in strs)
                            {
                                row.CreateCell(2).SetCellValue(str);

                                rowIndex++;
                                row = sheet.CreateRow(rowIndex);
                                emailSize++;
                            }
                        }
                        rowIndex = nowIndex;

                        foreach (KeyValuePairModel kv in contact.addresses)
                        {
                            List<string> strs = kv.Value;
                            foreach (string str in strs)
                            {
                                row.CreateCell(3).SetCellValue(str);

                                rowIndex++;
                                row = sheet.CreateRow(rowIndex);
                                AddressSize++;
                            }
                        }
                        rowIndex = nowIndex + Math.Max(AddressSize,
                            Math.Max(emailSize, phoneNumSize)) - 1;
                    }

                    // 保存 Excel 文件
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        workbook.Write(fs, false);
                    }

                    ContentDialog appInfoDialog = new ContentDialog
                    {
                        Title = "Info",
                        Content = "File exported successfully, path is \"" + path + "\"",
                        PrimaryButtonText = "OK",
                    };
                    appInfoDialog.XamlRoot = this.Content.XamlRoot;
                    await appInfoDialog.ShowAsync();
                }
                else
                {
                    String htmlPath = file.Path;

                    String path = GuideWindow.localPath + "\\contact.xls";


                    IWorkbook workbook = null;
                    workbook = new HSSFWorkbook(); // 处理xls格式文件

                    ISheet sheet = workbook.CreateSheet("contact"); // 操作第一个Sheet.

                    int rowIndex = 0;
                    IRow row = sheet.CreateRow(rowIndex);
                    row.CreateCell(0).SetCellValue("Name");
                    row.CreateCell(1).SetCellValue("MobileNum");
                    row.CreateCell(2).SetCellValue("Email");
                    row.CreateCell(3).SetCellValue("Address");

                    // 插入 通讯录记录
                    foreach (ContactShow contact in Contacts)
                    {
                        rowIndex++;
                        row = sheet.CreateRow(rowIndex);

                        int nowIndex = rowIndex;
                        int phoneNumSize = 0;
                        int emailSize = 0;
                        int AddressSize = 0;



                        row.CreateCell(0).SetCellValue(contact.structuredName[0]);
                        // 填入手机号码，邮件，地址数量
                        foreach (KeyValuePairModel kv in contact.phoneNumbers)
                        {
                            List<string> strs = kv.Value;
                            foreach (string str in strs)
                            {
                                row.CreateCell(1).SetCellValue(str);

                                rowIndex++;
                                row = sheet.CreateRow(rowIndex);
                                phoneNumSize++;
                            }
                        }
                        rowIndex = nowIndex;

                        foreach (KeyValuePairModel kv in contact.emails)
                        {
                            List<string> strs = kv.Value;
                            foreach (string str in strs)
                            {
                                row.CreateCell(2).SetCellValue(str);

                                rowIndex++;
                                row = sheet.CreateRow(rowIndex);
                                emailSize++;
                            }
                        }
                        rowIndex = nowIndex;

                        foreach (KeyValuePairModel kv in contact.addresses)
                        {
                            List<string> strs = kv.Value;
                            foreach (string str in strs)
                            {
                                row.CreateCell(3).SetCellValue(str);

                                rowIndex++;
                                row = sheet.CreateRow(rowIndex);
                                AddressSize++;
                            }
                        }
                        rowIndex = nowIndex + Math.Max(AddressSize,
                            Math.Max(emailSize, phoneNumSize)) - 1;
                    }

                    // 保存 Excel 文件
                    using (FileStream fs = new FileStream(path, FileMode.Create))
                    {
                        workbook.Write(fs, false);
                    }

                    var worksheet = workbook.GetSheetAt(0);
                    var table = new StringBuilder();
                    table.Append("<table>");
                    for (int i = 0; i <= worksheet.LastRowNum; i++)
                    {
                        var _row = worksheet.GetRow(i);
                        table.Append("<tr>");
                        for (int j = 0; j < _row.LastCellNum; j++)
                        {
                            var cell = _row.GetCell(j);
                            table.Append($"<td>{cell}</td>");
                        }
                        table.Append("</tr>");
                    }
                    table.Append("</table>");

                    // 将表格数据写入 HTML 文件
                    File.WriteAllText(htmlPath, table.ToString());


                    ContentDialog appInfoDialog = new ContentDialog
                    {
                        Title = "Info",
                        Content = "File exported successfully, path is \"" + htmlPath + "\"",
                        PrimaryButtonText = "OK",
                    };
                    appInfoDialog.XamlRoot = this.Content.XamlRoot;
                    await appInfoDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //添加联系人
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ContentDialog appInfoDialog = new ContentDialog
                {
                    Title = "Info",
                    Content = "Adding, please wait.",
                    PrimaryButtonText = "OK",
                };
                appInfoDialog.XamlRoot = this.Content.XamlRoot;
                appInfoDialog.ShowAsync();

                Dictionary<string, string> phoneMap = new Dictionary<string, string>() {
                { "Custom","0 Custom"},
                { "Home","1 Home"},
                { "Mobile","2 Mobile"},
                { "Work","3 Work"},
                { "WorkFax","4 WorkFax"},
                { "HomeFax","5 HomeFax"},
                { "Pager","6 Pager"},
                { "Other","7 Other"},
                { "Callback","8 Callback"},
            };
                Dictionary<string, string> emailMap = new Dictionary<string, string>() {
                { "Custom","0 Custom"},
                { "Home","1 Home"},
                { "Work","2 Work"},
                { "Other","3 Other"},
                { "Mobile","4 Mobile"},
            };
                Dictionary<string, string> addressMap = new Dictionary<string, string>() {
                { "Custom","0 Custom"},
                { "Home","1 Home"},
                { "Work","2 Work"},
                { "Other","3 Other"},
            };


                string telephoneType = TelephoneType.SelectedValue.ToString();
                string telephoneText = TelephoneText.Text;
                string emailType = EmailType.SelectedValue.ToString();
                string emailText = EmailText.Text;
                string addressType = AddressType.SelectedValue.ToString();
                string addressText = AddressText.Text;
                string displayName = DisplayName.Text;
                string note = Note.Text;

                ContactInfo contact = new ContactInfo();
                contact.displayName = displayName;
                contact.note = note;
                contact.phoneNumbers = new Dictionary<string, HashSet<string>> {
                { phoneMap[telephoneType], new HashSet<string>(){ telephoneText } }
            };
                contact.emails = new Dictionary<string, HashSet<string>> {
                { emailMap[emailType], new HashSet<string>(){ emailText } }
            };
                contact.addresses = new Dictionary<string, HashSet<string>> {
                { addressMap[addressType], new HashSet<string>(){ addressText } }
            };

                Data data = new Data();
                data.contact_info = contact;


                // 新增联系人
                Request request = new Request();
                request.command_id = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                request.module = "contacts";
                request.operation = "insert";
                request.info = data;

                string requestStr = JsonConvert.SerializeObject(request);

                SocketHelper helper = new SocketHelper();
                // 拿到结果

                Result result = new Result();
                await Task.Run(() =>
                {
                    result = helper.ExecuteOp(requestStr);
                });

                if (result.status.Equals("00"))
                {

                    DispatcherQueue.TryEnqueue(async () => {
                        await Init();
                        appInfoDialog.Hide();
                        appInfoDialog = new ContentDialog
                        {
                            Title = "Info",
                            Content = "Contact successfully added",
                            PrimaryButtonText = "OK",
                        };
                        appInfoDialog.XamlRoot = this.Content.XamlRoot;
                        ContentDialogResult re = await appInfoDialog.ShowAsync();


                        ContactList.Visibility = Visibility.Visible;
                        AddContactPage.Visibility = Visibility.Collapsed;
                    });
                }
                else
                {
                    // 不成功
                    show_error("Contact addition failed.");
                }
            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //添加联系人，显示页面
        private void AddContact_Click(object sender, RoutedEventArgs e)
        {
            ContactList.Visibility = Visibility.Collapsed;
            AddContactPage.Visibility = Visibility.Visible;
            ModifyContactPage.Visibility = Visibility.Collapsed;
        }

        //取消按钮
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            ContactList.Visibility = Visibility.Visible;
            AddContactPage.Visibility = Visibility.Collapsed;
            ModifyContactPage.Visibility = Visibility.Collapsed;
        }

        //删除联系人
        private async void DelContact_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int selectedIndex = ContactList.SelectedIndex;
                //报错
                ContentDialog appInfoDialog;
                if (selectedIndex > Contacts.Count || selectedIndex < 0)
                {
                    appInfoDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = "Please select a contact person.",
                        PrimaryButtonText = "OK",
                    };
                    appInfoDialog.XamlRoot = this.Content.XamlRoot;
                    await appInfoDialog.ShowAsync();
                    return;
                }

                ContentDialog appErrorDialog = new ContentDialog
                {
                    Title = "Info",
                    Content = "Are you sure you want to delete it ?",
                    PrimaryButtonText = "OK",
                    SecondaryButtonText = "Cancel"
                };
                appErrorDialog.XamlRoot = this.Content.XamlRoot;
                ContentDialogResult re = await appErrorDialog.ShowAsync();
                if (re == ContentDialogResult.Primary)
                {
                    ContactShow contact = Contacts[selectedIndex];

                    Contacts.Remove(contact);

                    Request request = new Request();


                    request.command_id = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                    request.module = "contacts";
                    request.operation = "delete";
                    request.info = new Data()
                    {
                        data_id = contact._id
                    };


                    string delRequestStr = JsonConvert.SerializeObject(request);

                    SocketHelper helper = new SocketHelper();
                    Result result = new Result();
                    await Task.Run(() =>
                    {
                        result = helper.ExecuteOp(delRequestStr);
                    });

                    if (result.status.Equals("00"))
                    {
                        appInfoDialog = new ContentDialog
                        {
                            Title = "Info",
                            Content = "Contact successfully deleted",
                            PrimaryButtonText = "OK",
                        };
                        appInfoDialog.XamlRoot = this.Content.XamlRoot;
                        await appInfoDialog.ShowAsync();
                    }
                    else
                    {
                        // 不成功
                        show_error("Contact deletion failed.");
                    }

                    
                }
            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //修改联系人
        private async void ModifyContact_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                int selectedIndex = ContactList.SelectedIndex;

                if (selectedIndex < 0)
                {
                    show_error("Please select a contact person.");
                    return;
                }
                ContactShow contact = Contacts[selectedIndex];
                selectedContact = contact;

                phoneNumsSet = new HashSet<string>();
                phoneTextBoxSet = new HashSet<TextBox>();
                addressSet = new HashSet<string>();
                addressTextBoxSet = new HashSet<TextBox>();
                emailSet = new HashSet<string>();
                emailTextBoxSet = new HashSet<TextBox>();


                //TODO 没有选中校验
                if(contact == null)
                {
                    show_error("No contact selected.");
                    return;
                }
                ContactList.Visibility = Visibility.Collapsed;
                ModifyContactPage.Visibility = Visibility.Visible;
                AddContactPage.Visibility = Visibility.Collapsed;

                DisplayName1.Text = contact.structuredName[0];

                PhoneNumsPanel.Children.Clear();
                EmailPanel.Children.Clear();
                AddressPanel.Children.Clear();


                //动态添加手机号码输入框
                foreach (var item in contact.phoneNumbers)
                {
                    foreach(var phoneNum in item.Value)
                    {
                        ComboBox comboBox = new ComboBox();
                        comboBox.Items.Add("Custom");
                        comboBox.Items.Add("Home");
                        comboBox.Items.Add("Mobile");
                        comboBox.Items.Add("Work");
                        comboBox.Items.Add("WorkFax");
                        comboBox.Items.Add("Pager");
                        comboBox.Items.Add("Other");
                        comboBox.Items.Add("Callback");
                        comboBox.SelectedIndex = 2;
                        TextBox textBox = new TextBox();
                        textBox.Text = phoneNum;
                        textBox.Width = 500;
                        textBox.TextChanged += TelephoneText_TextChanged;

                        StackPanel stackPanel = new StackPanel();
                        stackPanel.Orientation = Orientation.Horizontal;
                        stackPanel.Children.Add(comboBox);
                        stackPanel.Children.Add(textBox);
                        PhoneNumsPanel.Children.Add(stackPanel);
                        phoneTextBoxSet.Add(textBox);
                    }
                }
                if(contact.phoneNumbers.Count == 0)
                {
                    ComboBox comboBox = new ComboBox();
                    comboBox.Items.Add("Custom");
                    comboBox.Items.Add("Home");
                    comboBox.Items.Add("Mobile");
                    comboBox.Items.Add("Work");
                    comboBox.Items.Add("WorkFax");
                    comboBox.Items.Add("Pager");
                    comboBox.Items.Add("Other");
                    comboBox.Items.Add("Callback");
                    comboBox.SelectedIndex = 2;
                    TextBox textBox = new TextBox();
                    textBox.Text = "";
                    textBox.Width = 500;

                    StackPanel stackPanel = new StackPanel();
                    stackPanel.Orientation = Orientation.Horizontal;
                    stackPanel.Children.Add(comboBox);
                    stackPanel.Children.Add(textBox);
                    PhoneNumsPanel.Children.Add(stackPanel);
                    phoneTextBoxSet.Add(textBox);
                }

                //动态添加地址输入框
                foreach (var item in contact.addresses)
                {
                    foreach (var address in item.Value)
                    {
                        ComboBox comboBox = new ComboBox();
                        comboBox.Items.Add("Custom");
                        comboBox.Items.Add("Home");
                        comboBox.Items.Add("Wrok");
                        comboBox.Items.Add("Other");
                        comboBox.SelectedIndex = 2;
                        TextBox textBox = new TextBox();
                        textBox.Text = address;
                        textBox.Width = 500;

                        StackPanel stackPanel = new StackPanel();
                        stackPanel.Orientation = Orientation.Horizontal;
                        stackPanel.Children.Add(comboBox);
                        stackPanel.Children.Add(textBox);
                        AddressPanel.Children.Add(stackPanel);
                        addressTextBoxSet.Add(textBox);
                    }
                }
                if(contact.addresses.Count == 0)
                {
                    ComboBox comboBox = new ComboBox();
                    comboBox.Items.Add("Custom");
                    comboBox.Items.Add("Home");
                    comboBox.Items.Add("Wrok");
                    comboBox.Items.Add("Other");
                    comboBox.SelectedIndex = 2;
                    TextBox textBox = new TextBox();
                    textBox.Text = "";
                    textBox.Width = 500;

                    StackPanel stackPanel = new StackPanel();
                    stackPanel.Orientation = Orientation.Horizontal;
                    stackPanel.Children.Add(comboBox);
                    stackPanel.Children.Add(textBox);
                    AddressPanel.Children.Add(stackPanel);
                    addressTextBoxSet.Add(textBox);
                }

                //动态添加地址输入框
                foreach (var item in contact.emails)
                {
                    foreach (var email in item.Value)
                    {
                        ComboBox comboBox = new ComboBox();
                        comboBox.Items.Add("Custom");
                        comboBox.Items.Add("Home");
                        comboBox.Items.Add("Wrok");
                        comboBox.Items.Add("Other");
                        comboBox.Items.Add("Mobile");
                        comboBox.SelectedIndex = 2;
                        TextBox textBox = new TextBox();
                        textBox.Text = email;
                        textBox.Width = 500;

                        StackPanel stackPanel = new StackPanel();
                        stackPanel.Orientation = Orientation.Horizontal;
                        stackPanel.Children.Add(comboBox);
                        stackPanel.Children.Add(textBox);
                        EmailPanel.Children.Add(stackPanel);
                        emailTextBoxSet.Add(textBox);
                    }
                }
                if(contact.emails.Count == 0)
                {
                    ComboBox comboBox = new ComboBox();
                    comboBox.Items.Add("Custom");
                    comboBox.Items.Add("Home");
                    comboBox.Items.Add("Wrok");
                    comboBox.Items.Add("Other");
                    comboBox.Items.Add("Mobile");
                    comboBox.SelectedIndex = 2;
                    TextBox textBox = new TextBox();
                    textBox.Text = "";
                    textBox.Width = 500;

                    StackPanel stackPanel = new StackPanel();
                    stackPanel.Orientation = Orientation.Horizontal;
                    stackPanel.Children.Add(comboBox);
                    stackPanel.Children.Add(textBox);
                    EmailPanel.Children.Add(stackPanel);
                    emailTextBoxSet.Add(textBox);
                }

                Note1.Text = contact.note;

                
            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //还原
        private async void Restore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 开对话框，选择还原模式
                ContentDialog appErrorDialog = new ContentDialog
                {
                    Title = "Do you want to start restoring files ?",
                    PrimaryButtonText = "Confirm",
                    SecondaryButtonText = "Cancel"
                };
                appErrorDialog.XamlRoot = this.Content.XamlRoot;
                appErrorDialog.Content = new ContactRestoreDialog();
                ContentDialogResult re = await appErrorDialog.ShowAsync();
                if (re == ContentDialogResult.Primary)
                {
                    //ContactRestore window = new ContactRestore()
                    //{
                    //    Title= "Content to be restored",
                    //};
                    //window.Activate();

                    //window.Closed += async (sender, args) =>
                    //{
                    //    progressRing.Visibility = Visibility.Visible;
                    //    ContactList.Visibility = Visibility.Collapsed;
                    //    await Init();
                    //    InitPage();
                    //};




                    await Task.Run(async () =>
                    {
                        string mode =
                        SettingPage.ContactRestoreMode;

                        mode = string.IsNullOrEmpty(mode) ? "Incre_restore" : mode;
                        if (!string.IsNullOrEmpty(mode))
                        {
                            DispatcherQueue.TryEnqueue(async () =>
                            {
                                ContactRestore.ShowAsync();
                            });


                            if (mode.Equals("Overwrite_restore"))
                            {
                                await SendSyncOpTo("sync1");
                            }
                            else
                            {
                                await SendSyncOpTo("sync0");
                            }

                            await Init();
                            DispatcherQueue.TryEnqueue(async () =>
                            {
                                ContactRestore.Hide();
                                ContentDialog appInfoDialog = new ContentDialog
                                {
                                    Title = "Info",
                                    Content = "Successfully restored the contact.",
                                    PrimaryButtonText = "OK",
                                };
                                appInfoDialog.XamlRoot = this.Content.XamlRoot;
                                await appInfoDialog.ShowAsync();
                            });
                        }
                    });
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


        //备份按钮
        private async void BackUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ContentDialog appInfoDialog = new ContentDialog
                {
                    Title = "Info",
                    Content = "Are you sure to start the backup?",
                    PrimaryButtonText = "OK",
                    SecondaryButtonText = "Cancel",
                };
                appInfoDialog.XamlRoot = this.Content.XamlRoot;
                ContentDialogResult re = await appInfoDialog.ShowAsync();
                if(re == ContentDialogResult.Primary)
                {
                    appInfoDialog = new ContentDialog
                    {
                        Title = "Info",
                        Content = "Starting backup, please wait.",
                        PrimaryButtonText = "OK",
                    };
                    appInfoDialog.XamlRoot = this.Content.XamlRoot;
                    appInfoDialog.ShowAsync();


                    AdbHelper adbHelper = new AdbHelper();
                    // 通过socket拿到文件Path
                    SocketHelper helper = new SocketHelper();

                    Result result = new Result();
                    await Task.Run(() =>
                    {
                        result = helper.getResult("contacts", "query");
                    });

                    if (result.status.Equals("00"))
                    {

                        string winPath = MainWindow.ContactBackupPath + @"\WT.contact";
                        //把文件保存一份，之后用于还原
                        adbHelper.saveFromPath(result.path,
                            winPath);

                        appInfoDialog.Hide();
                        appInfoDialog = new ContentDialog
                        {
                            Title = "Info",
                            Content = "Successfully backed up the contact",
                            PrimaryButtonText = "OK",
                        };
                        appInfoDialog.XamlRoot = this.Content.XamlRoot;
                        await appInfoDialog.ShowAsync();
                    }
                    else if (result.status.Equals("101"))
                    {
                        // 不成功
                        DispatcherQueue.TryEnqueue(() => {
                            show_error(" No permissions granted.");
                        });
                    }
                    else
                    {
                        // 不成功
                        show_error("Contact query failed ,please check the phone connection.");
                    }
                }
            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //修改联系人
        private async void Button_Click_2(object sender, RoutedEventArgs e)
        {
            try
            {
                foreach (var item in phoneTextBoxSet)
                {
                    phoneNumsSet.Add(item.Text);
                }
                foreach (var item in addressTextBoxSet)
                {
                    addressSet.Add(item.Text);
                }
                foreach (var item in emailTextBoxSet)
                {
                    emailSet.Add(item.Text);
                }
                ContactInfo contact = new ContactInfo();
                contact.displayName = DisplayName1.Text;
                contact.note = Note1.Text;
                contact.phoneNumbers = new Dictionary<string, HashSet<string>> {
                { "2 Mobile", phoneNumsSet }
            };
                contact.emails = new Dictionary<string, HashSet<string>> {
                { "2 Work", emailSet }
            };
                contact.addresses = new Dictionary<string, HashSet<string>> {
                { "2 Work", addressSet }
            };

                Data data = new Data();
                data.contact_info = contact;
                data.data_id = selectedContact._id;

                // 修改联系人
                Request request = new Request();
                request.command_id = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                request.module = "contacts";
                request.operation = "update";
                request.info = data;

                string requestStr = JsonConvert.SerializeObject(request);

                SocketHelper helper = new SocketHelper();
                // 拿到结果
                Result result = new Result();
                await Task.Run(() =>
                {
                    result = helper.ExecuteOp(requestStr);
                });
                if (result.status.Equals("00"))
                {
                    //修改成功
                    int selectedIndex = ContactList.SelectedIndex;

                    ContactShow con = Contacts[selectedIndex];
                    con.phoneNumbers = new List<KeyValuePairModel>() {
                new KeyValuePairModel() {
                Key = "2 Mobile",
                Value = phoneNumsSet.ToList(),
            }};
                    con.emails = new List<KeyValuePairModel>() {
                new KeyValuePairModel() {
                Key = "2 Work",
                Value = emailSet.ToList(),
            }};
                    con.addresses = new List<KeyValuePairModel>() {
                new KeyValuePairModel() {
                Key = "2 Work",
                Value = addressSet.ToList(),
            }};
                    Contacts[selectedIndex] = con;
                    ContactList.ItemsSource = Contacts;

                    await Init();

                    ContentDialog appErrorDialog = new ContentDialog
                    {
                        Title = "Info",
                        Content = "Successfully modified",
                        PrimaryButtonText = "OK",
                    };
                    appErrorDialog.XamlRoot = this.Content.XamlRoot;
                    appErrorDialog.ShowAsync();

                    ContactList.Visibility = Visibility.Visible;
                    ModifyContactPage.Visibility = Visibility.Collapsed;
                }
                else
                {
                    // 修改不成功
                    show_error("Please check the phone connection and restart the software.");
                }
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

        //添加修改联系人时 只能输入数字
        private void TelephoneText_TextChanged(object sender, TextChangedEventArgs e)
        {
            TextBox textBox = sender as TextBox;
            string text = textBox.Text;

            // 使用正则表达式验证输入是否为数字
            string pattern = "^[0-9]*$";
            bool isNumeric = Regex.IsMatch(text, pattern);

            if (!isNumeric)
            {
                // 如果输入不是数字，则移除非法字符
                int caretIndex = textBox.SelectionStart;
                textBox.Text = RemoveNonNumericCharacters(text);
                textBox.SelectionStart = caretIndex - 1;
            }
        }

        private string RemoveNonNumericCharacters(string input)
        {
            // 移除非数字字符
            return Regex.Replace(input, "[^0-9]", "");
        }
    }


}
