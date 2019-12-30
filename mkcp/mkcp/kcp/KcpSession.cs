using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Net;

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

        public readonly Kcp kcp;
        /// <summary>
        /// 对端的IP和端口
        /// </summary>
        public readonly IPEndPoint Peer;
        public string IPPort => $"{Peer.Address}:{Peer.Port}";
        public DateTimeOffset LastRevicedTime { get; private set; }
        public bool Closed { get; private set; }
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


        public void Send(Span<byte> data) {
            kcp.Send(data, 0, data.Length);
        }
    }

}
