using AdvancedSharpAdbClient;
using Microsoft.UI.Xaml.Shapes;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Media.Protection.PlayReady;
using WT_Transfer.Pages;

namespace WT_Transfer.Helper
{
    public class AdbHelper
    {
        private AdbClient client = GuideWindow.client;
        private DeviceData device = GuideWindow.device;

        public string cmdExecute(string arg)
        {
            IShellOutputReceiver _receiver = new ConsoleOutputReceiver();
            client.ExecuteRemoteCommand(arg, device, _receiver);
            return _receiver.ToString();
        }

        // 带adb的指令 adb pull *****
        // 没有p.WaitForExit();
        public string cmdExecuteWithAdb(string arg)
        {
            Process p = new Process();
            // TODO 更改adb路径
            // p.StartInfo.FileName = ApplicationData.Current.LocalSettings.Values["ADB"].ToString();

            string appFolderPath = AppDomain.CurrentDomain.BaseDirectory;
            string DefaultPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(appFolderPath, "../platform-tools/adb.exe"));

            p.StartInfo.FileName = DefaultPath;
            string _arg = "-s " + GuideWindow.Serial + " " + arg;
            p.StartInfo.Arguments = _arg;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.StandardOutputEncoding = Encoding.UTF8; // 将编码类型设置为 UTF-8
            p.Start();
            //p.WaitForExit();
            string result = p.StandardOutput.ReadToEnd();
            p.Close();
            return result;
        }

        // 带adb的指令 adb pull *****
        // 没有p.WaitForExit();
        public string cmdExecuteWithAdbExit(string arg)
        {
            Process p = new Process();
            // TODO 更改adb路径
            // p.StartInfo.FileName = ApplicationData.Current.LocalSettings.Values["ADB"].ToString();

            string appFolderPath = AppDomain.CurrentDomain.BaseDirectory;
            string DefaultPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(appFolderPath, "../platform-tools/adb.exe"));

            p.StartInfo.FileName = DefaultPath;
            string _arg = "-s " + GuideWindow.Serial + " " + arg;
            p.StartInfo.Arguments = _arg;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardInput = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.StandardOutputEncoding = Encoding.UTF8; // 将编码类型设置为 UTF-8
            p.Start();
            p.WaitForExit();
            string result = p.StandardOutput.ReadToEnd();
            p.Close();
            return result;
        }

        public string readFromPath(string path,string module)
        {
            string filePath = GuideWindow.localPath + "/" + "WT." + module;

            string command = "pull -a \"" + "/" + path + "\"" + " \"" + filePath + "\"";
            string res = cmdExecuteWithAdb(command) + "\n";

            // 使用 File.ReadAllText 方法读取整个文件内容到字符串中
            string contactString = File.ReadAllText(filePath);

            return contactString;
        }

        // 从手机导出到电脑
        public string saveFromPath(string phonePath, string winPath)
        {
            string str = System.IO.Path.GetFullPath(winPath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(winPath));

            string command = "pull -a \"" + "/" + phonePath + "\"" + " \"" + winPath + "\"";
            string res = cmdExecuteWithAdb(command) + "\n";
            return res;
        }

        //有空格，改成单引号
        public string saveFromPathWithBlank(string phonePath, string winPath)
        {
            string str = System.IO.Path.GetFullPath(winPath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(winPath));

            string command = "pull -a \"" + phonePath + "\"" + " \"" + winPath + "\"";
            string res = cmdExecuteWithAdb(command) + "\n";
            return res;
        }

        public string savePathFromPath(string phonePath, string winPath)
        {
            string str = System.IO.Path.GetFullPath(winPath);
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(winPath));

            string command = "pull \"" + "/" + phonePath + "\"" + " \"" + winPath + "\"";
            string res = cmdExecuteWithAdb(command) + "\n";
            return res;
        }

        // 电脑传输到手机
        public string importFromPath(string winPath, string phonePath)
        {
            string command = "push \"" + winPath + "\"" + " \"" + phonePath + "\"";
            string res = cmdExecuteWithAdb(command) + "\n";
            return res;
        }

        //删除操作
        public string delFromPath(string path)
        {
            string command = "shell rm \'" + path+ "\'";
            //string command = "rm " + path;
            string res = cmdExecuteWithAdb(command) + "\n";

            return res;
        }
    }
}
