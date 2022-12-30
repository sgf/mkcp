using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("mkcp.xTest")]
namespace mkcp {


    //有个-idea
    //把包的编号按照顺序分配和排列 其实就是自增,但是他可以是循环的.这将直接决定包的并发量(2个字节最大值ushort6w多实际,如果为游戏设计,考虑到冗余,ack等每秒钟算2w个包,有效载荷470字节每个包)//或者1350字节

    //怎么计算有没有丢包呢? 官方的是现实是:
    //1.UNA/ACK 如果被跳过X次就认为丢包,立即重发.
        
    //这里当然也可以如此.只不过区别是官方使用的是什么?是链表.

    //我这里的思路是使用下标,与此同时. 可以直接以下标的跨度和实际发送出去的数量进行相减
    //    (从而拿到一个差值,这个差值应该要结合发送时间,来判断是否丢包?可能需要结合这里面有没有ACK,或者以第一个拿到ACK的包开始相减,具体怎么做这里面感觉大有文章可做)


    public partial class Kcp {

        //基本上10个字节搞定.2+1+2+2+2 

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        internal struct SegmentHead {
            /// <summary> 会话ID 仅仅用于 类似会话通道标记的功能，而不是用来作为SessionId标记（虽然也可以但是太浪费） </summary>
            [FieldOffset(0)]
            internal uint conv;
            [FieldOffset(4)]
            internal Cmd cmd;
            /// <summary> message中的segment分片ID（在message中的索引，由大到小，0表示最后一个分片） </summary>
            [FieldOffset(5)]
            internal byte frg;
            /// <summary> 剩余接收窗口大小(接收窗口大小-接收队列大小) 优化点:只需要告诉一个大概就可以了</summary>
            [FieldOffset(6)]
            internal ushort wnd;
            /// <summary> message发送时刻的时间戳 </summary>
            [FieldOffset(8)]
            internal uint ts;
            /// <summary> message分片segment的序号   若为减少分片,则可以规定例如:可以用一个bit表示是否按每1个,10个,100个字节为1个分片精度</summary>
            [FieldOffset(12)]
            internal uint sn;
            /// <summary>ts-sn联合字段 便于存储（缓存ACK信息）</summary>
            [FieldOffset(8)]
            internal ulong tssn;
            [FieldOffset(16)]
            internal uint una;
            [FieldOffset(20)]
            internal uint len;
        }

        internal unsafe class Segment : IDisposable {

            #region 网络传输部分
            private ref SegmentHead Head => ref Unsafe.As<byte, SegmentHead>(ref DataAll.Span[0]);

            /// <summary>
            /// 会话id
            /// </summary>
            internal ref uint conv => ref Head.conv;

            /// <summary>
            /// 协议命令
            /// </summary>
            internal ref Cmd cmd => ref Head.cmd;

            /// <summary>
            /// message中的segment分片ID（在message中的索引，由大到小，0表示最后一个分片）
            /// </summary>
            internal ref byte frg => ref Head.frg;

            /// <summary>
            /// 剩余接收窗口大小(接收窗口大小-接收队列大小)
            /// </summary>
            internal ref ushort wnd => ref Head.wnd;

            /// <summary>
            /// message发送时刻的时间戳
            /// </summary>
            internal ref uint ts => ref Head.ts;

            /// <summary>
            /// message分片segment的序号
            /// </summary>
            internal ref uint sn => ref Head.sn;


            /// <summary>
            /// ts-sn联合字段 便于存储(Ack列表)
            /// </summary>
            internal ref ulong tssn => ref Head.tssn;

            /// <summary>
            /// 待接收消息序号(接收滑动窗口左端)
            /// </summary>
            internal ref uint una => ref Head.una;

            /// <summary>
            /// 包长
            /// </summary>
            internal ref uint len => ref Head.len;

            #endregion

            /// <summary>
            /// 下次超时重传的时间戳
            /// </summary>
            public uint resend_ts { get; set; }

            /// <summary>
            /// 该分片的超时重传等待时间
            /// </summary>
            public uint rto { get; set; }

            /// <summary>
            /// 快速Ack,收到ack时计算的该分片被跳过的累计次数，即：该分片后的包都被对方收到了，达到一定次数，重传当前分片
            /// </summary>
            public int fastack { get; set; }

            /// <summary>
            /// segment累计发送次数，每发送一次加一
            /// </summary>
            public uint xmit { get; set; }

            /// <summary>
            /// 内存持有对象，用于释放持有的内存，从而回归到内存池（使用Dispose函数）
            /// </summary>
            private readonly IMemoryOwner<byte> mower;

            /// <summary>
            /// 包括了Head头部
            /// </summary>
            public readonly Memory<byte> DataAll;

