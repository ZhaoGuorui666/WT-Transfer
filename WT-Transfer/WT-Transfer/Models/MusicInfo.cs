using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Models
{
    public class MusicInfo
    {
        public string album { get; set; }
        public string duration { get; set; }
        public string fileName { get; set; }
        public string fileUrl { get; set; }
        public string singer { get; set; }
        public string size { get; set; }
        public string title { get; set; }
        public string type { get; set; }
        public string year { get; set; }

        // 添加一个新属性来跟踪选中状态
        public bool IsSelected { get; set; }
    }
}
