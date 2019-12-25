using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

[assembly: InternalsVisibleTo("mkcp.xTest")]
namespace mkcp {

    public partial class kcp {

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
            internal UInt32 faskack = 0;
            internal UInt32 xmit = 0;
            internal byte[] data;

            internal Segment(int size = 0) {
                data = new byte[size];
            }

            //MemoryMarshal.Cast<byte, SegmentHead>(ptr.AsSpan())[0] = Head; Unsafe.Copy<SegmentHead>(Unsafe.AsPointer(ref ptr.AsSpan()[0]), ref Head);
            internal unsafe void Encode(byte[] ptr, ref int offset) {
                this.len = (UInt32)data.Length;
                MemoryMarshal.AsRef<SegmentHead>(ptr.AsSpan()) = Head;
                offset += IKCP_OVERHEAD;
            }

            internal unsafe static ref SegmentHead Decode(byte[] ptr, ref int offset) {
                offset += IKCP_OVERHEAD;
                return ref MemoryMarshal.AsRef<SegmentHead>(ptr.AsSpan());
            }

        }
    }

}
