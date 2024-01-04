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

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class RestoreDialogContent : Page
    {
        public RestoreDialogContent()
        {
            this.InitializeComponent();
            // 默认增量模式
            string mode = (string)ApplicationData.Current.LocalSettings.Values[MainWindow.RestoreMode];
            
            if (mode != null)
            {
                if(mode =="Incre")
                {
                    Incre_restore.IsChecked = true;
                }
                else
                {
                    Overwrite_restore.IsChecked = true;
                }
            }
            else
            {
                Incre_restore.IsChecked = true;
            }
        }

        private void Incre_Checked(object sender, RoutedEventArgs e)
        {
            Overwrite_restore.IsChecked = false;
            ApplicationData.Current.LocalSettings.Values[MainWindow.RestoreMode] =
                "Incre";
        }

        private void Incre_Unchecked(object sender, RoutedEventArgs e)
        {
            Overwrite_restore.IsChecked = true;
            ApplicationData.Current.LocalSettings.Values[MainWindow.RestoreMode] =
                "Overwrite";
        }

        private void Overwrite_Checked(object sender, RoutedEventArgs e)
        {
            Incre_restore.IsChecked = false;
            ApplicationData.Current.LocalSettings.Values[MainWindow.RestoreMode] =
                "Overwrite";
        }

        private void Overwrite_Unchecked(object sender, RoutedEventArgs e)
        {
            Incre_restore.IsChecked = true;
            ApplicationData.Current.LocalSettings.Values[MainWindow.RestoreMode] =
                "Incre";
        }
    }
}
