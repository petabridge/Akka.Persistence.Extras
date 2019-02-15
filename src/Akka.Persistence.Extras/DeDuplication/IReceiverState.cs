// -----------------------------------------------------------------------
// <copyright file="IReceiverState.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Petabridge.Collections;

namespace Akka.Persistence.Extras
{
    /// <summary>
    ///     The order in which any single sender can deliver messages
    ///     to this receiver.
    /// </summary>
    /// <remarks>
    ///     The default behavior of all <see cref="AtLeastOnceDeliveryActor" />s is <see cref="AnyOrder" />.
    /// </remarks>
    public enum ReceiveOrdering
    {
        /// <summary>
        ///     Messages can be received in any order by a sender - therefore correlation IDs
        ///     can't be trusted to arrive in a particular order.
        /// </summary>
        AnyOrder,

        /// <summary>
        ///     Strict ordering. Messages are sent and confirmed one at a time. Correlation IDs
        ///     always increase monotonically.
        /// </summary>
        StrictOrder
    }

    /// <summary>
    /// Used to work around issues with value tuple syntax in older versions of the compiler.
    /// </summary>
    public struct PrunedResult
    {
        public PrunedResult(IReceiverState newState, IReadOnlyList<string> prunedSenders)
        {
            this.newState = newState;
            this.prunedSenders = prunedSenders;
        }

        public IReceiverState newState { get; }

        public IReadOnlyList<string> prunedSenders { get; }
    }

    /// <summary>
    ///     Interface for data structures used for tracking delivery state.
    /// </summary>
    public interface IReceiverState
    {
        /// <summary>
        ///     The ordering expected by the <see cref="DeDuplicatingReceiveActor" />.
        /// </summary>
        ReceiveOrdering Ordering { get; }

        /// <summary>
        ///     Returns the set of current senders by their IDs and the last time we processed a message sent from them.
        /// </summary>
        IReadOnlyDictionary<string, DateTime> TrackedSenders { get; }

        /// <summary>
        ///     Confirm that we've completed processing of a message from a specific sender.
        /// </summary>
        /// <param name="confirmationId">The correlation id for this specific message.</param>
        /// <param name="senderId">The identity of the sender.</param>
        /// <returns>A new copy of the <see cref="IReceiverState" /> or possibly the same. Varies by implementation.</returns>
        IReceiverState ConfirmProcessing(long confirmationId, string senderId);

        /// <summary>
        ///     Check to see if this message has already been processed or not.
        /// </summary>
        /// <param name="confirmationId">The correlation id for this specific message.</param>
        /// <param name="senderId">The identity of the sender.</param>
        /// <returns><c>true</c> if the message has been processed before. <c>false</c> otherwise.</returns>
        bool AlreadyProcessed(long confirmationId, string senderId);

        /// <summary>
        ///     Used to help reduce memory pressure on systems that have a large number of senders.
        ///     Prune any sender records that have not been updated in a LONG time.
        /// </summary>
        /// <param name="notUsedSince">The elapsed time since a sender was last used.</param>
        /// <returns>An updated state and the list of senders who were pruned during this operation.</returns>
        /// <remarks>
        ///     You can inadvertently break the de-duplication mechanism used be the <see cref="DeDuplicatingReceiveActor" />
        ///     class whenever you purge the receiver state. The bet you're making is that because messages
        ///     received from these senders are so infrequent, the possibility of receiving another message you've
        ///     already confirmed from them is effectively zero, therefore we're better off freeing up the memory
        ///     used to track them for other senders who might be doing work.
        /// </remarks>
        PrunedResult Prune(TimeSpan notUsedSince);

        /// <summary>
        ///     Convert this object into a <see cref="IReceiverStateSnapshot" />
        ///     so it can be serialized.
        /// </summary>
        /// <returns>A new snapshot object.</returns>
        IReceiverStateSnapshot ToSnapshot();

        /// <summary>
        ///     Re-populate this object from a <see cref="IReceiverStateSnapshot" />.
        /// </summary>
        /// <param name="snapshot">The snapshot recovered from Akka.Persistence.</param>
        /// <returns>
        ///     A new or possibly the same <see cref="IReceiverState" /> instance, with its data
        ///     modified by the <see cref="IReceiverStateSnapshot" />.
        /// </returns>
        IReceiverState FromSnapshot(IReceiverStateSnapshot snapshot);
    }

