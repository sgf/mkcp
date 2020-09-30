using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace mkcp {
    public partial class Kcp {

        // create a new kcp control object, 'conv' must equal in two endpoint
        // from the same connection. 'user' will be passed to the output callback
        // output callback can be setup like this: 'kcp->output = my_udp_output'
        public Kcp(uint conv, object user) {
            Debug.Assert(BitConverter.IsLittleEndian); // we only support little endian device
            user_ = user;
            this.conv = conv;
            snd_wnd = IKCP_WND_SND;
            rcv_wnd = IKCP_WND_RCV;
            rmt_wnd = IKCP_WND_RCV;
            mtu = IKCP_MTU_DEF;
            mss = mtu - (uint)IKCP_OVERHEAD;
            rx_rto = IKCP_RTO_DEF;
            rx_minrto = IKCP_RTO_MIN;
            interval_ = IKCP_INTERVAL;
            ts_flush_ = IKCP_INTERVAL;
            ssthresh = IKCP_THRESH_INIT;
            dead_link_ = IKCP_DEADLINK;
            buffer = new byte[(mtu + IKCP_OVERHEAD) * 3];
            snd_queue_ = new Queue<Segment>();
            rcv_queue_ = new LinkedList<Segment>();
            snd_buf_ = new LinkedList<Segment>();
            rcv_buf_ = new LinkedList<Segment>();
            ackList = new List<ulong>();
        }

        // release kcp control object
        public void Release() {
            snd_buf_.Clear();
            rcv_buf_.Clear();
            snd_queue_.Clear();
            rcv_queue_.Clear();
            ackList.Clear();
            buffer = null;
        }


        // check the size of next message in the recv queue
        public int PeekSize() {
            if (rcv_queue_.Count == 0)
                return -1;

            var node = rcv_queue_.First;
            var seg = node.Value;
            if (seg.frg == 0)
                return seg.Data.Length;

            if (rcv_queue_.Count < seg.frg + 1)
                return -1;

            int length = 0;
            for (node = rcv_queue_.First; node != null; node = node.Next) {
                seg = node.Value;
                length += seg.Data.Length;
                if (seg.frg == 0)
                    break;
            }
            return length;
        }

        // parse ack
        void UpdateACK(int rtt) {
            if (rx_srtt == 0) {
                rx_srtt = rtt;
                rx_rttval = rtt / 2;
            } else {
                int delta = rtt - rx_srtt;
                if (delta < 0)
                    delta = -delta;

                rx_rttval = (3 * rx_rttval + delta) / 4;
                rx_srtt = (7 * rx_srtt + rtt) / 8;
                if (rx_srtt < 1)
                    rx_srtt = 1;
            }

            var rto = rx_srtt + _imax_(interval_, (uint)(4 * rx_rttval));
            rx_rto = (int)_ibound_((uint)rx_minrto, (uint)rto, IKCP_RTO_MAX);
        }

        /// <summary>
        /// 更新本地 snd_una 数据，如snd_buff为空，snd_una指向snd_nxt，否则指向send_buff首端
        /// </summary>
        void ShrinkBuf() {
            var node = snd_buf_.First;
            if (node != null) {
                var seg = node.Value;
                snd_una = seg.sn;
            } else {
                snd_una = snd_nxt;
            }
        }

        /// <summary>
        /// 函数 ikcp_parse_ack 来根据 ACK 的编号确认对方收到了哪个数据包；
        /// 实际上 更新 rtt
        /// </summary>
        /// <param name="sn"></param>
        void ParseACK(uint sn) {
            if (_itimediff(sn, snd_una) < 0 || _itimediff(sn, snd_nxt) >= 0)
                return;

            LinkedListNode<Segment> next = null;
            for (var node = snd_buf_.First; node != null; node = next) {
                var seg = node.Value;
                next = node.Next;
                if (sn == seg.sn) {
                    snd_buf_.Remove(node);
                    break;
                }
                if (_itimediff(sn, seg.sn) < 0)
                    break;
            }
        }

        /// <summary>
        /// 分析una，看哪些segment远端收到了，删除send_buf中小于una的segment
        ///
        ///
        /// 调用 ikcp_parse_una 来确定已经发送的数据包有哪些被对方接收到。
        /// 注意: KCP 中所有的报文类型均带有 una 信息。
        /// 前面介绍过，发送端发送的数据都会缓存在 snd_buf 中，直到接收到对方确认信息之后才会删除。
        /// 当接收到 una 信息后，表明 sn 小于 una 的数据包都已经被对方接收到，因此可以直接从 snd_buf 中删除。
        /// 同时调用 ikcp_shrink_buf 来更新 KCP 控制块的 snd_una 数值。
        /// </summary>
        /// <param name="una"></param>
        void ParseUNA(uint una) {
            LinkedListNode<Segment> next = null;
            for (var node = snd_buf_.First; node != null; node = next) {
                var seg = node.Value;
                next = node.Next;
                if (_itimediff(una, seg.sn) > 0) {
                    snd_buf_.Remove(node);
                } else {
                    break;
                }
            }
        }

        void ParseFastACK(uint sn) {
            if (_itimediff(sn, snd_una) < 0 || _itimediff(sn, snd_nxt) >= 0)
                return;

            LinkedListNode<Segment> next = null;
            for (var node = snd_buf_.First; node != null; node = next) {
                var seg = node.Value;
                next = node.Next;
                if (_itimediff(sn, seg.sn) < 0) {
                    break;
                } else if (sn != seg.sn) {
                    seg.fastack++;
                }
            }
        }

        // parse data
        void ParseData(Segment newseg) {
            uint sn = newseg.sn;
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
            }

            // move available data from rcv_buf -> rcv_queue
            while (rcv_buf_.Count > 0) {
                node = rcv_buf_.First;
                var seg = node.Value;
                if (seg.sn == rcv_nxt && rcv_queue_.Count < rcv_wnd) {
                    rcv_buf_.Remove(node);
                    rcv_queue_.AddLast(node);
                    rcv_nxt++;
                } else {
                    break;
                }
            }
        }

        int WndUnused() {
            if (rcv_queue_.Count < rcv_wnd)
                return (int)(rcv_wnd - rcv_queue_.Count);
            return 0;
        }


        public int Interval(int interval) {
            if (interval > 5000)
                interval = 5000;
            else if (interval < 10)
                interval = 10;

            interval_ = (uint)interval;
            return 0;
        }




        // get how many packet is waiting to be sent
        public int WaitSnd => snd_buf_.Count + snd_queue_.Count;
        // read conv
        public uint GetConv() => conv;

        public uint GetState() => state;


        void Log(kLog mask, string format, params object[] args) {
            // Console.WriteLine(mask + String.Format(format, args));
        }
    }
}
