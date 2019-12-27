using System.Runtime.InteropServices;

namespace mkcp {

    /// <summary>
    /// 3字节int
    /// </summary>
    public struct Int24 {
        public byte B1;
        public byte B2;
        public byte B3;
    }

    /// <summary>
    /// 3字节int 最大1671万
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UInt24 {
        public byte B1;
        public byte B2;
        public byte B3;
        public unsafe static implicit operator uint(UInt24 u24) => (uint)(u24.B1 << 16 | u24.B2 << 8 | u24.B3);
        public unsafe static explicit operator UInt24(uint u32) => ((Box4ByteUInt24*)&u32)->UInt24;

        public static readonly UInt24
            MaxValue = (UInt24)16_711_425,
            MinValue = (UInt24)0;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct Box4ByteUInt24 {
            public byte NUse;
            public UInt24 UInt24;
        }
    }

    public struct MkcpAck {
        /// <summary>
        /// 支持6w个连接(底层使用Queue队列复用)
        /// </summary>
        public ushort Conv;  //连接标识(服务端这里大概需要使用IP地址+Conv配合 做成唯一连接ID，从而防止客户端端口变化)
        public byte CMD_OPT;
        /// <summary>
        /// 0-5 低三位
        /// </summary>
        public byte Cmd { get { return (byte)(CMD_OPT << 5 >> 3); } }
        /// <summary>
        /// 高5位
        /// </summary>
        public byte Opt { get { return (byte)(CMD_OPT >> 3); } }
        /// <summary>
        /// 窗口（大概可以变成byte类型，窗口的单元大概可以以 包长度计算，这个具体还要多学习）
        /// </summary>
        public ushort Wnd;
        /// <summary>
        /// 用于代替时间戳，本次发包距离上一次发包过去了多久。
        /// 最大值有效值为ushort.Max（代表大于），最大为ushort.Max（代表大于130.05秒）
        /// 每1点为2毫秒(ushort.Max*2=?ms 最长为130.05秒)
        /// </summary>
        public ushort TimeInterval;
        /// <summary>
        /// 包序号
        /// </summary>
        public UInt24 SN;
        /// <summary>
        ///（相对距离）未确认序列号rUna(接收端 UNA=SN-rUna) : 远端主机正在发送的，且尚未收到确认的最小的Sn的距离
        /// </summary>
        public ushort rUna;
        /// <summary>
        /// 数据长度
        /// </summary>
        public ushort Len;
    }

    /// <summary>
    /// 客户端发给服务端（间隔10秒以内）
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MkcpHeart {
        /// <summary>
        /// 支持6w个连接(底层使用Queue队列复用)
        /// </summary>
        public ushort Conv;  //连接标识(服务端这里大概需要使用IP地址+Conv配合 做成唯一连接ID，从而防止客户端端口变化)
        public byte CMD_OPT;
        /// <summary>
        /// 0-5 低三位
        /// </summary>
        public byte Cmd { get { return (byte)(CMD_OPT << 5 >> 3); } }
        /// <summary>
        /// 高5位
        /// </summary>
        public byte Opt { get { return (byte)(CMD_OPT >> 3); } }
        /// <summary>
        /// 下一接收序列号 Sn: 同确认片段的 Sn
        /// </summary>
        public UInt24 SN;
        /// <summary>
        ///（相对距离）未确认序列号rUna(接收端 UNA=SN-rUna) : 远端主机正在发送的，且尚未收到确认的最小的Sn的距离
        /// </summary>
        public ushort rUna;
        /// <summary>
        /// 延迟 Rto: 远端主机自己计算出的延迟
        /// </summary>
        public ushort Rto;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct MkcpSegmentOfData {
        /// <summary>
        /// 支持6w个连接(底层使用Queue队列复用)
        /// </summary>
        public ushort Conv;  //连接标识(服务端这里大概需要使用IP地址+Conv配合 做成唯一连接ID，从而防止客户端端口变化)
        public byte CMD_OPT;
        /// <summary>
        /// 0-5 低三位
        /// </summary>
        public byte Cmd { get { return (byte)(CMD_OPT << 5 >> 3); } }
        /// <summary>
        /// 高5位
        /// </summary>
        public byte Opt { get { return (byte)(CMD_OPT >> 3); } }
        /// <summary>
        /// 包序号
        /// </summary>
        public UInt24 SN;
        /// <summary>
        ///（相对距离）未确认序列号rUna(接收端 UNA=SN-rUna) : 远端主机正在发送的，且尚未收到确认的最小的Sn的距离
        /// </summary>
        public ushort rUna;



        /// <summary>
        /// 用于代替时间戳，本次发包距离上一次发包过去了多久。
        /// 最大值有效值为ushort.Max（代表大于），最大为ushort.Max（代表大于130.05秒）
        /// 每1点为2毫秒(ushort.Max*2=?ms 最长为130.05秒)
        /// </summary>
        //public ushort TimeInterval;（可以通过Ack返回时间计算rtt）
        /// <summary>
        /// 数据长度（可以省略掉 直接通过UDP的包长计算）
        /// </summary>
        //public ushort Len;
    }

}
