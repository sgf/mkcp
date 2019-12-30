using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace mkcp {

    internal class KcpSocket {

        private KcpSocket(Socket _socket, RawReceiveHandler rawReceiveHandler) {
            this._socket = _socket;
            rawReceive = rawReceiveHandler;
        }
        private readonly RawReceiveHandler rawReceive;
        public event Action<long> OnUpdate;

        public IMemoryOwner<byte> GetMemory(int size) => MemoryPool<byte>.Shared.Rent(size);
        public async Task ReceiveLoop(ushort buffSize = 1472) {
            while (true) {
                using var mem = GetMemory(buffSize);
                var rlt = await _socket.ReceiveMessageFromAsync(mem.Memory.ToArray(), SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));
                if (rlt.ReceivedBytes > 0)
                    rawReceive.Invoke(mem.Memory.Span.Slice(0, rlt.ReceivedBytes), (IPEndPoint)rlt.RemoteEndPoint);
                else {
                    //Log......
                }
            }
        }

        public async Task UdpSendToAsync(Memory<byte> buff, EndPoint remoteEndPoint) {
            var sendcnt = await _socket.SendToAsync(buff.ToArray(), SocketFlags.None, remoteEndPoint);
            if (sendcnt != buff.Length)
                throw new IOException("未处理错误（可能是设置的系统发送缓冲区不足）");
        }

        public static KcpSocket CreateSvr(IPEndPoint iPEndPoint, RawReceiveHandler rawReceiveHandler) {
            var ks = new KcpSocket(SocketHelper.GetUdpSvrSocket(iPEndPoint), rawReceiveHandler);
            ks.IPPortLocal = iPEndPoint;
            return ks;
        }
        public static KcpSocket CreateClient(IPEndPoint remote, RawReceiveHandler rawReceiveHandler) {
            var ks = new KcpSocket(SocketHelper.GetUdpClientSocket(), rawReceiveHandler);
            ks.IPPortLocal = (IPEndPoint)ks._socket.LocalEndPoint;
            ks.IPPortRemote = remote;
            return ks;
        }

        /// <summary>
        /// ClientAPI (当Socket为客户端时，服务器IP和端口)
        /// </summary>
        public IPEndPoint IPPortRemote { get; private set; }

        /// <summary>
        /// 本地端口
        /// </summary>
        public IPEndPoint IPPortLocal { get; private set; }

        private readonly Socket _socket;
        private bool Runing = false;

        public void RunKcpLoop(int updateDelay = 5/*5ms*/) {
            if (!Runing) {
                Runing = true;
                _ = Task.Factory.StartNew(async () => {
                    Stopwatch sw = new Stopwatch();
                    while (true) {
                        sw.Reset();
                        OnUpdate?.Invoke(DateTime.UtcNow.Ticks);
                        var takeTime = (int)sw.ElapsedMilliseconds;
                        var waitTime = takeTime - updateDelay >= 0 ? 0 : updateDelay - takeTime;//5ms延迟
                        await Task.Delay(waitTime);//延迟
                    }
                });
            }
        }
    }
}
