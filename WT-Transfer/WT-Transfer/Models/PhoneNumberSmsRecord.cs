using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Models
{
    public class PhoneNumberSmsRecord
    {
        public string Number { get; set; }

        public List<SmsRecord> Smss { get; set; }    // 通话记录列表
        public string date { get; set; }
        public string brief { get; set; }

        public PhoneNumberSmsRecord(string number)
        {
            this.Number = number;
            this.Smss = new List<SmsRecord>();
        }

        public void AddCallRecord(SmsRecord smsRecord)
        {
            this.Smss.Add(smsRecord);
        }

        public void sortByDate()
        {
            Smss = Smss.OrderByDescending(record => record.Date).ToList();
            string _date = Smss.FirstOrDefault().Date;  
            
            if(DateTime.Today == DateTime.Parse(_date).Date)
            {
                //如果是当天
                date = DateTime.Parse(_date).TimeOfDay.ToString();
            }
            else if (DateTime.Today.AddDays(-1) == DateTime.Parse(_date).Date)
            {
                //如果是昨天
                date = "Yesterday";
            }
            else
            {
                date = DateTime.Parse(_date).ToString("yyyy-MM-dd");
            }

            //set brief
            string str = Smss.FirstOrDefault().Body;
            brief = str.Substring(0, Math.Min(str.Length, 20))+"...";
        }

    }
}
