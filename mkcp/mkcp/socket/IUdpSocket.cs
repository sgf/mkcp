using System;
using System.Buffers;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace mkcp {

    public interface IUdpSeesion {

    }
    public interface ILinker {

    }

    public class UdpKcpLinker {

    }


    public interface IUdpSocket {

        public Socket InnerSocket { get; }
        public IMemoryOwner<byte> GetMemory(int size) => MemoryPool<byte>.Shared.Rent(size);

        public void OnUdpReceive(Span<byte> data, IPEndPoint endPoint);

        public async Task ReceiveMessageFromLoop(ushort buffSize = 1472) {
            while (true) {
                using var mem = GetMemory(buffSize);
                var rlt = await InnerSocket.ReceiveMessageFromAsync(mem.Memory.ToArray(), SocketFlags.None, new IPEndPoint(IPAddress.Any, 0));
                if (rlt.ReceivedBytes > 0)
                    OnUdpReceive(mem.Memory.Span.Slice(0, rlt.ReceivedBytes), (IPEndPoint)rlt.RemoteEndPoint);
                else {
                    //Log......
                }
            }
        }

        public async Task SendToAsync(Memory<byte> buff, EndPoint remoteEndPoint) {
            var sendcnt = await InnerSocket.SendToAsync(buff.ToArray(), SocketFlags.None, remoteEndPoint);
            if (sendcnt != buff.Length)
                throw new IOException("未处理错误（可能是设置的系统发送缓冲区不足）");
        }

    }
}
