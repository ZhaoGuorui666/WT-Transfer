using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_Transfer.Helper
{
    public class LogHelper
    {
        public LogHelper() {
            // 生成一个对应当前用户的logger
        }
        public void Info(Logger logger, string content)
        {
            logger.Log(new LogEventInfo()
            {
                Level = LogLevel.Info,
                LoggerName = logger.Name,
                Message = content,
                Properties = {
                    { "userid",GuideWindow.serialno}
                },
            });
        }

        public void Debug(Logger logger, string content)
        {
            logger.Log(new LogEventInfo()
            {
                Level = LogLevel.Debug,
                LoggerName = logger.Name,
                Message = content,
                Properties = {
                    { "userid",GuideWindow.serialno}
                },
            });
        }

        public void Error(Logger logger,string content) {
            logger.Log(new LogEventInfo()
            {
                Level = LogLevel.Error,
                LoggerName = logger.Name,
                Message = content,
                Properties = {
                    { "userid",GuideWindow.serialno}
                },
            });
        }
    }
}
