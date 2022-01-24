using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ServerDemo
{
    class Program
    {
        /// <summary>
        /// 为true关闭udp消息监听
        /// </summary>
        static bool CloseServer = false;
        static void Main(string[] args)
        {

            Debug.Log("请输入 UDP 服务监听的端口 缺省值为12800");

            string udp_port = Console.ReadLine();
            ushort udpPort = 12800;
            if (ushort.TryParse(udp_port, out var result_udpPort))
            {
                udpPort = result_udpPort;
            }
            
            UDPServer udpServer = new UDPServer(udpPort);
            Network network = new Network(udpServer);

            //启动udp接收消息的线程
            ThreadPool.QueueUserWorkItem(ServerUdpServerThread, udpServer);

            while (true)
            {
                network.Update();
                Thread.Sleep(10);
                GC.Collect();
            }

        }

        /// <summary>
        /// udp接收消息的线程
        /// </summary>
        /// <param name="parameter"></param>
        public static void ServerUdpServerThread(object parameter)
        {
            UDPServer server = parameter as UDPServer;
            IPEndPoint _endPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 0);
            while (!CloseServer)
            {

                Debug.Log($"监听udp消息");

                //阻塞接收udp数据
                byte[] data = server.Receive(ref _endPoint);

                int offset = 0;

                uint cmd = 0;

                offset += Utils.ikcp_decode32u(data, offset, ref cmd);

                #region cmd = 0 是 客户端请求conv , 除此之外 都是kcp通讯的消息
                if (cmd == 0)
                {
                    byte[] buffer = new byte[8];
                    offset = 4;
                    uint conv = ConvManager.GenerateConv();
                    offset += Utils.ikcp_encode32u(buffer, offset, conv);
                    Debug.Log($"分配 conv= {conv}");
                    EventSystem.DispatchEvent(EventID.new_conv, conv);
                    EventSystem.DispatchEvent(EventID.update_conv_ipendpoint, 0, _endPoint);
                    EventSystem.DispatchEvent(EventID.send_udp_buffer, buffer);

                }
                else 
                #endregion
                {
                    uint conv = cmd;
                    EventSystem.DispatchEvent(EventID.update_conv_ipendpoint, conv, _endPoint);

                    EventSystem.DispatchEvent(EventID.receive_udp_buffer, conv, data);

                    Debug.Log($"收到消息 leng= {data.Length}");
                }

                
            }
        }
    }
}
