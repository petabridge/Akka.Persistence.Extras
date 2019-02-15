// -----------------------------------------------------------------------
// <copyright file="DeDuplicatingReceiverStateSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
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

        [Fact(DisplayName = "UnorderedReceiverState should load and unload its state via Snapshot.")]
        public void UnorderedReceiverState_should_load_and_recover_from_snapshot()
        {
            var confirmable1 = new ConfirmableMessageEnvelope(1L, "foo", "bar");
            var confirmable2 = new ConfirmableMessageEnvelope(2L, "foo", "bar");
            var confirmable3 = new ConfirmableMessageEnvelope(1L, "fuber", "bar");
            var timeProvider = new FakeTimeProvider(DateTime.UtcNow);
            var receiverState = new UnorderedReceiverState(timeProvider);

            // update our initial state
            receiverState.ConfirmProcessing(confirmable1.ConfirmationId, confirmable1.SenderId);
            receiverState.ConfirmProcessing(confirmable2.ConfirmationId, confirmable2.SenderId);
            receiverState.ConfirmProcessing(confirmable3.ConfirmationId, confirmable3.SenderId);

            // save into a snapshot
            var snapshot = receiverState.ToSnapshot();
            snapshot.TrackedIds.Count.Should().Be(2);
            snapshot.TrackedIds[confirmable1.SenderId].Count.Should().Be(2); // two for "foo"
            snapshot.TrackedIds[confirmable3.SenderId].Count.Should().Be(1); // one for "fuber"
            snapshot.TrackedIds[confirmable1.SenderId].Any(x => x.Equals(confirmable1.ConfirmationId)).Should()
                .BeTrue();
            snapshot.TrackedSenders.Count.Should().Be(2);

            // reload snapshot into new state
            var receiverState2 = new UnorderedReceiverState(timeProvider);
            receiverState2.FromSnapshot(snapshot);

            // validate that we've already processed all of the same things as the previous state
            receiverState2.TrackedSenders.Should().BeEquivalentTo(receiverState.TrackedSenders);
            receiverState.AlreadyProcessed(confirmable1.ConfirmationId, confirmable1.SenderId).Should().BeTrue();
            receiverState.AlreadyProcessed(confirmable2.ConfirmationId, confirmable2.SenderId).Should().BeTrue();
            receiverState.AlreadyProcessed(confirmable3.ConfirmationId, confirmable3.SenderId).Should().BeTrue();
        }

        [Fact(DisplayName =
            "UnorderedReceiverState should prune senders who are more recent than the requested prune time")]
        public void UnorderedReceiverState_should_NOT_prune_newer_senders()
        {
            var confirmable = new ConfirmableMessageEnvelope(1L, "foo", "bar");
            var timeProvider = new FakeTimeProvider(DateTime.UtcNow);
            var receiverState = new UnorderedReceiverState(timeProvider);
            receiverState.ConfirmProcessing(confirmable.ConfirmationId, confirmable.SenderId);
            timeProvider.SetTime(TimeSpan.FromSeconds(10));

            var prune = receiverState.Prune(TimeSpan.FromSeconds(15));
            prune.prunedSenders.Should().BeEmpty();
        }

        [Fact(DisplayName = "UnorderedReceiverState should prune its older senders correctly")]
        public void UnorderedReceiverState_should_prune_older_senders_correctly()
        {
            var confirmable = new ConfirmableMessageEnvelope(1L, "foo", "bar");
            var timeProvider = new FakeTimeProvider(DateTime.UtcNow);
            var receiverState = new UnorderedReceiverState(timeProvider);
            receiverState.ConfirmProcessing(confirmable.ConfirmationId, confirmable.SenderId);
            timeProvider.SetTime(TimeSpan.FromSeconds(10));

            var prune = receiverState.Prune(TimeSpan.FromSeconds(5));
            prune.prunedSenders.Should().BeEquivalentTo("foo");
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
            return new DeDuplicatingReceiverModelState(ImmutableDictionary<string, DateTime>.Empty,
                ImmutableDictionary<string, ImmutableHashSet<long>>.Empty, StartTime);
        }
    }
}