// Copyright (c) Microsoft Corporation and Contributors.
// Licensed under the MIT License.

using AdvancedSharpAdbClient;
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
using Windows.Media.Protection.PlayReady;
using WT_Transfer.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace WT_Transfer.Dialog
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SwitchDevice : Page
    {
        List<DeviceRadioButton> radioButtons = new List<DeviceRadioButton>();
        ContentDialog _dialog;

        public SwitchDevice(ContentDialog dialog)
        {
            this.InitializeComponent();
            AdbClient client = GuideWindow.client;
            _dialog = dialog;
            List<AdvancedSharpAdbClient.DeviceData> devices = client.GetDevices();
            foreach(var device in devices)
            {
                DeviceRadioButton radioButton = new DeviceRadioButton();
                //横显示单个设备信息
                TextBlock textBlock = new TextBlock();

                //拿到设备的厂商和设备信息
                IShellOutputReceiver receiver = new ConsoleOutputReceiver();
                client.ExecuteRemoteCommand("getprop ro.product.manufacturer", device, receiver);
                String Manufacturer = receiver.ToString().Replace("\n", "").Replace("\r", "");
                string Model = device.Model.ToString();
                string text = Manufacturer + "-" + Model;
                textBlock.Text = text;
                radioButton.Content = text;
                radioButton.device = device;
                radioButtons.Add(radioButton);
                deviceList.Children.Add(radioButton);
            }
            radioButtons.First().IsChecked = true;
        }

        //选择设备按钮
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            foreach(DeviceRadioButton radioButton in radioButtons)
            {
                if(radioButton.IsChecked == true)
                {
                    GuideWindow.device = radioButton.device;
                    GuideWindow.Serial = radioButton.device.Serial;
                    GuideWindow.DeviceName = radioButton.Content.ToString();
                    _dialog.Hide();
                }
            }
        }
    }
}
