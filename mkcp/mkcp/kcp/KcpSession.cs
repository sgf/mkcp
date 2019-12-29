using System;
using System.Net;

namespace mkcp {

    public class KcpSession {
        private KcpSession(uint sid, IPEndPoint ep, KcpSessionManager sessionManager) {
            Peer = ep;
            SessionManager = sessionManager;
            Closed = false;
            LastRevicedTime = DateTimeOffset.UtcNow;
            kcp = new Kcp(sid, null);
            kcp.SetOutput(KCPOutput);
        }

        public static (bool isbad, uint conv) IsBadFormat(Span<byte> pk) {
            //有三种结果:
            //1.包不合法，丢弃+拉黑
            if (pk.Length < Kcp.IKCP_OVERHEAD) { //包数据太小
                //logger?.LogInformation($"Client:{kcpSession.IP} duplicate,cant Add to Session list!");
                return (true, 0);
            }
            var dataSize = pk.Length - Kcp.IKCP_OVERHEAD;
            ref var segHead = ref pk.Read<Kcp.SegmentHead>();
            if (dataSize < segHead.len //Data数据太小
               || segHead.cmd < Kcp.IKCP_CMD_PUSH || segHead.cmd > Kcp.IKCP_CMD_WINS) { //cmd命令不存在
                //logger?.LogInformation($"Client:{kcpSession.IP} duplicate,cant Add to Session list!");
                return (true, segHead.conv);
            }
            return (false, segHead.conv);
        }


        public static KcpSession CreateSvrSeesion(uint sid, IPEndPoint ep, KcpSessionManager sessionManager) => new KcpSession(sid, ep, sessionManager);

        public static KcpSession CreateClient(KcpSessionManager sessionManager) => new KcpSession(0, new IPEndPoint(IPAddress.Any, 0), sessionManager);


        public static void KCPInput(KcpSession session, Span<byte> data) => session.kcp.Input(data);

        public static void KCPOutput(byte[] data, int size, object user) =>
        public uint SID => kcp.GetConv();

        private readonly Kcp kcp;
        /// <summary>
        /// 对端的IP和端口
        /// </summary>
        public readonly IPEndPoint Peer;
        public string IPPort => $"{Peer.Address}:{Peer.Port}";
        public DateTimeOffset LastRevicedTime { get; set; }
        public bool Closed { get; private set; }
        private readonly KcpSessionManager SessionManager;
        /// <summary>
        /// 断开连接
        /// </summary>
        public void Close() {
            if (!Closed) {
                Closed = true;
                SessionManager.Remove(this);
            }
        }
    }

}
