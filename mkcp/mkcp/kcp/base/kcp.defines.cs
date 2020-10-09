namespace mkcp {






    /// <summary>
    /// 基本概念
    /// rto，RTO 超时重传 ——(Retransmission TimeOut)
    /// rtt,RTT  往返时延 ——(Round Trip Time)   由三部分组成：链路的传播时间（propagation delay),末端系统的处理时间，路由器缓存中的排队和处理时间（queuing delay）。
    /// UNA（此编号前所有包已收到，如TCP）—— 表示还没有被ACK确认的数据包里面最早的序列号 可能是Un-Ack 的缩写
    /// ACK（该编号包已收到） acknowledgement
    ///
    ///
    /// </summary>
    public partial class Kcp {




        /// <summary>
        /// NoDelay模式-超时重传 最小时间-30ms
        /// </summary>
        public const uint IKCP_RTO_NDL = 30;
        /// <summary>
        /// 超时重传 最小时间-100ms
        /// </summary>
        public const uint IKCP_RTO_MIN = 100;

        /// <summary>
        /// 超时重传 默认时间-200ms
        /// </summary>
        public const uint IKCP_RTO_DEF = 200;

        /// <summary>
        /// 超时重传 最大时间-60秒
        /// </summary>

        public const uint IKCP_RTO_MAX = 60000;


        /// <summary>
        /// 默认本地发送窗口大小
        /// </summary>
        public const int IKCP_WND_SND = 32;
        /// <summary>
        /// 默认接收发送窗口大小
        /// </summary>
        public const int IKCP_WND_RCV = 32;

        /// <summary>
        /// 建议值最好是1370以下（某些移动端的场景问题，甚至如果有必要 极端的 设置到480，穿透力更强）
        /// 原值为:1400; 通常MTU的长度为1500字节，IP协议规定所有的路由器均应该能够转发（512数据+60IP首部+4预留=576字节）的数据
        /// </summary>
        public const int IKCP_MTU_DEF = 1400;

        /// <summary>
        /// 默认始终频率
        /// </summary>
        public const int IKCP_INTERVAL = 100;
        /// <summary>
        /// Kcp包头大小
        /// </summary>
        public unsafe static int IKCP_OVERHEAD => sizeof(SegmentHead);

        /// <summary>
        /// 死连接 重传达到该值时认为连接是断开的
        /// </summary>
        public const int IKCP_DEADLINK = 20;
        public const int IKCP_THRESH_INIT = 2;
        public const int IKCP_THRESH_MIN = 2;
        /// <summary>
        /// 7 secs to probe window size
        /// </summary>
        public const int IKCP_PROBE_INIT = 7000;
        /// <summary>
        /// up to 120 secs to probe window
        /// </summary>
        public const int IKCP_PROBE_LIMIT = 120000;

    }
}
