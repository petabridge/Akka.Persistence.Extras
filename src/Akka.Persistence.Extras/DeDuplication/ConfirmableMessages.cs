// -----------------------------------------------------------------------
// <copyright file="ConfirmableMessages.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Extras
{
    /// <summary>
    ///     Used to decorate messages with the ability to be confirmed
    ///     without duplicates via a <see cref="DeDuplicatingReceiveActor" />.
    /// </summary>
    public interface IConfirmableMessage
    {
        /// <summary>
        ///     The confirmation ID assigned by any type of <see cref="AtLeastOnceDeliveryActor" />.
        /// </summary>
        /// <remarks>
        ///     Must be monotonic per-sender: this means that if your <see cref="AtLeastOnceDeliveryActor" />
        ///     doesn't persist it's delivery state, you're going to have a "bad time"
        ///     when using the <see cref="DeDuplicatingReceiveActor" />.
        /// </remarks>
        long ConfirmationId { get; }

        /// <summary>
        ///     The globally unique (cluster-wide) ID of the sender. Usually this is the
        ///     <see cref="PersistentActor.PersistenceId" />
        /// </summary>
        string SenderId { get; }
    }

    /// <summary>
    ///     A built-in envelope for making user-defined messages <see cref="IConfirmableMessage" />
    ///     without changing the types of the messages themselves.
    /// </summary>
    public sealed class ConfirmableMessageEnvelope : IConfirmableMessage
    {
        public ConfirmableMessageEnvelope(long confirmationId, string senderId, object message)
        {
            ConfirmationId = confirmationId;
            SenderId = senderId;
            Message = message;
        }

        /// <summary>
        ///     The user-defined message.
        /// </summary>
        public object Message { get; }

        /// <inheritdoc />
        public long ConfirmationId { get; }

        /// <inheritdoc />
        public string SenderId { get; }
    }

    /// <summary>
    ///     Used to persist the handling of a <see cref="IConfirmableMessage" />
    /// </summary>
    public sealed class Confirmation
    {
        public Confirmation(long confirmationId, string senderId)
        {
            ConfirmationId = confirmationId;
            SenderId = senderId;
        }

        public long ConfirmationId { get; }

        public string SenderId { get; }
    }
}