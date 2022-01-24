using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;


/// <summary>
/// 端口工具类
/// </summary>
class PortUtils
{
    /// <summary>
    /// 获取一个新的可用端口
    /// </summary>
    /// <returns></returns>
    public static int GetNewPort()
    {
        var portList =  PortIsUsed();
        int port = 0;
        var random = new Random();
        do
        {
            port = random.Next(10000, UInt16.MaxValue - 1);
            
        } while (portList.Contains(port));

        return port;
    }

    /// <summary>
    /// 获取已经用到的端口
    /// </summary>
    /// <returns></returns>
    public static List<int> PortIsUsed()

    {

        //获取本地计算机的网络连接和通信统计数据的信息            

        IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();

        //返回本地计算机上的所有Tcp监听程序            

        IPEndPoint[] ipsTCP = ipGlobalProperties.GetActiveTcpListeners();

        //返回本地计算机上的所有UDP监听程序            

        IPEndPoint[] ipsUDP = ipGlobalProperties.GetActiveUdpListeners();

        //返回本地计算机上的Internet协议版本4(IPV4 传输控制协议(TCP)连接的信息。            

        TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();

        List<int> allPorts = new List<int>();

        foreach (IPEndPoint ep in ipsTCP)

        {

            allPorts.Add(ep.Port);

        }

        foreach (IPEndPoint ep in ipsUDP)

        {

            allPorts.Add(ep.Port);

        }

        foreach (TcpConnectionInformation conn in tcpConnInfoArray)

        {

            allPorts.Add(conn.LocalEndPoint.Port);

        }

        return allPorts;

    }
}

