using System;
using System.Buffers;
using System.Net.Sockets.Kcp;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ClientDemo
{
    class Program
    {

        static void Main(string[] args)
        {
            Debug.Log("请输入服务器的IP地址 缺省值为127.0.0.1");
            string ip = Console.ReadLine();
            if (string.IsNullOrEmpty(ip.Trim()))
            {
                ip = "127.0.0.1";
            }
            Debug.Log("请输入 UDP 服务监听的端口 缺省值为12800");

            string udp_port = Console.ReadLine();


            ushort udpPort = 12800;
            if (ushort.TryParse(udp_port, out var result_udpPort))
            {
                udpPort = result_udpPort;
            }
            
            UdpClientSession clientSession = new UdpClientSession(ip, udpPort);
            KcpSession kcpsession = new KcpSession();
            Network network = new Network( clientSession, kcpsession);

            int heartbeat = 0;

            while (true)
            {
                if(kcpsession.GetConv > 0)
                {
                    heartbeat++;
                    if (heartbeat > 100)
                    {
                        heartbeat = 0;
                        byte[] buffer = BitConverter.GetBytes(kcpsession.GetConv);
                        EventSystem.DispatchEvent(EventID.send_kcp_buffer, buffer);

                    }
                }

                Thread.Sleep(10);
                network.Update();
                GC.Collect();
            }


        }
    }

    public class Handle : IKcpCallback
    {
        public Action<Memory<byte>> Out;
        public Action<byte[]> Recv;
        public void Receive(byte[] buffer)
        {
            Recv(buffer);
        }

        public IMemoryOwner<byte> RentBuffer(int lenght)
        {
            return null;
        }

        public void Output(IMemoryOwner<byte> buffer, int avalidLength)
        {
            using (buffer)
            {
                Out(buffer.Memory.Slice(0, avalidLength));
            }
        }
    }

}
