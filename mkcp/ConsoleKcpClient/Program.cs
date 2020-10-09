using mkcp.kcp;
using NDesk.Options;
using NetCoreServer;
using System;
using System.Collections.Generic;
using System.Threading;

namespace ConsoleKcpClient {
    class Program {
        static void Main(string[] args) {
            //var syncCtx = new SingleThreadSynchronizationContext();
            //SynchronizationContext.SetSynchronizationContext(syncCtx);
            bool help = false;
            string address = "127.0.0.1";
            int port = 3333;
            int clients = 1;
            int messages = 1000;
            int size = 32;
            int seconds = 10;

            var options = new OptionSet()
            {
                { "h|?|help",   v => help = v != null },
                { "a|address=", v => address = v },
                { "p|port=", v => port = int.Parse(v) },
                { "c|clients=", v => clients = int.Parse(v) },
                { "m|messages=", v => messages = int.Parse(v) },
                { "s|size=", v => size = int.Parse(v) },
                { "z|seconds=", v => seconds = int.Parse(v) }
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

            Console.WriteLine($"Server address: {address}");
            Console.WriteLine($"Server port: {port}");
            Console.WriteLine($"Working clients: {clients}");
            Console.WriteLine($"Working messages: {messages}");
            Console.WriteLine($"Message size: {size}");
            Console.WriteLine($"Seconds to benchmarking: {seconds}");

            Console.WriteLine();

            // Prepare a message to send
            //MessageToSend = new byte[size];
            KcpTestClient.MessageToSend = new byte[size];

            // Create echo clients
            var echoClients = new List<KcpTestClient>();
            for (int i = 0; i < clients; ++i) {
                var client = new KcpTestClient(address, port, messages);
                echoClients.Add(client);
            }

            KcpTestClient.TimestampStart = DateTime.UtcNow;

            // Connect clients
            Console.Write("Clients connecting...");
            foreach (var client in echoClients)
                client.Connect();
            Console.WriteLine("Done!");
            /*
            foreach (var client in echoClients)
                while (!client.IsConnected)
                    Thread.Yield();
            Console.WriteLine("All clients connected!");
            */

            // Wait for benchmarking
            Console.Write("Benchmarking...");
            Thread.Sleep(seconds * 1000);
            Console.WriteLine("Done!");
            Console.ReadLine();

            // Disconnect clients
            Console.Write("Clients disconnecting...");
            foreach (var client in echoClients)
                client.Disconnect();
            Console.WriteLine("Done!");
            foreach (var client in echoClients)
                while (client.IsConnected)
                    Thread.Yield();
            Console.WriteLine("All clients disconnected!");

            Console.WriteLine();

            Console.WriteLine($"Errors: {KcpTestClient.TotalErrors}");

            Console.WriteLine();

            Console.WriteLine($"Total time: {Utilities.GenerateTimePeriod((KcpTestClient.TimestampStop - KcpTestClient.TimestampStart).TotalMilliseconds)}");
            Console.WriteLine($"Total data: {Utilities.GenerateDataSize(KcpTestClient.TotalBytes)}");
            Console.WriteLine($"Total messages: {KcpTestClient.TotalMessages}");
            Console.WriteLine($"Data throughput: {Utilities.GenerateDataSize((long)(KcpTestClient.TotalBytes / (KcpTestClient.TimestampStop - KcpTestClient.TimestampStart).TotalSeconds))}/s");
            if (KcpTestClient.TotalMessages > 0) {
                Console.WriteLine($"Message latency: {Utilities.GenerateTimePeriod((KcpTestClient.TimestampStop - KcpTestClient.TimestampStart).TotalMilliseconds / KcpTestClient.TotalMessages)}");
                Console.WriteLine($"Message throughput: {(long)(KcpTestClient.TotalMessages / (KcpTestClient.TimestampStop - KcpTestClient.TimestampStart).TotalSeconds)} msg/s");
            }
        }
    }
}
