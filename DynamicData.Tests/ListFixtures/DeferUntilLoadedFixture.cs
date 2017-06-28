using System;
using System.Linq;
using DynamicData.Tests.Domain;
using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class DeferAnsdSkipFixture
    {
        [Test]
        public void DeferUntilLoadedDoesNothingUntilDataHasBeenReceived()
        {
            bool updateReceived = false;
            IChangeSet<Person> result = null;

            var cache = new SourceList<Person>();

            var deferStream = cache.Connect().DeferUntilLoaded()
                                   .Subscribe(changes =>
                                   {
                                       updateReceived = true;
                                       result = changes;
                                   });

            var person = new Person("Test", 1);

            updateReceived.Should().BeFalse();
            cache.Add(person);

            updateReceived.Should().BeTrue();
            result.Adds.Should().Be(1);
            result.First().Item.Current.Should().Be(person);
            deferStream.Dispose();
        }

        [Test]
        public void SkipInitialDoesNotReturnTheFirstBatchOfData()
        {
            bool updateReceived = false;

            var cache = new SourceList<Person>();

            var deferStream = cache.Connect().SkipInitial()
                                   .Subscribe(changes => updateReceived = true);

            updateReceived.Should().BeFalse();

            cache.Add(new Person("P1", 1));

            updateReceived.Should().BeFalse();

            cache.Add(new Person("P2", 2));
            updateReceived.Should().BeTrue();
            deferStream.Dispose();
        }
    }
}
