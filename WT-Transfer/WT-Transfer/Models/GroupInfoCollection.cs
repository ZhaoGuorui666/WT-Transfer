﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Models
{
    public class GroupInfoCollection<T> : ObservableCollection<T>
    {
        public object Key { get; set; }

        // 公开 Items 属性
        public new ObservableCollection<T> Items
        {
            get { return this; }
        }
    }
}

