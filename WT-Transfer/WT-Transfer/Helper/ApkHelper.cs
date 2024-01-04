using AdvancedSharpAdbClient;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Windows.Media.Protection.PlayReady;

namespace WT_Transfer.Helper
{
    #region Apk info class 
    public class ApkInfo
    {
        //应用名称、版本、版本号、包名、ICON路径、icon、sdk版本、系统要求、分辨率、用户权限、特性支持
        public string AppName { get; set; }
        public string AppVersion { get; set; }
        public string AppVersionCode { get; set; }
        public string PkgName { get; set; }
        public string IconPath { get; set; }
        public Image AppIcon { get; set; }
        public string MinSdk { get; set; }
        public string MinVersion { get; set; }
        public string ScreenSupport { get; set; }
        public string ScreenSolutions { get; set; }
        public string Permissions { get; set; }
        public string Features { get; set; }

        public ApkInfo(string appName, string appVersion, string appVersionCode, string pkgName, string iconPath, Image appIcon, string minSdk, string minVersion, string screenSupport, string screenSolutions, string permissions, string features)
        {
            AppName = appName;
            AppVersion = appVersion;
            AppVersionCode = appVersionCode;
            PkgName = pkgName;
            IconPath = iconPath;
            AppIcon = appIcon;
            MinSdk = minSdk;
            MinVersion = minVersion;
            ScreenSupport = screenSupport;
            ScreenSolutions = screenSolutions;
            Permissions = permissions;
            Features = features;
        }

        public ApkInfo()
        {

        }
    }
    #endregion


    public sealed partial class ApkHelper
    {
        private string appPath = AppDomain.CurrentDomain.BaseDirectory + "../platform-tools/aapt.exe";
        private string apkPath = AppDomain.CurrentDomain.BaseDirectory + "../Apk/Contacts.apk";

        private List<string> infos = new List<string>();

        public static Dictionary<int, string> SdkMap = new Dictionary<int, string> {
            {1, "Android 1.0 / BASE"},
            {2, "Android 1.1 / BASE_1_1"},
            {3, "Android 1.5 / CUPCAKE"},
            {4, "Android 1.6 / DONUT"},
            {5, "Android 2.0 / ECLAIR"},
            {6, "Android 2.0.1 / ECLAIR_0_1"},
            {7, "Android 2.1.x / ECLAIR_MR1"},
            {8, "Android 2.2.x / FROYO"},
            {9, "Android 2.3, 2.3.1, 2.3.2 / GINGERBREAD"},
            {10, "Android 2.3.3, 2.3.4 / GINGERBREAD_MR1"},
            {11, "Android 3.0.x / HONEYCOMB"},
            {12, "Android 3.1.x / HONEYCOMB_MR1"},
            {13, "Android 3.2 / HONEYCOMB_MR2"},
            {14, "Android 4.0, 4.0.1, 4.0.2 / ICE_CREAM_SANDWICH"},
            {15, "Android 4.0.3, 4.0.4 / ICE_CREAM_SANDWICH_MR1"},
            {16, "Android 4.1, 4.1.1 / JELLY_BEAN"},
            {17, "Android 4.2, 4.2.2 / JELLY_BEAN_MR1"},
            {18, "Android 4.3 / JELLY_BEAN_MR2"},
            {19, "Android 4.4 / KITKAT"}
        };
        public ApkHelper(String path)
        {
            this.apkPath = path;
        }
        public string ApkPath
        {
            get { return this.apkPath; }
        }

        public string ApkSize
        {
            get { return GetApkSize(this.apkPath); }
        }

        private string GetApkSize(string apkPath)
        {
            string apkSize = "0 M";
            if (!File.Exists(apkPath))
                return apkSize;

            FileInfo fi = new FileInfo(apkPath);
            if (fi.Length >= 1024 * 1024)
            {
                apkSize = string.Format("{0:N2} M", fi.Length / (1024 * 1024f));
            }
            else
            {
                apkSize = string.Format("{0:N2} K", fi.Length / 1024f);
            }
            return apkSize;
        }

        public ApkInfo Decoder()
        {
            try
            {
                var aaptPath = this.appPath;
                var startInfo = new ProcessStartInfo("cmd.exe");
                startInfo = new ProcessStartInfo(aaptPath);
                string args = string.Format("dump badging \"{0}\"", this.apkPath);
                startInfo.Arguments = args;
                startInfo.UseShellExecute = false;
                startInfo.RedirectStandardOutput = true;
                startInfo.CreateNoWindow = true;
                startInfo.StandardOutputEncoding = Encoding.UTF8;
                using (var process = Process.Start(startInfo))
                {
                    var sr = process.StandardOutput;
                    while (!sr.EndOfStream)
                    {
                        infos.Add(sr.ReadLine());
                    }
                    process.WaitForExit();
                    //解析
                    return ParseInfo(sr.CurrentEncoding);
                }
            }
            catch (Exception ex)
            {
                throw new Exception("APK Error");
            }

        }

