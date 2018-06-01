using BenchmarkDotNet.Running;
using System;

namespace DynamicData.Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<SubscriptionBenchmarks>();
            Console.ReadLine();
        }
    }
}
