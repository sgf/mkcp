using System.Runtime.InteropServices;

namespace mkcp {



    //压缩方案：

    //-2        sn,una 大概可以缩减到每个3字节。 共计8字节还要想办法缩小才行。
    //-4        另外长度可以考虑设计为0字节，从而直接交给上层UDP协议进行控制。 //老的思路：len:可以缩小到 10位长度， 规定大小必须为 2的整数倍,范围1-1020。 最小 2*1=2，最大 2*1020=2040
    //-4        TS 字段 TCP没有，那么TCP是如何计算的？这块能不能直接省掉4个字节！？？ //老的思路： ts:或许可以缩小到2个字节，因为反正是循环的(每个值代表3ms) 总共可以表示0-195秒 大约3分钟，3分钟时间一个数据包也不会不到了。
    //          Kcp群里大佬提示：TS字段TCP是你存放在选项位置的。因此可能无法省略！！！！！！
    //-3        conv:可以缩小到 1字节，最大支持6万个链接。作用 主要用于防止端口变动？（涉及到：UDP NAT 设备 端口变动）
    //-1.5      Wnd:可以缩小到 4位，传递的时候，用进一法(不足1补1)，传递个大概值就可以了， 规定窗口大小必须为 64的整数倍,范围1-63。 最小 64*1=64，最大 64*63=4032
    //-0.5      cmd:缩小到4位足够用就行。
    //以上共计 可以缩减 15个字节。 24-15=9个字节
    //9+UDP的8个字节 总计是17个字节 还是太大了！ 需要缩减到16个字节！ 比如：conv就不要了。

    //必要的字段：cmd frg(分片)
    //最好的办法是:包分类
    //cmd：dat(数据传输),ack(协议控制),


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
