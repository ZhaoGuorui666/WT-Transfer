using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Models
{
    public class PhoneNumberRecord
    {
        public string Number { get; set; }

        public List<CallRecord> Calls { get; set; }    // 通话记录列表

        public string date { get; set; }

        public PhoneNumberRecord(string number)
        {
            this.Number = number;
            this.Calls = new List<CallRecord>();
        }

        public void AddCallRecord(CallRecord callRecord)
        {
            this.Calls.Add(callRecord);
        }

        public void numberToName()
        {
            Calls = Calls.OrderByDescending(item=>item.Date).ToList();

            if (Calls.First()!=null && !string.IsNullOrEmpty(Calls.First().Name))
            {
                Number = Calls.First().Name;
            }

            string _date = Calls.FirstOrDefault().Date;

            if (DateTime.Today == DateTime.Parse(_date).Date)
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
        }
    }
}
