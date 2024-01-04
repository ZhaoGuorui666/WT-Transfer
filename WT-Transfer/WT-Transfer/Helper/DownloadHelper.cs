using Downloader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Helper
{
    public class DownloadHelper
    {
        public static DownloadConfiguration Configuration;

        static DownloadHelper()
        {
            Configuration = new DownloadConfiguration
            {
                ChunkCount = 8,
                ParallelDownload = true
            };
        }
    }
}
