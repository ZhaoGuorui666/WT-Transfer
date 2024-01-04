using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer
{
    public class Operation
    {
        public DateTime time { get; set; }
        //操作的模块
        public string module { get; set; }
        //详细内容
        public string content { get; set; }

        public Operation(DateTime _time, string _module, string _content)
        {
            time = _time;
            module = _module;
            content = _content;
        }

        public override string ToString()
        {
            return $"{module},{content},{time:yyyy-MM-dd HH:mm:ss}";
        }

        public static Operation Parse(string s)
        {
            var parts = s.Split(',');

            DateTime dateTime = DateTime.ParseExact(parts[2], "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            Operation operation = new Operation(dateTime, parts[0], parts[1]);

            return operation;
        }
    }
}
