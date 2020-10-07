using mkcp;
using mkcp.kcp;
using NDesk.Options;
using System;
using System.Net;
using System.Threading;

namespace ConsoleApp1 {

    class Program {
        static void Main(string[] args) {
            var syncCtx = new SingleThreadSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(syncCtx);
            bool help = false;
            int port = 3333;

            var options = new OptionSet()
            {
                { "h|?|help",   v => help = v != null },
                { "p|port=", v => port = int.Parse(v) }
            };

            try {
                options.Parse(args);
            } catch (OptionException e) {
                Console.Write("Command line error: ");
                Console.WriteLine(e.Message);
                Console.WriteLine("Try `--help' to get usage information.");
                return;
            }

            if (help) {
                Console.WriteLine("Usage:");
                options.WriteOptionDescriptions(Console.Out);
                return;
            }

            Console.WriteLine($"Server port: {port}");

            Console.WriteLine();

            // Create a new echo server
            var server = new KCPTestSvr(IPAddress.Any, port) { OptionReuseAddress = true };

            // Start the server
            Console.Write("Server starting...");
            server.Start();
            Console.WriteLine("Done!");

            Console.WriteLine("Press Enter to stop the server or '!' to restart the server...");

            // Perform text input
            for (; ; )
            {
                string line = Console.ReadLine();
                if (string.IsNullOrEmpty(line))
                    break;

                // Restart the server
                if (line == "!") {
                    Console.Write("Server restarting...");
                    server.Restart();
                    Console.WriteLine("Done!");
                }
            }

            // Stop the server
            Console.Write("Server stopping...");
            server.Stop();
            Console.WriteLine("Done!");

            server.Restart();
        }
    }
}
