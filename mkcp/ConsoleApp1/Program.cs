using System;

namespace ConsoleApp1 {
    class Program {


        static void Main(string[] args) {
            var offset = 2;
            for (int i = offset; i >= 0; i--) {
                Console.WriteLine(i);
            }
            var abc = uint.MaxValue;
            abc++;
            Console.WriteLine(abc);
            Console.WriteLine("Hello World!");
            Console.ReadKey();
        }
    }
}
