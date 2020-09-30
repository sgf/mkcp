using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace mkcp {
    public partial class Kcp {

        /// <summary>
        /// user/upper level send, returns below zero for error
        /// </summary>
        /// <remarks>
        /// 注意：函数不支持 发送超过 <see cref="SendMax"/> 大小的数据包
        /// 注意：内部删除了Stream流模式的传输（not implement streaming mode here as ikcp.c）
        /// </remarks>
        /// <param name="buffer"></param>
        /// <returns></returns>
        public int Send(Span<byte> buffer) {
            if (buffer.IsEmpty) {//发送空包
                snd_queue_.Enqueue(Segment.Create()); return 0;
            }
            if (buffer.Length > SendMax) return -2;//超过发送限制
            if (buffer.Length <= mss) {//小于mss的小包直接发送  这段本来可以和下面合并，但为了小包的性能,所以提前处理掉。
                snd_queue_.Enqueue(Segment.Create(buffer.Slice(0, buffer.Length), 0)); return 0;
            }

            int offset = 0;
            int remaind;//超出整包部分长度。
            var fullCount = Math.DivRem(buffer.Length, (int)mss, out remaind);//fullCount:完整包数量 如果有，返回整数倍，否则会返回0。
            var remaindCount = (remaind > 0 ? 1 : 0);
            var totalCount = fullCount + remaindCount;

            //分片逻辑（范围255-0,从大到小）

            //分片完整包
            for (int i = totalCount; i >= remaindCount; i--) {
                snd_queue_.Enqueue(Segment.Create(buffer.Slice(offset, (int)mss), i));
                offset += (int)mss;
            }
            //分片剩余
            if (remaindCount > 0)
                snd_queue_.Enqueue(Segment.Create(buffer.Slice(offset, (int)remaind), 0));//

            return 0;
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

        // when you received a low level packet (eg. UDP packet), call it
        public int Input(Span<byte> data) {
            UInt32 maxack = 0;
            bool flag = false;

            int offset = 0;
            int size = data.Length;

            Log(IKCP_LOG_INPUT, $"[RI] {size} bytes");

            while (true) {
                if (size < IKCP_OVERHEAD) break;//数据太小
                if (!Segment.TryRead(data, ref offset, out Segment seg, this.conv))
                    return -1;

                this.rmt_wnd = seg.wnd;//更新远程窗口大小
                ParseUNA(seg.una);
                ShrinkBuf();

                if (seg.cmd == Cmd.IKCP_CMD_ACK) {
                    if (current_ >= seg.ts)
                        UpdateACK(_itimediff(current_, seg.ts));
                    ParseACK(seg.sn);
                    ShrinkBuf();
                    if (!flag) {
                        flag = true;//快速重传标记
                        maxack = seg.sn;
                    } else {
                        if (_itimediff(seg.sn, maxack) > 0) {
                            maxack = seg.sn;
                        }
                    }
                    Log(IKCP_LOG_IN_DATA, "input ack: sn={0} rtt={1} rto={2}",
                        seg.sn, _itimediff(current_, seg.ts), rx_rto);
                } else if (seg.cmd == Cmd.IKCP_CMD_PUSH) {
                    Log(IKCP_LOG_IN_DATA, "input psh: sn={0} ts={1}", seg.sn, seg.ts);
                    if (_itimediff(seg.sn, rcv_nxt + rcv_wnd) < 0) {
                        ACKPush(seg.sn, seg.ts);
                        if (_itimediff(seg.sn, rcv_nxt) >= 0) {//（可能是判断是否是冗余包！）这里需要搞清楚为什么要判断，因为上面实际上如果是有数据的话读取包头的时候就一并读取出来 到 Segment里面去了
                            var seg1 = new Segment((int)seg.len);
                            seg1.conv = seg.conv;
                            seg1.cmd = seg.cmd;
                            seg1.frg = seg.frg;
                            seg1.wnd = seg.wnd;
                            seg1.ts = seg.ts;
                            seg1.sn = seg.sn;
                            seg1.una = seg.una;
                            if (seg.len > 0) {
                                Buffer.BlockCopy(data.ToArray(), offset, seg1.data, 0, (int)seg.len);
                            }
                            ParseData(seg1);
                        }
                    }
                } else if (seg.cmd == Cmd.IKCP_CMD_WASK) {//如果收到的包是远端发过来询问窗口大小的包
                    // ready to send back IKCP_CMD_WINS in ikcp_flush
                    // tell remote my window size
                    probe |= IKCP_ASK_TELL;
                    Log(IKCP_LOG_IN_PROBE, "input probe");
                } else if (seg.cmd == Cmd.IKCP_CMD_WINS) {
                    // do nothing
                    Log(IKCP_LOG_IN_WINS, "input wins: {seg.wnd}");
                } else {
                    return -3;
                }

                offset += (int)seg.len;
                size -= (int)seg.len;
            }

            if (flag)
                ParseFastACK(maxack);

            UInt32 unack = snd_una;
            if (_itimediff(snd_una, unack) > 0 && cwnd < rmt_wnd) {
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

            return 0;
        }

        /// <summary>
        /// flush pending data
        /// </summary>
        void Flush() {
            int change = 0;// 标识快重传发生
            int lost = 0; // 记录出现了报文丢失
            int offset = 0;

            // 'ikcp_update' haven't been called.
            //检查 kcp->update 是否更新，未更新直接返回。 //kcp->update 由 ikcp_update 更新，// 上层应用需要每隔一段时间（10-100ms）调用 ikcp_update 来驱动 KCP 发送数据；
            if (updated_ == 0) return;

            var seg = Segment.Create(conv, Cmd.IKCP_CMD_ACK, WndUnused(), rcv_nxt);
            // flush acknowledges
            int count = (int)ackcount_;
            for (int i = 0; i < count; i++) {
                if ((offset + IKCP_OVERHEAD) > mtu) {
                    output_(buffer, offset, user_);
                    offset = 0;
                }
                ACKGet(i, ref seg.Head.sn, ref seg.Head.ts);
                seg.Encode(buffer, ref offset);
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
                seg.cmd = Cmd.IKCP_CMD_WASK;
                if ((offset + IKCP_OVERHEAD) > mtu) {
                    output_(buffer, offset, user_);
                    offset = 0;
                }
                seg.Encode(buffer, ref offset);
            }

            // flush window probing commands
            if ((probe & IKCP_ASK_TELL) > 0) {
                seg.cmd = Cmd.IKCP_CMD_WINS;
                if ((offset + IKCP_OVERHEAD) > mtu) {
                    output_(buffer, offset, user_);
                    offset = 0;
                }
                seg.Encode(buffer, ref offset);
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

                var newseg = snd_queue_.Dequeue();
                snd_buf_.AddLast(newseg);
                nsnd_buf++;

                newseg.conv = conv;
                newseg.cmd = Cmd.IKCP_CMD_PUSH;
                newseg.wnd = seg.wnd;
                newseg.ts = current_;
                newseg.sn = snd_nxt++;
                newseg.una = rcv_nxt;
                newseg.resendts = current_;
                newseg.rto = (uint)rx_rto;
                newseg.fastack = 0;
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
                    segment.rto = (uint)rx_rto;
                    segment.resendts = current_ + segment.rto + rtomin;
                } else if (_itimediff(current_, segment.resendts) >= 0) {
                    needsend = 1;
                    segment.xmit++;
                    xmit_++;
                    if (nodelay_ == 0)
                        segment.rto += (uint)rx_rto;
                    else
                        segment.rto += (uint)rx_rto / 2;
                    segment.resendts = current_ + segment.rto;
                    lost = 1;
                } else if (segment.fastack >= resent) {
                    needsend = 1;
                    segment.xmit++;
                    segment.fastack = 0;
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
                        output_(buffer, offset, user_);
                        offset = 0;
                    }
                    segment.Encode(buffer, ref offset);
                    if (segment.data.Length > 0) {
                        Buffer.BlockCopy(segment.data, 0, buffer, offset, segment.data.Length);
                        offset += segment.data.Length;
                    }
                    if (segment.xmit >= dead_link_)
                        state = 0xffffffff;
                }
            }

            // flush remain segments
            if (offset > 0) {
                output_(buffer, offset, user_);
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

    }
}
