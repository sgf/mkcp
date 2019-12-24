using System;
using System.Runtime.CompilerServices;

namespace mkcp {
    public class kcp {

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


        // encode 8 bits unsigned int
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ikcp_encode8u(byte[] p, int offset, byte c) {
            p[offset] = c;
        }

        // decode 8 bits unsigned int
        public static byte ikcp_decode8u(byte[] p, ref int offset) {
            return p[offset++];
        }

        // encode 16 bits unsigned int (lsb)
        public static void ikcp_encode16u(byte[] p, int offset, UInt16 v) {
            p[offset] = (byte)(v & 0xFF);
            p[offset + 1] = (byte)(v >> 8);
        }

        // decode 16 bits unsigned int (lsb)
        public static UInt16 ikcp_decode16u(byte[] p, ref int offset) {
            int pos = offset;
            offset += 2;
            return (UInt16)((UInt16)p[pos] | (UInt16)(p[pos + 1] << 8));
        }

        // encode 32 bits unsigned int (lsb)
        public static void ikcp_encode32u(byte[] p, int offset, UInt32 l) {
            p[offset] = (byte)(l & 0xFF);
            p[offset + 1] = (byte)(l >> 8);
            p[offset + 2] = (byte)(l >> 16);
            p[offset + 3] = (byte)(l >> 24);
        }

        // decode 32 bits unsigned int (lsb)
        public static UInt32 ikcp_decode32u(byte[] p, ref int offset) {
            int pos = offset;
            offset += 4;
            return ((UInt32)p[pos] | (UInt32)(p[pos + 1] << 8)
                | (UInt32)(p[pos + 2] << 16) | (UInt32)(p[pos + 3] << 24));
        }

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
