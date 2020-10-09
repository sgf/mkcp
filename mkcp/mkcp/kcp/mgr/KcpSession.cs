using Nito.AsyncEx;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace mkcp {

    public class KcpSession {
        private KcpSession(uint sid, IPEndPoint peer, object sMgr) {
            Peer = peer;
            sessionMgr = sMgr;
            Closed = false;
            LastRevicedTime = DateTimeOffset.UtcNow;
            kcp = new Kcp(sid, null);
        }
        private readonly object sessionMgr;

        public static KcpSession CreateSvrSeesion(uint sid, IPEndPoint ep, object sessionManager) => new KcpSession(sid, ep, sessionManager);

        public static KcpSession CreateClientSession(IPEndPoint svrIpPort) => new KcpSession(0, svrIpPort, null);

        public static void KCPInput(KcpSession session, Span<byte> data) {
            if (!session.Closed) {
                session.LastRevicedTime = DateTimeOffset.Now;
                session.kcp.Input(data);
            }
        }

        public uint SID => kcp.GetConv();

        public Kcp kcp;
        /// <summary>
        /// 对端的IP和端口
        /// </summary>
        public readonly IPEndPoint Peer;
        public string IPPort => $"{Peer.Address}:{Peer.Port}";
        public DateTimeOffset LastRevicedTime { get; private set; }
        public bool Closed { get; private set; }

        public bool Connected { get; set; }
        private readonly AsyncProducerConsumerQueue<bool> ConnectingResult1 = new AsyncProducerConsumerQueue<bool>();
        private readonly AsyncCollection<bool> ConnectingResult = new AsyncCollection<bool>(1);
        private bool Connecting = false;

        internal void UpdateConv(uint conv) {
            kcp = new Kcp(conv, null);
        }

        private void OnReceive() {

        }

        /// <summary>
        /// 断开连接
        /// </summary>
        public void Close() {
            if (!Closed) {
                Closed = true;
                if (sessionMgr != null)
                    (sessionMgr as KcpSessionManager).Remove(this);
            }
        }


        /// <summary>
        /// Client Only
        /// </summary>
        /// <param name="timeOut">超时时间（单位毫秒：默认：3000ms 即3秒）</param>
        /// <returns></returns>
        public async ValueTask<bool> ConnectAsync(int timeOut = 3000) {
            if (CanConnectOp()) {
                Send(new Span<byte>());//Send First zero-Size Connect Pack to server
                try {
                    Connected = await ConnectingResult1.DequeueAsync(new CancellationTokenSource(timeOut).Token);
                } catch (Exception ex) {
                    if (ex.InnerException is TaskCanceledException)
                        Console.WriteLine(ex.Message);
                    return false;
                }
                Connecting = false;
                return Connected;
            }
            throw new InvalidOperationException("无法调用多次连接");
        }

        private bool CanConnectOp() {
            if (!Closed && !Connected && !Connecting) {
                Connecting = true;
                return true;
            }
            return false;
        }

        public async Task ReplyConnect() {
            if (CanConnectOp()) {
                Send(new Span<byte>());//Send First zero-Size ReplyConnect Pack to client
            }
        }

        public void Send(Span<byte> data) {
            if (!Closed) {
                if (Connected) {
                    kcp.Send(data);
                } else if (!Connected && !Connecting) {
                    Connecting = true;
                    kcp.Send(data);
                }
            }
        }
    }

}
