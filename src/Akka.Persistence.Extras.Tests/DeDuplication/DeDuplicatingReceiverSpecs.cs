using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using FsCheck;
using FsCheck.Experimental;
using Xunit;

namespace Akka.Persistence.Extras.Tests.DeDuplication
{
    public class DeDuplicatingReceiverSpecs
    {
        [Fact]
        public void TestMethod1()
        {
        }
    }

    public class DeDuplicatingReceiverState<TReceiverState> : Machine<TReceiverState, DeDuplicatingReceiverModelState>
        where TReceiverState : IReceiverState
    {
        public override Gen<Operation<TReceiverState, DeDuplicatingReceiverModelState>> Next(DeDuplicatingReceiverModelState obj0)
        {
            throw new NotImplementedException();
        }

        public override Arbitrary<Setup<TReceiverState, DeDuplicatingReceiverModelState>> Setup { get; }
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
