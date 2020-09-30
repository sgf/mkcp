using System;
using Xunit;
using static mkcp.Kcp;

namespace mkcp.xTest {
    public class SegmentTest {
        [Fact]
        public void Test1() {
            var segment = Segment.Create();
            segment.conv = 1;
            segment.cmd = Cmd.IKCP_CMD_ACK;
            segment.frg = 3;
            segment.len = 0;
            var buf = new byte[24];
            var offset = 0;
            segment.Encode(buf, ref offset);
            offset = 0;
            Assert.True(Segment.TryRead(buf, ref offset, out Segment seg, 1));
            //ref var head = ref buf.AsSpan().Read<SegmentHead>();
            Assert.Equal(1, (int)seg.conv);
            Assert.Equal(Cmd.IKCP_CMD_ACK, seg.cmd);
            Assert.Equal(3, (int)seg.frg);
            Assert.Equal(0, (int)seg.len);

        }
    }
}
