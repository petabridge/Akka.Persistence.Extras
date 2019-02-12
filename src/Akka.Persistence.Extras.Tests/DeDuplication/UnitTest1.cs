using System.Collections.Immutable;
using System;
using Xunit;

namespace Akka.Persistence.Extras.Tests
{
    public class DeDuplicatingReceiverSpecs
    {
        [Fact]
        public void TestMethod1()
        {
        }
    }

    public class DeDuplicatingReceiverModel
    {
        public ImmutableDictionary<string, DateTime> SenderLru { get; set; } = ImmutableDictionary<string, DateTime>.Empty;

        public ImmutableDictionary<string, ImmutableHashSet<long>> SenderIds { get; set; } = ImmutableDictionary<string, ImmutableHashSet<long>>.Empty;
    }
}
