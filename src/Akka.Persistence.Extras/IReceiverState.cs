using System;
using System.Collections.Generic;
using Petabridge.Collections;

namespace Akka.Persistence.Extras
{
    /// <summary>
    /// The order in which any single sender can deliver messages
    /// to this receiver.
    /// </summary>
    /// <remarks>
    /// The default behavior of all <see cref="AtLeastOnceDeliveryActor"/>s is <see cref="AnyOrder"/>.
    /// </remarks>
    public enum ReceiveOrdering
    {
        /// <summary>
        /// Messages can be received in any order by a sender - therefore correlation IDs
        /// can't be trusted to arrive in a particular order.
        /// </summary>
        AnyOrder,

        /// <summary>
        /// Strict ordering. Messages are sent and confirmed one at a time. Correlation IDs
        /// always increase monotonically.
        /// </summary>
        StrictOrdering,
    }

    /// <summary>
    /// Interface for data structures used for tracking delivery state.
    /// </summary>
    public interface IReceiverState
    {
        /// <summary>
        /// The ordering expected by the <see cref="DeDuplicatingReceiveActor"/>.
        /// </summary>
        ReceiveOrdering Ordering { get; }

        /// <summary>
        /// Confirm that we've completed processing of a message from a specific sender.
        /// </summary>
        /// <param name="message">The message being processed.</param>
        /// <returns>A new copy of the <see cref="IReceiverState"/> or possibly the same. Varies by implementation.</returns>
        IReceiverState ConfirmProcessing(IConfirmableMessage message);

        /// <summary>
        /// Check to see if this message has already been processed or not.
        /// </summary>
        /// <param name="message">The message we might process.</param>
        /// <returns><c>true</c> if the message has been processed before. <c>false</c> otherwise.</returns>
        bool AlreadyProcessed(IConfirmableMessage message);

        /// <summary>
        /// Returns the set of current senders by their IDs and the last time we processed a message sent from them.
        /// </summary>
        IReadOnlyDictionary<string, DateTime> TrackedSenders { get; }

        /// <summary>
        /// Used to help reduce memory pressure on systems that have a large number of senders.
        ///
        /// Prune any sender records that have not been updated in a LONG time.
        /// </summary>
        /// <param name="notUsedSince">The elapsed time since a sender was last used.</param>
        /// <returns>An updated state and the list of senders who were pruned during this operation.</returns>
        /// <remarks>
        /// You can inadvertently break the de-duplication mechanism used be the <see cref="DeDuplicatingReceiveActor"/>
        /// class whenever you purge the receiver state. The bet you're making is that because messages
        /// received from these senders are so infrequent, the possibility of receiving another message you've
        /// already confirmed from them is effectively zero, therefore we're better off freeing up the memory
        /// used to track them for other senders who might be doing work.
        /// </remarks>
        (IReceiverState newState, IReadOnlyList<string> prunedSenders) Prune(TimeSpan notUsedSince);
    }

    /// <summary>
    /// <see cref="IReceiverState"/> for <see cref="DeDuplicatingReceiveActor"/>s that are
    /// processing messages with <see cref="ReceiveOrdering.AnyOrder"/>
    /// </summary>
    /// <remarks>
    /// The implication of <see cref="ReceiveOrdering.AnyOrder"/> is that we can't rely on the
    /// sequence numbers for any individual sender being monotonic, therefore we have to store
    /// a finite-length array of them and check to see if the <see cref="IConfirmableMessage.ConfirmationId"/>
    /// has already been handled by this actor.
    /// </remarks>
    public sealed class UnorderedReceiverState : IReceiverState
    {
        /// <summary>
        /// Tracks the sequence numbers
        /// </summary>
        private readonly Dictionary<string, ICircularBuffer<long>> _trackedIds;

        /// <summary>
        /// Tracks the last recently updated LRU time for each sender.
        /// </summary>
        private readonly Dictionary<string, DateTime> _trackedLru;

        public UnorderedReceiverState(int maxConfirmationsPerSender)
        {
            MaxConfirmationsPerSender = maxConfirmationsPerSender;
        }

        public int MaxConfirmationsPerSender { get; }

        public ReceiveOrdering Ordering => ReceiveOrdering.AnyOrder;
        public IReceiverState ConfirmProcessing(IConfirmableMessage message)
        {
            throw new NotImplementedException();
        }

        public bool AlreadyProcessed(IConfirmableMessage message)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyDictionary<string, DateTime> TrackedSenders { get; }
        public (IReceiverState newState, IReadOnlyList<string> prunedSenders) Prune(TimeSpan notUsedSince)
        {
            throw new NotImplementedException();
        }
    }
}