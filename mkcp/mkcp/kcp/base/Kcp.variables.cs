using System;
using System.Buffers;
using System.Collections.Generic;

namespace mkcp {

    public partial class Kcp {

        #region 静态变量

        /// <summary>
        /// 当前时间(本来是成员，改成了静态的)
        /// </summary>
        static uint current_ { get; set; } = 0;

        #endregion


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
        int rx_rttval { get; set; } = 0;

        /// <summary>
        /// smoothed round trip time，平滑后的RTT；（ack接收rtt静态值）
        /// </summary>
        int rx_srtt { get; set; } = 0;

        /// <summary>
        /// 由ack接收延迟计算出来的重传超时时间(rto=retransmission timeout)
        /// </summary>
        uint rx_rto { get; set; } = 0;

        /// <summary>
        /// 最小重传超时时间；
        /// </summary>
        uint rx_minrto { get; set; } = 0;

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
        /// 内部flush刷新间隔，对系统循环效率有非常重要影响；
        /// </summary>
        uint interval_ { get; set; } = 0;

        /// <summary>
        /// 下次flush刷新时间戳
        /// </summary>
        uint ts_flush_ { get; set; } = 0;

        /// <summary>
        /// 发送超时次数
        /// </summary>
        uint xmit_ { get; set; } = 0;



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
        List<ulong> ackList { get; set; }

        /// <summary>
        /// 用于buffer的释放
        /// </summary>
        IMemoryOwner<byte> mowner { get; set; }

        /// <summary>
        /// 存储消息字节流；
        /// </summary>
        Memory<byte> buffer { get; set; }

        /// <summary>
        /// 指针，可以任意放置代表用户的数据，也可以设置程序中需要传递的变量；
        /// </summary>
        object user_ { get; set; }

        /// <summary>
        /// 快速重传模式(Ack可跨越次数阈值)      触发快速重传的重复ACK个数；
        /// allow ack Skip count Limit to Resend
        /// </summary>
        /// <remarks>FastAck 是指 收到后续Ack包但是前面的没有收到因此，进行统计Ack被后续包跳过了多少次数，从而判断是否丢包。此处存放的是触发阈值，而统计值由segment.fastAck进行统计
        /// </remarks>
        uint fastresend_ { get; set; } = uint.MaxValue;

        /// <summary>
        /// 取消拥塞控制(流量控制)；
        /// </summary>
        bool nocwnd_ { get; set; } = false;

        public delegate void OutputDelegate(Span<byte> data, object user);
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

        public static int _itimediff(uint later, uint earlier) {
            return (int)(later - earlier);
        }
    }
}
