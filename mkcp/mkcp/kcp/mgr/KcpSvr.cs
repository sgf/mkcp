using System;
using System.Net;

namespace mkcp {


    public delegate void KcpSvrReceiveHandler(KcpSession session, Span<byte> data, IPEndPoint endPoint);

    public class KcpSvr {
        private KcpSvr(IPEndPoint svrIpPort, bool autoOnService = true) {
            SessionMgr = new KcpSessionManager();
            _sock = KcpSocket.CreateSvr(svrIpPort, OnRawReceive);
            SessionMgr.OnNew = this.OnNew; //SessionMgr_OnNew;
            SessionMgr.OnKick = this.OnKick; //SessionMgr_OnKick;
            AutoOnService = autoOnService;//自动启动就是什么也不做就接收消息了。（否则，在接收到消息时 应该果断丢弃，这样客户端发过来的消息就得不到反馈，就意味着服务没有开启）

            _sock.OnUpdate += _sock_OnUpdate;
            _ = _sock.ReceiveAsyncLoop();
            _sock.RunKcpLoop();
        }

        private void _sock_OnUpdate(long obj) {
            SessionMgr.Update(obj);
        }

        private void OnRawReceive(Span<byte> data, IPEndPoint endPoint) {
            //检查黑名单
            if (SessionMgr.InBad(endPoint)) return;
            //检查格式
            var (isBad, conv) = Kcp.IsBadHeadFormat(data);
            if (isBad) { SessionMgr.AddBad(endPoint); return; }

            var session = SessionMgr.DetermineIsBadOrNewConnection(conv, endPoint);
            if (session == null) return;
            KcpSession.KCPInput(session, data);
            using var mem = _sock.GetMemory(OS._8kb);
            var buff = mem.Memory.ToArray();
            var rcnt = session.kcp.Recv(buff, 0, buff.Length);
            if (rcnt > 0)
                OnKcpReceive?.Invoke(session, buff.AsSpan().Slice(0, rcnt), endPoint);
        }


        private void SessionMgr_OnKick(KcpSession session) {
            throw new NotImplementedException();
        }

        private void SessionMgr_OnNew(KcpSession session) {
            //新的连接
        }


        public event KcpSvrReceiveHandler OnKcpReceive;


        public event NewSessionHandler OnNew;
        public event KickSessionHandler OnKick;
        private readonly KcpSessionManager SessionMgr;
        private readonly KcpSocket _sock;
        private readonly bool AutoOnService;
        public static KcpSvr Start(string svrIpPort, bool autoOnService = true) {
            if (!IPEndPoint.TryParse(svrIpPort, out IPEndPoint ipport))
                throw new FormatException("IP以及端口格式有问题，请检查");
            return new KcpSvr(ipport, autoOnService);
        }


    }
}
