using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace mkcp {
    public static class OS {
        public static bool IsLinux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        public static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        public static bool IsOSX => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        public static bool IsFreeBSD => RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD);

        public static bool IsX64 => RuntimeInformation.ProcessArchitecture == Architecture.X64;
        public static bool X86 => RuntimeInformation.ProcessArchitecture == Architecture.X86;
        public static bool Arm => RuntimeInformation.ProcessArchitecture == Architecture.Arm;
        public static bool Arm64 => RuntimeInformation.ProcessArchitecture == Architecture.Arm64;

        public const ushort _1kb = 1024;
        public const ushort _4kb = _1kb * 4;
        public const ushort _8kb = _1kb * 8;
    }
}
