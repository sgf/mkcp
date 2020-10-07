using NetCoreServer;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace mkcp.kcp {
    public class KCPTestSvr : UdpServer {

        public KCPTestSvr(string ipport) : base(IPEndPoint.Parse(ipport)) {
            //IPEndPoint.TryParse(ipport, out IPEndPoint end);
        }

        Dictionary<EndPoint, Kcp> Session = new Dictionary<EndPoint, Kcp>();

        //public void SendData(EndPoint end, Span<byte> data) {
        //    if (Session.TryGetValue(end, out Kcp kcp))
        //        kcp.Send(data);
        //}
        private void KcpSend(Span<byte> data, object userData) {
            var end = userData as EndPoint;
            this.Send(end, data.ToArray());
        }

        public KCPTestSvr(IPAddress address, int port) : base(address, port) { }

        protected override void OnStarted() {
            var mainCtx = SynchronizationContext.Current;
            Timer(mainCtx);
            // Start receive datagrams
            ReceiveAsync();
        }

        uint current() => (uint)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        private void OnTick(object _) {
            foreach (var kcp in Session.Values) {
                var nextUpdate = kcp.Check(current());
                if (current() > nextUpdate)
                    kcp.Update(current());
            }
        }

        private async void Timer(SynchronizationContext mainCtx) {
            while (true) {
                mainCtx.Post(new SendOrPostCallback(OnTick), null);
                await Task.Delay(5);
            }
        }

        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size) {

            // Continue receive datagrams.
            if (size == 0) {
                // Important: Receive using thread pool is necessary here to avoid stack overflow with Socket.ReceiveFromAsync() method!
                ThreadPool.QueueUserWorkItem(o => { ReceiveAsync(); });
            }
            if (size > 0) {
                if (!Session.TryGetValue(endpoint, out Kcp kcp)) {
                    kcp = Kcp.Create(userData: endpoint);
                    kcp.SetOutput(KcpSend);
                    Session.Add(endpoint, kcp);

                }
                kcp.Input(buffer);

                // Echo the message back to the sender
                kcp.Send(buffer.AsSpan().Slice((int)offset, (int)size));
            }
        }

        protected override void OnSent(EndPoint endpoint, long sent) {
            // Continue receive datagrams.
            // Important: Receive using thread pool is necessary here to avoid stack overflow with Socket.ReceiveFromAsync() method!
            ThreadPool.QueueUserWorkItem(o => { ReceiveAsync(); });
        }

        protected override void OnError(SocketError error) {
            Console.WriteLine($"Server caught an error with code {error}");
        }
    }

}
