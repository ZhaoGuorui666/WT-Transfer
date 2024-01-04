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
        // ��ʱ���ݵĶ�ʱ��
        private DispatcherTimer _timer;
        // �鿴��ǰ�Ƿ���timerִ�еı��ݷ���
        private bool isTimer = false;
        // ��ԭ�ã��洢���Զ������ļ�fulPath
        private List<string> fullPaths = new List<string>();
        // �ļ��Ƿ�ѡ��
        //private Dictionary<string, FileData> NewFileSelectedMap = new Dictionary<string, FileData>();
        // ͬ����ԭ ����ͬʱ����
        bool isBackuping = false;
        bool isRestoring = false;
        // ��ʾ���ݻ�ԭ������
        int total = 0;
        int nowCount = 0;
        string nowFileName = "";
        // �����أ���¼�ڼ���
        int lazyNum= 1;
        // һ����ʾ����������
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

                //��ʼ����ʱ��
                //_timer = new DispatcherTimer();
                //Backup_timer();

                // ��ʼ��FileSelectedMap
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
            // ִ�����뿪����ʱ��Ҫ��ɵĲ���

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

        // ���ݰ�ť
        private async void Backups_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // �ȵ���
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
                        //�û�ȡ�����ݣ�ʲôҲ����
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

        // ���屸�ݷ���
        private async Task Backup()
        {
            try
            {
                //����ѡ�е��ļ��ת����List<FileData>
                List<FileData> files = new List<FileData>();
                foreach (var fullPath in selectedSet)
                {

                    string res = adbHelper.cmdExecute("ls -l " + fullPath);
                    List<FileData> fileDatas = returnFile(res, fullPath);

                    if(fileDatas.Count == 0)
                    {
                        //�ж�Ŀ¼�Ƿ���ڣ������ڵĻ�����Ŀ¼
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
                //�洢�����Զ˵�·��
                var filePath = MainWindow.BackupPath;
                if (filePath != null)
                {
                    //�ж�File�Ƿ���Ŀ¼����Ŀ¼�Ļ�������Ŀ¼����
                    //���ļ��Ļ���ֱ�Ӹ���

                    //Ĭ�Ͻ���һ��generatePrefix()Ŀ¼
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

                // ����������
                SaveSetIntoSetting();

                // ���ö�ʱ����ͨ���ж�isTimer�����ⶨʱ�����ø÷����������ö�ʱ��
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

        //���ݴ����file��path�洢��������Ӧλ��
        //���file��Ŀ¼���ݹ飬(file,sdcard/file/,winPath)
        //������ļ���adb pull "/sdcard/Music/01.apk" "E:\��һ��ѧ��\APK��Ŀ\exe"
        private void saveFile(FileData file ,string linuxPath ,string winPath)
        {
            try
            {

                //�ж�Ŀ¼�Ƿ���ڣ������ڵĻ�����Ŀ¼
                if (!Directory.Exists(winPath + file.FullPath))
                {
                    DirectoryInfo directory =
                        new DirectoryInfo(winPath + file.FullPath);
                    directory.Create();
                }

                String res = "";
                if (!file.isDirectory)
                {
                    //�����ļ��Ļ��ж��Ƿ��޸Ĺ���ͨ��ʱ���ж�
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
                        //������ͬ���޲���
                    }
                    else
                    {
                        //���ڲ�ͬ�����ݵ�����
                        string command = "pull -a \"" + "/" + file.FullPath + "/" + file.AName + "\"" + " \"" + winPath + "/" + file.FullPath + "\"";
                        res += adbHelper.cmdExecuteWithAdb(command) + "\n";
                    }
                }
                else
                {
                    //��Ŀ¼����Ŀ¼����

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


        #region ѡ���ļ������͵�android��

        private async void PushFile_Click(object sender, RoutedEventArgs e)
        {
            //����ѡ�����ļ�
            try
            {
                var path = filePathtxt.Text;
                var picker = new FileOpenPicker();
                var hwnd = MainWindow.WindowHandle;
                InitializeWithWindow.Initialize(picker, hwnd);
                picker.FileTypeFilter.Add("*");
                // ѡ�����ļ�
                var files = await picker.PickMultipleFilesAsync();
                // ��ȡ�ļ�·��
                List<string> fileNames = files.Select(file => file.Path).ToList<string>();
                string res = "";
                if (fileNames.Count > 0)
                {
                    //ÿһ�������ͣ����ÿһ����������ܽ�Ȼ��Ի�����ʾ
                    //adb push test-app.apk /sdcard/Download
                    for (int i = 0; i < fileNames.Count; i++)
                    {

                        string command = "push \"" + fileNames[i] + "\"" + " \"" + path + "\"";
                        res += adbHelper.cmdExecute(command) + "\n";
                    }
                    //"push \"E:\\��һ��ѧ��\\APK��Ŀ\\ͼ��\\filetxt.png \" \"/sdcard/Music/ \""
                    show_info(res);
                }

            }
            catch (Exception ex)
            {
                show_error(ex.Message);
            }
        }

        // ��ԭ��ť
        private async void Restore_Click(object sender, RoutedEventArgs e)
        {
            //����ѡ�����ļ�
            try
            {
                // ���Ի���ѡ��ԭģʽ
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
                        // �ȼ�⵱ǰ�Ƿ����ڱ��ݻ��߻�ԭ
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

                // �洢�����Զ˵�·��
                var winPath = MainWindow.BackupPath;
                // ��ԭģʽ
                var mode = (string)ApplicationData.Current.LocalSettings.Values[SettingPage.FileRestoreModePath];

                // ��ȡ���ش��̣������ļ���fullPath
                DirectoryInfo directory = new DirectoryInfo(winPath);
                cirDirPath(directory);

                // �ж�ÿ��fullPath���ֻ����Ƿ����
                // ��¼�ɹ���ԭ���ļ���Ŀ
                int count = 0;
                total = fullPaths.Count;
                foreach (var fullPath in fullPaths)
                {
                    //��ʾ��ԭ����
                    nowCount++;

                    DispatcherQueue.TryEnqueue(() => {
                        infoBar.Message = "Restoring : " + nowCount + "/" + total +
                        " File:" + fullPath;
                    });

                    // ��Ӧ�� �ֻ��е��ļ�·��
                    string path = fullPath.Substring(winPath.Length);
                    string replacedPath = path.Replace('\\', '/');
                    //�鿴�Ƿ����
                    string res = adbHelper.cmdExecute("ls -l " + replacedPath);
                    if (res.IndexOf("No such file") == -1)
                    {
                        // �ҵõ��ļ�,����Ǹ���ģʽ�������ļ�
                        //"push \"E:\\��һ��ѧ��\\APK��Ŀ\\ͼ��\\filetxt.png \" \"/sdcard/Music/ \""
                        if (mode == "Overwrite_restore")
                        {
                            string command = "push \"" + fullPath + "\"" + " \"" + replacedPath + "\"";
                            res += adbHelper.cmdExecuteWithAdb(command) + "\n";
                            count++;
                        }
                    }
                    else
                    {
                        // �Ҳ����ļ�
                        string command = "push \"" + fullPath + "\"" + " \"" + replacedPath + "\"";
                        res += adbHelper.cmdExecuteWithAdb(command) + "\n";
                        count++;
                    }
                }
                //show_info("�ɹ���ԭ"+count+"���ļ�");
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

        // �ص���ҳ��ť
        private void UserDirectory_Click(object sender, RoutedEventArgs e)
        {
            this.UserLocation();
        }

        // ˢ�°�ť
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            this.refreshFile();
            //ˢ�£���ȡtxtֵ����λ����·��
        }

        // ˢ�º���
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

        // ˫��Ŀ¼
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


        // ��Map����������
        private void SaveSetIntoSetting() {
            string fileSelectedSet = JsonConvert.SerializeObject
                (selectedSet, Formatting.Indented);

            ApplicationData.Current.LocalSettings.Values[MainWindow.SelectedFileSet_BackupPath] 
                = fileSelectedSet;
        }

        // ���ذ�ť
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

        // ����result��ֵFiles
        private void FileDataBind(string result,string path)
        {
            try
            {
                FilesList.Clear();
                Files.Clear();
                //����result
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
                //����result
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
                        // lsĳЩ�ļ�ʱ��fullpath�����ļ�������Ҫȥ���ļ���
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

        // ����һ��linuxPath������һ��List<FileData>
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

        //��ȡ�ļ���С
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

        //ѡ���ļ�Set
        private void checkBox_Checked(object sender, RoutedEventArgs e)
        {
            // �� CheckBox ѡ��ʱִ�е��߼�
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
            // �� CheckBox ȡ��ѡ��ʱִ�е��߼�
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
            // todo Ĭ��0��0 �򲻿�����ʱ��,���ڻ�û��ʼ��
            //if (backup_hour == 0 &&
            //    backup_minute == 0)
            //    return;

            // ȫ��ת��Ϊ����
            // todo ���ڲ��ԣ�10s����һ��
            int minute = backup_hour * 60 + backup_minute;

            DispatcherQueue.TryEnqueue(() => {
                //_timer.Interval = TimeSpan.FromMinutes(minute);
                _timer.Interval = TimeSpan.FromSeconds(1000);

                // �� Tick �¼����
                _timer.Tick += OnTick;

                // ������ʱ��
                _timer.Start();
            });
        }

        private void OnTick(object sender, object e)
        {
            isTimer = true;

            // �����߳���ִ����Ҫ���õĺ���
            Backup();

            isTimer = false;
            UserLocation();
        }

        //������
        private void ScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            // ��ȡScrollViewer�ؼ�����
            ScrollViewer myScrollViewer = sender as ScrollViewer;

            // ����Ƿ������ҳ��ײ�
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
