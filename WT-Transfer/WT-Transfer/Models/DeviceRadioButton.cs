using AdvancedSharpAdbClient;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Models
{
    public  class DeviceRadioButton : RadioButton
    {
        public DeviceData device { get; set; }
    }
}
