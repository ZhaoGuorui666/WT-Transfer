using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Models
{
    public class DateSmsRecord
    {
        public string Date { get; set; }

        public List<SmsRecord> Smss { get; set; }    // 通话记录列表

        public DateSmsRecord(string date)
        {
            this.Date = date;
            this.Smss = new List<SmsRecord>();
        }

        public void AddCallRecord(SmsRecord smsRecord)
        {
            this.Smss.Add(smsRecord);
        }
    }
}
