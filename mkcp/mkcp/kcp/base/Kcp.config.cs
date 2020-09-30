using System;

namespace mkcp {
    public partial class Kcp {

        /// <summary>
        /// 创建Kcp对象
        /// </summary>
        /// <param name="fastMode">是否为快速工作模式</param>
        /// <returns></returns>
        public static Kcp Create(bool fastMode = true) {
            var kcp = new Kcp(0, null);
            if (!fastMode)
                kcp.SetNoDelay(false, 40, 0, false); //普通模式 ikcp_nodelay(kcp, 0, 40, 0, 0);
            else
                kcp.SetNoDelay(true, 10, 2, true);//极速模式 ikcp_nodelay(kcp, 1, 10, 2, 1);
            kcp.SetWndSize(128, 128);//收发队列大小(不绝对，有一定的弹性)
            kcp.SetMTU(1024); //最大传输单元
            kcp.SetMinRTO(10);
            return kcp;
        }


        /// <summary>
        /// ikcp_nodelay(kcp, 1, 20, 2, 1)
        /// </summary>
        /// <param name="nodelay">是否启用 nodelay模式，0不启用；1启用。
        /// 0:disable(default), 1:enable </param>
        /// <param name="interval">协议内部工作的 interval，单位毫秒，比如 10ms或者 20ms
        /// internal update timer interval in millisec, default is 100ms</param>
        /// <param name="resend">快速重传模式(Ack可跨越次数阈值)，默认0关闭，可以设置2（2次ACK跨越将会直接重传）
        /// 0:disable fast resend(default), 1:enable fast resend</param>
        /// <param name="nc">是否关闭拥塞控制(流控)，默认是false代表不关闭，true代表关闭。
        /// 0:normal congestion control(default), 1:disable congestion control</param>
        /// <returns></returns>
        public int SetNoDelay(bool nodelay, int interval, int resend, bool nc = false) {
                nodelay_ =nodelay;
                if (nodelay) {
                    rx_minrto = IKCP_RTO_NDL;
                } else {
                    rx_minrto = IKCP_RTO_MIN;
                }
            if (interval >= 0) {
                if (interval > 5000)
                    interval = 5000;
                else if (interval < 10)
                    interval = 10;

                interval_ = (uint)interval;
            }

            if (resend >= 0)
                fastresend_ = resend;

            if (nc)
                nocwnd_ = nc;

            return 0;
        }

        /// <summary>
        /// 设置最大窗口(单位:包数量)
        /// </summary>
        /// <remarks>
        /// 该调用将会设置协议的最大发送窗口和最大接收窗口大小，默认为32. 这个可以理解为 TCP的 SND_BUF 和 RCV_BUF，只不过单位不一样 SND/RCV_BUF 单位是字节，这个单位是包。
        /// </remarks>
        /// <param name="sndwnd">发送窗口大小(包数量)</param>
        /// <param name="rcvwnd">接收窗口大小(包数量)</param>
        /// <returns>结果可以忽略</returns>
        public bool SetWndSize(int sndwnd, int rcvwnd) {
            if (sndwnd > 0)
                snd_wnd = (uint)sndwnd;
            if (rcvwnd > 0)
                rcv_wnd = (uint)Math.Max(rcvwnd, IKCP_WND_RCV);// must >= max fragment size
            return true;
        }

        /// <summary>
        /// 设置最大传输单元
        /// </summary>
        /// <remarks>
        /// 纯算法协议并不负责探测 MTU，默认 mtu是1400字节，可以使用ikcp_setmtu来设置该值。该值将会影响数据包归并及分片时候的最大传输单元。
        /// </remarks>
        /// <param name="mtu">最大传输单元</param>
        /// <returns>设置是否成功</returns>
        public bool SetMTU(int mtu) {
            if (mtu < 50 || mtu < IKCP_OVERHEAD)
                return false;
            var buffer = new byte[(mtu + IKCP_OVERHEAD) * 3];
            this.mtu = (uint)mtu;
            mss = this.mtu - (uint)IKCP_OVERHEAD;
            this.buffer = buffer;
            return true;
        }

        /// <summary>
        /// 设置最小RTO。
        /// </summary>
        /// <remarks>
        /// 不管是 TCP还是 KCP计算 RTO时都有最小 RTO的限制，即便计算出来RTO为40ms，由于默认的 RTO是100ms，协议只有在100ms后才能检测到丢包，快速模式下为30ms，可以手动更改该值：
        /// </remarks>
        /// <param name="minrto"></param>
        public void SetMinRTO(int minrto) => this.rx_minrto = minrto;

        /// <summary>
        /// 设置触发快速重传的重复ACK个数；
        /// </summary>
        /// <remarks>
        /// NoDelay函数已经包含了resend参数内部已经设置了 fastresend_ 因此这里一般不会单独调用
        /// </remarks>
        /// <param name="resend"></param>
        public void SetFastResend(int resend) => fastresend_ = resend;

        /// <summary>
        /// Send函数单次，可以发送的最大数据大小
        /// </summary>
        /// <remarks>SendMax=255*mss , mss=(mtu-包头)</remarks>
        public int SendMax => byte.MaxValue * (int)mss;

    }
}
