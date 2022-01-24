using System;
using System.Threading;

public class Network
{
    UdpClientSession udpClientSession;
    KcpSession kcpSession;


    public Network( UdpClientSession udpClientSession, KcpSession kcpSession)
    {
        this.udpClientSession = udpClientSession;
        this.kcpSession = kcpSession;

        EventSystem.RegisterEvent(EventID.kcp_init_success, KCPInitSuccess);
        EventSystem.RegisterEvent(EventID.get_conv, OnGetConv);
        EventSystem.RegisterEvent(EventID.udp_init_success, UDPInitSuccess);
        EventSystem.RegisterEvent(EventID.udpbuffer_to_kcp, UDPBufferToKcp);
        EventSystem.RegisterEvent(EventID.send_kcp_buffer, SendKCPBuffer);
        EventSystem.RegisterEvent(EventID.receive_kcp_buffer, ReceiveKcpBuffer);
        EventSystem.RegisterEvent(EventID.try_get_conv, TryGetConv);
        EventSystem.RegisterEvent(EventID.network_disconnect, NetworkDisconnect);
        EventSystem.RegisterEvent(EventID.connect_udp, ConnectUDP);

        EventSystem.DispatchEvent(EventID.connect_udp);


    }
    /// <summary>
    /// 连接udp事件
    /// </summary>
    void ConnectUDP()
    {
        try
        {
            udpClientSession.Connect();
        }
        catch (Exception e)
        {
            Debug.Log(e);
            EventSystem.DispatchEvent(EventID.network_disconnect);
        }
    }
    /// <summary>
    /// 网络连接断开事件
    /// </summary>
    void NetworkDisconnect()
    {
        Debug.Log("释放网络连接");

        udpClientSession.Close();
        kcpSession.Close();

        Debug.Log("2秒后重连");
        Thread.Sleep(2000);

        EventSystem.DispatchEvent(EventID.connect_udp);
    }
    /// <summary>
    /// 尝试获取conv
    /// </summary>
    void TryGetConv()
    {
        byte[] buffer = new byte[4];
        EventSystem.DispatchEvent(EventID.send_udp_buffer, buffer);

    }

    /// <summary>
    /// kcp消息返回
    /// 也就是服务器返回的消息
    /// </summary>
    /// <param name="buffer"></param>
    void ReceiveKcpBuffer(byte[] buffer)
    {
        Debug.Log($"接收 KCP 消息 长度{buffer.Length}B");

        EventSystem.DispatchEvent(EventID.send_kcp_buffer, buffer);

    }
    /// <summary>
    /// 发送kcp数据
    /// </summary>
    /// <param name="buffer"></param>
    void SendKCPBuffer(byte[] buffer)
    {
        if(kcpSession.kcp != null)
        {
            kcpSession.kcp.Send(buffer);
        }
    }
    /// <summary>
    /// udp初始化成功事件
    /// </summary>
    void UDPInitSuccess()
    {
        EventSystem.DispatchEvent(EventID.try_get_conv);
    }
    /// <summary>
    /// kcp初始化成功事件
    /// </summary>
    void KCPInitSuccess()
    {
        byte[] str_buffer = Message.Get;//10kb的数据

        #region 给消息添加头部 4位 conv
        byte[] buffer = new byte[str_buffer.Length + 4];
        uint conv = kcpSession.GetConv;
        int offset = 0;
        offset += Utils.ikcp_encode32u(buffer, offset, conv);
        #endregion

        Array.Copy(str_buffer, 0, buffer, offset, str_buffer.Length);

        EventSystem.DispatchEvent(EventID.send_kcp_buffer, buffer);

    }
    /// <summary>
    /// udp 消息转发到kcp
    /// </summary>
    /// <param name="buffer"></param>
    void UDPBufferToKcp(byte[] buffer)
    {
        kcpSession.kcp.Input(buffer);
    }
    /// <summary>
    /// 获取到服务器分配的conv
    /// </summary>
    /// <param name="conv"></param>
    public void OnGetConv(uint conv)
    {
        Debug.Log("On Get Conv");
        kcpSession.SetConv(conv);

        kcpSession.Begin();
    }
    public void Update()
    {
        kcpSession.Update();

    }

}

