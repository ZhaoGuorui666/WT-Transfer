using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WT_TransferHelper
{
    public class resultState
    {
        public int status { get; set; }
        public String output { get; set; }
        public String errer { get; set; }
        public String exception { get; set; }

        public resultState(int status, String output, String errer, String exception)
        {
            this.status = status;  //0是正常结束，2是Exception，1是error
            this.output = output;
            this.errer = errer;
            this.exception = exception;
        }
    }
    public sealed partial class ProcessHelper
    {
        public ProcessHelper()
        {

        }
        public static resultState proc_run(string file, string cmd, bool mod = false)
        {
            try
            {
                System.Diagnostics.ProcessStartInfo psi =
                new System.Diagnostics.ProcessStartInfo();
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                psi.RedirectStandardInput = true;
                psi.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                psi.CreateNoWindow = true;
                psi.UseShellExecute = false;
                psi.FileName = file;
                psi.Arguments = cmd;
                System.Diagnostics.Process process = System.Diagnostics.Process.Start(psi);
                System.IO.StreamReader errStreamReader = process.StandardError;
                System.IO.StreamReader outputStreamReader = process.StandardOutput;

                String err = errStreamReader.ReadToEnd();
                String output = outputStreamReader.ReadToEnd();
                //process.WaitForExit();//等待程序执行完退出进程
                process.Close();

                if (err == "")
                {
                    return new resultState(0, output, err, "");
                }
                return new resultState(1, output, err, "");

            }
            catch (Exception ex)
            {
                return new resultState(2, "", "", ex.Message);
            }
        }
    }
}
