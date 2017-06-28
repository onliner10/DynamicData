using FluentAssertions;
using NUnit.Framework;

namespace DynamicData.Tests.ListFixtures
{
    [TestFixture]
    public class MergeManyChangeSetsFixture
    {
        [Test]
        public void MergeManyShouldWork()
        {
            var a = new SourceList<int>();
            var b = new SourceList<int>();
            var c = new SourceList<int>();

            var parent = new SourceList<SourceList<int>>();
            parent.Add(a);
            parent.Add(b);
            parent.Add(c);

            var d = parent.Connect()
                          .MergeMany(e => e.Connect().RemoveIndex())
                          .AsObservableList();

            0.Should().Be(d.Count);

            a.Add(1);

            1.Should().Be(d.Count);
            a.Add(2);
            2.Should().Be(d.Count);

            b.Add(3);
            3.Should().Be(d.Count);
            b.Add(5);
            4.Should().Be(d.Count);
            CollectionAssert.AreEquivalent(d.Items, new[] { 1, 2, 3, 5 });

            b.Clear();

            // Fails below
            2.Should().Be(d.Count);
            CollectionAssert.AreEquivalent(d.Items, new[] { 1, 2 });
        }
    }
}
