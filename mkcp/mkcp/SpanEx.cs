using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace mkcp {
    internal static class SpanEx {



        public static ref T Read<T>(this Span<byte> buff) where T : struct => ref MemoryMarshal.AsRef<T>(buff);
        public static ref T Read<T>(this Span<byte> buff, int offset) where T : struct => ref MemoryMarshal.AsRef<T>(buff.Slice(offset));

        /// <summary>
        /// 读取T类型并返回 src 位置不变<byte>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="src"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void Write<T>(this Span<byte> src, ref T tv) where T : unmanaged {
            //ref var tr= ref MemoryMarshal.AsRef<T>(src);
            ref var tr = ref Unsafe.As<byte, T>(ref src[0]);
            tr = tv;
            //return ref MemoryMarshal.AsRef<T>(src);
        }
    }
}
