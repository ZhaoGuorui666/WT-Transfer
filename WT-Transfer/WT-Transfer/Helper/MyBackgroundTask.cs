using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;

namespace WT_Transfer.Helper
{
    public class MyBackgroundTask : IBackgroundTask
    {
        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // 在这里执行后台任务的代码
            await Task.Run(() =>
            {
                // 在这里启动一个线程，并且该线程将在整个应用程序的生命周期中运行
                logHelper.Info(logger, "检测了一次USB状态MyBackgroundTask");
            });
        }
    }
}
