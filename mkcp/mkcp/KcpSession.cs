using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace mkcp {

    public class KcpSession {
        public KcpSession(uint sid,IPEndPoint ep, KcpSessionManager sessionManager) {
            Peer = ep;
            SessionManager = sessionManager;
            Closed = false;
            LastRevicedTime = DateTimeOffset.UtcNow;
            //kcp.Input(new Span<byte>());
            kcp = new Kcp(sid,null);
        }

        public uint SID => kcp.GetConv();
        private Kcp kcp;

        /// <summary>
        /// 对端的IP和端口
        /// </summary>
        public readonly IPEndPoint Peer;

        public string IP => $"{Peer.Address}:{Peer.Port}";

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

    public class KcpSession<TExtenedSessionInfo> : KcpSession {
        public KcpSession(IPEndPoint ep, KcpSessionManager sessionManager) : base(ep, sessionManager) {

        }
        public TExtenedSessionInfo ExtInfo { get; set; }
    }
}
