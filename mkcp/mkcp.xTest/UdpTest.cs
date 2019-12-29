using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace mkcp.xTest {
    public class UdpTest {

        protected readonly ITestOutputHelper Output;

        public UdpTest(ITestOutputHelper tempOutput) {
            Output = tempOutput;
        }

        [Fact]
        public void UDP() {
            svrEndPort = new IPEndPoint(IPAddress.Loopback, 7100);
            svr = SocketHelper.GetUdpSvrSocket(svrEndPort);
            cl1 = SocketHelper.GetUdpClientSocket();
            cl2 = SocketHelper.GetUdpClientSocket();
            SvrReceiveAsync();
            ClSendLoop(cl1);
            ClSendLoop(cl2);
            Console.ReadLine();
        }
        IPEndPoint svrEndPort;
        Socket svr;
        Socket cl1;
        Socket cl2;

        public async Task SvrReceiveAsync() {
            while (true) {
                Task.Delay(2000);
                var buff = new byte[8192];
                var endport = new IPEndPoint(IPAddress.Any, 0);
                var rlt = await svr.ReceiveMessageFromAsync(new ArraySegment<byte>(buff), SocketFlags.None, endport);
                Output.WriteLine($"{rlt.ReceivedBytes },{rlt.RemoteEndPoint}");
            }
        }
        int clident = 0;

        public async Task ClSendLoop(Socket cl) {
            var clid = clident++;
            while (true) {
                Task.Delay(2000);
                var nbuff = Encoding.Default.GetBytes($"Msg From:{clid},Time{DateTimeOffset.Now}");
                cl.SendToAsync(new ArraySegment<byte>(nbuff), SocketFlags.None, svrEndPort);
                Output.WriteLine($"客户端{clid}消息发送");
            }
        }


    }
}
