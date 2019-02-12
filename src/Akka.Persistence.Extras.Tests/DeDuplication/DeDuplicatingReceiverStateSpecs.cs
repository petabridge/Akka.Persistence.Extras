using System;
using System.Collections.Immutable;
using FluentAssertions;
using FsCheck;
using FsCheck.Experimental;
using FsCheck.Xunit;
using Xunit;

namespace Akka.Persistence.Extras.Tests.DeDuplication
{
    public class DeDuplicatingReceiverStateSpecs
    {
        [Property(MaxTest = 1000)]
        public Property DeDuplicatingReceiverStateModel_should_hold_for_AnyOrderReceiverState()
        {
            var model = new DeDuplicatingReceiverStateModel(new AnyOrderReceiverStateSetup());
            return model.ToProperty();
        }

        [Fact(DisplayName = "UnorderedReceiverState should prune its older senders correctly")]
        public void UnorderedReceiverState_should_prune_older_senders_correctly()
        {
            var timeProvider = new FakeTimeProvider(DateTime.UtcNow);
            var receiverState = new UnorderedReceiverState(timeProvider);
            receiverState.ConfirmProcessing(new ConfirmableMessageEnvelope(1000L, "foo", "bar"));
            timeProvider.SetTime(TimeSpan.FromSeconds(10));

            var prune = receiverState.Prune(TimeSpan.FromSeconds(5));
            prune.prunedSenders.Should().BeEquivalentTo("foo");
        }

        [Fact(DisplayName = "UnorderedReceiverState should prune senders who are more recent than the requested prune time")]
        public void UnorderedReceiverState_should_NOT_prune_newer_senders()
        {
            var timeProvider = new FakeTimeProvider(DateTime.UtcNow);
            var receiverState = new UnorderedReceiverState(timeProvider);
            receiverState.ConfirmProcessing(new ConfirmableMessageEnvelope(1000L, "foo", "bar"));
            timeProvider.SetTime(TimeSpan.FromSeconds(10));

            var prune = receiverState.Prune(TimeSpan.FromSeconds(15));
            prune.prunedSenders.Should().BeEmpty();
        }
    }

    public class AnyOrderReceiverStateSetup : Setup<IReceiverState, DeDuplicatingReceiverModelState>
    {
        public readonly DateTime StartTime = DateTime.UtcNow;

        public override IReceiverState Actual()
        {
            return new UnorderedReceiverState(new FakeTimeProvider(StartTime));
        }

        public override DeDuplicatingReceiverModelState Model()
        {
            return new DeDuplicatingReceiverModelState(ImmutableDictionary<string, DateTime>.Empty, ImmutableDictionary<string, ImmutableHashSet<long>>.Empty, StartTime);
        }
    }
}
