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
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class DialogContent : Page
    {

        public DialogContent()
        {
            this.InitializeComponent();
            // ��������ļ����У���ô��ť���ܵ��
            // �޸������ļ�·���ᵼ����ǰ�ı����Ҳ���
            if (MainWindow.BackupPath != null)
            {
                FilePathTextBox.Text = MainWindow.BackupPath;
                //FilePathTextBox.IsHitTestVisible = false;
                SelectButton.IsEnabled = false;
            }

            // ����Ƶ��
            //string backup_hour = (String)ApplicationData.Current.LocalSettings.Values[MainWindow.BackupHour_Path];
            //string backup_minute = (String)ApplicationData.Current.LocalSettings.Values[MainWindow.BackupMinute_Path];
            //if (backup_hour != null)
            //{
            //    backUpHour.Text = backup_hour;
            //    MainWindow.BackupHour = int.Parse(backup_hour);
            //}
            //if (backup_minute != null)
            //{
            //    backUpMinute.Text = backup_minute;
            //    MainWindow.BackupMinute = int.Parse(backup_minute);
            //}
        }

        private async void ChoosePathButton_Click(object sender, RoutedEventArgs e) {
            var filePicker = new FolderPicker();
            var hWnd = MainWindow.WindowHandle;
            InitializeWithWindow.Initialize(filePicker, hWnd);
            filePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            filePicker.FileTypeFilter.Add("*");
            Windows.Storage.StorageFolder storageFolder = await filePicker.PickSingleFolderAsync();
            if (storageFolder == null)
                return;
            ApplicationData.Current.LocalSettings.Values[MainWindow.Setting_BackupPath] 
                = storageFolder.Path;

            FilePathTextBox.Text = storageFolder.Path;
            MainWindow.BackupPath = storageFolder.Path;
        }

        private void backUpHour_TextChanged(object sender, RoutedEventArgs e)
        {
            var textBox = sender as TextBox;
            if (textBox != null)
            {
                ApplicationData.Current.LocalSettings.Values[MainWindow.BackupHour_Path]
                = textBox.Text;
                MainWindow.BackupHour = int.Parse(textBox.Text);
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
            }
        }


    }
}