        private ApkInfo ParseInfo(Encoding currentEncoding)
        {
            if (this.infos.Count != 0)
            {

                return DoParseInfo();
            }

            return null;
        }

        private ApkInfo DoParseInfo(Encoding currentEncoding = null)
        {
            ApkInfo apkinfo = new ApkInfo();
            //解析每个字串
            foreach (var info in this.infos)
            {
                if (string.IsNullOrEmpty(info))
                    continue;

                //application: label='MobileGo™' icon='r/l/icon.png'
                if (info.IndexOf("application:") == 0)
                {
                    apkinfo.AppName = GetKeyValue(info, "label=");
                    if (currentEncoding != null)
                        apkinfo.AppName = Encoding.Unicode.GetString(currentEncoding.GetBytes(apkinfo.AppName));
                    apkinfo.IconPath = GetKeyValue(info, "icon=");
                    //GetAppIcon(this.IconPath);
                }

                //package: name='com.wondershare.mobilego' versionCode='4773' versionName='7.5.2.4773'
                if (info.IndexOf("package:") == 0)
                {
                    apkinfo.PkgName = GetKeyValue(info, "name=");
                    apkinfo.AppVersion = GetKeyValue(info, "versionName=");
                    apkinfo.AppVersionCode = GetKeyValue(info, "versionCode=");
                }

                //sdkVersion:'8'
                if (info.IndexOf("sdkVersion:") == 0)
                {
                    apkinfo.MinSdk = GetKeyValue(info, "sdkVersion:");
                    apkinfo.MinVersion = string.Empty;
                    if (!string.IsNullOrEmpty(apkinfo.MinSdk))
                    {
                        int minSdk = 1;
                        if (int.TryParse(apkinfo.MinSdk, out minSdk) && minSdk >= 1 && minSdk <= 19)
                        {
                            apkinfo.MinVersion = SdkMap[minSdk];
                        }
                    }
                }

                //supports-screens: 'small' 'normal' 'large' 'xlarge'
                if (info.IndexOf("supports-screens:") == 0)
                {
                    apkinfo.ScreenSupport = info.Replace("supports-screens:", "").TrimStart().Replace("' '", ", ").Replace("'", "");
                }

                //densities: '120' '160' '213' '240' '320' '480' '640'
                if (info.IndexOf("densities:") == 0)
                {
                    apkinfo.ScreenSolutions = info.Replace("densities:", "").TrimStart().Replace("' '", ", ").Replace("'", "");
                }

                //uses-permission:'android.permission.READ_CONTACTS'
                //uses-permission:'android.permission.WRITE_CONTACTS'
                //uses-permission:'android.permission.READ_SMS'
                if (info.IndexOf("uses-permission:") == 0)
                {
                    string permission = info.Substring(info.LastIndexOf('.') + 1).Replace("'", "");
                    apkinfo.Permissions += permission + "\r\n";
                }

                //uses-feature:'android.hardware.touchscreen'
                if (info.IndexOf("uses-feature:") == 0)
                {
                    string feature = info.Substring(info.LastIndexOf('.') + 1).Replace("'", "");
                    apkinfo.Features += feature + "\r\n";
                }
            }
            if (!string.IsNullOrEmpty(apkinfo.Permissions))
            {
                apkinfo.Permissions = apkinfo.Permissions.Trim();
            }
            if (!string.IsNullOrEmpty(apkinfo.Features))
            {
                apkinfo.Features = apkinfo.Features.Trim();
            }


            return apkinfo;
        }

        private string GetKeyValue(string info, string key)
        {
            if (info.IndexOf(key) != -1)
            {
                int start = info.IndexOf(key) + @key.Length + 1;
                return info.Substring(start, info.IndexOf("'", start) - start);
            }
            return string.Empty;
        }

        private void GetAppIcon(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath))
                return;
            string unzipPath = System.IO.Path.Combine(appPath, @"tools\unzip.exe");
            if (!File.Exists(unzipPath))
                unzipPath = System.IO.Path.Combine(appPath, @"unzip.exe");
            if (!File.Exists(unzipPath))
                return;

            string destPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.IO.Path.GetFileName(iconPath));
            if (File.Exists(destPath))
            {
                File.Delete(destPath);
            }
            var startInfo = new ProcessStartInfo(unzipPath);
            string args = string.Format("-j \"{0}\" \"{1}\" -d \"{2}\"", this.apkPath, iconPath, System.IO.Path.GetDirectoryName(System.IO.Path.GetTempPath()));
            startInfo.Arguments = args;
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            using (var process = Process.Start(startInfo))
            {
                process.WaitForExit(2000);
            }

            if (File.Exists(destPath))
            {
                using (var fs = new FileStream(destPath, FileMode.Open, FileAccess.Read))
                {
                    // apkinfo.AppIcon = null;
                }
                File.Delete(destPath);
            }
        }

        
    }

}
