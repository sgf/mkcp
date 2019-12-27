﻿using System;
using System.Diagnostics;

namespace mkcp {
    public partial class Kcp {

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
            int flag = 0;

            int offset = 0;
            int size = data.Length;

            Log(IKCP_LOG_INPUT, "[RI] {0} bytes", size);

            if (data == null || size < IKCP_OVERHEAD) return -1; //没数据 或 数据太小

            while (true) {
                if (size < IKCP_OVERHEAD) break;//数据太小

                ref var tmpseg = ref data.Read<SegmentHead>();
                offset += IKCP_OVERHEAD;

                if (conv != tmpseg.conv) return -1; //连接ID不对

                size -= IKCP_OVERHEAD;
                if (size < tmpseg.len) return -2; //数据不够长 数据实际长度小于头部描述的长度

                if (tmpseg.cmd != IKCP_CMD_PUSH && tmpseg.cmd != IKCP_CMD_ACK &&
                    tmpseg.cmd != IKCP_CMD_WASK && tmpseg.cmd != IKCP_CMD_WINS)//cmd命令不存在
                    return -3;

                rmt_wnd = tmpseg.wnd;//远程窗口大小
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
                                Buffer.BlockCopy(data.ToArray(), offset, seg.data, 0, (int)tmpseg.len);
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
                seg.cmd = IKCP_CMD_WASK;
                if ((offset + IKCP_OVERHEAD) > mtu) {
                    output_(buffer, offset, user_);
                    offset = 0;
                }
                seg.Encode(buffer, ref offset);
            }

            // flush window probing commands
            if ((probe & IKCP_ASK_TELL) > 0) {
                seg.cmd = IKCP_CMD_WINS;
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