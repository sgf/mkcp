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
            for (int i = totalCount; i > remaindCount; i--) {
                snd_queue_.Enqueue(Segment.Create(buffer.Slice(offset, (int)mss), i));
                offset += (int)mss;
            }
            //分片剩余
            if (remaindCount > 0)
                snd_queue_.Enqueue(Segment.Create(buffer.Slice(offset, (int)remaind), 0));

            return 0;
        }

        // user/upper level recv: returns size, returns below zero for EAGAIN
        public int Recv(byte[] buffer, int offset, int len) {
            bool ispeek = (len < 0 ? true : false);
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

            if (rcv_queue_.Count >= rcv_wnd)
                recover = 1;

            // merge fragment
            len = 0;
            LinkedListNode<Segment> next = null;
            for (var node = rcv_queue_.First; node != null; node = next) {
                int fragment = 0;
                var seg = node.Value;
                next = node.Next;

                if (buffer != null) {
                    Buffer.BlockCopy(seg.Data.ToArray(), 0, buffer, offset, seg.Data.Length);
                    offset += seg.Data.Length;
                }
                len += seg.Data.Length;
                fragment = (int)seg.frg;

                Log(kLog.RECV, "recv sn={0}", seg.sn);

                if (!ispeek) {
                    rcv_queue_.Remove(node);
                }

                if (fragment == 0)
                    break;
            }

            Debug.Assert(len == peeksize);

            // move available data from rcv_buf -> rcv_queue
            while (rcv_buf_.Count > 0) {
                var node = rcv_buf_.First;
                var seg = node.Value;
                if (seg.sn == rcv_nxt && rcv_queue_.Count < rcv_wnd) {
                    rcv_buf_.Remove(node);
                    rcv_queue_.AddLast(node);
                    rcv_nxt++;
                } else {
                    break;
                }
            }

            // fast recover
            if (rcv_queue_.Count < rcv_wnd && recover != 0) {
                // ready to send back IKCP_CMD_WINS in ikcp_flush
                // tell remote my window size
                probe |= Porbe.IKCP_ASK_TELL;
            }

            return len;
        }


        // update state (call it repeatedly, every 10ms-100ms), or you can ask
        // ikcp_check when to call it again (without ikcp_input/_send calling).
        // 'current' - current timestamp in millisec.
        public void Update(uint current) {
            current_ = current;
            if (!updated_) {
                updated_ = true;
                ts_flush_ = current;
            }

            int slap = _itimediff(current_, ts_flush_);
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
        public uint Check(uint current) {
            uint ts_flush = ts_flush_;
            int tm_flush = 0x7fffffff;
            int tm_packet = 0x7fffffff;

            if (!updated_)
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
                int diff = _itimediff(seg.resend_ts, current);
                if (diff <= 0)
                    return current;

                if (diff < tm_packet)
                    tm_packet = diff;
            }

            uint minimal = (uint)(tm_packet < tm_flush ? tm_packet : tm_flush);
            if (minimal >= interval_)
                minimal = interval_;

            return current + minimal;
        }

        // when you received a low level packet (eg. UDP packet), call it
        public int Input(Span<byte> data) {
            uint maxack = 0;
            bool flag = false;

            int offset = 0;
            int size = data.Length;

            Log(kLog.INPUT, $"[RI] {size} bytes");

            while (true) {
                if (size < IKCP_OVERHEAD) break;
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
                    Log(kLog.IN_DATA, $"input ack: sn={seg.sn} rtt={_itimediff(current_, seg.ts)} rto={rx_rto}");
                } else if (seg.cmd == Cmd.IKCP_CMD_PUSH) {
                    Log(kLog.IN_DATA, $"input psh: sn={seg.sn} ts={seg.ts}");
                    if (_itimediff(seg.sn, rcv_nxt + rcv_wnd) < 0) {
                        ackList.Add(seg.tssn);
                        if (_itimediff(seg.sn, rcv_nxt) >= 0) {//（可能是判断是否是冗余包！）这里需要搞清楚为什么要判断，因为上面实际上如果是有数据的话读取包头的时候就一并读取出来 到 Segment里面去了
                            //var seg1 = new Segment((int)seg.len);
                            //seg1.conv = seg.conv;
                            //seg1.cmd = seg.cmd;
                            //seg1.frg = seg.frg;
                            //seg1.wnd = seg.wnd;
                            //seg1.ts = seg.ts;
                            //seg1.sn = seg.sn;
                            //seg1.una = seg.una;
                            //if (seg.len > 0) {
                            //    Buffer.BlockCopy(data.ToArray(), offset, seg1.data, 0, (int)seg.len);
                            //}
                            ParseData(seg);
                        }
                    }
                } else if (seg.cmd == Cmd.IKCP_CMD_WASK) {//如果收到的包是远端发过来询问窗口大小的包
                    // ready to send back IKCP_CMD_WINS in ikcp_flush
                    // tell remote my window size
                    probe |= Porbe.IKCP_ASK_TELL;
                    Log(kLog.IN_PROBE, "input probe");
                } else if (seg.cmd == Cmd.IKCP_CMD_WINS) {
                    // do nothing
                    Log(kLog.IN_WINS, "input wins: {seg.wnd}");
                } else {
                    return -3;
                }

                offset += (int)seg.len;
                size -= (int)seg.len;
            }

            if (flag)
                ParseFastACK(maxack);

            uint unack = snd_una;
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
            bool lost = false; // 记录出现了报文丢失
            int offset = 0;

            // 'ikcp_update' haven't been called.
            //检查 kcp->update 是否更新，未更新直接返回。 //kcp->update 由 ikcp_update 更新，// 上层应用需要每隔一段时间（10-100ms）调用 ikcp_update 来驱动 KCP 发送数据；
            if (!updated_) return;

            var seg = Segment.Create(conv, Cmd.IKCP_CMD_ACK, WndUnused(), rcv_nxt);

            //flush Ack(acknowledges)  把AckList(待发送的确认包列表)推送出去，这里可以考虑 把Ack内容 变成数据放在Data中发送出去（从而节约带宽）。
            FlushAckList();
            void FlushAckList() {
                for (int i = 0; i < ackList.Count; i++) {
                    if ((offset + IKCP_OVERHEAD) > mtu) {
                        output_(buffer.Span.Slice(0, offset), user_);
                        offset = 0;
                    }
                    seg.tssn = ackList[i];
                    seg.Encode(buffer.Span.Slice(offset), ref offset);
                }
                ackList.Clear();
            }

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
                        probe |= Porbe.IKCP_ASK_SEND;
                    }
                }
            } else {
                ts_probe_ = 0;
                probe_wait_ = 0;
            }

            // flush window probing commands
            if (probe.HasFlag(Porbe.IKCP_ASK_SEND)) {
                seg.cmd = Cmd.IKCP_CMD_WASK;
                if ((offset + IKCP_OVERHEAD) > mtu) {
                    output_(buffer.Span.Slice(0, offset), user_);
                    offset = 0;
                }
                seg.Encode(buffer.Span.Slice(offset), ref offset);
            }

            // flush window probing commands
            if (probe.HasFlag(Porbe.IKCP_ASK_TELL)) {
                seg.cmd = Cmd.IKCP_CMD_WINS;
                if ((offset + IKCP_OVERHEAD) > mtu) {
                    output_(buffer.Span.Slice(0, offset), user_);
                    offset = 0;
                }
                seg.Encode(buffer.Span.Slice(offset), ref offset);
            }

            probe = Porbe.Default;

            // calculate window size
            var cwnd = _imin_(snd_wnd, rmt_wnd);
            if (!nocwnd_) cwnd = _imin_(this.cwnd, cwnd);

            // move data from snd_queue to snd_buf
            while (_itimediff(snd_nxt, snd_una + cwnd) < 0) {
                if (snd_queue_.Count == 0)
                    break;

                var newseg = snd_queue_.Dequeue();
                snd_buf_.AddLast(newseg);

                newseg.conv = conv;
                newseg.cmd = Cmd.IKCP_CMD_PUSH;
                newseg.wnd = seg.wnd;
                newseg.ts = current_;
                newseg.sn = snd_nxt++;
                newseg.una = rcv_nxt;
                newseg.resend_ts = current_;
                newseg.rto = rx_rto;
                newseg.fastack = 0;
                newseg.xmit = 0;
            }


            // flush data segments
            for (var node = snd_buf_.First; node != null; node = node.Next) {
                var segment = node.Value;
                bool needSend() {
                    if (segment.xmit == 0) {//FirstSend 1. xmit为0，第一次发送，赋值rto及resendts
                        segment.rto = rx_rto;
                    } else if (current_ >= segment.resend_ts) {//RtoReSend 触发超时重传 2. 超过segment重发时间，却仍在send_buf中，说明长时间未收到ack，认为丢失，重发
                        xmit_++;
                        segment.rto += (rx_rto / 2);//以1.5倍的速度增长(Tcp是两倍增长)
                        lost = true;
                    } else if (segment.fastack >= fastresend_) {//FastAckResend 3. 达到快速重传阈值，重新发送
                        segment.fastack = 0;//发送之前清零之前的统计
                        change++;
                    } else {
                        return false;
                    }
                    return true;
                }

                if (needSend()) {
                    segment.resend_ts = current_ + segment.rto;
                    segment.xmit++;
                    segment.ts = current_;
                    segment.wnd = seg.wnd;
                    segment.una = rcv_nxt;
                    if (offset + segment.LengthAll > mtu) {
                        output_(buffer.Span.Slice(0, offset), user_);
                        offset = 0;
                    }
                    segment.Encode(buffer.Span.Slice(offset), ref offset);
                    if (segment.xmit >= dead_link_)
                        state = 0xffffffff;
                }

            }

            // flush remain segments //推送剩余的segment
            if (offset > 0) {
                output_(buffer.Span.Slice(0, offset), user_);
                offset = 0;
            }

            // update ssthresh
            // 如发生快速重传，将拥塞窗口阈值ssthresh调整为当前发送窗口的一半，
            // 将拥塞窗口调整为 ssthresh + resent，resent是触发快速重传的丢包的次数，
            // resent的值代表的意思在被弄丢的包后面收到了resent个数的包的ack。
            // 这样调整后kcp就进入了拥塞控制状态。
            if (change > 0) {
                uint inflight = snd_nxt - snd_una;
                ssthresh = inflight / 2;
                if (ssthresh < IKCP_THRESH_MIN)
                    ssthresh = IKCP_THRESH_MIN;
                this.cwnd = ssthresh + fastresend_;//这里加上fastresend_的值是什么意思？
                incr_ = this.cwnd * mss;
            }

            // update ssthresh
            // 当出现超时重传的时候，说明网络很可能死掉了，因为超时重传会出现，
            // 原因是有包丢失了，并且该包之后的包也没有收到，这很有可能是网络死了，
            // 这时候，拥塞窗口直接变为1。
            if (lost) {
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
