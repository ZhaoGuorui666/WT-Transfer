// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Pickers;
using Windows.Storage;
using WinRT.Interop;
using WT_Transfer.Helper;
using NLog;
using Org.BouncyCastle.Asn1.X509;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using WT_Transfer.Models;
using System.Diagnostics;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingPage : Page
    {
        LogHelper logHelper = new LogHelper();
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        AdbHelper adbHelper = new AdbHelper();
        SocketHelper helper = new SocketHelper();
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();

        private static string FileCheckedPath = GuideWindow.generatePrefix() + "-" + "FileChecked";
        private static string ContactCheckedPath = GuideWindow.generatePrefix() + "-" + "ContactChecked";
        private static string MusicCheckedPath = GuideWindow.generatePrefix() + "-" + "MusicChecked";
        private static string PhotoCheckedPath = GuideWindow.generatePrefix() + "-" + "PhotoChecked";
        private static string SmsCheckedPath = GuideWindow.generatePrefix() + "-" + "SmsChecked";

        public static string FileRestoreModePath = GuideWindow.generatePrefix() + "-" + "FileRestore";
        public static string ContactRestoreModePath = GuideWindow.generatePrefix() + "-" + "ContactRestore";
        public static string SmsRestoreModePath = GuideWindow.generatePrefix() + "-" + "SmsRestore";

        public static string FileRestoreMode = (string)ApplicationData.Current.LocalSettings.Values[FileRestoreModePath];
        public static string ContactRestoreMode = (string)ApplicationData.Current.LocalSettings.Values[ContactRestoreModePath];
        public static string SmsRestoreMode = (string)ApplicationData.Current.LocalSettings.Values[SmsRestoreModePath];


        public SettingPage()
        {
            try
            {
                this.InitializeComponent();
                logHelper = new LogHelper();

                //��ԭģʽ��ʼ��
                if (!string.IsNullOrEmpty(FileRestoreMode))
                {
                    if (FileRestoreMode.Equals("Incre_restore"))
                    {
                        FileRestore.SelectedIndex = 0;
                    }
                    else
                    {
                        FileRestore.SelectedIndex = 1;
                    }
                }
                else
                {
                    FileRestore.SelectedIndex = 0;
                }
                
                if (!string.IsNullOrEmpty(ContactRestoreMode))
                {
                    if (ContactRestoreMode.Equals("Incre_restore"))
                    {
                        ContactRestore.SelectedIndex = 0;
                    }
                    else
                    {
                        ContactRestore.SelectedIndex = 1;
                    }
                }
                else
                {
                    ContactRestore.SelectedIndex = 0;
                }

                if (!string.IsNullOrEmpty(SmsRestoreMode))
                {
                    if (SmsRestoreMode.Equals("Incre_restore"))
                    {
                        SmsRestore.SelectedIndex = 0;
                    }
                    else
                    {
                        SmsRestore.SelectedIndex = 1;
                    }
                }
                else
                {
                    SmsRestore.SelectedIndex = 0;
                }

                //BackUpPath.Text =
                //(string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_BackupPath];

                //if (MainWindow.AutoBackup)
                //{
                //    toggle.IsOn = true;// ����Ƶ��
                //    string backup_hour = (String)ApplicationData.Current.LocalSettings.Values[MainWindow.BackupHour_Path];
                //    string backup_minute = (String)ApplicationData.Current.LocalSettings.Values[MainWindow.BackupMinute_Path];
                //    if (backup_hour != null)
                //    {
                //        Hour.Text = backup_hour;
                //        MainWindow.BackupHour = int.Parse(backup_hour);
                //    }
                //    if (backup_minute != null)
                //    {
                //        Minute.Text = backup_minute;
                //        MainWindow.BackupMinute = int.Parse(backup_minute);
                //    }
                //}
                //else
                //{

                //    Hour.IsReadOnly = true;
                //    Minute.IsReadOnly = true;
                //}

                // ��ѡ��
                //FileCheck.IsChecked =
                //    (ApplicationData.Current.LocalSettings.Values[FileCheckedPath] == null
                //    || (bool)ApplicationData.Current.LocalSettings.Values[FileCheckedPath] == false)
                //    ? false : true;
                //ContactCheck.IsChecked =
                //    (ApplicationData.Current.LocalSettings.Values[ContactCheckedPath] == null
                //    || (bool)ApplicationData.Current.LocalSettings.Values[ContactCheckedPath] == false)
                //    ? false : true;
                //MusicCheck.IsChecked =
                //    (ApplicationData.Current.LocalSettings.Values[MusicCheckedPath] == null
                //    || (bool)ApplicationData.Current.LocalSettings.Values[MusicCheckedPath] == false)
                //    ? false : true;
                //PhotoCheck.IsChecked =
                //    (ApplicationData.Current.LocalSettings.Values[PhotoCheckedPath] == null
                //    || (bool)ApplicationData.Current.LocalSettings.Values[PhotoCheckedPath] == false)
                //    ? false : true;
                //SmsCheck.IsChecked =
                //    (ApplicationData.Current.LocalSettings.Values[SmsCheckedPath] == null
                //    || (bool)ApplicationData.Current.LocalSettings.Values[SmsCheckedPath] == false)
                //    ? false : true;


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

        //private void MenuItem_Click(object sender, RoutedEventArgs e)
        //{
        //    //string content = dropDownButton.Content.ToString();
        //    MenuFlyoutItem menuItem = (MenuFlyoutItem)sender;
        //    string content = menuItem.Text;

        //    if (content.Equals("File"))
        //    {
        //        BackUpPath.Text =
        //            (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_BackupPath];
        //        dropDownButton.Content = content;
        //    }
        //    else if (content.Equals("Contact"))
        //    {

        //        BackUpPath.Text =
        //            (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_ContactBackupPath];
        //        dropDownButton.Content = content;
        //    }
        //    else if (content.Equals("Music"))
        //    {

        //        BackUpPath.Text =
        //            (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_MusicBackupPath];
        //        dropDownButton.Content = content;
        //    }
        //    else if (content.Equals("Photo"))
        //    {

        //        BackUpPath.Text =
        //            (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_PhotoBackupPath];
        //        dropDownButton.Content = content;
        //    }
        //    else if (content.Equals("Sms"))
        //    {

        //        BackUpPath.Text =
        //            (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_SmsBackupPath];
        //        dropDownButton.Content = content;
        //    }
        //}

        //private async void FileChoose(object sender, RoutedEventArgs e)
        //{
        //    var filePicker = new FolderPicker();
        //    var hWnd = MainWindow.WindowHandle;
        //    InitializeWithWindow.Initialize(filePicker, hWnd);
        //    filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        //    filePicker.FileTypeFilter.Add("*");
        //    Windows.Storage.StorageFolder storageFolder = await filePicker.PickSingleFolderAsync();
        //    if (storageFolder == null)
        //        return;

        //    //ToDO����
        //    ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_BackupPath]
        //        = storageFolder.Path;

        //    //FileBackUpPath.Text = storageFolder.Path;
        //    MainWindow.BackupPath = storageFolder.Path;

        //    //logHelper.log(NLog.LogLevel.Info, "Reset the File Backup Path.");

        //    var logEventInfo =
        //        new LogEventInfo(NLog.LogLevel.Info, logger.Name, "Reset the File Backup Path.");
        //    logEventInfo.Properties["userid"] = GuideWindow.serialno;
        //    logger.Log(logEventInfo);
        //}


        //�Զ����ݺ���
        private void backUpHour_TextChanged(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                ApplicationData.Current.LocalSettings.Values[MainWindow.BackupHour_Path]
                = textBox.Text;
                MainWindow.BackupHour = int.Parse(textBox.Text);

                //logHelper.log(NLog.LogLevel.Debug, "Reset the frequency of automatic file backup.");

                var logEventInfo =
                    new LogEventInfo(NLog.LogLevel.Info, logger.Name, "Reset the frequency of automatic file backup.");
                logEventInfo.Properties["userid"] = GuideWindow.serialno;
                logger.Log(logEventInfo);
            }
        }

        private void backUpMinute_TextChanged(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                ApplicationData.Current.LocalSettings.Values[MainWindow.BackupMinute_Path]
                = textBox.Text;
                MainWindow.BackupMinute = int.Parse(textBox.Text);

                //logHelper.log(NLog.LogLevel.Info, "Reset the frequency of automatic file backup.");

                var logEventInfo =
                    new LogEventInfo(NLog.LogLevel.Info, logger.Name, "Reset the frequency of automatic file backup.");
                logEventInfo.Properties["userid"] = GuideWindow.serialno;
                logger.Log(logEventInfo);
            }
        }

        //private void myToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        //{
        //    ToggleSwitch toggleSwitch = (ToggleSwitch)sender;

        //    if (toggleSwitch.IsOn)
        //    {
        //        Hour.IsReadOnly = false;
        //        Hour.Text = "0";
        //        Minute.IsReadOnly = false;
        //        Minute.Text = "0";
        //    }
        //    else
        //    {
        //        Hour.IsReadOnly = true;
        //        Minute.IsReadOnly = true;
        //    }
        //}

        private void FileCheck_Checked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values[FileCheckedPath] = true;
        }

        private void FileCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values[FileCheckedPath] = false;
        }

        private void ContactCheck_Checked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values[ContactCheckedPath] = true;
        }

        private void ContactCheck_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values[ContactCheckedPath] = false;
        }

        private void MusicCheck_Checked(object sender, RoutedEventArgs e)
        {

            ApplicationData.Current.LocalSettings.Values[MusicCheckedPath] = true;
        }

        private void MusicCheck_Unchecked(object sender, RoutedEventArgs e)
        {

            ApplicationData.Current.LocalSettings.Values[MusicCheckedPath] = false;
        }

        private void PhotoCheck_Checked(object sender, RoutedEventArgs e)
        {

            ApplicationData.Current.LocalSettings.Values[PhotoCheckedPath] = true;
        }

        private void PhotoCheck_Unchecked(object sender, RoutedEventArgs e)
        {

            ApplicationData.Current.LocalSettings.Values[PhotoCheckedPath] = false;
        }

        private void SmsCheck_Checked(object sender, RoutedEventArgs e)
        {

            ApplicationData.Current.LocalSettings.Values[SmsCheckedPath] = true;
        }

        private void SmsCheck_Unchecked(object sender, RoutedEventArgs e)
        {

            ApplicationData.Current.LocalSettings.Values[SmsCheckedPath] = false;
        }

        // һ������
        //private async void Button_Click(object sender, RoutedEventArgs e)
        //{
        //    try
        //    {
        //        AdbHelper adbHelper = new AdbHelper();
        //        SocketHelper helper = new SocketHelper();
        //        ContentDialog appErrorDialog = new ContentDialog
        //        {
        //            Title = "Info",
        //            Content = "Are you sure to start the backup ?",
        //            PrimaryButtonText = "OK",
        //            SecondaryButtonText = "Cancel"
        //        };
        //        appErrorDialog.XamlRoot = this.Content.XamlRoot;
        //        ContentDialogResult re = await appErrorDialog.ShowAsync();
        //        if (re == ContentDialogResult.Primary)
        //        {
        //            await Task.Run(async () =>
        //            {
        //                DispatcherQueue.TryEnqueue(() =>
        //                {
        //                    if (FileCheck.IsChecked == true)
        //                    {
        //                        // ��ʾ���ݻ�ԭ������
        //                        int total = 0;
        //                        int nowCount = 0;
        //                        string nowFileName = "";
        //                        //�ļ�����
        //                        if (ApplicationData.Current.LocalSettings.Values[MainWindow.SelectedFileSet_BackupPath] != null)
        //                        {
        //                            HashSet<string> selectedSet = JsonConvert.DeserializeObject<HashSet<string>>(
        //                                (string)ApplicationData.Current.LocalSettings.Values[MainWindow.SelectedFileSet_BackupPath]
        //                                );
        //                            //����ѡ�е��ļ��ת����List<FileData>
        //                            List<FileData> files = new List<FileData>();
        //                            foreach (var fullPath in selectedSet)
        //                            {
        //                                string res = adbHelper.cmdExecute("ls -l " + fullPath);
        //                                List<FileData> fileDatas = returnFile(res, fullPath);
        //                                files.AddRange(fileDatas);
        //                            }
        //                            total = files.Count;
        //                            //�洢�����Զ˵�·��
        //                            var filePath = MainWindow.BackupPath;
        //                            if (filePath != null)
        //                            {
        //                                //�ж�File�Ƿ���Ŀ¼����Ŀ¼�Ļ�������Ŀ¼����
        //                                //���ļ��Ļ���ֱ�Ӹ���

        //                                //Ĭ�Ͻ���һ��generatePrefix()Ŀ¼
        //                                string path = filePath + "\\" + GuideWindow.generatePrefix();
        //                                if (!Directory.Exists(path))
        //                                {
        //                                    Directory.CreateDirectory(path);
        //                                }
        //                                foreach (var file in files)
        //                                {
        //                                    nowCount++;
        //                                    nowFileName = file.AName;
        //                                    saveFile(file,
        //                                        "",
        //                                        path);
        //                                }


        //                            }
        //                        }
        //                    }
        //                    if (ContactCheck.IsChecked == true)
        //                    {
        //                        //ͨѶ¼����

        //                        SocketModels.Result result = helper.getResult("contacts", "query");
        //                        //���ļ�����һ�ݣ�֮�����ڻ�ԭ
        //                        adbHelper.saveFromPath(result.path,
        //                            GuideWindow.localPath + "\\" + GuideWindow.generatePrefix() + @"\contact\WT.contact");

        //                    }
        //                    if (MusicCheck.IsChecked == true)
        //                    {
        //                        //���ֱ���
        //                        SocketModels.Result result = helper.getResult("music", "query");
        //                        string musicInfo = adbHelper.readFromPath(result.path, "music");

        //                        string winPath =
        //                    (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_MusicBackupPath];
        //                        //string path = "C:\\Users\\Windows 10\\Desktop\\1688302257535.music";
        //                        //string musicInfo = File.ReadAllText(path);
        //                        List<MusicInfo> list = JsonConvert.DeserializeObject<List<MusicInfo>>(musicInfo);

        //                        list.ForEach(music =>
        //                        {
        //                            string filePath = music.fileUrl;

        //                            string command = "pull -a \"" + "/" + filePath + "\"" + " \"" + winPath + "/" + music.fileName + "\"";
        //                            string res = adbHelper.cmdExecuteWithAdbExit(command) + "\n";
        //                        });


        //                    }
        //                    if (PhotoCheck.IsChecked == true)
        //                    {
        //                        //��Ƭ����
        //                    }
        //                    if (SmsCheck.IsChecked == true)
        //                    {
        //                        //���ű���

        //                        SocketModels.Result result = helper.getResult("sms", "query");
        //                        //���ļ�����һ�ݣ�֮�����ڻ�ԭ
        //                        adbHelper.saveFromPath(result.path,
        //                            GuideWindow.localPath + "\\" + GuideWindow.generatePrefix() + @"\sms\WT.sms");
        //                    }
        //                });
        //            });
        //            appErrorDialog = new ContentDialog
        //            {
        //                Title = "Info",
        //                Content = "Backup successful.",
        //                PrimaryButtonText = "OK"
        //            };
        //            appErrorDialog.XamlRoot = this.Content.XamlRoot;
        //            re = await appErrorDialog.ShowAsync();
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        logHelper.Error(logger,ex.ToString());
        //        throw;
        //    }
                


        //}

        private List<FileData> returnFile(string result, string fullPath)
        {
            List<FileData> fileDatas = new List<FileData>();
            try
            {
                //����result
                var t = result.Split("\n");
                for (int i = 0; i < t.Length; i++)
                {
                    var f = t[i];
                    if (f == "" || f.Substring(0, 5) == "total")
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
                        file.AName = strings[strings.Length - 1];
                        // lsĳЩ�ļ�ʱ��fullpath�����ļ�������Ҫȥ���ļ���
                        if (!file.isDirectory)
                        {
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
                Console.WriteLine(ex.ToString());
                return fileDatas;
            }
        }

        private string ReadFilesize(string m)
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

        private void saveFile(FileData file, string linuxPath, string winPath)
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

        // ����һ��linuxPath������һ��List<FileData>
        private List<FileData> getFileDatasByPath(String path)
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

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values[FileRestoreModePath] = "Incre_restore";
        }

        private void RadioButton_Checked_1(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values[FileRestoreModePath] = "Overwrite_restore";
        }

        private void RadioButton_Checked_2(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values[ContactRestoreModePath] = "Incre_restore";
        }

        private void RadioButton_Checked_3(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values[ContactRestoreModePath] = "Overwrite_restore";
        }


        //���ļ���
        //private void Button_Click_1(object sender, RoutedEventArgs e)
        //{
        //    string content = dropDownButton.Content.ToString();
        //    string folderPath = "";
        //    if (content.Equals("File"))
        //    {
        //        folderPath = MainWindow.BackupPath;
        //    }
        //    else if (content.Equals("Contact"))
        //    {
        //        folderPath = MainWindow.ContactBackupPath;
        //    }
        //    else if (content.Equals("Music"))
        //    {
        //        folderPath = MainWindow.MusicBackupPath;
        //    }
        //    else if (content.Equals("Photo"))
        //    {
        //        folderPath = MainWindow.PhotoBackupPath;
        //    }
        //    else if (content.Equals("Sms"))
        //    {
        //        folderPath = MainWindow.SmsBackupPath;
        //    }

        //    if (!string.IsNullOrEmpty(folderPath))
        //    {
        //        // ʹ���ļ���Դ��������ָ���ļ���
        //        Process.Start("explorer.exe", folderPath);
        //    }
        //}

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

        private void RadioButton_Checked_4(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values[SmsRestoreModePath] = "Incre_restore";
        }

        private void RadioButton_Checked_5(object sender, RoutedEventArgs e)
        {
            ApplicationData.Current.LocalSettings.Values[SmsRestoreModePath] = "Overwrite_restore";
        }
    }
}
