using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace mkcp {

    public class KcpSocket : IUdpSocket {
        //最大可保留对象数量 = Environment.ProcessorCount * 2
        private readonly Dictionary<int, KcpSession> Sessions = new Dictionary<int, KcpSession>();
        private KcpSocket(Socket _socket, bool svr = false) {
            InnerSocket = _socket;
            IsServer = svr;
            if (svr)
                SessionMgr = new KcpSessionManager();
        }

        private readonly KcpSessionManager SessionMgr;

        public void Output(IMemoryOwner<byte> buffer, int avalidLength, IPEndPoint iPEndPoint) {
            var rlt = _this.SendToAsync(buffer.Memory.Slice(0, avalidLength), iPEndPoint);
            if (!rlt.IsCompletedSuccessfully) {
                //log....
            }
        }


        public IUdpSocket _this => this as IUdpSocket;

        public static KcpSocket CreateSvr(IPEndPoint iPEndPoint) {
            var ks = new KcpSocket(SocketHelper.GetUdpSvrSocket(iPEndPoint), true);
            ks.IPPortLocalSvr = iPEndPoint;
            return ks;
        }
        public static KcpSocket CreateClient(IPEndPoint iPEndPoint) {
            var ks = new KcpSocket(SocketHelper.GetClientSocket(), false);
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
        public IPEndPoint IPPortLocalSvr { get; private set; }
        public Socket InnerSocket { get; internal set; }

        public void OnUdpReceive(Span<byte> data, IPEndPoint endPoint) {
            var (isbad, sessoin) = SessionMgr.DetermineIsBadOrNewConnection(data, endPoint);
            if (!isbad) {
                sessoin.INput(data, endPoint, );
            }

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
                //if (socket.ReceiveFromAsync(saea))
                //    saea.BytesTransferred

                while (true) {

                    var v = await _this.ReceiveFromA(512);
                    //new CancellationToken()
                    pipe.Reader.ReadAsync();

                }
            }

        }

    }
