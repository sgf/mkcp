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

    public struct UInt24 {
        public byte B1;
        public byte B2;
        public byte B3;
    }


    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct MkcpSegmentOfData {
        public ushort Conv;  //连接标识  (为了支持更多的客户端进行连接,这里采用3个字节)
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
        /// 用于代替时间戳，本次发包距离上一次发包过去了多久。
        /// 最大值有效值为ushort.Max（代表大于），最大为ushort.Max（代表大于130.05秒）
        /// 每1点为2毫秒(ushort.Max*2=?ms 最长为130.05秒)
        /// </summary>
        public ushort TimeInterval;
        /// <summary>
        /// 包序号
        /// </summary>
        public uint SN;
        /// <summary>
        /// 未确认序列号rUna(接收端 UNA=SN-rUna) : 远端主机正在发送的，且尚未收到确认的最小的Sn的距离
        /// （这里是用相对距离的方法表示 uint24可以表示1600多万够用了 单机1600多万暂时很难达到）
        /// </summary>
        public UInt24 rUna;
        /// <summary>
        /// 数据长度
        /// </summary>
        public UInt24 Len;
    }




    class mkcp {



    }
}
