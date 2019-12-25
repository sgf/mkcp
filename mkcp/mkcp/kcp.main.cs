using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace mkcp {
    public partial class kcp {

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
            buffer = new byte[(mtu + IKCP_OVERHEAD) * 3];
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
            buffer = null;
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

        int WndUnused() {
            if (nrcv_que_ < rcv_wnd)
                return (int)(rcv_wnd - nrcv_que_);
            return 0;
        }
        // change MTU size, default is 1400
        public int SetMTU(int mtu) {
            if (mtu < 50 || mtu < IKCP_OVERHEAD)
                return -1;

            var buffer = new byte[(mtu + IKCP_OVERHEAD) * 3];
            this.mtu = (UInt32)mtu;
            mss = this.mtu - IKCP_OVERHEAD;
            this.buffer = buffer;
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
