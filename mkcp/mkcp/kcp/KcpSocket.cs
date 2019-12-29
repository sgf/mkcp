using System;
using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace mkcp {

    public class KcpSocket : IUdpSocket {

        private KcpSocket(Socket _socket, IKcpIO kcpIO) {
            InnerSocket = _socket;
            SessionMgr = new KcpSessionManager();
            this.kcpIO = kcpIO;
        }

        private readonly KcpSessionManager SessionMgr;

        private readonly IKcpIO kcpIO;

        public void Output(IMemoryOwner<byte> buffer, int avalidLength, IPEndPoint iPEndPoint) {
            var rlt = _this.SendToAsync(buffer.Memory.Slice(0, avalidLength), iPEndPoint);
            if (!rlt.IsCompletedSuccessfully) {
                //log....
            }
        }

        public IUdpSocket _this => this as IUdpSocket;

        public static KcpSocket CreateSvr(IPEndPoint iPEndPoint, IKcpIO kcpIO) {
            var ks = new KcpSocket(SocketHelper.GetUdpSvrSocket(iPEndPoint), kcpIO);
            ks.IPPortLocal = iPEndPoint;
            return ks;
        }
        public static KcpSocket CreateClient(IPEndPoint iPEndPoint, IKcpIO kcpIO) {
            var ks = new KcpSocket(SocketHelper.GetClientSocket(), kcpIO);
            ks.IPPortLocal = (IPEndPoint)ks.InnerSocket.LocalEndPoint;
            ks.IPPortRemote = iPEndPoint;
            return ks;
        }

        /// <summary>
        /// ClientAPI (当Socket为客户端时，服务器IP和端口)
        /// </summary>
        public IPEndPoint IPPortRemote { get; private set; }
        /// <summary>
        /// ServerAPI (当Socket为服务器时，倾听的IP和端口)
        /// </summary>
        public IPEndPoint IPPortLocal { get; private set; }
        public Socket InnerSocket { get; internal set; }

        public void OnUdpReceive(Span<byte> data, IPEndPoint endPoint) {
            kcpIO.OnUdpReceive(data, endPoint);
        }


        public void UdpSend(KcpSession session) {
            _this.SendToAsync()
        }

        /// <summary>
        /// 是否是服务器
        /// </summary>
        private readonly bool IsServer;

        private bool Runing = false;

        public async Task KcpLoop() {
            var updateDelay = 5;//5ms
            var utcNow = DateTime.UtcNow;
            Stopwatch sw = new Stopwatch();
            while (true) {
                sw.Reset();
                var takeTime = (int)sw.ElapsedMilliseconds;
                var waitTime = takeTime - updateDelay >= 0 ? 0 : updateDelay - takeTime;//5ms延迟
                Task.Delay(waitTime);//延迟

            }

        }

        public async Task RunAsync() {
            if (!Runing) {
                Runing = true;
                KcpLoop();//

            }

        }

    }