    /// <inheritdoc />
    /// <summary>
    ///     <see cref="T:Akka.Persistence.Extras.IReceiverState" /> for
    ///     <see cref="T:Akka.Persistence.Extras.DeDuplicatingReceiveActor" />s that are
    ///     processing messages with <see cref="F:Akka.Persistence.Extras.ReceiveOrdering.AnyOrder" />
    /// </summary>
    /// <remarks>
    ///     The implication of <see cref="F:Akka.Persistence.Extras.ReceiveOrdering.AnyOrder" /> is that we can't rely on the
    ///     sequence numbers for any individual sender being monotonic, therefore we have to store
    ///     a finite-length array of them and check to see if the
    ///     <see cref="P:Akka.Persistence.Extras.IConfirmableMessage.ConfirmationId" />
    ///     has already been handled by this actor.
    /// </remarks>
    public sealed class UnorderedReceiverState : IReceiverState
    {
        /// <summary>
        ///     Determines the size of the circular buffer we're going to use to store the out-of-order confirmations
        /// </summary>
        public const int DefaultMaxConfirmationsPerSender = 1000;

        internal readonly ITimeProvider _timeProvider;

        /// <summary>
        ///     Tracks the sequence numbers
        /// </summary>
        private readonly Dictionary<string, ICircularBuffer<long>> _trackedIds =
            new Dictionary<string, ICircularBuffer<long>>();

        /// <summary>
        ///     Tracks the last recently updated LRU time for each sender.
        /// </summary>
        private readonly Dictionary<string, DateTime> _trackedLru = new Dictionary<string, DateTime>();

        public UnorderedReceiverState() : this(DateTimeOffsetNowTimeProvider.Instance)
        {
        }

        public UnorderedReceiverState(ITimeProvider timeProvider,
            int maxConfirmationsPerSender = DefaultMaxConfirmationsPerSender)
        {
            MaxConfirmationsPerSender = maxConfirmationsPerSender;
            _timeProvider = timeProvider;
        }

        /// <summary>
        ///     Determines the size of the circular buffer we're going to use to store the out-of-order confirmations
        /// </summary>
        public int MaxConfirmationsPerSender { get; }

        public ReceiveOrdering Ordering => ReceiveOrdering.AnyOrder;

        public IReceiverState ConfirmProcessing(long confirmationId, string senderId)
        {
            UpdateLru(senderId);

            // in the event that this is the first time we've seen this SenderId
            if (!_trackedIds.ContainsKey(senderId))
                _trackedIds[senderId] = new CircularBuffer<long>(MaxConfirmationsPerSender);

            // track the message id
            _trackedIds[senderId].Enqueue(confirmationId);

            return this;
        }

        public bool AlreadyProcessed(long confirmationId, string senderId)
        {
            UpdateLru(senderId);

            // TODO: performance optimize lookups in CircularBuffer
            return _trackedIds.ContainsKey(senderId)
                   && _trackedIds[senderId].Contains(confirmationId);
        }

        public IReadOnlyDictionary<string, DateTime> TrackedSenders => _trackedLru.ToImmutableDictionary();

        public PrunedResult Prune(TimeSpan notUsedSince)
        {
            var pruneTime = _timeProvider.Now.UtcDateTime - notUsedSince;

            // Get the set of IDs
            var senderIds = new List<string>();
            foreach (var senderId in _trackedLru.Where(x => x.Value <= pruneTime).Select(x => x.Key).ToList())
            {
                senderIds.Add(senderId);
                _trackedIds.Remove(senderId);
                _trackedLru.Remove(senderId);
            }

            return new PrunedResult(this, senderIds);
        }

        public IReceiverStateSnapshot ToSnapshot()
        {
            return new ReceiverStateSnapshot(
                _trackedIds.ToDictionary(x => x.Key, v => (IReadOnlyList<long>) v.Value.ToList()), TrackedSenders);
        }

        public IReceiverState FromSnapshot(IReceiverStateSnapshot snapshot)
        {
            foreach (var item in snapshot.TrackedIds)
            {
                _trackedIds[item.Key] = new CircularBuffer<long>(MaxConfirmationsPerSender);
                foreach (var id in item.Value)
                    _trackedIds[item.Key].Enqueue(id);
            }

            foreach (var item in snapshot.TrackedSenders) _trackedLru[item.Key] = item.Value;

            return this;
        }

        private void UpdateLru(string senderId)
        {
            _trackedLru[senderId] = _timeProvider.Now.UtcDateTime;
        }
    }
}