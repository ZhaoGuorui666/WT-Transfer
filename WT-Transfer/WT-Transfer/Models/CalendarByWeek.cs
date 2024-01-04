using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Models
{
    public class CalendarByWeek
    {
        public List<Calendar> Mo { get; set; }
        public List<Calendar> Tu { get; set; }
        public List<Calendar> We { get; set; }
        public List<Calendar> Th { get; set; }
        public List<Calendar> Fr { get; set; }
        public List<Calendar> Sa { get; set; }
        public List<Calendar> Su { get; set; }

        public String MoDate;
        public String TuDate;
        public String WeDate;
        public String ThDate;
        public String FrDate;
        public String SaDate;
        public String SuDate;
    }
}
