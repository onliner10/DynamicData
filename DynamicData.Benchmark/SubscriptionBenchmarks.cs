using BenchmarkDotNet.Attributes;
using Bogus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Threading.Tasks;

namespace DynamicData.Benchmark
{
    [MemoryDiagnoser]
    public class SubscriptionBenchmarks
    {
        private const int N = 100000;
        private SourceCache<DummyEntity, Guid> _sourceCache;
        private List<DummyEntity> _dummyInput;

        public SubscriptionBenchmarks()
        {
            _sourceCache = new SourceCache<DummyEntity, Guid>(de => de.Key);
        }

        [GlobalSetup]
        public void GenerateDummyEntities()
        {
            _dummyInput = new Faker<DummyEntity>().Generate(N);
        }

        [Benchmark]
        public void OneSubscription()
        {
            RunSubscriptionBenchmark(1);
        }

        [Benchmark]
        public void TenSubscriptions()
        {
            RunSubscriptionBenchmark(10);
        }


        [Benchmark]
        public void FiftySubscriptions()
        {
            RunSubscriptionBenchmark(50);
        }

        private void RunSubscriptionBenchmark(int subscriptionsCount)
        {
            var subscriptionTasks =
               Enumerable.Range(1, subscriptionsCount)
                   .Select(_ => {
                       int count = 0;
                       return _sourceCache.Connect().Select(x => { count = count + 1; return count; }).Where(c => c == N).Take(1).ToTask();
                   }).ToArray();

            _dummyInput.ForEach(e => _sourceCache.AddOrUpdate(e));

            Task.WaitAll(subscriptionTasks);
        }
    }
}
