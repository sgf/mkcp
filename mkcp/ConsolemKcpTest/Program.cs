using mkcp;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace ConsolemKcpTest {
    public class UDPTest {

        public UDPTest() {
            svrEndPort = new IPEndPoint(IPAddress.Loopback, 7100);
            svr = SocketHelper.GetUdpSvrSocket(svrEndPort);
            cl1 = SocketHelper.GetUdpClientSocket();
            cl2 = SocketHelper.GetUdpClientSocket();
            //kcpSeesion = new kcp(0, null);
            //Svrkcp.Update()
            SvrReceiveAsync();
            //ClSendLoop(cl1);
            ClSendLoop(cl2);
        }

        Kcp kcpSeesion;
        IPEndPoint svrEndPort;
        Socket svr;
        Socket cl1;
        Socket cl2;

        public async Task SvrReceiveAsync() {
            var buff = new byte[20];
            while (true) {
                await Task.Delay(6);
                try {
                    var endport = new IPEndPoint(IPAddress.Any, 0);
                    Console.WriteLine($"缓冲区包长度{svr.Available}");
                    var rlt = await svr.ReceiveMessageFromAsync(new ArraySegment<byte>(buff), SocketFlags.None, endport);
                    //var rlt = await svr.ReceiveFromAsync(new ArraySegment<byte>(buff), SocketFlags.None, endport);
                    //kcpSeesion.Input(buff.AsSpan().Slice(0, rlt.ReceivedBytes)); 

                    //Console.WriteLine($"端口{endport}");
                    Console.WriteLine($"端口{rlt.RemoteEndPoint},长度:{rlt.ReceivedBytes }，{Encoding.Default.GetString(buff, 0, rlt.ReceivedBytes)}");
                } catch (Exception ex) {
                    Console.WriteLine(ex.Message);
                }
            }
        }
        int clident = 0;

        public async Task ClSendLoop(Socket cl) {
            var clid = clident++;
            await Task.Delay(200);
            while (true) {
                var nbuff = Encoding.Default.GetBytes($"Msg From:{clid},Time{DateTimeOffset.Now}");
                var sendcnt = await cl.SendToAsync(new ArraySegment<byte>(nbuff), SocketFlags.None, svrEndPort);
                Console.WriteLine($"客户端{clid}消息发送,长度：{sendcnt}");
                await Task.Delay(200000);
            }
        }

    }
    class Program {
        static void Main(string[] args) {
            new UDPTest();

            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }
    }
}
