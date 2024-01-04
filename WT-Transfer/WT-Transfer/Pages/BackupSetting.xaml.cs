// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Newtonsoft.Json;
using WT_Transfer.Helper;
using System.Collections.ObjectModel;
using WT_Transfer.Models;
using System.Threading.Tasks;
using System.Diagnostics;
using Windows.Storage.Pickers;
using WinRT.Interop;
using MathNet.Numerics.Statistics;
using Newtonsoft.Json.Linq;
using NPOI.SS.Formula.Functions;
using NLog;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.UI.Dispatching;
using WT_Transfer.SocketModels;
using MathNet.Numerics.RootFinding;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class BackupSetting : Page
    {
        private static string FileCheckedPath = GuideWindow.generatePrefix() + "-" + "FileChecked";
        private static string ContactCheckedPath = GuideWindow.generatePrefix() + "-" + "ContactChecked";
        private static string MusicCheckedPath = GuideWindow.generatePrefix() + "-" + "MusicChecked";
        private static string PhotoCheckedPath = GuideWindow.generatePrefix() + "-" + "PhotoChecked";
        private static string SmsCheckedPath = GuideWindow.generatePrefix() + "-" + "SmsChecked";

        int total;
        int nowCount = 0;
        string nowFileName = "";
        AdbHelper adbHelper = new AdbHelper();
        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();
        CheckUsbHelper checkUsbHelper = new CheckUsbHelper();
        bool isShow = false;

        public BackupSetting()
        {

            try
            {
                this.InitializeComponent();
                //页面初始化
                RootDirBackUpPath.Text =
                    (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_RootBackupPath];

                FileBackUpPath.Text =
                    (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_BackupPath];

                ContactBackUpPath.Text =
                    (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_ContactBackupPath];

                MusicBackUpPath.Text =
                    (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_MusicBackupPath];

                PhotoBackUpPath.Text =
                    (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_PhotoBackupPath];

                SmsBackUpPath.Text =
                    (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_SmsBackupPath];

                // 复选框
                FileCheck.IsChecked =
                    (ApplicationData.Current.LocalSettings.Values[FileCheckedPath] == null
                    || (bool)ApplicationData.Current.LocalSettings.Values[FileCheckedPath] == false)
                    ? false : true;
                ContactCheck.IsChecked =
                    (ApplicationData.Current.LocalSettings.Values[ContactCheckedPath] == null
                    || (bool)ApplicationData.Current.LocalSettings.Values[ContactCheckedPath] == false)
                    ? false : true;
                MusicCheck.IsChecked =
                    (ApplicationData.Current.LocalSettings.Values[MusicCheckedPath] == null
                    || (bool)ApplicationData.Current.LocalSettings.Values[MusicCheckedPath] == false)
                    ? false : true;
                PhotoCheck.IsChecked =
                    (ApplicationData.Current.LocalSettings.Values[PhotoCheckedPath] == null
                    || (bool)ApplicationData.Current.LocalSettings.Values[PhotoCheckedPath] == false)
                    ? false : true;
                SmsCheck.IsChecked =
                    (ApplicationData.Current.LocalSettings.Values[SmsCheckedPath] == null
                    || (bool)ApplicationData.Current.LocalSettings.Values[SmsCheckedPath] == false)
                    ? false : true;
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

        //备份按钮
        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                fileInfoBar.Visibility = Visibility.Collapsed;
                contactInfoBar.Visibility = Visibility.Collapsed;
                musicInfoBar.Visibility = Visibility.Collapsed;
                photoInfoBar.Visibility = Visibility.Collapsed;
                smsInfoBar.Visibility = Visibility.Collapsed;


                //文件备份
                if ((bool)FileCheck.IsChecked)
                {
                    fileInfoBar.Visibility = Visibility.Visible;
                    fileInfoBar.IsOpen = true;

                    await BackupFile();
                }
                //联系人备份
                if ((bool)ContactCheck.IsChecked)
                {
                    contactInfoBar.Visibility = Visibility.Visible;
                    contactInfoBar.IsOpen = true;
                    contactInfoBar.Message = "Contact : Currently backing up contacts";

                    await BackupContactAsync();

                }
                //音乐备份
                if ((bool)MusicCheck.IsChecked)
                {
                    musicInfoBar.Visibility = Visibility.Visible;
                    musicInfoBar.IsOpen = true;
                    musicInfoBar.Message = "Music : Currently backing up musics";

                    await BackupMusics();
                }
                
                //短信备份
                if ((bool)SmsCheck.IsChecked)
                {

                    smsInfoBar.Visibility = Visibility.Visible;
                    smsInfoBar.IsOpen = true;
                    smsInfoBar.Message = "Sms : Currently backing up smss";

                    await BackupSmsAsync();

                }
                //照片备份
                if ((bool)PhotoCheck.IsChecked)
                {
                    photoInfoBar.Visibility = Visibility.Visible;
                    photoInfoBar.IsOpen = true;
                    photoInfoBar.Message = "Photo : Start backing up images.";

                    await BackupPhotoAsync();

                }

            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }

            async Task BackupFile()
            {
                try
                {
                    //初始化 selectedSet
                    HashSet<string> selectedSet = new HashSet<string>();
                    string str = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.SelectedFileSet_BackupPath];
                    HashSet<string> settingSet = new HashSet<string>();
                    if (!string.IsNullOrEmpty(str))
                        settingSet = JsonConvert.DeserializeObject<HashSet<string>>(str);
                    
                    HashSet<string> filePageSet = FileTransfer.selectedSet;
                    if (filePageSet!=null && filePageSet.Count != 0)
                    {
                        selectedSet = filePageSet;
                    }
                    else
                    {
                        selectedSet = settingSet;
                    }
                    if(selectedSet.Count == 0)
                        selectedSet = new HashSet<string>();

                    // 初始化FileSelectedMap
                    if (ApplicationData.Current.LocalSettings.Values[MainWindow.SelectedFileSet_BackupPath] != null)
                    {
                        selectedSet = JsonConvert.DeserializeObject<HashSet<string>>(
                            (string)ApplicationData.Current.LocalSettings.Values[MainWindow.SelectedFileSet_BackupPath]
                            );
                    }
                    await Task.Run(() => {
                        if (selectedSet.Count > 0)
                        {
                            //处理选中的文件项，转换成List<FileData>
                            List<FileData> files = new List<FileData>();
                            foreach (var fullPath in selectedSet)
                            {
                                string res = adbHelper.cmdExecute("ls -l " + fullPath);
                                List<FileData> fileDatas = returnFile(res, fullPath);
                                if (fileDatas.Count == 0)
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
                                    DispatcherQueue.TryEnqueue(() =>
                                    {
                                        fileInfoBar.Message = "File : Backing up : " + nowCount + "/" + total +
                                        " File:" + nowFileName;
                                    });

                                    saveFile(file,
                                        "",
                                        path);
                                }


                            }
                        }
                        DispatcherQueue.TryEnqueue(() => {
                            fileInfoBar.Message = "File : backup is complete.";
                        });
                    });
                }
                catch (Exception ex)
                {
                    logHelper.Info(logger, ex.ToString());
                    throw;
                }
            }

            //备份联系人
            async Task BackupContactAsync()
            {
                try
                {
                    //检查权限
                    if (MainWindow.Permissions[0] == '0')
                    {
                        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, async () =>
                        {
                            permission.XamlRoot = this.XamlRoot;
                            if (!isShow)
                            {
                                isShow = true;
                                ContentDialogResult re = await permission.ShowAsync();
                                if (re == ContentDialogResult.Primary)
                                {
                                    isShow = false;
                                }
                            }
                            MainWindow.Permissions[0] = '1';
                        });
                    }
                    await BackupContact();
                }
                catch (Exception ex)
                {
                    logHelper.Info(logger, ex.ToString());
                    throw;
                }

                async Task BackupContact()
                {
                    // 通过socket拿到文件Path
                    SocketHelper helper = new SocketHelper();


                    await Task.Run(async () =>
                    {

                        Result result = new Result();
                        await Task.Run(() =>
                        {
                            result = helper.getResult("contacts", "query");
                        });

                        if (result.status.Equals("00") && !string.IsNullOrEmpty(result.path))
                        {
                            //把文件保存一份，之后用于还原
                            adbHelper.saveFromPath(result.path,
                                MainWindow.ContactBackupPath + "\\WT.contact");
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                contactInfoBar.Message = "Contact : backup is complete.";
                            });
                        }
                        else if (result.status.Equals("101"))
                        {
                            // 不成功
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                show_error(" No permissions granted.");
                                MainWindow.Permissions[0] = '0';
                            });
                        }
                        else
                        {
                            // 不成功
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                contactInfoBar.Message = "Contact : Backup failed.";
                            });
                        }

                    });
                }

            }

            //Sms
            async Task BackupSmsAsync()
            {
                try
                {
                    //检查权限
                    if (MainWindow.Permissions[0] == '0' || MainWindow.Permissions[1] == '0')
                    {
                        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, async () =>
                        {
                            permission.XamlRoot = this.XamlRoot;
                            if (!isShow)
                            {
                                isShow = true;
                                ContentDialogResult re = await permission.ShowAsync();
                                if (re == ContentDialogResult.Primary)
                                {
                                    isShow = false;
                                }
                            }
                            MainWindow.Permissions[0] = '1';
                            MainWindow.Permissions[1] = '1';
                        });
                    }

                    await BackupSms();


                }
                catch (Exception ex)
                {
                    
                    logHelper.Info(logger, ex.ToString());
                    throw;
                }

                async Task BackupSms()
                {
                    // 通过socket拿到文件Path
                    SocketHelper helper = new SocketHelper();

                    await Task.Run(async () =>
                    {

                        Result result = new Result();
                        await Task.Run(() =>
                        {
                            result = helper.getResult("sms", "query");
                        });

                        if (result.status.Equals("00") && !string.IsNullOrEmpty(result.path))
                        {
                            adbHelper.saveFromPath(result.path,
                                MainWindow.SmsBackupPath + "\\WT.sms");
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                smsInfoBar.Message = "Sms : backup is complete.";
                            });
                        }
                        else if (result.status.Equals("101"))
                        {
                            // 不成功
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                show_error(" No permissions granted.");
                                MainWindow.Permissions[0] = '0';
                                MainWindow.Permissions[1] = '0';
                            });
                        }
                        else
                        {
                            // 不成功
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                smsInfoBar.Message = "Sms : Backup failed.";
                            });
                        }
                    });
                }

            }

            async Task BackupMusics()
            {
                try
                {
                    if (MainWindow.Permissions[4] == '0')
                    {

                        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, async () =>
                        {
                            permission.XamlRoot = this.XamlRoot;
                            if (!isShow)
                            {
                                isShow = true;
                                ContentDialogResult re = await permission.ShowAsync();
                                if (re == ContentDialogResult.Primary)
                                {
                                    isShow = false;
                                }
                            }
                            MainWindow.Permissions[4] = '1';
                        });
                    }

                    await BackupMusic();


                }
                catch (Exception ex)
                {
                  
                    logHelper.Info(logger, ex.ToString());
                    throw;
                }

                async Task BackupMusic()
                {
                    await Task.Run(async () =>
                    {
                        List<MusicInfo> Musics = new List<MusicInfo>();

                        SocketHelper helper = new SocketHelper();
                        AdbHelper adbHelper = new AdbHelper();

                        Result result = new Result();
                        await Task.Run(() =>
                        {
                            result = helper.getResult("music", "query");
                        });

                        if (result.status.Equals("00"))
                        {
                            string musicInfo = adbHelper.readFromPath(result.path, "music");
                            //string path = "C:\\Users\\Windows 10\\Desktop\\1688302257535.music";
                            //string musicInfo = File.ReadAllText(path);
                            List<MusicInfo> list = JsonConvert.DeserializeObject<List<MusicInfo>>(musicInfo);

                            Musics = new List<MusicInfo>(list);

                            string winPath =
                   (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_MusicBackupPath];
                            Musics.ToList().ForEach(music =>
                            {
                                string filePath = music.fileUrl;

                                string command = "pull -a \"" + "/" + filePath + "\"" + " \"" + winPath + "/" + music.fileName + "\"";
                                string res = adbHelper.cmdExecuteWithAdbExit(command) + "\n";

                                DispatcherQueue.TryEnqueue(() =>
                                {
                                    musicInfoBar.Message = "Music : Currently backing up musics: " + music.title;
                                });
                            });

                            DispatcherQueue.TryEnqueue(() =>
                            {
                                musicInfoBar.Message = "Music : backup is complete.";
                            });
                        }
                        else if (result.status.Equals("101"))
                        {
                            // 不成功
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                show_error(" No permissions granted.");
                                MainWindow.Permissions[4] = '0';
                            });
                        }
                        else
                        {
                            // 不成功
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                musicInfoBar.Message = "Music : Music backup failed";
                            });
                        }
                    });
                }
            }

            async Task BackupPhotoAsync()
            {

                try
                {
                    if (MainWindow.Permissions[4] == '0')
                    {
                        permission.XamlRoot = this.XamlRoot;
                        if (!isShow)
                        {
                            isShow = true;
                            ContentDialogResult re = await permission.ShowAsync();
                            if (re == ContentDialogResult.Primary)
                            {
                                isShow = false;
                            }
                        }
                        MainWindow.Permissions[4] = '1';
                    }
                    else
                        await BackupPhoto();

                }
                catch (Exception ex)
                {
                    
                    logHelper.Info(logger, ex.ToString());
                    throw;
                }

                async Task BackupPhoto()
                {
                    List<PhotoInfo> Photos = new List<PhotoInfo>();

                    List<PhotoInfo> PhotosSorted = new List<PhotoInfo>();

                    //查询图片
                    await Task.Run(async () =>
                    {

                        SocketHelper helper = new SocketHelper();
                        AdbHelper adbHelper = new AdbHelper();

                        Result result = new Result();
                        await Task.Run(() =>
                        {
                            result = helper.getResult("picture", "query");
                        });

                        if (result.status.Equals("00") && !string.IsNullOrEmpty(result.path))
                        {
                            string str = adbHelper.readFromPath(result.path, "picture");
                            //读数据
                            JArray jArray = JArray.Parse(str);
                            // use LINQ query to get DisplayName and MobileNum
                            var resultArray = (from item in jArray
                                               select new
                                               {
                                                   Bucket = item["bucket"]?.ToString(),
                                                   Date = item["date"]?.ToString(),
                                                   Path = item["path"]?.ToString()
                                               })
                                            .ToArray();

                            //渲染进map
                            foreach (var item in resultArray)
                            {
                                PhotoInfo photoInfo = new PhotoInfo();
                                photoInfo.Bucket = item.Bucket;
                                photoInfo.Date = item.Date;
                                photoInfo.Path = item.Path;
                                photoInfo.getTitle();

                                if (item.Bucket == null)
                                {
                                    photoInfo.Bucket = "null";
                                }
                                Photos.Add(photoInfo);
                            }

                            //Photos.OrderByDescending(item => item
                            //Todo 要在Photos找是哪张照片，这里不能排序
                            //Photos = Photos.OrderByDescending(item => item.Date).ToList();
                            PhotosSorted = new List<PhotoInfo>(Photos);
                            PhotosSorted = PhotosSorted.OrderBy(p => p.Date).ToList();
                            int num = PhotosSorted.Count;

                            DateTime lastBackUpTime;
                            if (DateTime.TryParse((string)ApplicationData.Current.LocalSettings.Values[MainWindow.PhotoBackUpDate_BackupPath], out lastBackUpTime))
                            {
                                lastBackUpTime = lastBackUpTime.AddDays(-1);
                                PhotosSorted.RemoveAll(item => DateTime.Parse(item.Date) < lastBackUpTime);
                            }
                            num = PhotosSorted.Count;
                        }
                        else if (result.status.Equals("101"))
                        {
                            // 不成功
                            DispatcherQueue.TryEnqueue(() =>
                            {
                                show_error(" No permissions granted.");
                                MainWindow.Permissions[4] = '0';
                            });
                        }
                        else
                        {
                            // 不成功
                            show_error("Photo deletion failed ,please check the phone connection.");
                        }
                    });

                    //备份图片
                    await Task.Run(() =>
                    {
                        foreach (var photo in PhotosSorted)
                        {

                            int num = PhotosSorted.Count;
                            string path = photo.Path;
                            string localPath = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_PhotoBackupPath];
                            string winPath = localPath + "\\" + photo.Bucket + "\\" + photo.Title;

                            adbHelper.saveFromPath(path, winPath);
                            DispatcherQueue.TryEnqueue(DispatcherQueuePriority.High, () =>
                            {
                                photoInfoBar.Message = "Photo : Currently backing up: " + photo.Title;

                                ApplicationData.Current.LocalSettings.Values[MainWindow.PhotoBackUpDate_BackupPath]
                                 = photo.Date;
                            });
                        }

                        DispatcherQueue.TryEnqueue(() =>
                        {
                            photoInfoBar.Message = "Photo : backup is complete.";
                        });
                    });
                }
            }
        }


        private List<FileData> returnFile(string result, string fullPath)
        {
            List<FileData> fileDatas = new List<FileData>();
            try
            {
                //解析result
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
                        // ls某些文件时，fullpath中有文件名，需要去掉文件名
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
     
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //根据传入的file和path存储到本机相应位置
        //如果file是目录，递归，(file,sdcard/file/,winPath)
        //如果是文件，adb pull "/sdcard/Music/01.apk" "E:\研一下学期\APK项目\exe"
        private void saveFile(FileData file, string linuxPath, string winPath)
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

                logHelper.Info(logger, ex.ToString());
                throw;
            } 
        }


        // 打开文件夹，选择文件夹
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                string folderPath = MainWindow.BackupPath;
                Process.Start("explorer.exe", folderPath);
            }
            catch (Exception ex)
            {

                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {

            string folderPath = MainWindow.ContactBackupPath;
            Process.Start("explorer.exe", folderPath);
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {

            string folderPath = MainWindow.MusicBackupPath;
            Process.Start("explorer.exe", folderPath);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {

            string folderPath = MainWindow.PhotoBackupPath;
            Process.Start("explorer.exe", folderPath);
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {

            string folderPath = MainWindow.SmsBackupPath;
            Process.Start("explorer.exe", folderPath);
        }

        private async void Button_Click_6(object sender, RoutedEventArgs e)
        {
            try
            {

                var filePicker = new FolderPicker();
                var hWnd = MainWindow.WindowHandle;
                InitializeWithWindow.Initialize(filePicker, hWnd);
                filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                filePicker.FileTypeFilter.Add("*");
                Windows.Storage.StorageFolder storageFolder = await filePicker.PickSingleFolderAsync();
                if (storageFolder == null)
                    return;

                FileBackUpPath.Text = storageFolder.Path;
                ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_BackupPath] = storageFolder.Path;
            }
            catch (Exception ex)
            {
 
                logHelper.Info(logger, ex.ToString());
                throw;
            }


        }

        private async void Button_Click_7(object sender, RoutedEventArgs e)
        {
            var filePicker = new FolderPicker();
            var hWnd = MainWindow.WindowHandle;
            InitializeWithWindow.Initialize(filePicker, hWnd);
            filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            filePicker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFolder storageFolder = await filePicker.PickSingleFolderAsync();
            if (storageFolder == null)
                return;

            ContactBackUpPath.Text = storageFolder.Path;
            ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_ContactBackupPath] = storageFolder.Path;
        }

        private async void Button_Click_8(object sender, RoutedEventArgs e)
        {
            var filePicker = new FolderPicker();
            var hWnd = MainWindow.WindowHandle;
            InitializeWithWindow.Initialize(filePicker, hWnd);
            filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            filePicker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFolder storageFolder = await filePicker.PickSingleFolderAsync();
            if (storageFolder == null)
                return;

            MusicBackUpPath.Text = storageFolder.Path;
            ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_MusicBackupPath] = storageFolder.Path;
        }

        private async void Button_Click_9(object sender, RoutedEventArgs e)
        {
            var filePicker = new FolderPicker();
            var hWnd = MainWindow.WindowHandle;
            InitializeWithWindow.Initialize(filePicker, hWnd);
            filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            filePicker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFolder storageFolder = await filePicker.PickSingleFolderAsync();
            if (storageFolder == null)
                return;

            PhotoBackUpPath.Text = storageFolder.Path;
            ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_PhotoBackupPath] = storageFolder.Path;
        }

        private async void Button_Click_10(object sender, RoutedEventArgs e)
        {
            var filePicker = new FolderPicker();
            var hWnd = MainWindow.WindowHandle;
            InitializeWithWindow.Initialize(filePicker, hWnd);
            filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            filePicker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFolder storageFolder = await filePicker.PickSingleFolderAsync();
            if (storageFolder == null)
                return;

            SmsBackUpPath.Text = storageFolder.Path;
            ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_SmsBackupPath] = storageFolder.Path;
        }

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

        private void Button_Click_11(object sender, RoutedEventArgs e)
        {
            try
            {
                string folderPath = MainWindow.RootDirBackUpPath;
                Process.Start("explorer.exe", folderPath);
            }
            catch (Exception ex)
            {

                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        //更改根目录
        private async void Button_Click_12(object sender, RoutedEventArgs e)
        {
            var filePicker = new FolderPicker();
            var hWnd = MainWindow.WindowHandle;
            InitializeWithWindow.Initialize(filePicker, hWnd);
            filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            filePicker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFolder storageFolder = await filePicker.PickSingleFolderAsync();
            if (storageFolder == null)
                return;


            string path = storageFolder.Path;
            //判断这个根目录有没有被占用
            Object isUsed = ApplicationData.Current.LocalSettings.Values[path];
            if(isUsed != null && (bool)isUsed == true)
            {
                show_error("The selected directory is already in use. Please choose another folder.");
                return;
            }


            ApplicationData.Current.LocalSettings.Values[RootDirBackUpPath.Text] = false;
            RootDirBackUpPath.Text = path;
            ApplicationData.Current.LocalSettings.Values[path] = true;
            ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_RootBackupPath] = path;
            ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_BackupPath] = path + @"\file";
            ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_MusicBackupPath] = path + @"\music";
            ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_ContactBackupPath] = path + @"\contact";
            ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_PhotoBackupPath] = path + @"\photo";
            ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_SmsBackupPath] = path + @"\sms";

            // 创建目录
            Directory.CreateDirectory(path + @"\file");
            Directory.CreateDirectory(path + @"\music");
            Directory.CreateDirectory(path + @"\contact");
            Directory.CreateDirectory(path + @"\photo");
            Directory.CreateDirectory(path + @"\sms");

            FileBackUpPath.Text =
                    (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_BackupPath];

            ContactBackUpPath.Text =
                (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_ContactBackupPath];

            MusicBackUpPath.Text =
                (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_MusicBackupPath];

            PhotoBackUpPath.Text =
                (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_PhotoBackupPath];

            SmsBackUpPath.Text =
                (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_SmsBackupPath];

            MainWindow.RootDirBackUpPath = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_RootBackupPath];
            MainWindow.BackupPath = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_BackupPath];
            MainWindow.ContactBackupPath = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_ContactBackupPath];
            MainWindow.MusicBackupPath = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_MusicBackupPath];
            MainWindow.PhotoBackupPath = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_PhotoBackupPath];
            MainWindow.SmsBackupPath = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_SmsBackupPath];
        }
    }
}
