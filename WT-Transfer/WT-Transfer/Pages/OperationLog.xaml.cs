// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using NLog.Targets;
using NLog;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.Extensions.Logging;
using NPOI.Util;
using WT_Transfer.Helper;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class OperationLog : Page
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        LogHelper loghelper = new LogHelper();
        public static string[] logs;
        public static string[] logsBackup;


        public OperationLog()
        {
            this.InitializeComponent();
            if(logs == null || logs.Length == 0)
            {
                Init();
            }
            else
            {
                listView.ItemsSource = logs;
            }
        }

        private void Init()
        {
            try
            {
                var fileTarget = MainWindow.fileTarget;

                var logEventInfo = new LogEventInfo();
                logEventInfo.Properties["userid"] = GuideWindow.serialno;
                string path = fileTarget?.FileName.Render(logEventInfo);

                //Todo
                //string logFilePath = "C:\\Users\\Windows 10\\source\\repos\\App23\\App23\\App23 (Package)\\bin\\x86\\Debug\\AppX\\App23\\file.txt";
                //string[] strings = File.ReadAllLines(logFilePath);
                NLog.LogManager.Shutdown();
                
                logs = File.ReadAllLines(path);
                Array.Reverse(logs);
                List<string> logList = logs.ToList();
                logsBackup = new string[logs.Length];
                Array.Copy(logs, logsBackup, logs.Length);

                listView.ItemsSource = logs;

                LogManager.ReconfigExistingLoggers();
            }
            catch (Exception ex)
            {
                loghelper.Error(logger, ex.ToString());
                throw;
            }
        }


        // Ë¢ÐÂ°´Å¥
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            this.Init();

        }


        private void AutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            // Since selecting an item will also change the text,
            // only listen to changes caused by user entering text.
            if (string.IsNullOrEmpty(sender.Text.ToString()))
            {
                //Contacts = new ObservableCollection<Contact>(ContactsBackUps);
                Array.Clear(logs);

                logs = new string[logsBackup.Length];
                Array.Copy(logsBackup, logs, logsBackup.Length);

                listView.ItemsSource = logs;
                return;
            }

            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                var suitableItems = new List<string>();
                var splitText = sender.Text.ToLower().Split(" ");


                foreach (var log in logs)
                {
                    var found = splitText.All((key) =>
                    {
                        return log.ToLower().Contains(key);
                    });
                    if (found)
                    {
                        suitableItems.Add(log);
                    }
                }
                if (suitableItems.Count == 0)
                {
                    suitableItems.Add("No results found");
                }
                sender.ItemsSource = suitableItems;
            }
        }


        private void AutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            List<string> list = new List<string>();
            if (args.ChosenSuggestion != null)
            {
                //User selected an item, take an action
                Array.Clear(logs);
                list.Add(args.ChosenSuggestion.ToString());
                logs = list.ToArray();

                listView.ItemsSource = logs;
            }
            else if (!string.IsNullOrEmpty(args.QueryText))
            {
                Array.Clear(logs);
                //Do a fuzzy search based on the text
                foreach (var log in logsBackup)
                {
                    if (log.Contains(args.QueryText))
                    {
                        list.Add(log);
                    }
                }

                logs = list.ToArray();
                listView.ItemsSource = logs;
            }
        }

    }
}
