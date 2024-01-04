using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.SocketModels
{
    public class Request
    {
        public String command_id;
        public String module;
        public String operation;
        public Data info;
        public class Data
        {
            public String path;
            public String data_id;
            public ContactInfo contact_info;
        }

        public class ContactInfo
        {
            public String displayName;
            public String note;
            //电话号码
            public Dictionary<String, HashSet<String>> phoneNumbers;
            //邮箱
            public Dictionary<String, HashSet<String>> emails;
            //网站
            public Dictionary<String, HashSet<String>> websites;
            //地址
            public Dictionary<String, HashSet<String>> addresses;
            //组织
            public Dictionary<String, List<List<String>>> organizations;
        }

        public void set(String id, String module, String operation, Data info)
        {
            this.command_id = id;
            this.module = module;
            this.operation = operation;
            this.info = info;
        }
    }
}