            /// <summary>
            /// 包括了Head头部的长度
            /// </summary>
            internal readonly int LengthAll;

            /// <summary>
            /// 不包含Head头部
            /// </summary>
            public readonly Memory<byte> Data;

            /// <summary>
            /// 用于创建不带数据的空包
            /// </summary>
            private Segment() {
                LengthAll = IKCP_OVERHEAD;
                mower = MemoryPool<byte>.Shared.Rent(LengthAll);
                DataAll = mower.Memory.Slice(0, LengthAll);
                Data = Memory<byte>.Empty;
                this.len = (uint)Kcp.IKCP_OVERHEAD;
            }

            //if (allData.IsEmpty || allData.Length < IKCP_OVERHEAD) throw new ArgumentOutOfRangeException("allData 不能为空 且长度不能小于包头");、

            /// <summary>
            /// 用于创建带数据的包
            /// </summary>
            /// <param name="data"></param>
            /// <param name="fragmentId"></param>
            private Segment(Span<byte> data, int fragmentId) {
                LengthAll = IKCP_OVERHEAD + data.Length;
                mower = MemoryPool<byte>.Shared.Rent(LengthAll);
                DataAll = mower.Memory.Slice(0, LengthAll);
                Data = DataAll.Slice(Kcp.IKCP_OVERHEAD, data.Length);
                data.CopyTo(Data.Span);

                this.len = (uint)(DataAll.Length - Kcp.IKCP_OVERHEAD);
                if (fragmentId > 0)
                    this.frg = (byte)fragmentId;
            }

            /// <summary>
            /// 用于解析
            /// </summary>
            /// <param name="allData"></param>
            private Segment(Span<byte> allData) {
                LengthAll = allData.Length;
                var dataLen = allData.Length - IKCP_OVERHEAD;
                mower = MemoryPool<byte>.Shared.Rent(LengthAll);
                DataAll = mower.Memory.Slice(0, LengthAll);
                allData.CopyTo(DataAll.Span);
                if (dataLen == 0)
                    Data = Memory<byte>.Empty;
                else
                    Data = DataAll.Slice(Kcp.IKCP_OVERHEAD, dataLen);//无需Copy数据了因为DataAll中已经包含了所有数据


            }

            /// <summary>
            /// 带数据的
            /// </summary>
            /// <param name="data"></param>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Segment Create(Span<byte> data, int fragmentId = 0) => new Segment(data, fragmentId);

            /// <summary>
            /// 空包
            /// </summary>
            /// <returns></returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Segment Create() => new Segment();

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Segment Create(uint conv, Cmd cmd, int wndUnUsed, uint rcv_nxt) {
                var sg = new Segment();
                sg.conv = conv;
                sg.cmd = cmd;
                sg.wnd = (ushort)wndUnUsed;
                sg.una = rcv_nxt;
                return sg;
            }


            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal static Segment Prase(Span<byte> allData) => new Segment(allData);

            //MemoryMarshal.Cast<byte, SegmentHead>(ptr.AsSpan())[0] = Head; Unsafe.Copy<SegmentHead>(Unsafe.AsPointer(ref ptr.AsSpan()[0]), ref Head);
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal unsafe void Encode(Span<byte> ptr, ref int offset) {
                DataAll.Span.CopyTo(ptr);
                offset += DataAll.Length;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

                //seg.data = head.len == 0 ? Array.Empty<byte>() :
                //    reamainData.Slice(0, (int)head.len).ToArray();
                seg = Prase(pkdata);

                offset += (int)head.len;
                return true;
            }



            private bool disposed = false;
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Dispose() {
                if (!disposed) {
                    disposed = true;
                    mower?.Dispose();
                }
            }

        }

        //internal static (bool isbad, uint conv) IsBadHeadFormat(Span<byte> pk) {
        //    //有三种结果:
        //    //1.包不合法，丢弃+拉黑
        //    if (pk.Length < Kcp.IKCP_OVERHEAD) { //包数据太小
        //                                         //logger?.LogInformation($"Client:{kcpSession.IP} duplicate,cant Add to Session list!");
        //        return (true, 0);
        //    }
        //    var dataSize = pk.Length - Kcp.IKCP_OVERHEAD;
        //    ref var segHead = ref pk.Read<Kcp.SegmentHead>();
        //    if (dataSize < segHead->len //Data数据太小
        //       || segHead->cmd < Cmd.IKCP_CMD_PUSH || segHead->cmd > Cmd.IKCP_CMD_WINS) { //cmd命令不存在
        //                                                                                  //logger?.LogInformation($"Client:{kcpSession.IP} duplicate,cant Add to Session list!");
        //        return (true, segHead->conv);
        //    }
        //    return (false, segHead->conv);
        //}
    }


}
