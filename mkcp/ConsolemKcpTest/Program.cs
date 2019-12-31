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
using mkcp;
using System.Threading;
using Nito.AsyncEx;

namespace ConsolemKcpTest {
    public class UDPTest {

        public UDPTest() {

            var svrPort = "127.0.0.1:7100";
            var kcpSvr = KcpSvr.Start(svrPort);
            kcpSvr.OnKcpReceive += KcpSvr_OnKcpReceive;
            kcpSvr.OnNew += KcpSvr_OnNew;
            cl1 = KcpClient.Create(svrPort);
            cl1.OnKcpReceive += Kcpcl_OnKcpReceive;
            //cl1.Run();
            //ClSendLoop(cl1);
            var t2 = ClSendLoop(cl1);
            Task.WaitAll(t2);
        }

        private void KcpSvr_OnNew(KcpSession session) {
            Console.WriteLine($"新客户端连接{session.IPPort}");

        }

        private void Kcpcl_OnKcpReceive(Span<byte> data, IPEndPoint endPoint) {
            Console.WriteLine(Encoding.Default.GetString(data));
        }

        private void KcpSvr_OnKcpReceive(KcpSession session, Span<byte> data, IPEndPoint endPoint) {
            Console.WriteLine(Encoding.Default.GetString(data));
        }

        KcpClient cl1;
        int clident = 0;

        public async Task ClSendLoop(KcpClient cl) {

            var clid = clident++;
            await Task.Delay(200);
            while (true) {
                cl.Send(Encoding.Default.GetBytes($"Msg From:{clid},Time{DateTimeOffset.Now}").AsSpan());
                Console.WriteLine($"客户端{clid}消息发送,长度：{41}");
                await Task.Delay(2000);
            }
        }

    }
    class Program {
        private static readonly AsyncCollection<bool> AConnecting = new AsyncCollection<bool>(1);
        static void Main(string[] args) {

            //new UDPTest();

            TestAsync();
            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }



        public static async Task TestAsync() {
            Console.WriteLine(await AsyncTest());
        }

        public static async Task<bool> AsyncTest(int timeOut = 3000) {
            try {
                return await AConnecting.TakeAsync(new CancellationTokenSource(timeOut).Token);
            } catch (Exception ex) {
                if (ex.InnerException is TaskCanceledException)
                    Console.WriteLine(ex.Message);
                return false;
            }
        }
    }
}
