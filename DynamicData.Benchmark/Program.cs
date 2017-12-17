using System;
using BenchmarkDotNet.Running;

namespace DynamicData.Benchmark
{
    internal class Program
    {
        
        public static void Main(string[] args)
        {

            BenchmarkRunner.Run<SourceCacheSortingBenchmark>();
        }
    }
}