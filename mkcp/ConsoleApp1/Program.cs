using System;
using System.Buffers;
using System.Runtime.InteropServices;

namespace ConsoleApp1 {
    class Program {


        public struct TestS {
          public int abc;

        }

        public unsafe class TestC {
            public unsafe TestC() {
                data = ArrayPool<byte>.Shared.Rent(255);

            }
            public ref TestS tests => ref MemoryMarshal.AsRef<TestS>(data.Span);
            public Memory<TestS> tests;
            public Memory<byte> data;


        }


        static void Main(string[] args) {
            TestC testC = new Program.TestC();
            ref var ttt =ref testC.tests;
            ttt.abc = 0;


            Console.WriteLine("Hello World!");
        }
    }
}
