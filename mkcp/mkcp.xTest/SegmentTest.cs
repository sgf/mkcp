using System;
using Xunit;
using mkcp;
using static mkcp.kcp;

namespace mkcp.xTest {
    public class SegmentTest {
        [Fact]
        public void Test1() {
            Segment segment = new Segment();
            segment.conv = 1;
            segment.cmd = 2;
            segment.frg = 3;
            segment.len = 0;
            var buf= new byte[24];
            var offset = 0;
            segment.Encode(buf, ref offset);
            offset = 0;
            var head= Segment.Decode(buf, ref offset);
            Assert.Equal(1, (int)head.conv);
            Assert.Equal(2, (int)head.cmd);
            Assert.Equal(3, (int)head.frg);
            Assert.Equal(0, (int)head.len);

        }
    }
}
