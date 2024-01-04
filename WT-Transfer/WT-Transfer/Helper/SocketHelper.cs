using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WT_Transfer.Pages;
using WT_Transfer.SocketModels;
using Org.BouncyCastle.Asn1;
using WT_Transfer.Models;
using NLog;

namespace WT_Transfer.Helper
{
    public class SocketHelper
    {
        Logger logger = LogManager.GetCurrentClassLogger();
        LogHelper logHelper = new LogHelper();

        // 通过socket拿到文件手机返回的第二条命令
        public Result getResult(string module,string operation)
        {
            //Socket client_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            //IPAddress ipAddress = IPAddress.Parse("127.0.0.1");

            //IPEndPoint ipEndpoint = new IPEndPoint(ipAddress, 8888);

            //client_socket.Connect(ipEndpoint);

            try
            {
                Socket client_socket = MainWindow.client_socket;
                while (true)
                {
                    Request request = new Request();
                    request.command_id = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
                    request.module = module;
                    request.operation = operation;
                    string op = JsonConvert.SerializeObject(request);

                    //发送消息到服务端
                    client_socket.Send(Encoding.ASCII.GetBytes(op));

                    byte[] buffer = new byte[1024 * 1024];
                    //接收服务端消息
                    int num;
                    int count = 0;
                    do
                    {
                        if (!client_socket.Connected)
                        {
                            logHelper.Info(logger, "socket disconnected");
                            return new Result();
                        }
                        num = client_socket.Receive(buffer, buffer.Length, 0);
                        string str = Encoding.UTF8.GetString(buffer);
                        if (count == 0)
                        {
                            RequestConfirm requestConfirm = JsonConvert.DeserializeObject<RequestConfirm>(str);
                            if (requestConfirm == null)
                                continue;

                            //检查状态码
                            if (requestConfirm.status.Equals("100"))
                            {
                                count++;
                                continue;
                            }
                            else if (requestConfirm.status.Equals("101"))
                            {
                                Result result = new Result()
                                {
                                    status = "101",
                                };
                                return result;
                            }
                            else
                            {
                                //TODO 提示出错
                                count++;
                            }
                        }
                        else if (count == 1)
                        {
                            Result result = JsonConvert.DeserializeObject<Result>(str);
                            return result;
                        }

                    } while (num > 0);


                    if (count == 2)
                    {
                        break;
                    }
                    //num = client_socket.Receive(buffer);

                    //if (num == 0) continue;
                    ////string str = Encoding.UTF8.GetString(buffer);
                    //Console.WriteLine(str);
                }

                return null;
            }
            catch (Exception ex)
            {
                logHelper.Info(logger, ex.ToString());
                throw;
            }
        }

        public Result ExecuteOp(string op)
        {
            try
            {
                Socket client_socket = MainWindow.client_socket;
                while (true)
                {
                    //发送消息到服务端
                    client_socket.Send(Encoding.UTF8.GetBytes(op));

                    byte[] buffer = new byte[1024 * 1024];
                    //接收服务端消息
                    int num;
                    int count = 0;
                    do
                    {
                        if (!client_socket.Connected)
                        {
                            logHelper.Info(logger, "socket disconnected");
                            return new Result();
                        }

                        num = client_socket.Receive(buffer, buffer.Length, 0);
                        string str = Encoding.UTF8.GetString(buffer);
                        if (count == 0)
                        {
                            //判断是不是粘包
                            string[] strs = str.Split("{");
                            if (strs.Length > 2)
                            {
                                return null;
                            }

                            RequestConfirm requestConfirm = JsonConvert.DeserializeObject<RequestConfirm>(str);
                            if (requestConfirm == null)
                                continue;

                            //检查状态码
                            if (requestConfirm.status.Equals("100"))
                            {
                                count++;
                                continue;
                            }
                            else if (requestConfirm.status.Equals("101"))
                            {
                                Result result = new Result()
                                {
                                    status = "101",
                                };
                                return result;
                            }
                            else
                            {
                                //TODO 提示出错
                                count++;
                            }
                        }
                        else if (count == 1)
                        {
                            Result result = JsonConvert.DeserializeObject<Result>(str);
                            return result;
                        }

                    } while (num > 0);

                }
            }
            catch (Exception ex)
            {

                throw;
            }

        }
    }
}
