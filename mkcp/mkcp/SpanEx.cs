using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace mkcp {
    internal static class SpanEx {
        public static ref T Read<T>(this Span<byte> buff) where T : struct => ref MemoryMarshal.AsRef<T>(buff);
        public static ref T Read<T>(this Span<byte> buff, int offset) where T : struct => ref MemoryMarshal.AsRef<T>(buff.Slice(offset));
    }
}
