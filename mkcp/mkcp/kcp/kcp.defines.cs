using System;
using System.Collections.Generic;

namespace mkcp {
    public partial class Kcp {
        /// <summary>
        /// no delay min rto
        /// </summary>
        public const int IKCP_RTO_NDL = 30;
        /// <summary>
        /// normal min rto
        /// </summary>
        public const int IKCP_RTO_MIN = 100;
        public const int IKCP_RTO_DEF = 200;
        public const int IKCP_RTO_MAX = 60000;
        /// <summary>
        /// cmd: push data
        /// </summary>
        public const int IKCP_CMD_PUSH = 81;
        /// <summary>
        /// cmd: ack
        /// </summary>
        public const int IKCP_CMD_ACK = 82;
        /// <summary>
        /// cmd: window probe (ask) 询问对方当前剩余窗口大小 请求
        /// </summary>
        public const int IKCP_CMD_WASK = 83;
        /// <summary>
        /// cmd: window size (tell) 返回本地当前剩余窗口大小
        /// </summary>
        public const int IKCP_CMD_WINS = 84;
        /// <summary>
        /// need to send IKCP_CMD_WASK
        /// </summary>
        public const int IKCP_ASK_SEND = 1;
        /// <summary>
        /// need to send IKCP_CMD_WINS
        /// </summary>
        public const int IKCP_ASK_TELL = 2;
        public const int IKCP_WND_SND = 32;
        public const int IKCP_WND_RCV = 32;
        public const int IKCP_MTU_DEF = 1400;
        public const int IKCP_ACK_FAST = 3;//???这个似乎没有？
        public const int IKCP_INTERVAL = 100;
        /// <summary>
        /// Kcp包头大小
        /// </summary>
        public const int IKCP_OVERHEAD = 24;
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


        public const int IKCP_SN_OFFSET = 12;//？？？？

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
        uint conv = 0;
        /// <summary>
        /// 最大传输单元
        /// </summary>
        uint mtu = 0;
        /// <summary>
        /// 最大分片大小
        /// </summary>
        uint mss = 0;
        /// <summary>
        /// 连接状态（0xFFFFFFFF表示断开连接）
        /// </summary>
        uint state = 0;
        /// <summary>
        /// 第一个未确认的包
        /// </summary>
        uint snd_una = 0;
        /// <summary>
        /// 待发送包的序号
        /// </summary>
        uint snd_nxt = 0;
        /// <summary>
        /// 待接收消息序号
        /// </summary>
        uint rcv_nxt = 0;


#pragma warning disable S1144 // Unused private types or members should be removed
        uint ts_recent_ = 0;
        uint ts_lastack_ = 0;
#pragma warning restore S1144 // Unused private types or members should be removed
        /// <summary>
        /// 拥塞窗口阈值
        /// </summary>
        uint ssthresh = 0;
        /// <summary>
        /// ack接收rtt浮动值
        /// </summary>
        Int32 rx_rttval = 0;
        /// <summary>
        /// ack接收rtt静态值
        /// </summary>
        Int32 rx_srtt = 0;
        /// <summary>
        /// 由ack接收延迟计算出来的重传超时时间(rto=retransmission timeout)
        /// </summary>
        Int32 rx_rto = 0;
        /// <summary>
        /// 最小重传超时时间
        /// </summary>
        Int32 rx_minrto = 0;
        /// <summary>
        /// 发送窗口大小, 一旦设置之后就不会变了, 默认32
        /// </summary>
        uint snd_wnd = 0;
        /// <summary>
        /// 接收窗口大小, 一旦设置之后就不会变了, 默认128
        /// </summary>
        uint rcv_wnd = 0;
        /// <summary>
        /// 远端接收窗口大小
        /// </summary>
        uint rmt_wnd = 0;
        /// <summary>
        /// 拥塞窗口大小
        /// </summary>
        uint cwnd = 0;
        /// <summary>
        /// 探查变量，IKCP_ASK_TELL表示告知远端窗口大小。IKCP_ASK_SEND表示请求远端告知窗口大小
        /// </summary>
        uint probe = 0;
        uint current_ = 0;
        /// <summary>
        /// 内部flush刷新间隔
        /// </summary>
        uint interval_ = 0;
        /// <summary>
        /// 下次flush刷新时间戳
        /// </summary>
        uint ts_flush_ = 0;
        uint xmit_ = 0;
        /// <summary>
        /// 收缓存区中的Segment数量
        /// </summary>
        uint nrcv_buf_ = 0;
        /// <summary>
        /// 发缓存区中的Segment数量
        /// </summary>
        uint nsnd_buf = 0;
        /// <summary>
        /// 接收队列rcv_queue中的Segment数量, 需要小于 rcv_wnd
        /// </summary>
        uint nrcv_que_ = 0;
        /// <summary>
        /// 发送队列snd_queue中的Segment数量
        /// </summary>
        uint nsnd_que_ = 0;
        /// <summary>
        /// 是否启动无延迟模式
        /// </summary>
        uint nodelay_ = 0;
        /// <summary>
        /// 是否调用过update函数的标识
        /// </summary>
        uint updated_ = 0;
        /// <summary>
        /// 下次探查窗口的时间戳
        /// </summary>
        uint ts_probe_ = 0;
        /// <summary>
        /// 探查窗口需要等待的时间
        /// </summary>
        uint probe_wait_ = 0;
        /// <summary>
        /// 最大重传次数
        /// </summary>
        uint dead_link_ = 0;
        /// <summary>
        /// 可发送的最大数据量
        /// </summary>
        uint incr_ = 0;

        LinkedList<Segment> snd_queue_;
        LinkedList<Segment> rcv_queue_;
        LinkedList<Segment> snd_buf_;
        LinkedList<Segment> rcv_buf_;


        uint[] acklist_;
        uint ackcount_ = 0;
        uint ackblock_ = 0;

        byte[] buffer;
        object user_;

        Int32 fastresend_ = 0;
        Int32 nocwnd_ = 0;

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
