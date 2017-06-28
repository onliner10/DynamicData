﻿using System;
using System.Linq;
using DynamicData.Kernel;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.CacheFixtures
{
    [TestFixture]
    public class GroupImmutableFixture
    {
        private ISourceCache<Person, string> _source;
        private ChangeSetAggregator<IGrouping<Person, string, int>, int> _results;

        [SetUp]
        public void Initialise()
        {
            _source = new SourceCache<Person, string>(p => p.Name);
            _results = _source.Connect().GroupWithImmutableState(p => p.Age).AsAggregator();
        }

        [TearDown]
        public void CleanUp()
        {
            _source.Dispose();
            _results.Dispose();
        }



        [Test]
        public void Add()
        {

            _source.AddOrUpdate(new Person("Person1", 20));
            _results.Data.Count.Should().Be(1, "Should be 1 add");
            _results.Messages.First().Adds.Should().Be(1);
        }

        [Test]
        public void UpdatesArePermissible()
        {
            _source.AddOrUpdate(new Person("Person1", 20));
            _source.AddOrUpdate(new Person("Person2", 20));

            _results.Data.Count.Should().Be(1);//1 group
            _results.Messages.First().Adds.Should().Be(1);
            _results.Messages.Skip(1).First().Updates.Should().Be(1);

            var group = _results.Data.Items.First();
            group.Count.Should().Be(2);
        }

        [Test]
        public void UpdateAnItemWillChangedThegroup()
        {
            _source.AddOrUpdate(new Person("Person1", 20));
            _source.AddOrUpdate(new Person("Person1", 21));

            _results.Data.Count.Should().Be(1);
            _results.Messages.First().Adds.Should().Be(1);
            _results.Messages.Skip(1).First().Adds.Should().Be(1);
            _results.Messages.Skip(1).First().Removes.Should().Be(1);
            var group = _results.Data.Items.First();
            group.Count.Should().Be(1);

            group.Key.Should().Be(21);
        }

        [Test]
        public void Remove()
        {
            _source.AddOrUpdate(new Person("Person1", 20));
            _source.Remove(new Person("Person1", 20));

            _results.Messages.Count.Should().Be(2);
            _results.Data.Count.Should().Be(0);
        }

        [Test]
        public void FiresManyValueForBatchOfDifferentAdds()
        {
            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 21));
                updater.AddOrUpdate(new Person("Person3", 22));
                updater.AddOrUpdate(new Person("Person4", 23));
            });

            _results.Data.Count.Should().Be(4);
            _results.Messages.Count.Should().Be(1);
            _results.Messages.First().Count.Should().Be(4);
            foreach (var update in _results.Messages.First())
            {
                update.Reason.Should().Be(ChangeReason.Add);
            }
        }

        [Test]
        public void FiresOnlyOnceForABatchOfUniqueValues()
        {
            _source.Edit(updater =>
            {
                updater.AddOrUpdate(new Person("Person1", 20));
                updater.AddOrUpdate(new Person("Person2", 20));
                updater.AddOrUpdate(new Person("Person3", 20));
                updater.AddOrUpdate(new Person("Person4", 20));
            });

            _results.Messages.Count.Should().Be(1);
            _results.Messages.First().Adds.Should().Be(1);
            _results.Data.Items.First().Count.Should().Be(4);
        }

        [Test]
        public void ChanegMultipleGroups()
        {
            var initialPeople = Enumerable.Range(1, 100)
                .Select(i => new Person("Person" + i, i % 10))
                .ToArray();

            _source.AddOrUpdate(initialPeople);

            initialPeople.GroupBy(p => p.Age)
                .ForEach(group =>
                {
                    var cache = _results.Data.Lookup(group.Key).Value;
                    CollectionAssert.AreEquivalent(group, cache.Items);
                });

            var changedPeople = Enumerable.Range(1, 100)
                 .Select(i => new Person("Person" + i, i % 5))
                 .ToArray();

            _source.AddOrUpdate(changedPeople);

            changedPeople.GroupBy(p => p.Age)
                .ForEach(group =>
                {
                    var cache = _results.Data.Lookup(group.Key).Value;
                    CollectionAssert.AreEquivalent(group,cache.Items);

                });
            
            _results.Messages.Count.Should().Be(2);
            _results.Messages.First().Adds.Should().Be(10);
            _results.Messages.Skip(1).First().Removes.Should().Be(5);
            _results.Messages.Skip(1).First().Updates.Should().Be(5);
        }

        [Test]
        public void Reevaluate()
        {
            var initialPeople = Enumerable.Range(1, 10)
                .Select(i => new Person("Person" + i, i % 2))
                .ToArray();

            _source.AddOrUpdate(initialPeople); 
            _results.Messages.Count.Should().Be(1);

            //do an inline update
            foreach (var person in initialPeople)
                person.Age = person.Age + 1;

            //signal operators to evaluate again
            _source.Refresh();

            initialPeople.GroupBy(p => p.Age)
                .ForEach(group =>
                {
                    var cache = _results.Data.Lookup(group.Key).Value;
                    CollectionAssert.AreEquivalent(group, cache.Items);

                });

            _results.Data.Count.Should().Be(2);
            _results.Messages.Count.Should().Be(2);

            var secondMessage = _results.Messages.Skip(1).First();
            secondMessage.Removes.Should().Be(1);
            secondMessage.Updates.Should().Be(1);
            secondMessage.Adds.Should().Be(1);
        }
    }
}