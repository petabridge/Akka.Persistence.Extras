// -----------------------------------------------------------------------
// <copyright file="DeDuplicatingReceiverStateModel.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Util.Internal;
using FsCheck;
using FsCheck.Experimental;

namespace Akka.Persistence.Extras.Tests.DeDuplication
{
    public class DeDuplicatingReceiverStateModel : Machine<IReceiverState, DeDuplicatingReceiverModelState>
    {
        public DeDuplicatingReceiverStateModel(Setup<IReceiverState, DeDuplicatingReceiverModelState> setup)
        {
            Setup = Arb.From(Gen.Fresh(() => setup));
        }

        public override Arbitrary<Setup<IReceiverState, DeDuplicatingReceiverModelState>> Setup { get; }

        public override Gen<Operation<IReceiverState, DeDuplicatingReceiverModelState>> Next(
            DeDuplicatingReceiverModelState obj0)
        {
            return Gen.OneOf(ReceiveNewMessage.Gen(), ReceiveDuplicateMessage.Gen(), AdvanceClock.Generator(),
                PruneOlderEntries.Generator());
        }

        #region StateOperations

        public class PruneOlderEntries : Operation<IReceiverState, DeDuplicatingReceiverModelState>
        {
            private readonly int _additionalSeconds;

            public PruneOlderEntries(int additionalSeconds)
            {
                _additionalSeconds = additionalSeconds;
            }

            /// <summary>
            ///     The set of entries pruned by the model.
            /// </summary>
            public IReadOnlyList<string> PrunedEntries { get; private set; }

            public static Gen<Operation<IReceiverState, DeDuplicatingReceiverModelState>> Generator()
            {
                return Gen.Choose(1, 1000).Select(x =>
                    (Operation<IReceiverState, DeDuplicatingReceiverModelState>) new PruneOlderEntries(x));
            }

            public override Property Check(IReceiverState actual, DeDuplicatingReceiverModelState model)
            {
                var prunedActual = actual.Prune(TimeSpan.FromSeconds(_additionalSeconds));

                var prunedItems = prunedActual.prunedSenders.ToImmutableHashSet();
                var missingEntriesFromActual = prunedItems.Except(PrunedEntries);
                var missingEntriesFromModel = PrunedEntries.Except(prunedItems);

                var goodPrune = prunedItems.SetEquals(PrunedEntries);

                return goodPrune.ToProperty()
                    .Label("Expected pruned items to be same in both actual and model, " +
                           $"but found inconsistencies with entries in actual [{string.Join(",", missingEntriesFromActual)}]" +
                           $"and entries in model [{string.Join(",", missingEntriesFromModel)}]");
            }

            public override DeDuplicatingReceiverModelState Run(DeDuplicatingReceiverModelState model)
            {
                var pruned = model.Prune(TimeSpan.FromSeconds(_additionalSeconds));
                PrunedEntries = pruned.prunedSenders;
                return (DeDuplicatingReceiverModelState) pruned.newState;
            }

            public override string ToString()
            {
                return $"{GetType()}(PruneTime={_additionalSeconds}s)";
            }
        }

        public class AdvanceClock : Operation<IReceiverState, DeDuplicatingReceiverModelState>
        {
            private readonly int _additionalSeconds;

            public AdvanceClock(int additionalSeconds)
            {
                _additionalSeconds = additionalSeconds;
            }

            public static Gen<Operation<IReceiverState, DeDuplicatingReceiverModelState>> Generator()
            {
                return Gen.Choose(1, 1000).Select(x =>
                    (Operation<IReceiverState, DeDuplicatingReceiverModelState>) new AdvanceClock(x));
            }

            public override Property Check(IReceiverState actual, DeDuplicatingReceiverModelState model)
            {
                if (actual is UnorderedReceiverState unordered)
                {
                    var provider = unordered._timeProvider.AsInstanceOf<FakeTimeProvider>();
                    provider.SetTime(TimeSpan.FromSeconds(_additionalSeconds));

                    return provider.Now.UtcDateTime.Equals(model.CurrentTime).ToProperty()
                        .Label("Timestamps should match");
                }

                return false.ToProperty().Label($"Tests do not currently support [{actual}]");
            }

            public override DeDuplicatingReceiverModelState Run(DeDuplicatingReceiverModelState model)
            {
                return model.AddTime(TimeSpan.FromSeconds(_additionalSeconds));
            }

            public override string ToString()
            {
                return $"{GetType()}(AdvanceTime={_additionalSeconds}s)";
            }
        }

        public class ReceiveDuplicateMessage : Operation<IReceiverState, DeDuplicatingReceiverModelState>
        {
            private readonly IConfirmableMessage _confirmable;

            public ReceiveDuplicateMessage(IConfirmableMessage confirmable)
            {
                _confirmable = confirmable;
            }

