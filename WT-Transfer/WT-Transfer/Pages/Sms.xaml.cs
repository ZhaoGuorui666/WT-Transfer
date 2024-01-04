// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using CommunityToolkit.WinUI.UI.Controls;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using NPOI.HSSF.UserModel;
using NPOI.SS.Formula.Functions;
using NPOI.SS.UserModel;
using SelectPdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WT_Transfer.Dialog;
using WT_Transfer.Helper;
using WT_Transfer.Models;
using WT_Transfer.SocketModels;
using static WT_Transfer.SocketModels.Request;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class Sms : Page
    {
        public ICollection<PhoneNumberSmsRecord> Smss { get; set; }
        public List<PhoneNumberSmsRecord> SmssList { get; set; }


        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        CheckUsbHelper checkUsbHelper = GuideWindow.checkUsbHelper;
        

        public Sms()
        {
            this.InitializeComponent();
            this.Loaded += LoadingPage_Loaded;
        }

        private async void LoadingPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (this.Smss == null)
                {
                    if (MainWindow.Smss == null)
                    {
                        // 进行初始化操作，例如解析数据并赋值给 calls
                        if (!MainWindow.sms_isRuning)
                            await Init();
                        else
                        {
                            await Task.Run(() =>
                            {
                                while (MainWindow.Smss == null)
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

                if (MainWindow.Permissions[0] == '0' || MainWindow.Permissions[1] == '0')
                {
                    permission.XamlRoot = this.XamlRoot;
                    permission.ShowAsync();

                    MainWindow.Permissions[0] = '1';
                    MainWindow.Permissions[1] = '1';
                }

                await Task.Run(async () => {
                    SocketHelper helper = new SocketHelper();
                    AdbHelper adbHelper = new AdbHelper();

                    Result result = new Result();
                    result = helper.getResult("sms", "query");

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
                            string callString = adbHelper.readFromPath(result.path, "sms");
                            // 读取文件的完整路径
                            //string filePath = @"C:\Users\Windows 10\Desktop\1685342918355.sms";

                            // 使用 File.ReadAllText 方法读取整个文件内容到字符串中
                            //string callString = File.ReadAllText(filePath);

                            //IEnumerable<Object> contacts = (IEnumerable<object>)JsonConvert.DeserializeObject(contact);

                            string winPath = MainWindow.SmsBackupPath + @"\WT.sms";
                            //检查备份文件是否存在，如果不存在，把文件保存一份，之后用于还原
                            if (!File.Exists(winPath))
                                adbHelper.saveFromPath(result.path, winPath);


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
                                        Type = item.Type == "1"?"Receive":"Send",
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

                                //if (recordsByDate.TryGetValue(item.Date, out recordByDate))
                                //{
                                //    // map中有，直接存入
                                //    recordByDate.AddCallRecord(new SmsRecord
                                //    {
                                //        Date = item.Date,
                                //        Type = item.Type,
                                //        Body = item.Body,
                                //        Number = item.Number,
                                //    });
                                //}
                                //else
                                //{
                                //    // map中没，新增
                                //    recordByDate = new DateSmsRecord(item.Date);
                                //    SmsRecord callRecord = new SmsRecord
                                //    {
                                //        Date = item.Date,
                                //        Type = item.Type,
                                //        Body = item.Body,
                                //        Number = item.Number,
                                //    };
                                //    recordByDate.AddCallRecord(callRecord);
                                //    recordsByDate.Add(item.Date, recordByDate);
                                //}
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
                        }
                    }
                    else if (result.status.Equals("101"))
                    {
                        // 不成功
                        DispatcherQueue.TryEnqueue(() => {
                            permission.Hide();
                            show_error(" No permissions granted.");
                            MainWindow.Permissions[0] = '0';
                            MainWindow.Permissions[1] = '0';
                        });
                    }
                    else
                    {
                        // 不成功
                        show_error("Photo deletion failed ,please check the phone connection.");
                    }

                    
                });

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
            // 执行在离开界面时需要完成的操作
            Smss =null; 
            GC.Collect();
            base.OnNavigatedFrom(e);
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
                savePicker.FileTypeChoices.Add(".Csv", new List<string>() { ".Csv" });
                savePicker.FileTypeChoices.Add(".Html", new List<string>() { ".Html" });
                savePicker.FileTypeChoices.Add(".pdf", new List<string>() { ".pdf" });

                savePicker.SuggestedFileName = "sms";

                StorageFile file = await savePicker.PickSaveFileAsync();
                if (file == null)
                {
                    return;
                }

                if (file.FileType.Equals(".Csv"))
                {
                    String path = file.Path;

                    IWorkbook workbook = null;
                    workbook = new HSSFWorkbook(); // 处理xls格式文件

                    ISheet sheet = workbook.CreateSheet("smss"); // 操作第一个Sheet.

                    int rowIndex = 0;
                    IRow row = sheet.CreateRow(rowIndex);
                    row.CreateCell(0).SetCellValue("Number");
                    row.CreateCell(1).SetCellValue("Date");
                    row.CreateCell(2).SetCellValue("Type");
                    row.CreateCell(3).SetCellValue("Body");
                    rowIndex++;
                    row = sheet.CreateRow(rowIndex);

                    // 插入 通讯录记录
                    foreach (PhoneNumberSmsRecord call in Smss)
                    {
                        row.CreateCell(0).SetCellValue(call.Number);
                        foreach (SmsRecord record in call.Smss)
                        {
                            row.CreateCell(1).SetCellValue(record.Date);
                            row.CreateCell(2).SetCellValue(record.Type);
                            row.CreateCell(3).SetCellValue(record.Body);

                            rowIndex++;
                            row = sheet.CreateRow(rowIndex);
                        }
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
                else if (file.FileType.Equals(".Html"))
                {
                    String htmlPath = file.Path;
                    String path = GuideWindow.localPath + "\\smss.xls";

                    IWorkbook workbook = null;
                    workbook = new HSSFWorkbook(); // 处理xls格式文件

                    ISheet sheet = workbook.CreateSheet("smss"); // 操作第一个Sheet.

                    int rowIndex = 0;
                    IRow row = sheet.CreateRow(rowIndex);
                    row.CreateCell(0).SetCellValue("Number");
                    row.CreateCell(1).SetCellValue("Date");
                    row.CreateCell(2).SetCellValue("Type");
                    row.CreateCell(3).SetCellValue("Body");
                    rowIndex++;
                    row = sheet.CreateRow(rowIndex);

                    // 插入 通讯录记录
                    foreach (PhoneNumberSmsRecord call in Smss)
                    {
                        row.CreateCell(0).SetCellValue(call.Number);
                        foreach (SmsRecord record in call.Smss)
                        {
                            row.CreateCell(1).SetCellValue(record.Date);
                            row.CreateCell(2).SetCellValue(record.Type);
                            row.CreateCell(3).SetCellValue(record.Body);

                            rowIndex++;
                            row = sheet.CreateRow(rowIndex);
                        }
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
                else
                {
                    //pdf
                    ContentDialog appErrorDialog = new ContentDialog
                    {
                        Title = "Info",
                        Content = "Starting export, please wait",
                        PrimaryButtonText = "OK",
                    };
                    appErrorDialog.XamlRoot = this.Content.XamlRoot;
                    appErrorDialog.ShowAsync();

                    await Task.Run(() =>
                    {
                        String htmlPath = GuideWindow.localPath + "\\smss.html";
                        String path = GuideWindow.localPath + "\\smss.xls";
                        string pdfPath = file.Path;

                        IWorkbook workbook = null;
                        workbook = new HSSFWorkbook(); // 处理xls格式文件

                        ISheet sheet = workbook.CreateSheet("smss"); // 操作第一个Sheet.

                        int rowIndex = 0;
                        IRow row = sheet.CreateRow(rowIndex);
                        row.CreateCell(0).SetCellValue("Number");
                        row.CreateCell(1).SetCellValue("Date");
                        row.CreateCell(2).SetCellValue("Type");
                        row.CreateCell(3).SetCellValue("Body");
                        rowIndex++;
                        row = sheet.CreateRow(rowIndex);

                        // 插入 通讯录记录
                        foreach (PhoneNumberSmsRecord call in Smss)
                        {
                            row.CreateCell(0).SetCellValue(call.Number);
                            foreach (SmsRecord record in call.Smss)
                            {
                                row.CreateCell(1).SetCellValue(record.Date);
                                row.CreateCell(2).SetCellValue(record.Type);
                                row.CreateCell(3).SetCellValue(record.Body);

                                rowIndex++;
                                row = sheet.CreateRow(rowIndex);
                            }
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

                        //Html To Pdf
                        HtmlToPdf c = new HtmlToPdf();
                        string str = File.ReadAllText(htmlPath);
                        PdfDocument doc = c.ConvertHtmlString(str);
                        //PdfDocument doc = c.ConvertUrl("C:\\Users\\Windows 10\\Desktop\\contact.html");

                        // save pdf document
                        doc.Save(pdfPath);

                        // close pdf document
                        doc.Close();

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            appErrorDialog.Hide();
                            ContentDialog appInfoDialog = new ContentDialog
                            {
                                Title = "Info",
                                Content = "File exported successfully.",
                                PrimaryButtonText = "OK",
                            };
                            appInfoDialog.XamlRoot = this.Content.XamlRoot;
                            appInfoDialog.ShowAsync();
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

        // 获取 ScrollViewer 控件
        private ScrollViewer GetScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer scrollViewer)
                return scrollViewer;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(element, i);
                ScrollViewer childScrollViewer = GetScrollViewer(child);
                if (childScrollViewer != null)
                    return childScrollViewer;
            }

            return null;
        }

        private async void BackUp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ContentDialog appInfoDialog = new ContentDialog
                {
                    Title = "Info",
                    Content = "Backing up, please wait...",
                    PrimaryButtonText = "OK",
                };
                appInfoDialog.XamlRoot = this.Content.XamlRoot;
                appInfoDialog.ShowAsync();


                AdbHelper adbHelper = new AdbHelper();
                // 通过socket拿到文件Path
                SocketHelper helper = new SocketHelper();

                Result result = new Result();
                await Task.Run(async () =>
                {
                    result = helper.getResult("sms", "query");

                    if (result.status.Equals("00"))
                    {
                        string winPath = MainWindow.SmsBackupPath + @"\WT.sms";
                        //把文件保存一份，之后用于还原
                        adbHelper.saveFromPath(result.path,
                            winPath);

                        DispatcherQueue.TryEnqueue(async () =>
                        {
                            appInfoDialog.Hide();
                            appInfoDialog = new ContentDialog
                            {
                                Title = "Info",
                                Content = "Successfully backed up the sms",
                                PrimaryButtonText = "OK",
                            };
                            appInfoDialog.XamlRoot = this.Content.XamlRoot;
                            await appInfoDialog.ShowAsync();
                        });
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
                        DispatcherQueue.TryEnqueue(() => {
                            show_error("Sms query failed ,please check the phone connection.");
                        });
                    }
                });

            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        private async void Restore_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 开对话框，选择还原模式
                ContentDialog appErrorDialog = new ContentDialog
                {
                    Title = "Do you want to start restore sms ?",
                    Content = new SmsRestoreDialog(),
                    PrimaryButtonText = "Confirm",
                    SecondaryButtonText = "Cancel"
                };
                appErrorDialog.XamlRoot = this.Content.XamlRoot;
                ContentDialogResult re = await appErrorDialog.ShowAsync();
                
                if (re == ContentDialogResult.Primary)
                {
                    //SmsRestore window = new SmsRestore()
                    //{
                    //    Title = "Content to be restored",
                    //};
                    //window.Activate();

                    //window.Closed += async (sender, args) =>
                    //{
                    //    // 在窗口关闭时，执行一些清理任务
                    //    // 例如，保存应用程序的设置或关闭数据库连接

                    //    progressRing.Visibility = Visibility.Visible;
                    //    listDetailsView.Visibility = Visibility.Collapsed;
                    //    await Init();
                    //    InitPage();
                    //};



                    await Task.Run(async () =>
                    {
                        string mode = SettingPage.SmsRestoreMode;
                        mode = string.IsNullOrEmpty(mode) ? "Incre_restore" : mode;

                        if (!string.IsNullOrEmpty(mode))
                        {
                            DispatcherQueue.TryEnqueue(async () =>
                            {
                                SmsRestore.ShowAsync();
                            });

                            Result result = new Result();
                            if (mode.Equals("Overwrite_restore"))
                            {
                                result = await SendSyncOpTo("sync1");
                            }
                            else
                            {
                                result = await SendSyncOpTo("sync0");
                            }

                            if (result.status.Equals("107"))
                            {
                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    SmsRestore.Hide();
                                    show_error("The app has not been set as the default app");
                                });
                            }
                            else
                            {
                                await Init();
                                DispatcherQueue.TryEnqueue(async () =>
                                {
                                    SmsRestore.Hide();
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
                        }
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
