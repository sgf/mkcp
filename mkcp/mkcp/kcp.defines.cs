using System;
using System.Collections.Generic;

namespace mkcp {
    public partial class kcp {
        public const int IKCP_RTO_NDL = 30;         // no delay min rto
        public const int IKCP_RTO_MIN = 100;        // normal min rto
        public const int IKCP_RTO_DEF = 200;
        public const int IKCP_RTO_MAX = 60000;
        public const int IKCP_CMD_PUSH = 81;        // cmd: push data
        public const int IKCP_CMD_ACK = 82;         // cmd: ack
        public const int IKCP_CMD_WASK = 83;        // cmd: window probe (ask)
        public const int IKCP_CMD_WINS = 84;        // cmd: window size (tell)
        public const int IKCP_ASK_SEND = 1;         // need to send IKCP_CMD_WASK
        public const int IKCP_ASK_TELL = 2;         // need to send IKCP_CMD_WINS
        public const int IKCP_WND_SND = 32;
        public const int IKCP_WND_RCV = 32;
        public const int IKCP_MTU_DEF = 1400;
        public const int IKCP_ACK_FAST = 3;
        public const int IKCP_INTERVAL = 100;
        /// <summary>
        /// Kcp包头大小
        /// </summary>
        public const int IKCP_OVERHEAD = 24;
        public const int IKCP_DEADLINK = 20;
        public const int IKCP_THRESH_INIT = 2;
        public const int IKCP_THRESH_MIN = 2;
        public const int IKCP_PROBE_INIT = 7000;    // 7 secs to probe window size
        public const int IKCP_PROBE_LIMIT = 120000; // up to 120 secs to probe window

        public const int IKCP_LOG_OUTPUT = 0x1;
        public const int IKCP_LOG_INPUT = 0x2;
        public const int IKCP_LOG_SEND = 0x4;
        public const int IKCP_LOG_RECV = 0x8;
        public const int IKCP_LOG_IN_DATA = 0x10;
        public const int IKCP_LOG_IN_ACK = 0x20;
        public const int IKCP_LOG_IN_PROBE = 0x40;
        public const int IKCP_LOG_IN_WINS = 0x80;
        public const int IKCP_LOG_OUT_DATA = 0x100;
        public const int IKCP_LOG_OUT_ACK = 0x200;
        public const int IKCP_LOG_OUT_PROBE = 0x400;
        public const int IKCP_LOG_OUT_WINS = 0x800;

        /// <summary>
        /// 会话ID
        /// </summary>
        UInt32 conv = 0;
        /// <summary>
        /// 最大传输单元
        /// </summary>
        UInt32 mtu = 0;
        /// <summary>
        /// 最大分片大小
        /// </summary>
        UInt32 mss = 0;
        /// <summary>
        /// 连接状态（0xFFFFFFFF表示断开连接）
        /// </summary>
        UInt32 state = 0;
        /// <summary>
        /// 第一个未确认的包
        /// </summary>
        UInt32 snd_una = 0;
        /// <summary>
        /// 待发送包的序号
        /// </summary>
        UInt32 snd_nxt = 0;
        /// <summary>
        /// 待接收消息序号
        /// </summary>
        UInt32 rcv_nxt = 0;

        UInt32 ts_recent_ = 0;
        UInt32 ts_lastack_ = 0;
        /// <summary>
        /// 拥塞窗口阈值
        /// </summary>
        UInt32 ssthresh = 0;
        /// <summary>
        /// ack接收rtt浮动值
        /// </summary>
        Int32 rx_rttval = 0;
        /// <summary>
        /// ack接收rtt静态值
        /// </summary>
        Int32 rx_srtt = 0;
        /// <summary>
        /// 由ack接收延迟计算出来的重传超时时间
        /// </summary>
        Int32 rx_rto = 0;
        /// <summary>
        /// 最小重传超时时间
        /// </summary>
        Int32 rx_minrto = 0;
        /// <summary>
        /// 发送窗口大小, 一旦设置之后就不会变了, 默认32
        /// </summary>
        UInt32 snd_wnd = 0;
        /// <summary>
        /// 接收窗口大小, 一旦设置之后就不会变了, 默认128
        /// </summary>
        UInt32 rcv_wnd = 0;
        /// <summary>
        /// 远端接收窗口大小
        /// </summary>
        UInt32 rmt_wnd = 0;
        /// <summary>
        /// 拥塞窗口大小
        /// </summary>
        UInt32 cwnd = 0;
        /// <summary>
        /// 探查变量，IKCP_ASK_TELL表示告知远端窗口大小。IKCP_ASK_SEND表示请求远端告知窗口大小
        /// </summary>
        UInt32 probe = 0;
        UInt32 current_ = 0;
        /// <summary>
        /// 内部flush刷新间隔
        /// </summary>
        UInt32 interval_ = 0;
        /// <summary>
        /// 下次flush刷新时间戳
        /// </summary>
        UInt32 ts_flush_ = 0;
        UInt32 xmit_ = 0;
        /// <summary>
        /// 收缓存区中的Segment数量
        /// </summary>
        UInt32 nrcv_buf_ = 0;
        /// <summary>
        /// 发缓存区中的Segment数量
        /// </summary>
        UInt32 nsnd_buf = 0;
        /// <summary>
        /// 接收队列rcv_queue中的Segment数量, 需要小于 rcv_wnd
        /// </summary>
        UInt32 nrcv_que_ = 0;
        /// <summary>
        /// 发送队列snd_queue中的Segment数量
        /// </summary>
        UInt32 nsnd_que_ = 0;
        /// <summary>
        /// 是否启动无延迟模式
        /// </summary>
        UInt32 nodelay_ = 0;
        /// <summary>
        /// 是否调用过update函数的标识
        /// </summary>
        UInt32 updated_ = 0;
        /// <summary>
        /// 下次探查窗口的时间戳
        /// </summary>
        UInt32 ts_probe_ = 0;
        /// <summary>
        /// 探查窗口需要等待的时间
        /// </summary>
        UInt32 probe_wait_ = 0;
        /// <summary>
        /// 最大重传次数
        /// </summary>
        UInt32 dead_link_ = 0;
        /// <summary>
        /// 可发送的最大数据量
        /// </summary>
        UInt32 incr_ = 0;

        LinkedList<Segment> snd_queue_;
        LinkedList<Segment> rcv_queue_;
        LinkedList<Segment> snd_buf_;
        LinkedList<Segment> rcv_buf_;


        UInt32[] acklist_;
        UInt32 ackcount_ = 0;
        UInt32 ackblock_ = 0;

        byte[] buffer;
        object user_;

        Int32 fastresend_ = 0;
        Int32 nocwnd_ = 0;

        public delegate void OutputDelegate(byte[] data, int size, object user);
        OutputDelegate output_;

        public static UInt32 _imin_(UInt32 a, UInt32 b) {
            return a <= b ? a : b;
        }

        public static UInt32 _imax_(UInt32 a, UInt32 b) {
            return a >= b ? a : b;
        }

        public static UInt32 _ibound_(UInt32 lower, UInt32 middle, UInt32 upper) {
            return _imin_(_imax_(lower, middle), upper);
        }

        public static Int32 _itimediff(UInt32 later, UInt32 earlier) {
            return (Int32)(later - earlier);
        }

    }
}
