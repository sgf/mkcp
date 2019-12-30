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

        internal static (bool isbad, uint conv) IsBadHeadFormat(Span<byte> pk) {
            //有三种结果:
            //1.包不合法，丢弃+拉黑
            if (pk.Length < Kcp.IKCP_OVERHEAD) { //包数据太小
                                                 //logger?.LogInformation($"Client:{kcpSession.IP} duplicate,cant Add to Session list!");
                return (true, 0);
            }
            var dataSize = pk.Length - Kcp.IKCP_OVERHEAD;
            ref var segHead = ref pk.Read<Kcp.SegmentHead>();
            if (dataSize < segHead.len //Data数据太小
               || segHead.cmd < Kcp.IKCP_CMD_PUSH || segHead.cmd > Kcp.IKCP_CMD_WINS) { //cmd命令不存在
                                                                                        //logger?.LogInformation($"Client:{kcpSession.IP} duplicate,cant Add to Session list!");
                return (true, segHead.conv);
            }
            return (false, segHead.conv);
        }
    }


}
