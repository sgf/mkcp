using HellBrick.Collections;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace ConsoleApp2 {
    class Program {

        public class testClass {
            public testClass(string txt) => Txt = txt;
            public string Txt;
        }
        public class testClassABC<T> where T : class {
            public T _awaiterTail = null;
            public async ValueTask<T> ABCTest() {
                SpinWait swait = new SpinWait();
                var sw = Stopwatch.StartNew();
                while (true) {
                    var t = Volatile.Read(ref _awaiterTail);
                    if (t != null)
                        return t;
                    //await Task.Delay(5000);
                    swait.SpinOnce();
                }
            }

        }


        static void Main(string[] args) {
            Console.WriteLine("5秒后输出结果");

            var abcd = new testClassABC<testClass>();
            var abcdRlt = Task.Factory.StartNew(abcd.ABCTest);
            Task.Delay(5000).ContinueWith(async => {
                abcd._awaiterTail = new testClass("dsfsadtfdfgsdgdfgrdftgdfgf");
            });
            abcdRlt.Wait();
            Console.WriteLine(abcdRlt.Result.Result.Txt);

            Task.Run(Worker);
            bool ConsoleExit = false;
            Console.WriteLine(
                @"输入 ""[any]"" worker 会展示消息(只要不是[true/false]的任何值) ,输入 ""[true / false]"" 退出 , 默认5秒会展示消息");
            while (!ConsoleExit) {
                var line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) {
                    Console.WriteLine("输入错误，重新尝试！");
                    continue;
                }

                if (!bool.TryParse(line, out bool exit)) {
                    q1.Add($"来自用户输入的消息：{line}");//发消息给q1
                    continue;
                }
                if (exit) {
                    q2.Add(exit);//发消息给q2
                    break;
                }
            }

            Console.WriteLine("等待工作线程退出");
            qexitAll.TakeAsync().Wait();
            Console.WriteLine("工作线已经退出");

            Console.WriteLine("Hello World!");
        }

        static void Do1(string msg) => Console.WriteLine($"Worer: Work来源:{msg}");
        static void ExitMsg(bool exit) => Console.WriteLine($"Exit来源：获取到 Exit的值{exit}");
        static void DoExit() => Console.WriteLine("Exit来源：Do Exit");
        static void DoTimeOut() => Console.WriteLine("Worer:超时消息");

        static AsyncQueue<string> q1 = new AsyncQueue<string>();
        static AsyncQueue<bool> q2 = new AsyncQueue<bool>();
        static AsyncQueue<int> qexitAll = new AsyncQueue<int>();

        static void Worker() {
            int interval = 5000;
            var sw = Stopwatch.StartNew();


            while (true) {
                var css = new[] { new CancellationTokenSource(), new CancellationTokenSource(), new CancellationTokenSource() };
                var work = q1.TakeAsync(css[0].Token);
                var exit = q2.TakeAsync(css[1].Token);
                var needWait = interval - (int)sw.ElapsedMilliseconds;
                var waitTime = needWait > 0 ? needWait : 0;
                Task[] ts = {
                       work,
                        exit,
                        Task.Delay(waitTime, css[2].Token) //延迟剩余时间
                    };
                var idx = Task.WaitAny(ts);
                if (!(idx >= 0 && idx <= 2)) {
                    throw new Exception("Worer:未知任务索引错误");
                }
                for (int i = 0; i < ts.Length; i++) {//除了返回的任务之外其余全部取消
                    try {
                        if (i != idx) css[i].Cancel();//这里大概还要考虑 取消失败的情况？？？取消失败怎么搞？
                    } catch (Exception ex) {
                        Console.WriteLine(ex.Message);
                    }
                }
                switch (idx) {
                    case 0:
                        Do1(work.Result);
                        break;
                    case 1:
                        ExitMsg(exit.Result);
                        if (exit.Result) {
                            DoExit();
                            goto EndWork;
                        }
                        break;
                    case 2:
                        //DoTimeOut();//不需要做任何事情(因为这里如果别的Task成功返回，这里又基本恰好满足的话就会漏执行)
                        break;
                }
                if (sw.ElapsedMilliseconds > interval) {
                    sw.Restart();
                    DoTimeOut();
                }

            }
            EndWork:
            qexitAll.Add(Task.CurrentId ?? 0);
            ;
        }

        static readonly Channel<int> chan = Channel.CreateUnbounded<int>();

        static void Worker1() {
            BlockingCollection<int>.TakeFromAny(new BlockingCollection<int>[] { }, out int it);
            //chan.Reader. ()

        }
    }
}
