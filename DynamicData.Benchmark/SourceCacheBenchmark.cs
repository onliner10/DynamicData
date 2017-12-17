﻿﻿using System;
using System.Collections.Generic;
using System.Reactive.Linq;
 using System.Reactive.Subjects;
 using System.Reactive.Threading.Tasks;
 using BenchmarkDotNet.Attributes;
using Bogus;

namespace DynamicData.Benchmark
{
    public class SourceCacheSortingBenchmark
    {
        private SourceCache<DummyUser, Guid> _sut;
        
        static SourceCacheSortingBenchmark()
        {
            Randomizer.Seed = new Random(8675309);
        }

        [GlobalSetup]
        public void Init()
        {
            _sut = new SourceCache<DummyUser, Guid>(e => e.Id);

            var generator =
                new Faker<DummyUser>()
                    .RuleFor(u => u.Id, Guid.NewGuid)
                    .RuleFor(u => u.FirstName, f => f.Name.FirstName())
                    .RuleFor(u  => u.LastName, f => f.Name.LastName())
                    .RuleFor(u => u.Email, f => f.Internet.Email())
                    .RuleFor(u => u.Birthday, f => f.Date.Past())
                    .RuleFor(u => u.Age, f => f.Random.Number(0, 100));

            var dataSet = generator.Generate(100000);

            _sut.AddOrUpdate(dataSet);
        }


        [Benchmark]
        public ISortedChangeSet<DummyUser, Guid> Sorting_100K_Collection()
        {
            return _sut.Connect()
                .Sort(Comparer<DummyUser>.Create((x, y) => String.Compare(x.Email, y.Email, StringComparison.Ordinal)),
                    SortOptimisations.None, 25).FirstAsync().Wait();
        }
        
        [Benchmark]
        public ISortedChangeSet<DummyUser, Guid> ReSorting_100K_Collection()
        {
            var connected = _sut.Connect();
            
            connected
                .Sort(Comparer<DummyUser>.Create((x, y) => String.Compare(x.Email, y.Email, StringComparison.Ordinal)),
                    SortOptimisations.None, 25).FirstAsync().Wait();
            
            return connected
                .Sort(Comparer<DummyUser>.Create((x, y) => String.Compare(x.FirstName, y.FirstName, StringComparison.Ordinal)),
                    SortOptimisations.None, 25).FirstAsync().Wait();
        }
    }

    public class DummyUser
    {
        public Guid Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public DateTime Birthday { get; set; }
        public int Age { get; set; }
    }
}