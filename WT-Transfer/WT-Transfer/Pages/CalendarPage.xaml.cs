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
using Newtonsoft.Json.Linq;
using NLog;
using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
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
    public sealed partial class CalendarPage : Page
    {
        public ICollection<Calendar> Calendars { get; set; }
        public Dictionary<DateTime, List<Calendar>> CalendarDic = new Dictionary<DateTime, List<Calendar>>();
        public List<CalendarByDate> calendarByDates = new List<CalendarByDate>();
        public bool isInitializing = false;
        public bool isRemoving = false;
        public bool isAdding = false;

        public bool isList = true;
        public bool isDay = false;
        public bool isWeek = false;
        public bool isMonth = false;

        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        public CalendarPage()
        {
            try
            {
                isInitializing = true;
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
                if (this.Calendars == null)
                {
                    if (MainWindow.Calendars == null)
                    {
                        // 进行初始化操作，例如解析数据并赋值给 calls
                        if (!MainWindow.calendar_isRuning)
                            await Init();
                        else
                        {
                            await Task.Run(() =>
                            {
                                while (MainWindow.Calendars == null)
                                {
                                    Task.Delay(1000).Wait();
                                }
                                DispatcherQueue.TryEnqueue(() =>
                                {

                                    this.Calendars = MainWindow.Calendars;
                                    this.CalendarDic = MainWindow.CalendarDic;
                                    this.calendarByDates = MainWindow.calendarByDates;

                                    progressRing.Visibility = Visibility.Collapsed;
                                    listViewByList.ItemsSource = calendarByDates;
                                    listViewByList.Visibility = Visibility.Visible;

                                });
                            });
                        }
                    }
                    else
                    {
                        this.Calendars = MainWindow.Calendars;
                        this.CalendarDic = MainWindow.CalendarDic;
                        this.calendarByDates = MainWindow.calendarByDates;

                        progressRing.Visibility = Visibility.Collapsed;
                        listViewByList.ItemsSource = calendarByDates;
                        listViewByList.Visibility = Visibility.Visible;
                    }
                }
            }
            catch (Exception ex)
            {

                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        private async Task Init()
        {
            try
            {
                //请求权限
                if (MainWindow.Permissions[3]=='0')
                {
                    permission.XamlRoot = this.XamlRoot;
                    permission.ShowAsync();
                    MainWindow.Permissions[3] = '1';
                }

                await Task.Run(async () => {
                    SocketHelper helper = new SocketHelper();
                    AdbHelper adbHelper = new AdbHelper();

                    Result result = new Result();
                    await Task.Run(() =>
                    {
                        result = helper.getResult("calendar", "query");
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
                            string calendarString = adbHelper.readFromPath(result.path, "calendar");

                            JArray jArray = JArray.Parse(calendarString);

                            DateTime date;
                            var resultArray = (from item in jArray
                                               select new
                                               {
                                                   Title = item["title"]?.ToString(),
                                                   Description = item["description"]?.ToString(),
                                                   Dtstart = item["dtstart"]?.ToString(),
                                                   Dtend = item["dtend"]?.ToString()
                                               })
                                            .Where(item => !string.IsNullOrEmpty(item.Title))
                                            .Where(item => DateTime.TryParse(item.Dtstart, out date))
                                            .Where(item => DateTime.TryParse(item.Dtend, out date))
                                            .ToArray();

                            //Calendars = new List<Calendar>();


                            DispatcherQueue.TryEnqueue(() => {

                                foreach (var item in resultArray)
                                {
                                    // 前端 选中该天日期
                                    DateTime startTime = DateTime.Parse(item.Dtstart);
                                    calendarView.SelectedDates.Add(startTime);

                                    monthView.SelectedDates.Add(startTime);

                                    if (!CalendarDic.ContainsKey(startTime.Date))
                                    {
                                        Calendar calendar = new Calendar
                                        {
                                            Title = item.Title,
                                            Description = item.Description,
                                            Dtstart = item.Dtstart,
                                            Dtend = item.Dtend,
                                        };
                                        List<Calendar> calendars = new List<Calendar>
                    {
                        calendar
                    };
                                        CalendarDic.Add(startTime.Date, calendars);
                                    }
                                    else
                                    {
                                        Calendar calendar = new Calendar
                                        {
                                            Title = item.Title,
                                            Description = item.Description,
                                            Dtstart = item.Dtstart,
                                            Dtend = item.Dtend,
                                        };
                                        CalendarDic[startTime.Date].Add(calendar);
                                    }

                                }

                                // 按照时间排序，map转为list
                                Dictionary<DateTime, List<Calendar>> sortedDic = CalendarDic.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

                                foreach (var item in sortedDic)
                                {
                                    CalendarByDate calendarByDate = new CalendarByDate();
                                    calendarByDate.date = item.Key;
                                    calendarByDate.calendars = item.Value;
                                    calendarByDates.Add(calendarByDate);
                                }


                                calendarByDates.Reverse();

                                listViewByList.ItemsSource = calendarByDates;
                                isInitializing = false;

                                progressRing.Visibility = Visibility.Collapsed;
                                listViewByList.Visibility = Visibility.Visible;
                                
                                calendarView.SetDisplayDate(calendarByDates.FirstOrDefault().date);

                                MainWindow.Calendars = this.Calendars;
                                MainWindow.CalendarDic = CalendarDic;
                                MainWindow.calendarByDates = calendarByDates;
                            });
                        }
                    }
                    else if (result.status.Equals("101"))
                    {
                        // 不成功
                        DispatcherQueue.TryEnqueue(() => {
                            show_error(" No permissions granted.");
                            MainWindow.Permissions[3] = '0';
                        });
                    }
                    else
                    {
                        // 不成功
                        show_error("Calendar query failed ,please check the phone connection.");
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

        }

        private void selectedDatesChanged
            (CalendarView sender, CalendarViewSelectedDatesChangedEventArgs args)
        {
            try
            {

                if (!isInitializing & args.AddedDates.Count > 0)
                {
                    if (isRemoving)
                    {
                        isRemoving = false;
                        return;
                    }

                    // 获取用户选定的日期
                    DateTime selectedDate = args.AddedDates[0].Date;
                    isAdding = true;
                    sender.SelectedDates.Remove(selectedDate);
                }

                if (!isInitializing & args.RemovedDates.Count > 0)
                {
                    if (isAdding)
                    {
                        isAdding = false;
                        return;
                    }
                    // 获取用户选定的日期
                    DateTime selectedDate = args.RemovedDates[0].Date;
                    isRemoving = true;
                    calendarView.SelectedDates.Add(selectedDate);

                    // 如果当前是日视图
                    if (isDay)
                        listViewByDay.ItemsSource = CalendarDic[selectedDate];
                    if (isWeek)
                    {
                        List<CalendarByWeek> calendarByWeeks = new List<CalendarByWeek>();
                        CalendarByWeek calendarByWeek = new CalendarByWeek();

                        // 计算周一日期
                        int diff = (7 + (selectedDate.DayOfWeek - DayOfWeek.Monday)) % 7; // 计算当前日期和周一之间的差距
                        DateTime monday = selectedDate.AddDays(-diff); // 计算当前日期所在周的周一是哪一天
                        List<Calendar> MoCalendars = new List<Calendar>();
                        calendarByWeek.MoDate = monday.ToShortDateString();
                        CalendarDic.TryGetValue(monday, out MoCalendars);
                        calendarByWeek.Mo = MoCalendars;

                        DateTime Tu = monday.AddDays(1);
                        List<Calendar> TuCalendars = new List<Calendar>();
                        calendarByWeek.TuDate = Tu.ToShortDateString();
                        CalendarDic.TryGetValue(Tu, out TuCalendars);
                        calendarByWeek.Tu = TuCalendars;

                        DateTime We = Tu.AddDays(1);
                        List<Calendar> WeCalendars = new List<Calendar>();
                        calendarByWeek.WeDate = We.ToShortDateString();
                        CalendarDic.TryGetValue(We, out WeCalendars);
                        calendarByWeek.We = WeCalendars;

                        DateTime Th = We.AddDays(1);
                        List<Calendar> ThCalendars = new List<Calendar>();
                        calendarByWeek.ThDate = Th.ToShortDateString();
                        CalendarDic.TryGetValue(Th, out ThCalendars);
                        calendarByWeek.Th = ThCalendars;

                        DateTime Fr = Th.AddDays(1);
                        List<Calendar> FrCalendars = new List<Calendar>();
                        calendarByWeek.FrDate = Fr.ToShortDateString();
                        CalendarDic.TryGetValue(Fr, out FrCalendars);
                        calendarByWeek.Fr = FrCalendars;

                        DateTime Sa = Fr.AddDays(1);
                        List<Calendar> SaCalendars = new List<Calendar>();
                        calendarByWeek.SaDate = Sa.ToShortDateString();
                        CalendarDic.TryGetValue(Sa, out SaCalendars);
                        calendarByWeek.Sa = SaCalendars;

                        DateTime Su = Sa.AddDays(1);
                        List<Calendar> SuCalendars = new List<Calendar>();
                        calendarByWeek.SuDate = Su.ToShortDateString();
                        CalendarDic.TryGetValue(Su, out SuCalendars);
                        calendarByWeek.Su = SuCalendars;

                        calendarByWeeks.Add(calendarByWeek);
                        listViewByWeek.ItemsSource = calendarByWeeks;
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


        private void monthView_CalendarViewDayItemChanging(CalendarView sender, CalendarViewDayItemChangingEventArgs args)
        {
            CalendarViewDayItem item = args.Item;
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
                savePicker.FileTypeChoices.Add(".Txt", new List<string>() { ".Txt" });

                savePicker.SuggestedFileName = "calendar";

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

                    ISheet sheet = workbook.CreateSheet("calendar"); // 操作第一个Sheet.

                    int rowIndex = 0;
                    IRow row = sheet.CreateRow(rowIndex);
                    row.CreateCell(0).SetCellValue("Date");
                    row.CreateCell(1).SetCellValue("Title");
                    row.CreateCell(2).SetCellValue("Description");
                    row.CreateCell(3).SetCellValue("Dtstart");
                    row.CreateCell(4).SetCellValue("Dtend");
                    rowIndex++;
                    row = sheet.CreateRow(rowIndex);

                    // 插入 通讯录记录
                    foreach (KeyValuePair<DateTime, List<Calendar>> kvp in CalendarDic)
                    {


                        row.CreateCell(0).SetCellValue(kvp.Key.ToString());
                        foreach (Calendar calendar in kvp.Value)
                        {
                            row.CreateCell(1).SetCellValue(calendar.Title);
                            row.CreateCell(2).SetCellValue(calendar.Description);
                            row.CreateCell(3).SetCellValue(calendar.Dtstart);
                            row.CreateCell(4).SetCellValue(calendar.Dtend);

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


                    String path = GuideWindow.localPath + "\\calendar.xls";

                    IWorkbook workbook = null;
                    workbook = new HSSFWorkbook(); // 处理xls格式文件

                    ISheet sheet = workbook.CreateSheet("calendar"); // 操作第一个Sheet.

                    int rowIndex = 0;
                    IRow row = sheet.CreateRow(rowIndex);
                    row.CreateCell(0).SetCellValue("Date");
                    row.CreateCell(1).SetCellValue("Title");
                    row.CreateCell(2).SetCellValue("Description");
                    row.CreateCell(3).SetCellValue("Dtstart");
                    row.CreateCell(4).SetCellValue("Dtend");
                    rowIndex++;
                    row = sheet.CreateRow(rowIndex);

                    // 插入 通讯录记录
                    foreach (KeyValuePair<DateTime, List<Calendar>> kvp in CalendarDic)
                    {


                        row.CreateCell(0).SetCellValue(kvp.Key.ToString());
                        foreach (Calendar calendar in kvp.Value)
                        {
                            row.CreateCell(1).SetCellValue(calendar.Title);
                            row.CreateCell(2).SetCellValue(calendar.Description);
                            row.CreateCell(3).SetCellValue(calendar.Dtstart);
                            row.CreateCell(4).SetCellValue(calendar.Dtend);

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
                else if (file.FileType.Equals(".Txt"))
                {
                    String txtPath = file.Path;

                    String path = GuideWindow.localPath + "\\calendar.xls";

                    IWorkbook workbook = null;
                    workbook = new HSSFWorkbook(); // 处理xls格式文件

                    ISheet sheet = workbook.CreateSheet("calendar"); // 操作第一个Sheet.

                    int rowIndex = 0;
                    IRow row = sheet.CreateRow(rowIndex);
                    row.CreateCell(0).SetCellValue("Date");
                    row.CreateCell(1).SetCellValue("Title");
                    row.CreateCell(2).SetCellValue("Description");
                    row.CreateCell(3).SetCellValue("Dtstart");
                    row.CreateCell(4).SetCellValue("Dtend");
                    rowIndex++;
                    row = sheet.CreateRow(rowIndex);

                    // 插入 通讯录记录
                    foreach (KeyValuePair<DateTime, List<Calendar>> kvp in CalendarDic)
                    {


                        row.CreateCell(0).SetCellValue(kvp.Key.ToString());
                        foreach (Calendar calendar in kvp.Value)
                        {
                            row.CreateCell(1).SetCellValue(calendar.Title);
                            row.CreateCell(2).SetCellValue(calendar.Description);
                            row.CreateCell(3).SetCellValue(calendar.Dtstart);
                            row.CreateCell(4).SetCellValue(calendar.Dtend);

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
                    // 读取 Excel 中的文本数据
                    var textData = new StringBuilder();
                    for (int i = 0; i <= worksheet.LastRowNum; i++)
                    {
                        var _row = worksheet.GetRow(i);
                        for (int j = 0; j < _row.LastCellNum; j++)
                        {
                            var cell = _row.GetCell(j);
                            if (cell != null && cell.CellType == CellType.String)
                            {
                                textData.Append(cell.StringCellValue);
                                textData.Append("\t"); // 每个单元格用制表符分隔
                            }
                        }
                        textData.AppendLine(); // 行与行之间换行
                    }

                    // 将文本数据写入纯文本文件
                    File.WriteAllText(txtPath, textData.ToString());

                    ContentDialog appInfoDialog = new ContentDialog
                    {
                        Title = "Info",
                        Content = "File exported successfully, path is \"" + txtPath + "\"",
                        PrimaryButtonText = "OK",
                    };
                    appInfoDialog.XamlRoot = this.Content.XamlRoot;
                    await appInfoDialog.ShowAsync();
                }
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //更改视图
        private void MenuFlyoutItem_Click(object sender, RoutedEventArgs e)
        {
            dropDownButton.Content = "ListView";
            isDay = false;
            isMonth = false;
            isWeek = false;


            listViewBycalendar.Visibility = Visibility.Collapsed;
            listViewByList.Visibility = Visibility.Visible;
        }

        private void MenuFlyoutItem_Click_1(object sender, RoutedEventArgs e)
        {
            dropDownButton.Content = "DayView";
            isDay = true;
            isMonth = false;
            isWeek = false;


            listViewBycalendar.Visibility = Visibility.Visible;
            listViewByList.Visibility = Visibility.Collapsed;

            listViewByDay.Visibility = Visibility.Visible;
            listViewByMonth.Visibility = Visibility.Collapsed;
            listViewByWeek.Visibility = Visibility.Collapsed;
        }

        private void MenuFlyoutItem_Click_2(object sender, RoutedEventArgs e)
        {
            dropDownButton.Content = "WeekView";
            isDay = false;
            isMonth = false;
            isWeek = true;

            listViewBycalendar.Visibility = Visibility.Visible;
            listViewByList.Visibility = Visibility.Collapsed;

            listViewByDay.Visibility = Visibility.Collapsed;
            listViewByMonth.Visibility = Visibility.Collapsed;
            listViewByWeek.Visibility = Visibility.Visible;
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
