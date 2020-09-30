using System;
using System.Collections.Generic;

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
        public const int IKCP_RTO_NDL = 30;
        /// <summary>
        /// 超时重传 最小时间-100ms
        /// </summary>
        public const int IKCP_RTO_MIN = 100;

        /// <summary>
        /// 超时重传 默认时间-200ms
        /// </summary>
        public const int IKCP_RTO_DEF = 200;

        /// <summary>
        /// 超时重传 最大时间-60秒
        /// </summary>

        public const int IKCP_RTO_MAX = 60000;


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





        /// <summary>
        /// 会话ID
        /// </summary>
        uint conv { get; set; } = 0;

        /// <summary>
        /// 最大传输单元
        /// </summary>
        uint mtu { get; set; } = 0;

        /// <summary>
        /// 最大分片大小
        /// </summary>
        uint mss { get; set; } = 0;

        /// <summary>
        /// 连接状态（0xFFFFFFFF表示断开连接）
        /// </summary>
        uint state { get; set; } = 0;

        /// <summary>
        /// 第一个未确认的包
        /// </summary>
        uint snd_una { get; set; } = 0;

        /// <summary>
        /// 待发送包的序号
        /// </summary>
        uint snd_nxt { get; set; } = 0;

        /// <summary>
        /// 待接收消息序号
        /// </summary>
        uint rcv_nxt { get; set; } = 0;


#pragma warning disable S1144 // Unused private types or members should be removed
        uint ts_recent_ { get; set; } = 0;
        uint ts_lastack_ { get; set; } = 0;
#pragma warning restore S1144 // Unused private types or members should be removed

        /// <summary>
        /// 拥塞窗口阈值，以包为单位（TCP以字节为单位）；
        /// </summary>
        uint ssthresh { get; set; } = 0;

        /// <summary>
        /// RTT的变化量，代表连接的抖动情况；（ack接收rtt浮动值）
        /// </summary>
        Int32 rx_rttval { get; set; } = 0;

        /// <summary>
        /// smoothed round trip time，平滑后的RTT；（ack接收rtt静态值）
        /// </summary>
        Int32 rx_srtt { get; set; } = 0;

        /// <summary>
        /// 由ack接收延迟计算出来的重传超时时间(rto=retransmission timeout)
        /// </summary>
        Int32 rx_rto { get; set; } = 0;

        /// <summary>
        /// 最小重传超时时间；
        /// </summary>
        Int32 rx_minrto { get; set; } = 0;

        /// <summary>
        /// 发送窗口大小
        /// </summary>
        uint snd_wnd { get; set; } = 0;

        /// <summary>
        /// 接收窗口大小
        /// </summary>
        uint rcv_wnd { get; set; } = 0;

        /// <summary>
        /// 远端接收窗口大小
        /// </summary>
        uint rmt_wnd { get; set; } = 0;

        /// <summary>
        /// 拥塞窗口大小(流控)
        /// </summary>
        uint cwnd { get; set; } = 0;

        /// <summary>
        /// 探查变量，IKCP_ASK_TELL表示告知远端窗口大小。IKCP_ASK_SEND表示请求远端告知窗口大小
        /// </summary>
        Porbe probe { get; set; } = 0;

        /// <summary>
        /// 当前时间
        /// </summary>
        uint current_ { get; set; } = 0;

        /// <summary>
        /// 内部flush刷新间隔，对系统循环效率有非常重要影响；
        /// </summary>
        uint interval_ { get; set; } = 0;

        /// <summary>
        /// 下次flush刷新时间戳
        /// </summary>
        uint ts_flush_ { get; set; } = 0;

        /// <summary>
        /// 发送segment的次数，当segment的xmit增加时，xmit增加（第一次或重传除外）；
        /// </summary>
        uint xmit_ { get; set; } = 0;

        ///// <summary>
        ///// 发送队列snd_queue中的Segment数量
        ///// </summary>
        //uint nsnd_que_ => (uint)snd_queue_.Count;

        /// <summary>
        /// 是否启动无延迟模式。无延迟模式rtomin将设置为0，拥塞控制不启动；
        /// </summary>
        bool nodelay_ { get; set; } = false;

        /// <summary>
        /// 是否调用过update函数的标识
        /// </summary>
        bool updated_ { get; set; } = false;
        /// <summary>
        /// 下次探查窗口的时间戳
        /// </summary>
        uint ts_probe_ { get; set; } = 0;
        /// <summary>
        /// 探查窗口需要等待的时间
        /// </summary>
        uint probe_wait_ { get; set; } = 0;
        /// <summary>
        /// 最大重传次数，被认为连接中断；
        /// </summary>
        uint dead_link_ { get; set; } = 0;
        /// <summary>
        /// 可发送的最大数据量；
        /// </summary>
        uint incr_ { get; set; } = 0;

        Queue<Segment> snd_queue_ { get; set; }
        /// <summary>
        /// 缓存接收到、连续的数据包，接收队列rcv_queue中的Segment数量, 需要小于 rcv_wnd
        /// </summary>
        LinkedList<Segment> rcv_queue_ { get; set; }
        /// <summary>
        /// 已发送 待确认的包
        /// </summary>
        LinkedList<Segment> snd_buf_ { get; set; }
        /// <summary>
        /// 接收到的数据会先存放到rcv_buf中。
        /// </summary>
        LinkedList<Segment> rcv_buf_ { get; set; }

        /// <summary>
        /// 待发送的ack列表：存放TS,SN 两个字段
        /// </summary>
        /// <remarks> 收到包后要发送的回传确认。  push当前包的ack给远端（会在flush中发送ack出去)</remarks>
        List<ulong> ackList { get; set; } = new List<ulong>();

        /// <summary>
        /// 存储消息字节流；
        /// </summary>
        byte[] buffer { get; set; }
        /// <summary>
        /// 指针，可以任意放置代表用户的数据，也可以设置程序中需要传递的变量；
        /// </summary>
        object user_ { get; set; }
        /// <summary>
        /// 快速重传模式(Ack可跨越次数阈值)      触发快速重传的重复ACK个数；
        /// </summary>
        Int32 fastresend_ { get; set; } = 0;
        /// <summary>
        /// 取消拥塞控制(流量控制)；
        /// </summary>
        bool nocwnd_ { get; set; } = false;

        public delegate void OutputDelegate(byte[] data, int size, object user);
        OutputDelegate output_;

        public static uint _imin_(uint a, uint b) {
            return a <= b ? a : b;
        }

        public static uint _imax_(uint a, uint b) {
            return a >= b ? a : b;
        }

        public static uint _ibound_(uint lower, uint middle, uint upper) {
            return _imin_(_imax_(lower, middle), upper);
        }

        public static Int32 _itimediff(uint later, uint earlier) {
            return (Int32)(later - earlier);
        }

    }
}
