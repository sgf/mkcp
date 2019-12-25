using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace ConsolemKcpTest {

    /// <summary>
    /// 3字节int
    /// </summary>
    [StructLayout(LayoutKind.Sequential,Pack =1)]
    public struct TestS {
        public byte B1;
        public byte B2;
        public byte B3;
    }

    class Program {
        static void Main(string[] args) {
            Dictionary<int, int> keys = new Dictionary<int, int>();

                TestS testS = new TestS { B1 = 1, B2 = 2, B3 = 3 };
            unsafe {
                testS.B2 = 5;
                Console.WriteLine(testS.B3);
            }

            Console.WriteLine("Hello World!");
            Console.ReadLine();
        }
    }
}
