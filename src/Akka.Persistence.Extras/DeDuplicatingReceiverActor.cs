// -----------------------------------------------------------------------
// <copyright file="DeDuplicatingReceiverActor.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Extras
{
    /// <summary>
    /// The settings used to configure how the <see cref="DeDuplicatingReceiveActor"/> will
    /// process duplicates of messages and how it will configure its state.
    /// </summary>
    public sealed class DeDuplicatingReceiverSettings
    {
        public const int DefaultBufferSizePerSender = 1000;

        public static readonly TimeSpan DefaultPruneInterval = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Creates a default <see cref="DeDuplicatingReceiverSettings"/> with the following values:
        ///
        /// <see cref="ReceiverType"/> = <see cref="ReceiveOrdering.AnyOrder"/>
        /// <see cref="PruneInterval"/> = 30m
        /// <see cref="BufferSizePerSender"/> = 1000
        /// </summary>
        public DeDuplicatingReceiverSettings() 
            : this(ReceiveOrdering.AnyOrder, DefaultPruneInterval, DefaultBufferSizePerSender) { }

        public DeDuplicatingReceiverSettings(ReceiveOrdering receiverType, TimeSpan pruneInterval, int bufferSizePerSender)
        {
            ReceiverType = receiverType;
            PruneInterval = pruneInterval;

            if (PruneInterval.Equals(TimeSpan.Zero) 
                || PruneInterval.Equals(TimeSpan.MaxValue)
                || PruneInterval.Equals(TimeSpan.MinValue))
            {
                throw new ArgumentOutOfRangeException(nameof(pruneInterval), $"{pruneInterval} is not an acceptable prune interval. " +
                                                                             $"Need to set a realistic value.");
            }

            BufferSizePerSender = bufferSizePerSender;

            if (BufferSizePerSender <= 1)
            {
                throw new ArgumentOutOfRangeException(nameof(bufferSizePerSender), $"{bufferSizePerSender} is not an acceptable buffer size. Please" +
                                                                                   $"pick a value greater than 1.");
            }
        }

        /// <summary>
        /// The order in which this receiver expects to receive messages.
        /// </summary>
        public ReceiveOrdering ReceiverType { get; }

        /// <summary>
        /// The rate at which "quiet" senders will be purged from our internal
        /// receiver state. For instance, if this setting is set to 30 minutes,
        /// if we haven't received any messages (including duplicates) from a receiver
        /// for more than 30 minutes we will automatically purge our state for that
        /// receiver in order to conserve memory.
        ///
        /// We assume that senders are extremely unlikely to resend an unconfirmed message
        /// after anything longer than this interval. If that is not the case in your
        /// application you will want to increase this value to something larger.
        /// </summary>
        public TimeSpan PruneInterval { get; }

        /// <summary>
        /// For each individual sender who sends us a message, we will store up to
        /// this many confirmationIds (<see cref="long"/> integers) in memory.
        /// </summary>
        /// <remarks>
        /// For <see cref="ReceiveOrdering.AnyOrder"/>, we use a circular buffer
        /// thus we are never permitted to allocate more than this amount of memory
        /// for any individual sender.
        ///
        /// For <see cref="ReceiveOrdering.StrictOrder"/>
        /// we assume that all confirmationIds increase monotonically (because they are sent
        /// and confirmed in a strict order) and thus we only preserve the most recently
        /// confirmed message ID.
        /// </remarks>
        public int BufferSizePerSender { get; }
    }

    public abstract class DeDuplicatingReceiveActor : ReceivePersistentActor
    {
    }
}