            public static Gen<Operation<IReceiverState, DeDuplicatingReceiverModelState>> Gen()
            {
                return ConfirmableGenerators.CreateConfirmableMessage().Generator
                    .Select(x =>
                        (Operation<IReceiverState, DeDuplicatingReceiverModelState>) new ReceiveDuplicateMessage(x));
            }

            public override bool Pre(DeDuplicatingReceiverModelState model)
            {
                return model.SenderIds.ContainsKey(_confirmable.SenderId) &&
                       model.SenderIds[_confirmable.SenderId].Contains(_confirmable.ConfirmationId);
            }

            public override Property Check(IReceiverState actual, DeDuplicatingReceiverModelState model)
            {
                var actualHasProcessedBefore = actual.AlreadyProcessed(_confirmable.ConfirmationId, _confirmable.SenderId).ToProperty()
                    .Label(
                        $"Should have processed message [{_confirmable.SenderId}-{_confirmable.ConfirmationId}] before");

                var lruTimesAreTheSame = actual.TrackedSenders[_confirmable.SenderId]
                    .Equals(model.TrackedSenders[_confirmable.SenderId])
                    .ToProperty()
                    .Label(
                        $"Actual should have same LRU time as model for sender [{_confirmable.SenderId}], but instead was " +
                        $"(Actual={actual.TrackedSenders[_confirmable.SenderId]}, Model={model.TrackedSenders[_confirmable.SenderId]}");

                return lruTimesAreTheSame.And(actualHasProcessedBefore);
            }

            public override DeDuplicatingReceiverModelState Run(DeDuplicatingReceiverModelState model)
            {
                return (DeDuplicatingReceiverModelState) model.ConfirmProcessing(_confirmable.ConfirmationId, _confirmable.SenderId);
            }

            public override string ToString()
            {
                return $"{GetType()}(SenderId={_confirmable.SenderId}, ConfirmationId={_confirmable.ConfirmationId})";
            }
        }

        public class ReceiveNewMessage : Operation<IReceiverState, DeDuplicatingReceiverModelState>
        {
            private readonly IConfirmableMessage _confirmable;

            public ReceiveNewMessage(IConfirmableMessage confirmable)
            {
                _confirmable = confirmable;
            }

            public static Gen<Operation<IReceiverState, DeDuplicatingReceiverModelState>> Gen()
            {
                return ConfirmableGenerators.CreateConfirmableMessage().Generator
                    .Select(x => (Operation<IReceiverState, DeDuplicatingReceiverModelState>) new ReceiveNewMessage(x));
            }

            public override bool Pre(DeDuplicatingReceiverModelState model)
            {
                var hasKey = model.SenderIds.ContainsKey(_confirmable.SenderId);
                var hasConfirmId =
                    hasKey && model.SenderIds[_confirmable.SenderId].Contains(_confirmable.ConfirmationId);
                return !(hasKey && hasConfirmId);
            }

            public override Property Check(IReceiverState actual, DeDuplicatingReceiverModelState model)
            {
                var actualHasProcessedBefore = (!actual.AlreadyProcessed(_confirmable.ConfirmationId, _confirmable.SenderId)).ToProperty()
                    .Label(
                        $"Should NOT have processed message [{_confirmable.SenderId}-{_confirmable.ConfirmationId}] before");
                actual.ConfirmProcessing(_confirmable.ConfirmationId, _confirmable.SenderId);
                var actualHasProcessedAfter = actual.AlreadyProcessed(_confirmable.ConfirmationId, _confirmable.SenderId).ToProperty()
                    .Label(
                        $"Should have processed message [{_confirmable.SenderId}-{_confirmable.ConfirmationId}] after");

                var lruTimesAreTheSame = actual.TrackedSenders[_confirmable.SenderId]
                    .Equals(model.TrackedSenders[_confirmable.SenderId])
                    .ToProperty()
                    .Label(
                        $"Actual should have same LRU time as model for sender [{_confirmable.SenderId}], but instead was " +
                        $"(Actual={actual.TrackedSenders[_confirmable.SenderId]}, Model={model.TrackedSenders[_confirmable.SenderId]}");

                return lruTimesAreTheSame.And(actualHasProcessedAfter).And(actualHasProcessedBefore);
            }

            public override DeDuplicatingReceiverModelState Run(DeDuplicatingReceiverModelState model)
            {
                return (DeDuplicatingReceiverModelState) model.ConfirmProcessing(_confirmable.ConfirmationId, _confirmable.SenderId);
            }

            public override string ToString()
            {
                return $"{GetType()}(SenderId={_confirmable.SenderId}, ConfirmationId={_confirmable.ConfirmationId})";
            }
        }

        #endregion
    }
}