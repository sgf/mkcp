using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("mkcp.xTest")]
namespace mkcp {

    public partial class Kcp {

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct SegmentHead {
            internal uint conv;
            internal byte cmd;
            internal byte frg;
            internal ushort wnd;
            internal uint ts;
            internal uint sn;
            internal uint una;
            internal uint len;
        }

        internal class Segment {
            internal SegmentHead Head;
            internal uint conv { get { return Head.conv; } set { Head.conv = value; } }
            internal byte cmd { get { return Head.cmd; } set { Head.cmd = value; } }
            internal byte frg { get { return Head.frg; } set { Head.frg = value; } }
            internal ushort wnd { get { return Head.wnd; } set { Head.wnd = value; } }
            internal uint ts { get { return Head.ts; } set { Head.ts = value; } }
            internal uint sn { get { return Head.sn; } set { Head.sn = value; } }
            internal uint una { get { return Head.una; } set { Head.una = value; } }
            internal uint len { get { return Head.len; } set { Head.len = value; } }

            internal UInt32 resendts = 0;
            internal UInt32 rto = 0;
            /// <summary>
            /// 快速Ack
            /// </summary>
            internal UInt32 faskack = 0;
            /// <summary>
            /// 最大重传次数
            /// </summary>
            internal UInt32 xmit = 0;
            internal byte[] data { get; set; }

            internal Segment(int size = 0) {
                data = new byte[size];
            }

            //MemoryMarshal.Cast<byte, SegmentHead>(ptr.AsSpan())[0] = Head; Unsafe.Copy<SegmentHead>(Unsafe.AsPointer(ref ptr.AsSpan()[0]), ref Head);
            internal unsafe void Encode(byte[] ptr, ref int offset) {
                this.len = (UInt32)data.Length;
                MemoryMarshal.AsRef<SegmentHead>(ptr.AsSpan()) = Head;
                offset += IKCP_OVERHEAD;
            }

        }


    }
    internal static class SpanEx {
        public static ref T Read<T>(this Span<byte> buff) where T : struct => ref MemoryMarshal.AsRef<T>(buff);
        public static ref T Read<T>(this Span<byte> buff, int offset) where T : struct => ref MemoryMarshal.AsRef<T>(buff.Slice(offset));

    }

}
