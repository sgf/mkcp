﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("mkcp.xTest")]
namespace mkcp {

    public partial class Kcp {




        internal enum Cmd : byte {
            /// <summary>
            /// cmd: push data
            /// </summary>
            IKCP_CMD_PUSH = 81,
            /// <summary>
            /// cmd: ack
            /// </summary>
            IKCP_CMD_ACK = 82,
            /// <summary>
            /// cmd: window probe (ask) 询问对方当前剩余窗口大小 请求
            /// </summary>
            IKCP_CMD_WASK = 83,
            /// <summary>
            /// cmd: window size (tell) 返回本地当前剩余窗口大小
            /// </summary>
            IKCP_CMD_WINS = 84,
        }


        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct SegmentHead {
            internal uint conv;
            internal Cmd cmd;
            internal byte frg;
            internal ushort wnd;
            internal uint ts;
            internal uint sn;
            internal uint una;
            internal uint len;
        }

        internal class Segment {

            #region 网络传输部分
            internal SegmentHead Head;

            /// <summary>
            /// 会话id
            /// </summary>
            internal uint conv { get { return Head.conv; } set { Head.conv = value; } }
            /// <summary>
            /// 协议命令
            /// </summary>
            internal Cmd cmd { get { return Head.cmd; } set { Head.cmd = value; } }
            /// <summary>
            /// message中的segment分片ID（在message中的索引，由大到小，0表示最后一个分片）
            /// </summary>
            internal byte frg { get { return Head.frg; } set { Head.frg = value; } }
            /// <summary>
            /// 剩余接收窗口大小(接收窗口大小-接收队列大小)
            /// </summary>
            internal ushort wnd { get { return Head.wnd; } set { Head.wnd = value; } }

            /// <summary>
            /// message发送时刻的时间戳
            /// </summary>
            internal uint ts { get { return Head.ts; } set { Head.ts = value; } }

            /// <summary>
            /// message分片segment的序号
            /// </summary>
            internal uint sn { get { return Head.sn; } set { Head.sn = value; } }
            /// <summary>
            /// 待接收消息序号(接收滑动窗口左端)
            /// </summary>
            internal uint una { get { return Head.una; } set { Head.una = value; } }

            /// <summary>
            /// 包长
            /// </summary>
            internal uint len { get { return Head.len; } set { Head.len = value; } }

            /// <summary>
            /// 数据包本体（如果携带数据的话）
            /// </summary>
            internal byte[] data { get; set; }


            #endregion

            /// <summary>
            /// 下次超时重传的时间戳
            /// </summary>
            public long resendts { get; set; }

            /// <summary>
            /// 该分片的超时重传等待时间
            /// </summary>
            public int rto { get; set; }

            /// <summary>
            /// 快速Ack,收到ack时计算的该分片被跳过的累计次数，即：该分片后的包都被对方收到了，达到一定次数，重传当前分片
            /// </summary>
            public int fastack { get; set; }

            /// <summary>
            /// 发送分片的次数，每发送一次加一
            /// </summary>
            public int xmit { get; set; }


            internal Segment(int size = 0) {
                data = new byte[size];
            }

            //MemoryMarshal.Cast<byte, SegmentHead>(ptr.AsSpan())[0] = Head; Unsafe.Copy<SegmentHead>(Unsafe.AsPointer(ref ptr.AsSpan()[0]), ref Head);
            internal unsafe void Encode(Span<byte> ptr, ref int offset) {
                this.len = (uint)(data?.Length ?? 0);
                ptr.Write(ref Head);
                offset += Kcp.IKCP_OVERHEAD;
                data.CopyTo(ptr.Slice(Kcp.IKCP_OVERHEAD));
                offset += (int)this.len;
            }

            internal static bool TryRead(Span<byte> pkdata, ref int offset, out Segment seg, uint conv) {
                seg = null;
                //包数据太小 kcp包头一共24个字节, size减去IKCP_OVERHEAD即24个字节应该不小于len
                if (pkdata.Length < Kcp.IKCP_OVERHEAD) return false;
                ref var head = ref pkdata.Read<SegmentHead>();
                offset += Kcp.IKCP_OVERHEAD;
                if (conv != head.conv) return false;//连接ID不对
                switch (head.cmd) { //cmd命令不存在
                    case Cmd.IKCP_CMD_PUSH:
                    case Cmd.IKCP_CMD_ACK:
                    case Cmd.IKCP_CMD_WASK:
                    case Cmd.IKCP_CMD_WINS:
                        break;
                    default:
                        return false;
                }
                var reamainData = pkdata.Slice(Kcp.IKCP_OVERHEAD);
                if (reamainData.Length < head.len) return false;//剩下的数据长度不足

                seg = new Segment();
                seg.Head = head;
                seg.data = head.len == 0 ? Array.Empty<byte>() :
                    reamainData.Slice(0, (int)head.len).ToArray();
                offset += (int)head.len;
                return true;
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
               || segHead.cmd < Cmd.IKCP_CMD_PUSH || segHead.cmd > Cmd.IKCP_CMD_WINS) { //cmd命令不存在
                                                                                        //logger?.LogInformation($"Client:{kcpSession.IP} duplicate,cant Add to Session list!");
                return (true, segHead.conv);
            }
            return (false, segHead.conv);
        }
    }


}
