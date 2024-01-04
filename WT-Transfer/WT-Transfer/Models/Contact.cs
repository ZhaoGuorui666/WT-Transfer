using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Models
{
    public class Contact
    {
        public string _id { get; set; }
        public Dictionary<string, List<string>> addresses { get; set; }
        public Dictionary<string, List<string>> emails { get; set; }
        public Dictionary<string, List<string>> ims { get; set; }
        public string nickname { get; set; }
        public string note { get; set; }
        public Dictionary<string, List<List<object>>> organizations { get; set; }
        public Dictionary<string, List<string>> phoneNumbers { get; set; }
        public Dictionary<string, List<string>> sipAddresses { get; set; }
        public List<string> structuredName { get; set; }
        public Dictionary<string, List<string>> websites { get; set; }
    }
}
