using ClientDemo;
using System;
using System.Net.Sockets.Kcp;
using System.Threading.Tasks;

/// <summary>
/// kcp 会话
/// </summary>
public class KcpSession
{
    /// <summary>
    /// Kcp类
    /// </summary>
    public Kcp kcp;
    Handle handle;
    /// <summary>
    /// 服务器分配的conv存储在这里
    /// </summary>
    public uint conv;
    public void SetConv(uint conv)
    {
        this.conv = conv;
    }
    public uint GetConv
    {
        get
        {
            return conv;
        }
    }
    /// <summary>
    /// 关闭kcp 
    /// </summary>
    public void Close()
    {
        kcp = null;
    }

    /// <summary>
    /// 开始启动kcp
    /// </summary>
    public void Begin()
    {
        handle = new Handle();
        kcp = new Kcp(conv, handle);

        kcp.NoDelay(1, 10, 2, 1);//fast
        kcp.WndSize(64, 64);
        kcp.SetMtu(512);

        handle.Out += buffer =>
        {
            Task.Run(() =>
            {

                //发送消息udp 
                byte[] data = buffer.ToArray();
                EventSystem.DispatchEvent(EventID.send_udp_buffer, data);
            });
        };

        handle.Recv += buffer =>
        {
            //buffer 是服务器发送的原始数据

            EventSystem.DispatchEvent(EventID.receive_kcp_buffer, buffer);

        };
        EventSystem.DispatchEvent(EventID.kcp_init_success);

    }

    public void Update()
    {
        if(kcp != null)
        {
            try
            {
                kcp.Update(DateTime.UtcNow);
                int len;
                while ((len = kcp.PeekSize()) > 0)
                {
                    var buffer = new byte[len];
                    if (kcp.Recv(buffer) >= 0)
                    {
                        handle.Receive(buffer);
                    }
                }
            }
            catch (Exception e)
            {

                Debug.Log(e);
                EventSystem.DispatchEvent(EventID.network_disconnect);
            }

        }

    }
}

