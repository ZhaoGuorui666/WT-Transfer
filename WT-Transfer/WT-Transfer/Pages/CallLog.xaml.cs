// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using NLog;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using PdfSharp;
using PdfSharp.Pdf;
using SelectPdf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using TheArtOfDev.HtmlRenderer.PdfSharp;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;
using WT_Transfer.Helper;
using WT_Transfer.Models;
using WT_Transfer.SocketModels;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class CallLog : Page
    {

        public ICollection<PhoneNumberRecord> Calls { get; set; }
        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        public CallLog()
        {
            try
            {
                this.InitializeComponent();
                this.Loaded += LoadingPage_Loaded;
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }

        }

        private async void LoadingPage_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {

                //显示加载框
                if (this.Calls == null)
                {
                    if (MainWindow.Calls == null)
                    {
                        // 进行初始化操作，例如解析数据并赋值给 calls
                        if (!MainWindow.calllog_isRuning)
                            await Init();
                        else
                        {
                            await Task.Run(() =>
                            {
                                while (MainWindow.Calls == null)
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

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
        }

        private void InitPage()
        {
            this.Calls = MainWindow.Calls;
            listDetailsView.ItemsSource = Calls;
            progressRing.Visibility = Visibility.Collapsed;
            listDetailsView.Visibility = Visibility.Visible;
        }

        private async Task Init()
        {
            try
            {
                if (MainWindow.Permissions[0] == '0' || MainWindow.Permissions[2] == '0')
                {
                    permission.XamlRoot = this.XamlRoot;
                    permission.ShowAsync();

                    MainWindow.Permissions[0] = '1';
                    MainWindow.Permissions[2] = '1';
                }


                await Task.Run(async () => {
                    SocketHelper helper = new SocketHelper();
                    AdbHelper adbHelper = new AdbHelper();

                    Result result = new Result();
                    await Task.Run(() =>
                    {
                        result = helper.getResult("calllogs", "query");
                    });

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
                            string callString = adbHelper.readFromPath(result.path, "calllogs");

                            JArray jArray = JArray.Parse(callString);

                            // use LINQ query to get DisplayName and MobileNum
                            var resultArray = (from item in jArray
                                               select new
                                               {
                                                   Number = item["number"]?.ToString(),
                                                   Date = item["date"]?.ToString(),
                                                   Type = item["type"]?.ToString(),
                                                   Duration = item["duration"]?.ToString(),
                                                   Name = item["name"]?.ToString()
                                               })
                                            .Where(item => !string.IsNullOrEmpty(item.Number))
                                            .ToArray();


                            Calls = new List<PhoneNumberRecord>();
                            // output result
                            Dictionary<string, PhoneNumberRecord> records = new Dictionary<string, PhoneNumberRecord>();
                            foreach (var item in resultArray)
                            {
                                PhoneNumberRecord record;
                                if (records.TryGetValue(item.Number, out record))
                                {
                                    // map中有，直接存入
                                    record.AddCallRecord(new CallRecord
                                    {
                                        Date = item.Date,
                                        Type = item.Type,
                                        Duration = item.Duration,
                                        Name = item.Name,
                                    });
                                }
                                else
                                {
                                    // map中没，新增
                                    record = new PhoneNumberRecord(item.Number);
                                    CallRecord callRecord = new CallRecord
                                    {
                                        Date = item.Date,
                                        Type = item.Type,
                                        Duration = item.Duration,
                                        Name = item.Name,
                                    };
                                    record.AddCallRecord(callRecord);
                                    records.Add(item.Number, record);
                                }
                            }

                            foreach (var item in records)
                            {
                                item.Value.numberToName();
                                Calls.Add(item.Value);
                            }

                            DispatcherQueue.TryEnqueue(() => {
                                Calls = Calls.OrderByDescending(item => item.Calls[0].Date).ToList();
                                listDetailsView.ItemsSource = Calls;
                                progressRing.Visibility = Visibility.Collapsed;
                                listDetailsView.Visibility = Visibility.Visible;
                                MainWindow.Calls = this.Calls;
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
                            MainWindow.Permissions[2] = '0';
                        });
                    }
                    else
                    {
                        // 不成功
                        show_error("Call record query failed ,please check the phone connection.");
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

        public void OnXamlRendered(FrameworkElement control)
        {
            // Transfer Data Context so we can access Emails Collection.
            control.DataContext = this;
        }

        private async void CopyToPC_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                FileSavePicker savePicker = new Windows.Storage.Pickers.FileSavePicker();
                var hWnd = MainWindow.WindowHandle;
                WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);
                savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                // Dropdown of file types the user can save the file as
                savePicker.FileTypeChoices.Add(".Xls", new List<string>() { ".Xls" });
                savePicker.FileTypeChoices.Add(".html", new List<string>() { ".html" });
                savePicker.FileTypeChoices.Add(".pdf", new List<string>() { ".pdf" });

                savePicker.SuggestedFileName = "CallLog";

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

                    ISheet sheet = workbook.CreateSheet("CallLogs"); // 操作第一个Sheet.

                    int rowIndex = 0;
                    IRow row = sheet.CreateRow(rowIndex);
                    row.CreateCell(0).SetCellValue("Number");
                    row.CreateCell(1).SetCellValue("Date");
                    row.CreateCell(2).SetCellValue("Type");
                    row.CreateCell(3).SetCellValue("Duration");
                    rowIndex++;
                    row = sheet.CreateRow(rowIndex);

                    // 插入 通讯录记录
                    foreach (PhoneNumberRecord call in Calls)
                    {
                        row.CreateCell(0).SetCellValue(call.Number);
                        foreach (CallRecord record in call.Calls)
                        {
                            row.CreateCell(1).SetCellValue(record.Date);
                            row.CreateCell(2).SetCellValue(record.Type);
                            row.CreateCell(3).SetCellValue(record.Duration);

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
                else if (file.FileType.Equals(".html"))
                {
                    String htmlPath = file.Path;


                    String path = GuideWindow.localPath + "\\calllog.xls";

                    IWorkbook workbook = null;
                    workbook = new HSSFWorkbook(); // 处理xls格式文件

                    ISheet sheet = workbook.CreateSheet("CallLogs"); // 操作第一个Sheet.

                    int rowIndex = 0;
                    IRow row = sheet.CreateRow(rowIndex);
                    row.CreateCell(0).SetCellValue("Number");
                    row.CreateCell(1).SetCellValue("Date");
                    row.CreateCell(2).SetCellValue("Type");
                    row.CreateCell(3).SetCellValue("Duration");
                    rowIndex++;
                    row = sheet.CreateRow(rowIndex);

                    // 插入 通讯录记录
                    foreach (PhoneNumberRecord call in Calls)
                    {


                        row.CreateCell(0).SetCellValue(call.Number);
                        foreach (CallRecord record in call.Calls)
                        {
                            row.CreateCell(1).SetCellValue(record.Date);
                            row.CreateCell(2).SetCellValue(record.Type);
                            row.CreateCell(3).SetCellValue(record.Duration);

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

                    await Task.Run(() => {
                        String pdfPath = file.Path;
                        String htmlPath = GuideWindow.localPath + "\\calllog.html";
                        String path = GuideWindow.localPath + "\\calllog.xls";

                        IWorkbook workbook = null;
                        workbook = new HSSFWorkbook(); // 处理xls格式文件

                        ISheet sheet = workbook.CreateSheet("CallLogs"); // 操作第一个Sheet.

                        int rowIndex = 0;
                        IRow row = sheet.CreateRow(rowIndex);
                        row.CreateCell(0).SetCellValue("Number");
                        row.CreateCell(1).SetCellValue("Date");
                        row.CreateCell(2).SetCellValue("Type");
                        row.CreateCell(3).SetCellValue("Duration");
                        rowIndex++;
                        row = sheet.CreateRow(rowIndex);

                        // 插入 通讯录记录
                        foreach (PhoneNumberRecord call in Calls)
                        {


                            row.CreateCell(0).SetCellValue(call.Number);
                            foreach (CallRecord record in call.Calls)
                            {
                                row.CreateCell(1).SetCellValue(record.Date);
                                row.CreateCell(2).SetCellValue(record.Type);
                                row.CreateCell(3).SetCellValue(record.Duration);

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
                        SelectPdf.PdfDocument doc = c.ConvertHtmlString(str);
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
    }
}
