using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

public class UDPServer
{
    //EndPoint mEndPoint;
    UdpClient udpClient;
    IPEndPoint RemotePoint;
    /// <summary>
    /// UDP 服务类
    /// </summary>
    /// <param name="port"></param>
    public UDPServer(UInt16 port)
    {
        IPAddress ipaddress = IPAddress.Parse("127.0.0.1");
        IPEndPoint endpoint = new IPEndPoint(ipaddress, port);
        udpClient = new UdpClient(endpoint);

        RemotePoint = new IPEndPoint(IPAddress.Any, 0);



        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            uint IOC_IN = 0x80000000;
            uint IOC_VENDOR = 0x18000000;
            uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;
            udpClient.Client.IOControl((int)SIO_UDP_CONNRESET, new byte[] { Convert.ToByte(false) }, null);
        }

        //mEndPoint = new IPEndPoint(IPAddress.Any, 0);
    }
    /// <summary>
    /// 发送udp消息
    /// </summary>
    /// <param name="data"></param>
    /// <param name="endPoint"></param>
    public void Send(byte[] data, IPEndPoint endPoint)
    {
        udpClient.Send(data, data.Length, endPoint);
    }
    /// <summary>
    /// 把接收方法给调用方来阻塞
    /// </summary>
    /// <returns></returns>
    public byte[] Receive(ref IPEndPoint RemotePoint)
    {

        byte[] buffer = udpClient.Receive(ref RemotePoint);
        return buffer;
        
    }
}
