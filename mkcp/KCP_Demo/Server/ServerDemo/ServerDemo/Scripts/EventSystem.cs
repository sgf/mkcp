using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

public enum EventID
{
    /// <summary>
    /// 获取到了conv
    /// </summary>
    get_conv,

    /// <summary>
    /// 收到消息
    /// </summary>
    receive_udp_buffer,

    /// <summary>
    /// 发送消息
    /// </summary>
    send_udp_buffer,

    /// <summary>
    /// 收到kcp消息
    /// </summary>
    receive_kcp_buffer,
    /// <summary>
    /// 发送kcp消息
    /// </summary>
    send_kcp_buffer,
    /// <summary>
    /// kcp初始化成功
    /// </summary>
    kcp_init_success,
    /// <summary>
    /// udp初始化成功
    /// </summary>
    udp_init_success,
    /// <summary>
    /// udp收到消息 转发到kcp
    /// </summary>
    udpbuffer_to_kcp,
    /// <summary>
    /// 新客户端连接
    /// </summary>
    new_conv,
    /// <summary>
    /// 更新conv和ip 端口 的关系
    /// </summary>
    update_conv_ipendpoint,

}
public class EventSystem
{
    static Dictionary<EventID, Action> actions = new Dictionary<EventID, Action>();
    static Dictionary<EventID, Action<uint>> action_int = new Dictionary<EventID, Action<uint>>();
    static Dictionary<EventID, Action<byte[]>> action_bytes = new Dictionary<EventID, Action<byte[]>>();
    static Dictionary<EventID, Action<uint,byte[]>> action_uint_bytes = new Dictionary<EventID, Action<uint, byte[]>>();
    static Dictionary<EventID, Action<uint, IPEndPoint>> action_uint_ipEndPoint = new Dictionary<EventID, Action<uint, IPEndPoint>>();

    public static void RegisterEvent(EventID eventID, Action<uint> action)
    {
        if (action_int.ContainsKey(eventID))
        {
            action_int[eventID] += action;
        }
        else
        {
            action_int[eventID] = action;
        }
    }
    public static void UnRegisterEvent(EventID eventID, Action<uint> action)
    {
        if (action_int.ContainsKey(eventID))
        {
            action_int[eventID] -= action;
        }
    }
    public static void DispatchEvent(EventID eventID, uint value)
    {
        if (action_int.ContainsKey(eventID))
        {
            action_int[eventID](value);
        }
    }


    public static void RegisterEvent(EventID eventID, Action<byte[]> action)
    {
        if (action_bytes.ContainsKey(eventID))
        {
            action_bytes[eventID] += action;
        }
        else
        {
            action_bytes[eventID] = action;
        }
    }
    public static void UnRegisterEvent(EventID eventID, Action<byte[]> action)
    {
        if (action_bytes.ContainsKey(eventID))
        {
            action_bytes[eventID] -= action;
        }
    }
    public static void DispatchEvent(EventID eventID, byte[] value)
    {
        if (action_bytes.ContainsKey(eventID))
        {
            action_bytes[eventID](value);
        }
    }
    public static void RegisterEvent(EventID eventID, Action action)
    {
        if (action_bytes.ContainsKey(eventID))
        {
            actions[eventID] += action;
        }
        else
        {
            actions[eventID] = action;
        }
    }
    public static void UnRegisterEvent(EventID eventID, Action action)
    {
        if (actions.ContainsKey(eventID))
        {
            actions[eventID] -= action;
        }
    }
    public static void DispatchEvent(EventID eventID)
    {
        if (actions.ContainsKey(eventID))
        {
            actions[eventID]();
        }
    }
    public static void RegisterEvent(EventID eventID, Action<uint, byte[]> action)
    {
        if (action_bytes.ContainsKey(eventID))
        {
            action_uint_bytes[eventID] += action;
        }
        else
        {
            action_uint_bytes[eventID] = action;
        }
    }
    public static void UnRegisterEvent(EventID eventID, Action<uint, byte[]> action)
    {
        if (action_uint_bytes.ContainsKey(eventID))
        {
            action_uint_bytes[eventID] -= action;
        }
    }
    public static void DispatchEvent(EventID eventID, uint conv, byte[] buffer)
    {
        if (action_uint_bytes.ContainsKey(eventID))
        {
            action_uint_bytes[eventID](conv, buffer);
        }
    }


    public static void RegisterEvent(EventID eventID, Action<uint, IPEndPoint> action)
    {
        if (action_bytes.ContainsKey(eventID))
        {
            action_uint_ipEndPoint[eventID] += action;
        }
        else
        {
            action_uint_ipEndPoint[eventID] = action;
        }
    }
    public static void UnRegisterEvent(EventID eventID, Action<uint, IPEndPoint> action)
    {
        if (action_uint_ipEndPoint.ContainsKey(eventID))
        {
            action_uint_ipEndPoint[eventID] -= action;
        }
    }
    public static void DispatchEvent(EventID eventID, uint conv, IPEndPoint buffer)
    {
        if (action_uint_ipEndPoint.ContainsKey(eventID))
        {
            action_uint_ipEndPoint[eventID](conv, buffer);
        }
    }
}

