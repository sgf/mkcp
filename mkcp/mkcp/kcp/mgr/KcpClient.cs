using System;
using System.Net;
using System.Threading.Tasks;
using static mkcp.Kcp;

namespace mkcp {
    public delegate void RawReceiveHandler(Span<byte> data, IPEndPoint endPoint);
    public delegate void KcpClientReceiveHandler(Span<byte> data, IPEndPoint endPoint);

    public class KcpClient {
        private KcpClient(IPEndPoint svrIpPort) {
            KcpSession = KcpSession.CreateClientSession(svrIpPort);
            _sock = KcpSocket.CreateClient(svrIpPort, OnRawReceive);
            KcpSession.kcp.SetOutput((data, size, user) => {
                if (!KcpSession.Closed)
                    _ = _sock.UdpSendToAsync(new Memory<byte>(data, 0, size), KcpSession.Peer);
            });
            _sock.OnUpdate += _sock_OnUpdate;
        }

        private void _sock_OnUpdate(long obj) {
            KcpSession.kcp.Update((uint)obj);
        }

        public static KcpClient Create(string svrIpPort) {
            if (!IPEndPoint.TryParse(svrIpPort, out IPEndPoint ipport))
                throw new FormatException("IP以及端口格式有问题，请检查");
            return new KcpClient(ipport);
        }

        private readonly KcpSocket _sock;

        private readonly KcpSession KcpSession;

        public event KcpClientReceiveHandler OnKcpReceive;

        private void OnRawReceive(Span<byte> data, IPEndPoint endPoint) {
            if (endPoint != KcpSession.Peer) return;//忽略不是目标服务器的端口
            ref var seghead = ref data.Read<SegmentHead>();
            if (!KcpSession.Connected && seghead.conv > 0) {
                KcpSession.Connected = true;
                KcpSession.UpdateConv(seghead.conv);
            }

            KcpSession.KCPInput(KcpSession, data);
            using var mem = _sock.GetMemory(OS._4kb);
            var buff = mem.Memory.ToArray();
            var rcnt = KcpSession.kcp.Recv(buff, 0, mem.Memory.Length);
            if (rcnt > 0)
                OnKcpReceive?.Invoke(buff.AsSpan().Slice(0, rcnt), endPoint);
        }

        public void Send(Span<byte> data) => KcpSession.Send(data);

        public async Task<bool> ConnectAsync() {
            _ = _sock.ReceiveAsyncLoop();
            _sock.RunKcpLoop();
            return await this.KcpSession.ConnectAsync();
        }
    }
}
