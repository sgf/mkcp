using System;
using System.Net.Sockets.Kcp;
using System.Text;
using System.Threading.Tasks;


public class KcpSession
{
    public Kcp kcp;
    Handle handle;
    public uint conv;
    public void SetConv(uint conv)
    {
        this.conv = conv;
    }
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

                //udp 发送消息
                byte[] data = buffer.ToArray();
                Debug.Log($"发送消息长度 = {data.Length}");
                EventSystem.DispatchEvent(EventID.send_udp_buffer, data);
            });
        };

        handle.Recv += buffer =>
        {
            //接收消息
            //var str = Encoding.UTF8.GetString(buffer);
            //Debug.Log($"kcp 返回buffer to string = {str}");
            EventSystem.DispatchEvent(EventID.receive_kcp_buffer, buffer);

        };
        EventSystem.DispatchEvent(EventID.kcp_init_success);

    }

    public void Update()
    {
        if (kcp != null)
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

    }
}

