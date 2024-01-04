using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Windows.Storage;

namespace WT_Transfer.Helper
{
    public class LocalHelper
    {

        #region 下载状态
        public static string getDownState()
        {
            if (ApplicationData.Current.LocalSettings.Values["downState"] != null)
            {
                return ApplicationData.Current.LocalSettings.Values["downState"].ToString();
            }
            else
                return "";
        }

        public static void setDownState(string state)
        {
            ApplicationData.Current.LocalSettings.Values["downState"] = state;
        }
        #endregion

        #region 下载目录

        public static string getDownPath()
        {
            if (ApplicationData.Current.LocalSettings.Values["downPath"] != null)
            {
                return ApplicationData.Current.LocalSettings.Values["downPath"].ToString();
            }
            else
                return "";
        }

        public static void setDownPath(string path)
        {
            ApplicationData.Current.LocalSettings.Values["downPath"] = path;
        }
        #endregion

        #region 下载的文件名
        public static string getDownName()
        {
            if (ApplicationData.Current.LocalSettings.Values["downName"] != null)
            {
                return ApplicationData.Current.LocalSettings.Values["downName"].ToString();
            }
            else
                return "";
        }

        public static void setDownName(string name)
        {
            ApplicationData.Current.LocalSettings.Values["downName"] = name;
        }
        #endregion

        #region 本地安装条件检测

        public static string IsPackageToInstall(string path, string version)
        {
            string installPath = "";
            //读取xml文件，依次比对给定路径中，遍历对应版本下所有没有子节点的filename节点
            DirectoryInfo exePath = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            string nowPath = exePath.Parent.Parent.Parent.Parent.Parent.FullName;
            string xmlPath = nowPath + "\\Files\\XMLFile.xml";
            XmlDocument xml = new XmlDocument();
            xml.Load(xmlPath);
            //获取到All--Files--对应Version下所有子节点，每个检查
            //读取xml文件,All--InstallPackage--Version--URL、PassWord
            XmlElement root = xml.DocumentElement;  //取到根结点
            XmlNodeList xnl2 = root.ChildNodes;
            XmlNodeList xnl3;
            XmlNodeList xnl4;
            foreach (XmlNode items in xnl2)
            {
                if (items.Name == "Files")
                {
                    xnl3 = items.ChildNodes;
                    foreach (XmlNode item in xnl3)
                    {
                        if (item.Attributes["Name"].Value == version)
                        {
                            //获取它的所有子节点值
                            xnl4 = item.ChildNodes;
                            foreach (XmlNode item2 in xnl4)
                            {
                                string p = path + item2.Attributes["Path"].Value;
                                string n = item2.Attributes["Name"].Value;
                                if (n == "AppxManifest.xml" && version == "win10") installPath = p;
                                else if (version == "win11") installPath = p;
                                if (!File.Exists(p)) { return ""; }
                            }
                        }
                    }
                }
            }
            return installPath;
        }


        #endregion

        #region 获取安装路径和密码

        public static string[] getUrlAndPassWord(string version)
        {
            string[] result = new string[2];
            result[0] = ""; result[1] = "";
            try
            {
                DirectoryInfo exePath = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                string nowPath = exePath.Parent.Parent.Parent.Parent.Parent.FullName;
                string xmlPath = nowPath + "\\Files\\XMLFile.xml";
                XmlDocument xml = new XmlDocument();
                xml.Load(xmlPath);
                //读取xml文件,All--InstallPackage--Version--URL、PassWord
                XmlElement root = xml.DocumentElement;  //取到根结点
                XmlNodeList xnl2 = root.ChildNodes;
                XmlNodeList xnl3;
                foreach (XmlNode items in xnl2)
                {
                    if (items.Name == "InstallPackage")
                    {
                        xnl3 = items.ChildNodes;
                        foreach (XmlNode item in xnl3)
                        {
                            if (item.Attributes["Name"].Value == version)
                            {
                                result[0] = item.Attributes["URL"].Value;
                                result[1] = item.Attributes["PassWord"].Value;
                                break;
                            }
                        }
                    }
                }
            }
            catch
            {
                return result;
            }
            return result;
        }

        #endregion

        #region 获取XML中的微软商店地址和WSA地址

        public static string getStoreUrl()
        {
            string result = "";
            try
            {
                //string appFolderPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                //string xmlPath = Path.GetFullPath(Path.Combine(appFolderPath, "../../Files/XMLFile.xml"));

                string appFolderPath = AppDomain.CurrentDomain.BaseDirectory;
                string xmlPath = Path.GetFullPath(Path.Combine(appFolderPath, "../Files/XMLFile.xml"));


                XmlDocument xml = new XmlDocument();
                xml.Load(xmlPath);
                //读取xml文件,All--InstallPackage--Version--URL、PassWord
                XmlElement root = xml.DocumentElement;  //取到根结点
                XmlNodeList xnl2 = root.ChildNodes;
                XmlNodeList xnl3;
                foreach (XmlNode items in xnl2)
                {
                    if (items.Name == "InstallRemote")
                    {
                        xnl3 = items.ChildNodes;
                        foreach (XmlNode item in xnl3)
                        {
                            if (item.Name == "StoreURL")
                            {
                                result = item.Attributes["Path"].Value;
                                return result;
                            }
                        }
                    }
                }
            }
            catch
            {
                return result;
            }
            return result;
        }

        public static string getStoreWSAUrl()
        {
            string result = "";
            try
            {
                //string appFolderPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                //string xmlPath = Path.GetFullPath(Path.Combine(appFolderPath, "../../Files/XMLFile.xml"));

                string appFolderPath = AppDomain.CurrentDomain.BaseDirectory;
                string xmlPath = Path.GetFullPath(Path.Combine(appFolderPath, "../Files/XMLFile.xml"));

                XmlDocument xml = new XmlDocument();
                xml.Load(xmlPath);
                //读取xml文件,All--InstallPackage--Version--URL、PassWord
                XmlElement root = xml.DocumentElement;  //取到根结点
                XmlNodeList xnl2 = root.ChildNodes;
                XmlNodeList xnl3;
                foreach (XmlNode items in xnl2)
                {
                    if (items.Name == "InstallRemote")
                    {
                        xnl3 = items.ChildNodes;
                        foreach (XmlNode item in xnl3)
                        {
                            if (item.Name == "WSAURL")
                            {
                                result = item.Attributes["Path"].Value;
                                return result;
                            }
                        }
                    }
                }
            }
            catch
            {
                return result;
            }
            return result;
        }

        #endregion

    }
}
