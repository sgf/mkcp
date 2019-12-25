using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace mkcp {
    public partial class kcp {
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

        byte[] buffer_;
        object user_;

        Int32 fastresend_ = 0;
        Int32 nocwnd_ = 0;

        public delegate void OutputDelegate(byte[] data, int size, object user);
        OutputDelegate output_;

        // create a new kcp control object, 'conv' must equal in two endpoint
        // from the same connection. 'user' will be passed to the output callback
        // output callback can be setup like this: 'kcp->output = my_udp_output'
        public kcp(UInt32 conv, object user) {
            Debug.Assert(BitConverter.IsLittleEndian); // we only support little endian device

            user_ = user;
            this.conv = conv;
            snd_wnd = IKCP_WND_SND;
            rcv_wnd = IKCP_WND_RCV;
            rmt_wnd = IKCP_WND_RCV;
            mtu = IKCP_MTU_DEF;
            mss = mtu - IKCP_OVERHEAD;
            rx_rto = IKCP_RTO_DEF;
            rx_minrto = IKCP_RTO_MIN;
            interval_ = IKCP_INTERVAL;
            ts_flush_ = IKCP_INTERVAL;
            ssthresh = IKCP_THRESH_INIT;
            dead_link_ = IKCP_DEADLINK;
            buffer_ = new byte[(mtu + IKCP_OVERHEAD) * 3];
            snd_queue_ = new LinkedList<Segment>();
            rcv_queue_ = new LinkedList<Segment>();
            snd_buf_ = new LinkedList<Segment>();
            rcv_buf_ = new LinkedList<Segment>();
        }

        // release kcp control object
        public void Release() {
            snd_buf_.Clear();
            rcv_buf_.Clear();
            snd_queue_.Clear();
            rcv_queue_.Clear();
            nrcv_buf_ = 0;
            nsnd_buf = 0;
            nrcv_que_ = 0;
            nsnd_que_ = 0;
            ackblock_ = 0;
            ackcount_ = 0;
            buffer_ = null;
            acklist_ = null;
        }

        // set output callback, which will be invoked by kcp
        public void SetOutput(OutputDelegate output) {
            output_ = output;
        }

        // user/upper level recv: returns size, returns below zero for EAGAIN
        public int Recv(byte[] buffer, int offset, int len) {
            int ispeek = (len < 0 ? 1 : 0);
            int recover = 0;

            if (rcv_queue_.Count == 0)
                return -1;

            if (len < 0)
                len = -len;

            int peeksize = PeekSize();
            if (peeksize < 0)
                return -2;

            if (peeksize > len)
                return -3;

            if (nrcv_que_ >= rcv_wnd)
                recover = 1;

            // merge fragment
            len = 0;
            LinkedListNode<Segment> next = null;
            for (var node = rcv_queue_.First; node != null; node = next) {
                int fragment = 0;
                var seg = node.Value;
                next = node.Next;

                if (buffer != null) {
                    Buffer.BlockCopy(seg.data, 0, buffer, offset, seg.data.Length);
                    offset += seg.data.Length;
                }
                len += seg.data.Length;
                fragment = (int)seg.frg;

                Log(IKCP_LOG_RECV, "recv sn={0}", seg.sn);

                if (ispeek == 0) {
                    rcv_queue_.Remove(node);
                    nrcv_que_--;
                }

                if (fragment == 0)
                    break;
            }

            Debug.Assert(len == peeksize);

            // move available data from rcv_buf -> rcv_queue
            while (rcv_buf_.Count > 0) {
                var node = rcv_buf_.First;
                var seg = node.Value;
                if (seg.sn == rcv_nxt && nrcv_que_ < rcv_wnd) {
                    rcv_buf_.Remove(node);
                    nrcv_buf_--;
                    rcv_queue_.AddLast(node);
                    nrcv_que_++;
                    rcv_nxt++;
                } else {
                    break;
                }
            }

            // fast recover
            if (nrcv_que_ < rcv_wnd && recover != 0) {
                // ready to send back IKCP_CMD_WINS in ikcp_flush
                // tell remote my window size
                probe |= IKCP_ASK_TELL;
            }

            return len;
        }

        // check the size of next message in the recv queue
        public int PeekSize() {
            if (rcv_queue_.Count == 0)
                return -1;

            var node = rcv_queue_.First;
            var seg = node.Value;
            if (seg.frg == 0)
                return seg.data.Length;

            if (nrcv_que_ < seg.frg + 1)
                return -1;

            int length = 0;
            for (node = rcv_queue_.First; node != null; node = node.Next) {
                seg = node.Value;
                length += seg.data.Length;
                if (seg.frg == 0)
                    break;
            }
            return length;
        }

        // user/upper level send, returns below zero for error
        public int Send(byte[] buffer, int offset, int len) {
            Debug.Assert(mss > 0);
            if (len < 0)
                return -1;

            //
            // not implement streaming mode here as ikcp.c
            //

            int count = 0;
            if (len <= (int)mss)
                count = 1;
            else
                count = (len + (int)mss - 1) / (int)mss;

            if (count > 255) // maximum value `frg` can present
                return -2;

            if (count == 0)
                count = 1;

            // fragment
            for (int i = 0; i < count; i++) {
                int size = len > (int)mss ? (int)mss : len;
                var seg = new Segment(size);
                if (buffer != null && len > 0) {
                    Buffer.BlockCopy(buffer, offset, seg.data, 0, size);
                    offset += size;
                }
                seg.frg = (byte)(count - i - 1);
                snd_queue_.AddLast(seg);
                nsnd_que_++;
                len -= size;
            }
            return 0;
        }

        // parse ack
        void UpdateACK(Int32 rtt) {
            if (rx_srtt == 0) {
                rx_srtt = rtt;
                rx_rttval = rtt / 2;
            } else {
                Int32 delta = rtt - rx_srtt;
                if (delta < 0)
                    delta = -delta;

                rx_rttval = (3 * rx_rttval + delta) / 4;
                rx_srtt = (7 * rx_srtt + rtt) / 8;
                if (rx_srtt < 1)
                    rx_srtt = 1;
            }

            var rto = rx_srtt + _imax_(interval_, (UInt32)(4 * rx_rttval));
            rx_rto = (Int32)_ibound_((UInt32)rx_minrto, (UInt32)rto, IKCP_RTO_MAX);
        }

        void ShrinkBuf() {
            var node = snd_buf_.First;
            if (node != null) {
                var seg = node.Value;
                snd_una = seg.sn;
            } else {
                snd_una = snd_nxt;
            }
        }

        void ParseACK(UInt32 sn) {
            if (_itimediff(sn, snd_una) < 0 || _itimediff(sn, snd_nxt) >= 0)
                return;

            LinkedListNode<Segment> next = null;
            for (var node = snd_buf_.First; node != null; node = next) {
                var seg = node.Value;
                next = node.Next;
                if (sn == seg.sn) {
                    snd_buf_.Remove(node);
                    nsnd_buf--;
                    break;
                }
                if (_itimediff(sn, seg.sn) < 0)
                    break;
            }
        }

        void ParseUNA(UInt32 una) {
            LinkedListNode<Segment> next = null;
            for (var node = snd_buf_.First; node != null; node = next) {
                var seg = node.Value;
                next = node.Next;
                if (_itimediff(una, seg.sn) > 0) {
                    snd_buf_.Remove(node);
                    nsnd_buf--;
                } else {
                    break;
                }
            }
        }

        void ParseFastACK(UInt32 sn) {
            if (_itimediff(sn, snd_una) < 0 || _itimediff(sn, snd_nxt) >= 0)
                return;

            LinkedListNode<Segment> next = null;
            for (var node = snd_buf_.First; node != null; node = next) {
                var seg = node.Value;
                next = node.Next;
                if (_itimediff(sn, seg.sn) < 0) {
                    break;
                } else if (sn != seg.sn) {
                    seg.faskack++;
                }
            }
        }

        // ack append
        void ACKPush(UInt32 sn, UInt32 ts) {
            var newsize = ackcount_ + 1;
            if (newsize > ackblock_) {
                UInt32 newblock = 8;
                for (; newblock < newsize; newblock <<= 1)
                    ;

                var acklist = new UInt32[newblock * 2];
                if (acklist_ != null) {
                    for (var i = 0; i < ackcount_; i++) {
                        acklist[i * 2] = acklist_[i * 2];
                        acklist[i * 2 + 1] = acklist_[i * 2 + 1];
                    }
                }
                acklist_ = acklist;
                ackblock_ = newblock;
            }
            acklist_[ackcount_ * 2] = sn;
            acklist_[ackcount_ * 2 + 1] = ts;
            ackcount_++;
        }

        void ACKGet(int pos, ref UInt32 sn, ref UInt32 ts) {
            sn = acklist_[pos * 2];
            ts = acklist_[pos * 2 + 1];
        }

        // parse data
        void ParseData(Segment newseg) {
            UInt32 sn = newseg.sn;
            int repeat = 0;

            if (_itimediff(sn, rcv_nxt + rcv_wnd) >= 0 ||
                _itimediff(sn, rcv_nxt) < 0) {
                return;
            }

            LinkedListNode<Segment> node = null;
            LinkedListNode<Segment> prev = null;
            for (node = rcv_buf_.Last; node != null; node = prev) {
                var seg = node.Value;
                prev = node.Previous;
                if (seg.sn == sn) {
                    repeat = 1;
                    break;
                }
                if (_itimediff(sn, seg.sn) > 0) {
                    break;
                }
            }
            if (repeat == 0) {
                if (node != null) {
                    rcv_buf_.AddAfter(node, newseg);
                } else {
                    rcv_buf_.AddFirst(newseg);
                }
                nrcv_buf_++;
            }

            // move available data from rcv_buf -> rcv_queue
            while (rcv_buf_.Count > 0) {
                node = rcv_buf_.First;
                var seg = node.Value;
                if (seg.sn == rcv_nxt && nrcv_que_ < rcv_wnd) {
                    rcv_buf_.Remove(node);
                    nrcv_buf_--;
                    rcv_queue_.AddLast(node);
                    nrcv_que_++;
                    rcv_nxt++;
                } else {
                    break;
                }
            }
        }

        // when you received a low level packet (eg. UDP packet), call it
        public int Input(byte[] data, int offset, int size) {
            UInt32 maxack = 0;
            int flag = 0;

            Log(IKCP_LOG_INPUT, "[RI] {0} bytes", size);

            if (data == null || size < IKCP_OVERHEAD)
                return -1;

            while (true) {
                if (size < IKCP_OVERHEAD)
                    break;

                ref var tmpseg =ref Segment.Decode(data, ref offset);
                if (conv != tmpseg.conv)
                    return -1;

                size -= IKCP_OVERHEAD;
                if (size < tmpseg.len)
                    return -2;

                if (tmpseg.cmd != IKCP_CMD_PUSH && tmpseg.cmd != IKCP_CMD_ACK &&
                    tmpseg.cmd != IKCP_CMD_WASK && tmpseg.cmd != IKCP_CMD_WINS)
                    return -3;

                rmt_wnd = tmpseg.wnd;
                ParseUNA(tmpseg.una);
                ShrinkBuf();

                if (tmpseg.cmd == IKCP_CMD_ACK) {
                    if (_itimediff(current_, tmpseg.ts) >= 0) {
                        UpdateACK(_itimediff(current_, tmpseg.ts));
                    }
                    ParseACK(tmpseg.sn);
                    ShrinkBuf();
                    if (flag == 0) {
                        flag = 1;
                        maxack = tmpseg.sn;
                    } else {
                        if (_itimediff(tmpseg.sn, maxack) > 0) {
                            maxack = tmpseg.sn;
                        }
                    }
                    Log(IKCP_LOG_IN_DATA, "input ack: sn={0} rtt={1} rto={2}",
                        tmpseg.sn, _itimediff(current_, tmpseg.ts), rx_rto);
                } else if (tmpseg.cmd == IKCP_CMD_PUSH) {
                    Log(IKCP_LOG_IN_DATA, "input psh: sn={0} ts={1}", tmpseg.sn, tmpseg.ts);
                    if (_itimediff(tmpseg.sn, rcv_nxt + rcv_wnd) < 0) {
                        ACKPush(tmpseg.sn, tmpseg.ts);
                        if (_itimediff(tmpseg.sn, rcv_nxt) >= 0) {
                            var seg = new Segment((int)tmpseg.len);
                            seg.conv = tmpseg.conv;
                            seg.cmd = tmpseg.cmd;
                            seg.frg = tmpseg.frg;
                            seg.wnd = tmpseg.wnd;
                            seg.ts = tmpseg.ts;
                            seg.sn = tmpseg.sn;
                            seg.una = tmpseg.una;
                            if (tmpseg.len > 0) {
                                Buffer.BlockCopy(data, offset, seg.data, 0, (int)tmpseg.len);
                            }
                            ParseData(seg);
                        }
                    }
                } else if (tmpseg.cmd == IKCP_CMD_WASK) {
                    // ready to send back IKCP_CMD_WINS in ikcp_flush
                    // tell remote my window size
                    probe |= IKCP_ASK_TELL;
                    Log(IKCP_LOG_IN_PROBE, "input probe");
                } else if (tmpseg.cmd == IKCP_CMD_WINS) {
                    // do nothing
                    Log(IKCP_LOG_IN_WINS, "input wins: {0}", tmpseg.wnd);
                } else {
                    return -3;
                }

                offset += (int)tmpseg.len;
                size -= (int)tmpseg.len;
            }

            if (flag != 0) {
                ParseFastACK(maxack);
            }

            UInt32 unack = snd_una;
            if (_itimediff(snd_una, unack) > 0) {
                if (cwnd < rmt_wnd) {
                    if (cwnd < ssthresh) {
                        cwnd++;
                        incr_ += mss;
                    } else {
                        if (incr_ < mss)
                            incr_ = mss;
                        incr_ += (mss * mss) / incr_ + (mss / 16);
                        if ((cwnd + 1) * mss <= incr_)
                            cwnd++;
                    }
                    if (cwnd > rmt_wnd) {
                        cwnd = rmt_wnd;
                        incr_ = rmt_wnd * mss;
                    }
                }
            }

            return 0;
        }

        int WndUnused() {
            if (nrcv_que_ < rcv_wnd)
                return (int)(rcv_wnd - nrcv_que_);
            return 0;
        }

        // flush pending data
        void Flush() {
            int change = 0;
            int lost = 0;
            int offset = 0;

            // 'ikcp_update' haven't been called. 
            if (updated_ == 0)
                return;

            var seg = new Segment {
                conv = conv,
                cmd = IKCP_CMD_ACK,
                wnd = (ushort)WndUnused(),
                una = rcv_nxt,
            };

            // flush acknowledges
            int count = (int)ackcount_;
            for (int i = 0; i < count; i++) {
                if ((offset + IKCP_OVERHEAD) > mtu) {
                    output_(buffer_, offset, user_);
                    offset = 0;
                }
                ACKGet(i, ref seg.Head.sn, ref seg.Head.ts);
                seg.Encode(buffer_, ref offset);
            }

            ackcount_ = 0;

            // probe window size (if remote window size equals zero)
            if (rmt_wnd == 0) {
                if (probe_wait_ == 0) {
                    probe_wait_ = IKCP_PROBE_INIT;
                    ts_probe_ = current_ + probe_wait_;
                } else {
                    if (_itimediff(current_, ts_probe_) >= 0) {
                        if (probe_wait_ < IKCP_PROBE_INIT)
                            probe_wait_ = IKCP_PROBE_INIT;
                        probe_wait_ += probe_wait_ / 2;
                        if (probe_wait_ > IKCP_PROBE_LIMIT)
                            probe_wait_ = IKCP_PROBE_LIMIT;
                        ts_probe_ = current_ + probe_wait_;
                        probe |= IKCP_ASK_SEND;
                    }
                }
            } else {
                ts_probe_ = 0;
                probe_wait_ = 0;
            }

            // flush window probing commands
            if ((probe & IKCP_ASK_SEND) > 0) {
                seg.cmd = IKCP_CMD_WASK;
                if ((offset + IKCP_OVERHEAD) > mtu) {
                    output_(buffer_, offset, user_);
                    offset = 0;
                }
                seg.Encode(buffer_, ref offset);
            }

            // flush window probing commands
            if ((probe & IKCP_ASK_TELL) > 0) {
                seg.cmd = IKCP_CMD_WINS;
                if ((offset + IKCP_OVERHEAD) > mtu) {
                    output_(buffer_, offset, user_);
                    offset = 0;
                }
                seg.Encode(buffer_, ref offset);
            }

            probe = 0;

            // calculate window size
            UInt32 cwnd = _imin_(snd_wnd, rmt_wnd);
            if (nocwnd_ == 0)
                cwnd = _imin_(this.cwnd, cwnd);

            // move data from snd_queue to snd_buf
            while (_itimediff(snd_nxt, snd_una + cwnd) < 0) {
                if (snd_queue_.Count == 0)
                    break;

                var node = snd_queue_.First;
                var newseg = node.Value;
                snd_queue_.Remove(node);
                snd_buf_.AddLast(node);
                nsnd_que_--;
                nsnd_buf++;

                newseg.conv = conv;
                newseg.cmd = IKCP_CMD_PUSH;
                newseg.wnd = seg.wnd;
                newseg.ts = current_;
                newseg.sn = snd_nxt++;
                newseg.una = rcv_nxt;
                newseg.resendts = current_;
                newseg.rto = (UInt32)rx_rto;
                newseg.faskack = 0;
                newseg.xmit = 0;
            }

            // calculate resent
            UInt32 resent = (fastresend_ > 0 ? (UInt32)fastresend_ : 0xffffffff);
            UInt32 rtomin = (nodelay_ == 0 ? (UInt32)(rx_rto >> 3) : 0);

            // flush data segments
            for (var node = snd_buf_.First; node != null; node = node.Next) {
                var segment = node.Value;
                int needsend = 0;
                if (segment.xmit == 0) {
                    needsend = 1;
                    segment.xmit++;
                    segment.rto = (UInt32)rx_rto;
                    segment.resendts = current_ + segment.rto + rtomin;
                } else if (_itimediff(current_, segment.resendts) >= 0) {
                    needsend = 1;
                    segment.xmit++;
                    xmit_++;
                    if (nodelay_ == 0)
                        segment.rto += (UInt32)rx_rto;
                    else
                        segment.rto += (UInt32)rx_rto / 2;
                    segment.resendts = current_ + segment.rto;
                    lost = 1;
                } else if (segment.faskack >= resent) {
                    needsend = 1;
                    segment.xmit++;
                    segment.faskack = 0;
                    segment.resendts = current_ + segment.rto;
                    change++;
                }

                if (needsend > 0) {
                    segment.ts = current_;
                    segment.wnd = seg.wnd;
                    segment.una = rcv_nxt;

                    int need = IKCP_OVERHEAD;
                    if (segment.data != null)
                        need += segment.data.Length;

                    if (offset + need > mtu) {
                        output_(buffer_, offset, user_);
                        offset = 0;
                    }
                    segment.Encode(buffer_, ref offset);
                    if (segment.data.Length > 0) {
                        Buffer.BlockCopy(segment.data, 0, buffer_, offset, segment.data.Length);
                        offset += segment.data.Length;
                    }
                    if (segment.xmit >= dead_link_)
                        state = 0xffffffff;
                }
            }

            // flush remain segments
            if (offset > 0) {
                output_(buffer_, offset, user_);
                offset = 0;
            }

            // update ssthresh
            if (change > 0) {
                UInt32 inflight = snd_nxt - snd_una;
                ssthresh = inflight / 2;
                if (ssthresh < IKCP_THRESH_MIN)
                    ssthresh = IKCP_THRESH_MIN;
                this.cwnd = ssthresh + resent;
                incr_ = this.cwnd * mss;
            }

            if (lost > 0) {
                ssthresh = cwnd / 2;
                if (ssthresh < IKCP_THRESH_MIN)
                    ssthresh = IKCP_THRESH_MIN;
                this.cwnd = 1;
                incr_ = mss;
            }

            if (this.cwnd < 1) {
                this.cwnd = 1;
                incr_ = mss;
            }
        }

        // update state (call it repeatedly, every 10ms-100ms), or you can ask 
        // ikcp_check when to call it again (without ikcp_input/_send calling).
        // 'current' - current timestamp in millisec. 
        public void Update(UInt32 current) {
            current_ = current;
            if (updated_ == 0) {
                updated_ = 1;
                ts_flush_ = current;
            }

            Int32 slap = _itimediff(current_, ts_flush_);
            if (slap >= 10000 || slap < -10000) {
                ts_flush_ = current;
                slap = 0;
            }

            if (slap >= 0) {
                ts_flush_ += interval_;
                if (_itimediff(current_, ts_flush_) >= 0)
                    ts_flush_ = current_ + interval_;

                Flush();
            }
        }

        // Determine when should you invoke ikcp_update:
        // returns when you should invoke ikcp_update in millisec, if there 
        // is no ikcp_input/_send calling. you can call ikcp_update in that
        // time, instead of call update repeatly.
        // Important to reduce unnacessary ikcp_update invoking. use it to 
        // schedule ikcp_update (eg. implementing an epoll-like mechanism, 
        // or optimize ikcp_update when handling massive kcp connections)
        public UInt32 Check(UInt32 current) {
            UInt32 ts_flush = ts_flush_;
            Int32 tm_flush = 0x7fffffff;
            Int32 tm_packet = 0x7fffffff;

            if (updated_ == 0)
                return current;

            if (_itimediff(current, ts_flush) >= 10000 ||
                _itimediff(current, ts_flush) < -10000) {
                ts_flush = current;
            }

            if (_itimediff(current, ts_flush) >= 0)
                return current;

            tm_flush = _itimediff(ts_flush, current);

            for (var node = snd_buf_.First; node != null; node = node.Next) {
                var seg = node.Value;
                Int32 diff = _itimediff(seg.resendts, current);
                if (diff <= 0)
                    return current;

                if (diff < tm_packet)
                    tm_packet = diff;
            }

            UInt32 minimal = (UInt32)(tm_packet < tm_flush ? tm_packet : tm_flush);
            if (minimal >= interval_)
                minimal = interval_;

            return current + minimal;
        }

        // change MTU size, default is 1400
        public int SetMTU(int mtu) {
            if (mtu < 50 || mtu < IKCP_OVERHEAD)
                return -1;

            var buffer = new byte[(mtu + IKCP_OVERHEAD) * 3];
            this.mtu = (UInt32)mtu;
            mss = this.mtu - IKCP_OVERHEAD;
            buffer_ = buffer;
            return 0;
        }

        public int Interval(int interval) {
            if (interval > 5000)
                interval = 5000;
            else if (interval < 10)
                interval = 10;

            interval_ = (UInt32)interval;
            return 0;
        }

        // fastest: ikcp_nodelay(kcp, 1, 20, 2, 1)
        // nodelay: 0:disable(default), 1:enable
        // interval: internal update timer interval in millisec, default is 100ms 
        // resend: 0:disable fast resend(default), 1:enable fast resend
        // nc: 0:normal congestion control(default), 1:disable congestion control
        public int NoDelay(int nodelay, int interval, int resend, int nc) {
            if (nodelay >= 0) {
                nodelay_ = (UInt32)nodelay;
                if (nodelay > 0) {
                    rx_minrto = IKCP_RTO_NDL;
                } else {
                    rx_minrto = IKCP_RTO_MIN;
                }
            }
            if (interval >= 0) {
                if (interval > 5000)
                    interval = 5000;
                else if (interval < 10)
                    interval = 10;

                interval_ = (UInt32)interval;
            }

            if (resend >= 0)
                fastresend_ = resend;

            if (nc >= 0)
                nocwnd_ = nc;

            return 0;
        }

        // set maximum window size: sndwnd=32, rcvwnd=32 by default
        public int WndSize(int sndwnd, int rcvwnd) {
            if (sndwnd > 0)
                snd_wnd = (UInt32)sndwnd;
            if (rcvwnd > 0)
                rcv_wnd = (UInt32)rcvwnd;
            return 0;
        }

        // get how many packet is waiting to be sent
        public int WaitSnd() {
            return (int)(nsnd_buf + nsnd_que_);
        }

        // read conv
        public UInt32 GetConv() {
            return conv;
        }

        public UInt32 GetState() {
            return state;
        }

        public void SetMinRTO(int minrto) {
            rx_minrto = minrto;
        }

        public void SetFastResend(int resend) {
            fastresend_ = resend;
        }

        void Log(int mask, string format, params object[] args) {
            // Console.WriteLine(mask + String.Format(format, args));
        }
    }
}
