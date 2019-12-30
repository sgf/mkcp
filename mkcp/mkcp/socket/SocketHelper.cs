using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace mkcp {

    public static class SocketHelper {
        private static Socket GetShareConfigSocket(ProtocolType protocol = ProtocolType.Tcp, int send = _8K, int receive = _8K) {
            Socket _socket = null;
            if (protocol == ProtocolType.Tcp) {
                _socket = new Socket(SocketType.Stream, protocol);
                _socket.Blocking = false;
                //关闭后立即释放（据说这玩意儿不定有用）  https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.tcpclient.lingerstate?view=netframework-4.8
                _socket.LingerState = new LingerOption(true, 0);//The socket will linger for 0 seconds after Socket.Close is called.

                _socket.NoDelay = true;//for reduce delay
                /*/SetIPProtectionLevel(Unrestricted) ( 此套接字选项可以扩大或缩小侦听套接字的范围，
                //从而使得公共用户和私人用户可以在适当情况下对站点进行无限制的访问，或者可以根据需要对同一站点进行有限制的访问。) 
                //https://docs.microsoft.com/zh-cn/dotnet/api/system.net.sockets.ipprotectionlevel?view=netframework-4.8/ */
                _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);//运行套接字绑定到已在使用的地址上（可能是端口复用相关，也可能和Socket复用有关） //可能是一些有趣的选项 SocketOptionName.DontFragment,SocketOptionName.DontLinger,SocketOptionName.DontRoute
                //                Path MTU Discovery
                //                NetworkInterface.GetIPProperties().GetIPv4Properties().Mtu
                //NetworkInterface.GetIPProperties().GetIPv6Properties().Mtu
                _socket.SendTimeout = 0;
                _socket.ReceiveTimeout = 0;
                //_socket.Connected
            } else if (protocol == ProtocolType.Udp) {
                _socket = new Socket(SocketType.Dgram, protocol);
                if (OS.IsWindows) {
                    const uint IOC_IN = 0x80000000;
                    const uint IOC_VENDOR = 0x18000000;
                    uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
                    _socket.IOControl((int)SIO_UDP_CONNRESET, new[] { Convert.ToByte(false) }, null);
                }
            } else throw new Exception("协议不支持,仅支持TCP,UDP");

            if (OS.IsWindows)
                _socket.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);//设置IP保护级别为不受限的
            _socket.SendBufferSize = send;
            _socket.ReceiveBufferSize = receive;
            return _socket;
        }
        /// <summary>
        /// 一般用于单个用户的客户端 缓冲区大小适中
        /// </summary>
        /// <returns></returns>
        public static Socket GetClientSocket() => GetShareConfigSocket(send: _16K, receive: _16K);

        /// <summary>
        /// 一般用于网关上下游 数据中转传输 因此分配大缓冲区 大一些
        /// </summary>
        /// <returns></returns>
        public static Socket GetTransferClientSocket() => GetShareConfigSocket(send: _64K, receive: _64K);

        const int _1k = 1024, _4k = _1k * 4, _8K = _1k * 8, _16K = _8K * 2, _32k = _16K * 2, _64K = _8K * 8, _8mb = _8K * _1k, _16mb = _16K * _1k;
        public static Socket GetSessionSocket() => GetShareConfigSocket(send: _16K,//考虑到 MMO 一般来说服务器发送的数据量要比客户端大因此 Session的发送缓冲区 设稍大一点
            receive: _8K);

        public static Socket GetServerAcceptSocket() => GetShareConfigSocket(send: 0,//Accept Socket 因为无须进行任何的数据收发 因此缓冲区置零
            receive: _8K);//设置Socket底层SO_SNDBUF为0 等同于：SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.SendBuffer, 0);
                          //_socket.ExclusiveAddressUse = true; // Don't allow another socket to bind to this port.


        public static Socket GetUdpSvrSocket(IPEndPoint iPEndPoint) {
            var _socket = GetShareConfigSocket(ProtocolType.Udp, send: _8mb, receive: _8mb);
            //收发缓冲区大小将直接影响丢包等(Udp缓冲区满会丢包)
            _socket.Bind(iPEndPoint);
            return _socket;
        }


        public static Socket GetUdpClientSocket() {
            //收发缓冲区大小将直接影响丢包等(Udp缓冲区满会丢包)
            var _socket = GetShareConfigSocket(ProtocolType.Udp, send: _32k, receive: _32k);
            _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
            return _socket;
        }
    }
}
