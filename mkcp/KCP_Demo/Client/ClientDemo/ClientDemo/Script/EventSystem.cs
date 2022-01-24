using System;
using System.Collections.Generic;

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
    /// 收到kcp数据包
    /// </summary>
    receive_kcp_buffer,

    /// <summary>
    /// 发送 kcp数据包
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
    /// udp消息输入tpc
    /// </summary>
    udpbuffer_to_kcp,

    /// <summary>
    /// 网络连接断开
    /// </summary>
    network_disconnect,

    /// <summary>
    /// 尝试获取conv
    /// </summary>
    try_get_conv,

    /// <summary>
    /// 
    /// </summary>
    connect_udp,
}
public class EventSystem
{
    static Dictionary<EventID, Action> actions = new Dictionary<EventID, Action>();
    static Dictionary<EventID, Action<uint>> action_int = new Dictionary<EventID, Action<uint>>();
    static Dictionary<EventID, Action<byte[]>> action_bytes = new Dictionary<EventID, Action<byte[]>>();

    public static void RegisterEvent(EventID eventID , Action<uint> action)
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
}

