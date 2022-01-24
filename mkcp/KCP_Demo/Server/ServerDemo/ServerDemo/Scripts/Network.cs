using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;


/// <summary>
/// 网络管理类
/// </summary>
public class Network
{
    UDPServer udpServer;

    /// <summary>
    /// key 为 conv , value 为 对应客户端的 Kcpsession
    /// </summary>
    ConcurrentDictionary<uint, KcpSession> kcpSesstionDic = new ConcurrentDictionary<uint, KcpSession>();
    /// <summary>
    /// key 为 conv , value 为 对应客户端的 ip 端口
    /// </summary>
    ConcurrentDictionary<uint, IPEndPoint> conv_ipEndPointDic = new ConcurrentDictionary<uint, IPEndPoint>();

    public Network(UDPServer udpServer)
    {
        this.udpServer = udpServer;

        #region 注册监听事件
        EventSystem.RegisterEvent(EventID.udpbuffer_to_kcp, UDPBufferToKcp);
        EventSystem.RegisterEvent(EventID.send_kcp_buffer, SendKCPBuffer);
        EventSystem.RegisterEvent(EventID.receive_kcp_buffer, ReceiveKcpBuffer);
        EventSystem.RegisterEvent(EventID.receive_udp_buffer, ReceiveUDPBuffer);
        EventSystem.RegisterEvent(EventID.new_conv, NewConv);
        EventSystem.RegisterEvent(EventID.send_udp_buffer, SendUDPBuffer);
        EventSystem.RegisterEvent(EventID.update_conv_ipendpoint, UpdateConvIpEndPoint);

        #endregion


    }
    /// <summary>
    /// 更新conv和ip 端口 的关系
    /// </summary>
    /// <param name="conv"></param>
    /// <param name="ipEndPoint"></param>
    void UpdateConvIpEndPoint(uint conv, IPEndPoint ipEndPoint)
    {
        conv_ipEndPointDic[conv] = ipEndPoint;
    }

    /// <summary>
    /// 发送UDP消息
    /// </summary>
    /// <param name="buffer"></param>
    void SendUDPBuffer(byte[] buffer)
    {
        int offset = 0;

        uint conv = 0;

        offset += Utils.ikcp_decode32u(buffer, offset, ref conv);
        
        conv_ipEndPointDic.TryGetValue(conv, out var iPEndPoint);
        if (iPEndPoint != null)
        {
            Debug.Log($"upd消息发送 发送地址:{iPEndPoint}");
            udpServer.Send(buffer, iPEndPoint);
        }
    }
    /// <summary>
    /// udp收到消息 转发到kcp
    /// </summary>
    /// <param name="conv"></param>
    /// <param name="buffer"></param>
    private void UDPBufferToKcp(uint conv, byte[] buffer)
    {
        kcpSesstionDic.TryGetValue(conv, out var kcpSession);
        kcpSession?.kcp.Input(buffer);
    }
    /// <summary>
    /// 发送KCP消息
    /// </summary>
    /// <param name="conv"></param>
    /// <param name="buffer"></param>
    private void SendKCPBuffer(uint conv, byte[] buffer)
    {
        kcpSesstionDic.TryGetValue(conv, out var kcpSession);
        kcpSession?.kcp.Send(buffer);
    }

    /// <summary>
    /// 新客户端连接 
    /// </summary>
    /// <param name="conv"></param>
    void NewConv(uint conv)
    {
        KcpSession kcpSession = new KcpSession();
        kcpSession.SetConv(conv);
        kcpSession.Begin();

        kcpSesstionDic[conv] = kcpSession;

    }
    /// <summary>
    /// 接收UDP消息
    /// </summary>
    /// <param name="buffer"></param>
    void ReceiveUDPBuffer(uint conv, byte[] buffer)
    {

        EventSystem.DispatchEvent(EventID.udpbuffer_to_kcp, conv, buffer);
    }
    /// <summary>
    /// 接收Kcp处理过的消息
    /// </summary>
    /// <param name="buffer"></param>
    void ReceiveKcpBuffer(byte[] buffer)
    {
        
        int offset = 0;
        uint conv = 0;
        offset += Utils.ikcp_decode32u(buffer, offset, ref conv);//消息体的前4位保存的是客户端的conv

        string str = Encoding.UTF8.GetString(buffer, offset, buffer.Length - offset);

        Debug.Log($"接收消息:{str}");


        //循环发送消息
        EventSystem.DispatchEvent(EventID.send_kcp_buffer, conv, buffer);
        

    }



    public void Update()
    {
        //遍历所有客户端的KcpSession 调用更新周期
        foreach (var kcpSession in kcpSesstionDic)
        {
            kcpSession.Value.Update();

        }

    }

}

