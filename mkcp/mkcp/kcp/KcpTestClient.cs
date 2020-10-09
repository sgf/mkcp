using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace mkcp.kcp {
    public class KcpTestClient : NetCoreServer.UdpClient {

        public KcpTestClient(string address, int port, int messages) : base(address, port) {
            _messages = messages;
            kcp = Kcp.Create();
            kcp.SetOutput(KcpOutput);
        }
        public void SendData(Memory<byte> data) => kcpOps.Enqueue(KcpOp.Send(kcp, data));
        private void KcpOutput(Span<byte> data, object _) => this.Send(data.ToArray());

        public Kcp kcp;

        public static byte[] MessageToSend;
        public static DateTime TimestampStart = DateTime.UtcNow;
        public static DateTime TimestampStop = DateTime.UtcNow;
        public static long TotalErrors;
        public static long TotalBytes;
        public static long TotalMessages;

        public enum kop {
            connect,
            send,
            onRecive,
            onUdpRecive,
            close
        }

        public struct KcpOp {
            private KcpOp(Kcp kcp, kop op, Memory<byte> buff) {
                this.op = op;
                this.kcp = kcp;
                this.buff = buff;
            }
            public readonly kop op;
            public readonly Kcp kcp;
            public readonly Memory<byte> buff;
            public static KcpOp Send(Kcp kcp, Memory<byte> buff) => new KcpOp(kcp, kop.send, buff);
            public static KcpOp OnReceive(Kcp kcp, Memory<byte> buff) => new KcpOp(kcp, kop.send, buff);
        }

        ConcurrentQueue<KcpOp> kcpOps = new ConcurrentQueue<KcpOp>();

        uint current() => (uint)new DateTimeOffset(DateTime.UtcNow).ToUnixTimeSeconds();
        private void OnTick() {
            kcp.Update(current());
            var nextUpdate = kcp.Check(current());
            var nexttimespan = nextUpdate - current();
        }

        private async void Timer() {
            int interval = 500;
            var sw = new Stopwatch();
            var swait = new SpinWait();
            while (true) {
                if (!kcpOps.TryDequeue(out KcpOp op)) {
                    swait.SpinOnce();
                } else {

                    switch (op.op) {
                        case kop.connect://连接
                        case kop.send://发送
                            kcp.Send(op.buff.Span);
                            break;
                        case kop.onUdpRecive://Udp接收
                            kcp.Input(op.buff.Span);
                            break;
                        case kop.onRecive://kcp接收
                            break;
                        case kop.close://关闭
                            break;
                    }
                }
                if (sw.ElapsedMilliseconds > interval) { //timeout
                    sw.Restart();
                    OnTick();
                }
            }
        }

        protected override void OnConnected() {
            Timer();

            // Start receive datagrams
            ReceiveAsync();

            for (long i = _messages; i > 0; --i)
                SendMessage();
        }

        protected override void OnReceived(EndPoint endpoint, byte[] buffer, long offset, long size) {
            if (size > 0) {
                kcp.Input(buffer);
            }

            TimestampStop = DateTime.UtcNow;
            TotalBytes += size;
            ++TotalMessages;

            // Continue receive datagrams
            // Important: Receive using thread pool is necessary here to avoid stack overflow with Socket.ReceiveFromAsync() method!
            ThreadPool.QueueUserWorkItem(o => { ReceiveAsync(); });

            SendMessage();
        }

        protected override void OnError(SocketError error) {
            Console.WriteLine($"Client caught an error with code {error}");
            ++TotalErrors;
        }

        private void SendMessage() {
            SendData(MessageToSend);
        }

        private long _messages;
    }
}
