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

    struct mkcpSegmentOfData {
        public Int24 Conv;  //标识(为了支持更多的客户端进行连接,这里采用3个字节)
        public byte CMD_OPT;
        /// <summary>
        /// 0-5
        /// </summary>
        public byte Cmd { get { return (byte)(CMD_OPT << 5 >> 3); } }
        public byte Opt;//5位
        /// <summary>
        /// 最大值有效值为254（代表大于），最大为255（代表大于12.7秒）
        /// 每1点为50毫秒(254*50=12700ms 最长为12.7秒)
        /// </summary>
        public byte TimeInterval;
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
        public uint Len;

    }



    class mkcp {



    }
}
