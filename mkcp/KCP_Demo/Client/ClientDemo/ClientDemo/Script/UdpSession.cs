using System;
using System.Net;
using System.Net.Sockets;

public class UdpState
{
    public UdpClient u;
    public IPEndPoint e;
}

/// <summary>
/// udp 客户端 session
/// </summary>
public class UdpClientSession
{

    UdpClient udpClient;

    IPEndPoint e;

    public UdpClientSession(string ip, int port)
    {
        IPAddress ipaddress = IPAddress.Parse(ip);
        e = new IPEndPoint(ipaddress, port);
    }
    public void Close()
    {
        udpClient?.Dispose();
        udpClient?.Close();
        udpClient = null;
    }
    
    /// <summary>
    /// 初始化udp
    /// </summary>
    public void Connect()
    {
        udpClient = new UdpClient(PortUtils.GetNewPort());
        UdpState s = new UdpState();
        s.e = e;
        s.u = udpClient;
        udpClient.BeginReceive(new AsyncCallback(ReceiveBuffer), s);
        EventSystem.RegisterEvent(EventID.send_udp_buffer, SendBuffer);
        EventSystem.RegisterEvent(EventID.receive_udp_buffer, ReceiveUdpBuffer);
        //去初始udp
        EventSystem.DispatchEvent(EventID.udp_init_success);
        
    }


    /// <summary>
    /// 接收udp消息
    /// </summary>
    /// <param name="buffer"></param>
    public void ReceiveUdpBuffer(byte[] buffer)
    {
        #region byte[] 头部4个字节为0 表示返回conv
        uint cmd = 0;
        int offset = 0;
        offset += Utils.ikcp_decode32u(buffer, offset, ref cmd);
        if (cmd == 0)
        {
            uint conv = 0;
            offset += Utils.ikcp_decode32u(buffer, offset, ref conv);
            Debug.Log($"返回的conv = {conv}");

            EventSystem.DispatchEvent(EventID.get_conv, conv);
        }
        else
        #endregion
        {
            //把udp消息分发给kcp
            EventSystem.DispatchEvent(EventID.udpbuffer_to_kcp, buffer);

        }

    }
    /// <summary>
    /// 发送buffer
    /// </summary>
    /// <param name="buffer"></param>
    public void SendBuffer(byte[] buffer)
    {
        Debug.Log("发送udp消息");

        if (udpClient != null)
        {
            try
            {
                int count = udpClient.Send(buffer, buffer.Length, e);
                Debug.Log($"udp消息发完毕 发送长度 = {count}");
            }
            catch (Exception e)
            {
                Debug.Log(e);
                EventSystem.DispatchEvent(EventID.network_disconnect);
            }
        }
    }
    /// <summary>
    /// 发送buffer回调
    /// </summary>
    /// <param name="ar"></param>
    void SendBuffer_Callback(IAsyncResult ar)
    {
        UdpClient u = (UdpClient)ar.AsyncState;
        u.EndSend(ar);
        Debug.Log("udp消息发完毕");

    }
    /// <summary>
    /// 收到buffer
    /// </summary>
    /// <param name="ar"></param>
    void ReceiveBuffer(IAsyncResult ar)
    {
        Debug.Log("收到upd buffer");
        try
        {
            UdpState s = (UdpState)ar.AsyncState;
            UdpClient u = s.u;
            IPEndPoint e = s.e;
            byte[] buffer = u.EndReceive(ar, ref e);
            EventSystem.DispatchEvent(EventID.receive_udp_buffer, buffer);
            Debug.Log("监听upd");
            udpClient.BeginReceive(new AsyncCallback(ReceiveBuffer), s);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            EventSystem.DispatchEvent(EventID.network_disconnect);
        }

    }
}

