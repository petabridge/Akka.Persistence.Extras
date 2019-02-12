using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Experimental;
using FsCheck.Xunit;
using Xunit;

namespace Akka.Persistence.Extras.Tests.DeDuplication
{
    public class DeDuplicatingReceiverSpecs
    {
        [Property]
        public Property DeDuplicatingReceiverStateModel_should_hold_for_AnyOrderReceiverState()
        {
            var model = new DeDuplicatingReceiverStateModel(new AnyOrderReceiverStateSetup());
            return model.ToProperty();
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

    public class DeDuplicatingReceiverStateModel : Machine<IReceiverState, DeDuplicatingReceiverModelState>
    {

        public DeDuplicatingReceiverStateModel(Setup<IReceiverState, DeDuplicatingReceiverModelState> setup)
        {
            Setup = Arb.From(Gen.Fresh(() => setup));
        }

        public override Gen<Operation<IReceiverState, DeDuplicatingReceiverModelState>> Next(DeDuplicatingReceiverModelState obj0)
        {
            return Gen.OneOf(ReceiveNewMessage.Gen());
        }

        public override Arbitrary<Setup<IReceiverState, DeDuplicatingReceiverModelState>> Setup { get; }

        #region StateOperations

        public class ReceiveNewMessage : Operation<IReceiverState, DeDuplicatingReceiverModelState>
        {
            private readonly IConfirmableMessage _confirmable;

            public static Gen<Operation<IReceiverState, DeDuplicatingReceiverModelState>> Gen()
            {
                return ConfirmableGenerators.CreateConfirmableMessage().Generator
                    .Select(x => (Operation<IReceiverState, DeDuplicatingReceiverModelState>)new ReceiveNewMessage(x));
            }

            public ReceiveNewMessage(IConfirmableMessage confirmable)
            {
                _confirmable = confirmable;
            }

            public override bool Pre(DeDuplicatingReceiverModelState model)
            {
                return !model.AlreadyProcessed(_confirmable);
            }

            public override Property Check(IReceiverState actual, DeDuplicatingReceiverModelState model)
            {
                var actualHasProcessedBefore = (!actual.AlreadyProcessed(_confirmable)).ToProperty()
                    .Label($"Should NOT have processed message [{_confirmable.SenderId}-{_confirmable.ConfirmationId}] before");
                actual.ConfirmProcessing(_confirmable);
                var actualHasProcessedAfter = actual.AlreadyProcessed(_confirmable).ToProperty()
                    .Label($"Should have processed message [{_confirmable.SenderId}-{_confirmable.ConfirmationId}] after");

                var lruTimesAreTheSame = (actual.TrackedSenders[_confirmable.SenderId]
                        .Equals(model.TrackedSenders[_confirmable.SenderId]))
                    .ToProperty()
                    .Label(
                        $"Actual should have same LRU time as model for sender [{_confirmable.SenderId}], but instead was " +
                        $"(Actual={actual.TrackedSenders[_confirmable.SenderId]}, Model={model.TrackedSenders[_confirmable.SenderId]}");

                return lruTimesAreTheSame.
                    And(actualHasProcessedAfter).
                    And(actualHasProcessedBefore);
            }

            public override DeDuplicatingReceiverModelState Run(DeDuplicatingReceiverModelState model)
            {
                return (DeDuplicatingReceiverModelState) model.ConfirmProcessing(_confirmable);
            }

            public override string ToString()
            {
                return $"{GetType()}(SenderId={_confirmable.SenderId}, ConfirmationId={_confirmable.ConfirmationId})";
            }
        }

        #endregion
    }

    public class DeDuplicatingReceiverModelState : IReceiverState
    {
        public DeDuplicatingReceiverModelState(ImmutableDictionary<string, DateTime> senderLru, ImmutableDictionary<string, ImmutableHashSet<long>> senderIds, DateTime currentTime)
        {
            SenderLru = senderLru;
            SenderIds = senderIds;
            CurrentTime = currentTime;
        }

        public DateTime CurrentTime { get; }

        public ImmutableDictionary<string, DateTime> SenderLru { get; private set; }

        public ImmutableDictionary<string, ImmutableHashSet<long>> SenderIds { get; private set; }
        public ReceiveOrdering Ordering => ReceiveOrdering.AnyOrder;
        public IReceiverState ConfirmProcessing(IConfirmableMessage message)
        {
            UpdateLru(message.SenderId);
            var buffer = SenderIds.ContainsKey(message.SenderId)
                ? SenderIds[message.SenderId]
                : ImmutableHashSet<long>.Empty;

            return new DeDuplicatingReceiverModelState(SenderLru, 
                SenderIds.SetItem(message.SenderId, buffer.Add(message.ConfirmationId)), 
                CurrentTime);
        }

        public bool AlreadyProcessed(IConfirmableMessage message)
        {
            UpdateLru(message.SenderId);
            return SenderIds.ContainsKey(message.SenderId) &&
                   SenderIds[message.SenderId].Contains(message.ConfirmationId);
        }

        public IReadOnlyDictionary<string, DateTime> TrackedSenders => SenderLru;
        public (IReceiverState newState, IReadOnlyList<string> prunedSenders) Prune(TimeSpan notUsedSince)
        {
            var targetTime = CurrentTime + notUsedSince;
            var prunedSenderIds = SenderLru.Where(x => x.Value < targetTime).Select(x => x.Key).ToList();
            return (
                new DeDuplicatingReceiverModelState(SenderLru.RemoveRange(prunedSenderIds),
                    SenderIds.RemoveRange(prunedSenderIds), CurrentTime), prunedSenderIds);
        }

        public IReceiverState AddTime(TimeSpan additionalTime)
        {
            return new DeDuplicatingReceiverModelState(SenderLru, SenderIds, CurrentTime + additionalTime);
        }

        private void UpdateLru(string senderId)
        {
            SenderLru = SenderLru.SetItem(senderId, CurrentTime);
        }

        public override string ToString()
        {
            return
                $"DeDuplicatingReceiverModel(CurrentTime={CurrentTime}, SenderLru=[{string.Join(",", SenderLru.Select(x => x.Key + ":" + x.Value))}]," +
                $"SenderRecvCounts=[{string.Join(",", SenderIds.Select(x => x.Key + "->" + x.Value.Count))}]";
        }
    }
}
