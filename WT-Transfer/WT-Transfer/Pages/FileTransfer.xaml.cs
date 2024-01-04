// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using WT_TransferHelper;
using AdvancedSharpAdbClient;
using Windows.ApplicationModel.Chat;
using System.Threading;
using Microsoft.UI.Xaml.Shapes;
using System.Xml.Linq;
using static System.Net.WebRequestMethods;
using Newtonsoft.Json;
using NLog;
using WT_Transfer.Helper;
using NPOI.SS.Formula.Functions;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FileTransfer : Page
    {
        public ObservableCollection<FileData> Files { get; set; }
        public List<FileData> FilesList { get; set; }

        private DeviceData device = GuideWindow.device;
        private AdbClient client = GuideWindow.client;
        private IShellOutputReceiver receiver = new ConsoleOutputReceiver();
        private bool isNavigating = false;
        public static HashSet<string> selectedSet = new HashSet<string>();
        // 定时备份的定时器
        private DispatcherTimer _timer;
        // 查看当前是否是timer执行的备份方法
        private bool isTimer = false;
        // 还原用，存储电脑端所有文件fulPath
        private List<string> fullPaths = new List<string>();
        // 文件是否被选中
        //private Dictionary<string, FileData> NewFileSelectedMap = new Dictionary<string, FileData>();
        // 同步还原 不能同时进行
        bool isBackuping = false;
        bool isRestoring = false;
        // 显示备份还原进程用
        int total = 0;
        int nowCount = 0;
        string nowFileName = "";
        // 懒加载，记录第几面
        int lazyNum= 1;
        // 一面显示多少条数据
        int pageNum = 50;


        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        AdbHelper adbHelper = new AdbHelper();

        public FileTransfer()
        {
            try
            {
                this.InitializeComponent();
                this.NavigationCacheMode = NavigationCacheMode.Enabled;
                Files = new ObservableCollection<FileData>();
                FilesList = new List<FileData>();

                //初始化定时器
                //_timer = new DispatcherTimer();
                //Backup_timer();

                // 初始化FileSelectedMap
                if (ApplicationData.Current.LocalSettings.Values[MainWindow.SelectedFileSet_BackupPath] != null)
                {
                    selectedSet = JsonConvert.DeserializeObject<HashSet<string>>(
                        (string)ApplicationData.Current.LocalSettings.Values[MainWindow.SelectedFileSet_BackupPath]
                        );
                }

                this.UserLocation();


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

            GC.Collect();
            base.OnNavigatedFrom(e);
        }


        private void UserLocation()
        {
            isNavigating = true;
            try
            {
                DispatcherQueue.TryEnqueue(() => {
                    filePathtxt.Text = "/sdcard/";
                    Back.IsEnabled = true;
                    string result = adbHelper.cmdExecute("ls -l /sdcard/");
                    FileDataBind(result, "/sdcard/");
                    isNavigating = false;

                    FilesList.Take(pageNum).ToList().ForEach(file => { 
                        Files.Add(file);
                    });
                });
            }
            catch (Exception ex)
            {
                isNavigating = false;
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        private async void show_error(string msg){
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

        // 备份按钮
        private async void Backups_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 先弹窗
                ContentDialog dialog = new ContentDialog();

                dialog.XamlRoot = this.XamlRoot;
                dialog.Style = Application.Current.Resources["DefaultContentDialogStyle"] as Style;
                dialog.Title = "Are you sure to back up the selected files?";
                dialog.PrimaryButtonText = "Start Backup";
                dialog.CloseButtonText = "Cancel";
                dialog.DefaultButton = ContentDialogButton.Primary;
                //dialog.Content = new DialogContent();

                var result = await dialog.ShowAsync();


                infoBar.Visibility = Visibility.Visible;

                await Task.Run(async () => {
                    if (result == ContentDialogResult.Primary)
                    {
                        if (isRestoring)
                        {
                            bool v = DispatcherQueue.TryEnqueue(() => {
                                PullFileTip.Title = "Notice";
                                PullFileTip.Subtitle = "Currently restoring and cannot be backed up.";
                                PullFileTip.IsOpen = true;
                            });

                            return;
                        }
                        if (isBackuping)
                        {
                            bool v = DispatcherQueue.TryEnqueue(() => {
                                PullFileTip.Title = "Notice";
                                PullFileTip.Subtitle = "Currently backing up, please wait.";
                                PullFileTip.IsOpen = true;
                            });

                            return;
                        }

                        try
                        {
                            isBackuping = true;

                            bool v = DispatcherQueue.TryEnqueue(() => {
                                PullFileTip.Title = "Notice";
                                PullFileTip.Subtitle = "Currently backing up.";
                                PullFileTip.IsOpen = true;
                            });

                            await Backup();
                        }
                        catch (Exception ex)
                        {
                        }
                        finally
                        {
                            isBackuping = false;
                            total = 0;
                            nowCount = 0;
                            nowFileName = "";
                        }

                    }
                    else
                    {
                        //用户取消备份，什么也不做
                    }

                });


                infoBar.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // 具体备份方法
        private async Task Backup()
        {
            try
            {
                //处理选中的文件项，转换成List<FileData>
                List<FileData> files = new List<FileData>();
                foreach (var fullPath in selectedSet)
                {

                    string res = adbHelper.cmdExecute("ls -l " + fullPath);
                    List<FileData> fileDatas = returnFile(res, fullPath);

                    if(fileDatas.Count == 0)
                    {
                        //判断目录是否存在，不存在的话创建目录
                        if (!Directory.Exists(MainWindow.BackupPath + "\\" + fullPath))
                        {
                            DirectoryInfo directory =
                                new DirectoryInfo(MainWindow.BackupPath + "\\" + fullPath);
                            directory.Create();
                        }
                    }
                    files.AddRange(fileDatas);
                }
                total = files.Count;
                //存储到电脑端的路径
                var filePath = MainWindow.BackupPath;
                if (filePath != null)
                {
                    //判断File是否是目录，是目录的话，遍历目录下面
                    //是文件的话，直接复制

                    //默认建立一个generatePrefix()目录
                    //string path = filePath + "\\" + GuideWindow.generatePrefix();
                    string path = filePath;
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                    foreach (var file in files)
                    {
                        nowCount++;
                        nowFileName = file.AName;
                        DispatcherQueue.TryEnqueue(() => {
                            infoBar.Message = "Backing up : " + nowCount + "/" + total +
                            " File:" + nowFileName;
                        });

                        saveFile(file,
                            "",
                            path);
                    }


                }

                // 存入配置中
                SaveSetIntoSetting();

                // 设置定时器，通过判断isTimer，避免定时器调用该方法继续设置定时器
                //if (!isTimer)
                //{
                //    Backup_timer();
                //}


                UserLocation();

                bool v = DispatcherQueue.TryEnqueue(() => {
                    PullFileTip.Title = "Success";
                    PullFileTip.Subtitle = "Backup successful.";
                    PullFileTip.IsOpen = true;
                });
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //根据传入的file和path存储到本机相应位置
        //如果file是目录，递归，(file,sdcard/file/,winPath)
        //如果是文件，adb pull "/sdcard/Music/01.apk" "E:\研一下学期\APK项目\exe"
        private void saveFile(FileData file ,string linuxPath ,string winPath)
        {
            try
            {

                //判断目录是否存在，不存在的话创建目录
                if (!Directory.Exists(winPath + file.FullPath))
                {
                    DirectoryInfo directory =
                        new DirectoryInfo(winPath + file.FullPath);
                    directory.Create();
                }

                String res = "";
                if (!file.isDirectory)
                {
                    //备份文件的话判断是否修改过：通过时间判断
                    FileInfo fileInfo =
                        new FileInfo(winPath + "/" + file.FullPath + "/" + file.AName);
                    DateTime lastWriteTime = fileInfo.LastWriteTime;

                    DateTime dateTime = DateTime.Parse(file.ModifyTime);


                    if (dateTime.Year == lastWriteTime.Year &&
                        dateTime.Month == lastWriteTime.Month &&
                        dateTime.Day == lastWriteTime.Day &&
                        dateTime.Hour == lastWriteTime.Hour &&
                        dateTime.Minute == lastWriteTime.Minute
                        )
                    {
                        //日期相同，无操作
                    }
                    else
                    {
                        //日期不同，备份到电脑
                        string command = "pull -a \"" + "/" + file.FullPath + "/" + file.AName + "\"" + " \"" + winPath + "/" + file.FullPath + "\"";
                        res += adbHelper.cmdExecuteWithAdb(command) + "\n";
                    }
                }
                else
                {
                    //是目录，把目录建了

                    if (!Directory.Exists(winPath + file.FullPath + "/" + file.AName))
                    {
                        DirectoryInfo directory =
                            new DirectoryInfo(winPath + file.FullPath + "/" + file.AName);
                        directory.Create();
                    }


                    List<FileData> fileDatas = getFileDatasByPath(file.FullPath + "/" + file.AName);
                    foreach (FileData fileData in fileDatas)
                    {
                        saveFile(fileData,
                            "",
                            winPath);
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


        #region 选择文件，推送到android下

        private async void PushFile_Click(object sender, RoutedEventArgs e)
        {
            //可以选择多个文件
            try
            {
                var path = filePathtxt.Text;
                var picker = new FileOpenPicker();
                var hwnd = MainWindow.WindowHandle;
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.FileTypeFilter.Add("*");
                // 选择多个文件
                var files = await picker.PickMultipleFilesAsync();
                // 获取文件路径
                List<string> fileNames = files.Select(file => file.Path).ToList<string>();
                string res = "";
                if (fileNames.Count > 0)
                {
                    //每一个都推送，最后每一个推送情况总结然后对话框提示
                    //adb push test-app.apk /sdcard/Download
                    for (int i = 0; i < fileNames.Count; i++)
                    {

                        string command = "push \"" + fileNames[i] + "\"" + " \"" + path + "\"";
                        res += adbHelper.cmdExecute(command) + "\n";
                    }
                    //"push \"E:\\研一下学期\\APK项目\\图标\\filetxt.png \" \"/sdcard/Music/ \""
                    show_info(res);
                }

            }
            catch (Exception ex)
            {
                show_error(ex.Message);
            }
        }

        // 还原按钮
        private async void Restore_Click(object sender, RoutedEventArgs e)
        {
            //可以选择多个文件
            try
            {
                // 开对话框，选择还原模式
                ContentDialog appErrorDialog = new ContentDialog
                {
                    Title = "Info",
                    Content = "Do you want to start restoring files ?",
                    PrimaryButtonText = "Yes",
                    SecondaryButtonText = "Cancel"
                };
                appErrorDialog.XamlRoot = this.Content.XamlRoot;
                ContentDialogResult re = await appErrorDialog.ShowAsync();

                infoBar.Visibility = Visibility.Visible;
                await Task.Run(async () => {
                    if (re == ContentDialogResult.Primary)
                    {
                        // 先检测当前是否正在备份或者还原
                        if(isBackuping) {
                            bool v = DispatcherQueue.TryEnqueue(() => {
                                PushFileTip.Title = "Notice";
                                PushFileTip.Subtitle = "Currently backing up, unable to restore.";
                                PushFileTip.IsOpen = true;
                            });

                            return;
                        }
                        if (isRestoring)
                        {
                            bool v = DispatcherQueue.TryEnqueue(() => {
                                PushFileTip.Title = "Notice";
                                PushFileTip.Subtitle = "Currently restoring, please wait.";
                                PushFileTip.IsOpen = true;
                            });

                            return;
                        }

                        try
                        {
                            isRestoring = true;
                            bool v = DispatcherQueue.TryEnqueue(() => {
                                PushFileTip.Title = "Notice";
                                PushFileTip.Subtitle = "Currently restoring.";
                                PushFileTip.IsOpen = true;
                            });

                            await Restore_files();
                        }catch (Exception ex){
                        }finally { 
                            isRestoring = false;
                            total = 0;
                            nowCount = 0;
                            nowFileName = "";
                        }
                    }
                });


                infoBar.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        private async Task Restore_files()
        {
            try
            {

                // 存储到电脑端的路径
                var winPath = MainWindow.BackupPath;
                // 还原模式
                var mode = (string)ApplicationData.Current.LocalSettings.Values[SettingPage.FileRestoreModePath];

                // 获取本地磁盘，所有文件的fullPath
                DirectoryInfo directory = new DirectoryInfo(winPath);
                cirDirPath(directory);

                // 判断每个fullPath在手机中是否存在
                // 记录成功还原的文件数目
                int count = 0;
                total = fullPaths.Count;
                foreach (var fullPath in fullPaths)
                {
                    //显示还原进程
                    nowCount++;

                    DispatcherQueue.TryEnqueue(() => {
                        infoBar.Message = "Restoring : " + nowCount + "/" + total +
                        " File:" + fullPath;
                    });

                    // 对应的 手机中的文件路径
                    string path = fullPath.Substring(winPath.Length);
                    string replacedPath = path.Replace('\\', '/');
                    //查看是否存在
                    string res = adbHelper.cmdExecute("ls -l " + replacedPath);
                    if (res.IndexOf("No such file") == -1)
                    {
                        // 找得到文件,如果是覆盖模式，覆盖文件
                        //"push \"E:\\研一下学期\\APK项目\\图标\\filetxt.png \" \"/sdcard/Music/ \""
                        if (mode == "Overwrite_restore")
                        {
                            string command = "push \"" + fullPath + "\"" + " \"" + replacedPath + "\"";
                            res += adbHelper.cmdExecuteWithAdb(command) + "\n";
                            count++;
                        }
                    }
                    else
                    {
                        // 找不到文件
                        string command = "push \"" + fullPath + "\"" + " \"" + replacedPath + "\"";
                        res += adbHelper.cmdExecuteWithAdb(command) + "\n";
                        count++;
                    }
                }
                //show_info("成功还原"+count+"个文件");
                bool v = DispatcherQueue.TryEnqueue(() => {
                    PushFileTip.Title = "Success";
                    PushFileTip.Subtitle = "Restore successful.";
                    PushFileTip.IsOpen = true;
                });
            }
            catch (Exception ex)
            {
                DispatcherQueue.TryEnqueue(() =>
                {
                    show_error(ex.ToString());
                });
                logHelper.Info(logger, ex.ToString());
                throw;
            }

        }

        private void cirDirPath(DirectoryInfo dir)
        {
            try
            {

                FileInfo[] fileInfos = dir.GetFiles();
                foreach (FileInfo file in fileInfos)
                {
                    fullPaths.Add(file.FullName);
                }

                DirectoryInfo[] directoryInfos = dir.GetDirectories();
                foreach (DirectoryInfo directory in directoryInfos)
                {
                    cirDirPath(directory);
                }

            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }

        }
        #endregion

        private async void show_info(string msg)
        {
            InfoDialog.Content = msg;
            richTextBlock.FontFamily = new FontFamily("Microsoft YaHei");
            paragraph.Inlines.Add(new Run() { Text = msg });
            //richTextBlock.Blocks.Add(paragraph);
            InfoDialog.XamlRoot = Content.XamlRoot;
            ContentDialogResult result = await InfoDialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                this.refreshFile();
            }
        }

        // 回到主页按钮
        private void UserDirectory_Click(object sender, RoutedEventArgs e)
        {
            this.UserLocation();
        }

        // 刷新按钮
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            this.refreshFile();
            //刷新，获取txt值，定位到此路径
        }

        // 刷新函数
        private void refreshFile()
        {
            isNavigating = true;
            string nowPath = filePathtxt.Text;
            try
            {
                string result = adbHelper.cmdExecute("ls -l " + nowPath);
                FileDataBind(result, nowPath);
                isNavigating = false;
            }
            catch
            {
                isNavigating = false;
            }
        }

        // 双击目录
        private void fileDataGrid_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            try
            {
                isNavigating = true;
                int selectedIndex = fileDataGrid.SelectedIndex;
                FileData fd = (FileData)fileDataGrid.SelectedItem;
                string selectedName = fd.AName;
                string nowPath = filePathtxt.Text + selectedName + "/";
                if (fd.isDirectory == true)
                {
                    string result = adbHelper.cmdExecute("ls -l " + nowPath);
                    filePathtxt.Text = nowPath;
                    Back.IsEnabled = true;

                    FileDataBind(result, nowPath);
                    isNavigating = false;
                }
                else
                {
                    isNavigating = false;
                }
            }
            catch (Exception ex)
            {
                isNavigating = false;
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }


        // 把Map存入配置中
        private void SaveSetIntoSetting() {
            string fileSelectedSet = JsonConvert.SerializeObject
                (selectedSet, Formatting.Indented);

            ApplicationData.Current.LocalSettings.Values[MainWindow.SelectedFileSet_BackupPath] 
                = fileSelectedSet;
        }

        // 返回按钮
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                isNavigating = true;

                string nowPath = filePathtxt.Text;
                string path1 = nowPath.Substring(0, nowPath.Length - 1);
                int x = path1.LastIndexOf('/');
                string path2 = path1.Substring(0, x + 1);
                filePathtxt.Text = path2;
                if (filePathtxt.Text == "/") Back.IsEnabled = false;
                string result = adbHelper.cmdExecute("ls -l " + path2);
                FileDataBind(result, path2);

                isNavigating = false;
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }

        }

        // 根据result赋值Files
        private void FileDataBind(string result,string path)
        {
            try
            {
                FilesList.Clear();
                Files.Clear();
                //解析result
                var t = result.Split("\n");
                for (int i = 1; i < t.Length; i++)
                {
                    var f = t[i];
                    if (string.IsNullOrEmpty(f))
                        continue;
                    var firf = f[0];
                    var tm = System.Text.RegularExpressions.Regex.Split(f, @"\s{1,}");
                    
                    if (tm[1] != "?" && (firf == 'd' || firf == '-'))
                    {
                        FileData file = new FileData()
                        {
                            AName = tm[7],
                            ModifyTime = tm[5] + " " + tm[6],
                            Size = ReadFilesize(tm[4]),
                            Image = firf == 'd' ? "/Images/fileSingle.png" : "/Images/filetxt.png",
                            isDirectory = firf == 'd' ? true : false,
                            isSelected = false,
                            // isVisible = firf == 'd' ? false : true,
                            isVisible = true,
                            Path = path,
                            FullPath = path + tm[7],
                        };

                        FileData fileData = new FileData();
                        string str = "";
                        bool v = selectedSet.TryGetValue(file.FullPath, out str);
                        if (v)
                        {
                            file.isSelected = true;
                        }
                        FilesList.Add(file);
                    }
                }

                FilesList.Take(pageNum).ToList().ForEach(file =>
                {
                    Files.Add(file);
                });
            }
            catch (Exception ex)
            {
                isNavigating = false;
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        private List<FileData> returnFile(string result, string fullPath)
        {
            List<FileData> fileDatas = new List<FileData>();
            try
            {
                DispatcherQueue.TryEnqueue(() => {
                    FilesList.Clear();
                });
                //解析result
                var t = result.Split("\n");
                for (int i = 0; i < t.Length; i++)
                {
                    var f = t[i];
                    if(f=="" || f.Substring(0, 5) == "total")
                    {
                        continue;
                    }
                    var firf = f[0];
                    var tm = System.Text.RegularExpressions.Regex.Split(f, @"\s{1,}");
                    if (tm[1] != "?" && (firf == 'd' || firf == '-'))
                    {
                        FileData file = new FileData()
                        {
                            AName = tm[7],
                            ModifyTime = tm[5] + " " + tm[6],
                            Size = ReadFilesize(tm[4]),
                            Image = firf == 'd' ? "/Images/fileSingle.png" : "/Images/filetxt.png",
                            isDirectory = firf == 'd' ? true : false,
                            isSelected = false,
                            // isVisible = firf == 'd' ? false : true,
                            isVisible = true,
                            Path = fullPath,
                            FullPath = fullPath,
                        };

                        string[] strings = file.AName.Split('/');
                        file.AName = strings[strings.Length-1];
                        // ls某些文件时，fullpath中有文件名，需要去掉文件名
                        if(!file.isDirectory) {
                            int index = file.FullPath.IndexOf(file.AName);

                            if (index != -1)
                            {
                                file.FullPath = file.FullPath.Substring(0, index);
                            }
                        }


                        fileDatas.Add(file);
                    }
                }
                return fileDatas;
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        // 传入一个linuxPath，返回一个List<FileData>
        private List<FileData> getFileDatasByPath(String path)
        {
            try
            {

                var result = new List<FileData>();

                string str = adbHelper.cmdExecute("ls -l " + path);
                var t = str.Split("\n").ToList();
                t.RemoveAt(t.Count - 1);
                t.RemoveAt(0);
                for (int i = 0; i < t.Count; i++)
                {
                    var f = t[i];
                    var firf = f[0];
                    var tm = System.Text.RegularExpressions.Regex.Split(f, @"\s{1,}");
                    if (tm[1] != "?" && (firf == 'd' || firf == '-'))
                    {
                        result.Add(new FileData()
                        {
                            AName = tm[7],
                            ModifyTime = tm[5] + " " + tm[6],
                            Size = ReadFilesize(tm[4]),
                            Image = firf == 'd' ? "/Images/fileSingle.png" : "/Images/filetxt.png",
                            isDirectory = firf == 'd' ? true : false,
                            isSelected = false,
                            // isVisible = firf == 'd' ? false : true,
                            isVisible = true,
                            FullPath = path
                        });
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }

        }

        //读取文件大小
        private string ReadFilesize(string m)
        {
            try
            {

                double size = Convert.ToDouble(m);
                string[] units = new string[] { "B", "KB", "MB", "GB", "TB", "PB" };
                double mod = 1024.0;
                int i = 0;
                while (size >= mod)
                {
                    size /= mod;
                    i++;
                }
                return Math.Round(size) + units[i];
            }
            catch (Exception ex)
            {
                show_error(ex.ToString());
                logHelper.Info(logger, ex.ToString());
                throw;
            }

        }

        //选择文件Set
        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {
            // 当 CheckBox 选中时执行的逻辑
            CheckBox checkBox = sender as CheckBox;
            if (checkBox != null && !isNavigating)
            {
                if (checkBox.Tag == null) return;
                string fullPath = checkBox.Tag.ToString();
                //NewFileSelectedMap.Remove(fullPath);
                selectedSet.Add(fullPath);
            }
        }
        private void checkBox_Unchecked(object sender, RoutedEventArgs e)
        {
            // 当 CheckBox 取消选中时执行的逻辑
            CheckBox checkBox = sender as CheckBox;
            if (checkBox != null && !isNavigating)
            {
                string fullPath = checkBox.Tag.ToString();
                //NewFileSelectedMap.Remove(fullPath);
                selectedSet.Remove(fullPath);
            }
        }

        private void Backup_timer()
        {

            int backup_hour = MainWindow.BackupHour;
            int backup_minute = MainWindow.BackupMinute;
            // todo 默认0，0 则不开启定时器,现在还没初始化
            //if (backup_hour == 0 &&
            //    backup_minute == 0)
            //    return;

            // 全部转化为分钟
            // todo 便于测试，10s备份一次
            int minute = backup_hour * 60 + backup_minute;

            DispatcherQueue.TryEnqueue(() => {
                //_timer.Interval = TimeSpan.FromMinutes(minute);
                _timer.Interval = TimeSpan.FromSeconds(1000);

                // 绑定 Tick 事件句柄
                _timer.Tick += OnTick;

                // 启动定时器
                _timer.Start();
            });
        }

        private void OnTick(object sender, object e)
        {
            isTimer = true;

            // 在主线程上执行需要调用的函数
            Backup();

            isTimer = false;
            UserLocation();
        }

        //懒加载
        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            // 获取ScrollViewer控件对象
            ScrollViewer myScrollViewer = sender as ScrollViewer;

            // 检查是否滚动到页面底部
            if (myScrollViewer.VerticalOffset + myScrollViewer.ViewportHeight == myScrollViewer.ExtentHeight)
            {
                FilesList.Skip(lazyNum * pageNum).Take(pageNum).ToList().ForEach(file => {
                    Files.Add(file);
                });

                lazyNum++;
            }
        }
    }

    public class FileData
    {
        public string AName { get; set; }
        public string ModifyTime { get; set; }
        public string Size { get; set; }
        public string Image { get; set; }
        public bool isDirectory { get; set; }
        public bool isSelected { get; set; }
        public bool isVisible { get; set; }
        public string Path { get; set; }
        public string FullPath { get; set; }
    }
}
