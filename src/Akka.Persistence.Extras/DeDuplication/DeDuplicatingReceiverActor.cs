// -----------------------------------------------------------------------
// <copyright file="DeDuplicatingReceiverActor.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using Akka.Actor;
using Akka.Util.Internal;

namespace Akka.Persistence.Extras
{
    /// <summary>
    ///     The settings used to configure how the <see cref="DeDuplicatingReceiveActor" /> will
    ///     process duplicates of messages and how it will configure its state.
    /// </summary>
    public sealed class DeDuplicatingReceiverSettings
    {
        public const int DefaultBufferSizePerSender = 1000;

        public const int DefaultSnapshotPerNMessages = 100;

        public static readonly TimeSpan DefaultPruneInterval = TimeSpan.FromMinutes(30);

        /// <summary>
        ///     Creates a default <see cref="DeDuplicatingReceiverSettings" /> with the following values:
        ///     <see cref="ReceiverType" /> = <see cref="ReceiveOrdering.AnyOrder" />
        ///     <see cref="PruneInterval" /> = 30m
        ///     <see cref="BufferSizePerSender" /> = 1000
        /// </summary>
        public DeDuplicatingReceiverSettings()
            : this(ReceiveOrdering.AnyOrder, DefaultPruneInterval, DefaultBufferSizePerSender,
                DefaultSnapshotPerNMessages)
        {
        }

        public DeDuplicatingReceiverSettings(ReceiveOrdering receiverType, TimeSpan pruneInterval,
            int bufferSizePerSender, int takeSnapshotEveryNMessages, ITimeProvider timeProvider = null)
        {
            ReceiverType = receiverType;
            PruneInterval = pruneInterval;

            if (PruneInterval.Equals(TimeSpan.Zero)
                || PruneInterval.Equals(TimeSpan.MaxValue)
                || PruneInterval.Equals(TimeSpan.MinValue))
                throw new ArgumentOutOfRangeException(nameof(pruneInterval),
                    $"{pruneInterval} is not an acceptable prune interval. " +
                    "Need to set a realistic value.");

            BufferSizePerSender = bufferSizePerSender;
            TakeSnapshotEveryNMessages = takeSnapshotEveryNMessages;

            if (BufferSizePerSender <= 1)
                throw new ArgumentOutOfRangeException(nameof(bufferSizePerSender),
                    $"{bufferSizePerSender} is not an acceptable buffer size. Please" +
                    "pick a value greater than 1.");

            if (TakeSnapshotEveryNMessages <= 1)
                throw new ArgumentOutOfRangeException(nameof(takeSnapshotEveryNMessages),
                    $"{takeSnapshotEveryNMessages} is not an acceptable value for snapshot intervals. " +
                    "Please set a value greater than 1.");

            TimeProvider = timeProvider ?? DateTimeOffsetNowTimeProvider.Instance;
        }

        /// <summary>
        ///     The order in which this receiver expects to receive messages.
        /// </summary>
        public ReceiveOrdering ReceiverType { get; }

        /// <summary>
        ///     The rate at which "quiet" senders will be purged from our internal
        ///     receiver state. For instance, if this setting is set to 30 minutes,
        ///     if we haven't received any messages (including duplicates) from a receiver
        ///     for more than 30 minutes we will automatically purge our state for that
        ///     receiver in order to conserve memory.
        ///     We assume that senders are extremely unlikely to resend an unconfirmed message
        ///     after anything longer than this interval. If that is not the case in your
        ///     application you will want to increase this value to something larger.
        /// </summary>
        public TimeSpan PruneInterval { get; }

        /// <summary>
        ///     For each individual sender who sends us a message, we will store up to
        ///     this many confirmationIds (<see cref="long" /> integers) in memory.
        /// </summary>
        /// <remarks>
        ///     For <see cref="ReceiveOrdering.AnyOrder" />, we use a circular buffer
        ///     thus we are never permitted to allocate more than this amount of memory
        ///     for any individual sender.
        ///     For <see cref="ReceiveOrdering.StrictOrder" />
        ///     we assume that all confirmationIds increase monotonically (because they are sent
        ///     and confirmed in a strict order) and thus we only preserve the most recently
        ///     confirmed message ID.
        /// </remarks>
        public int BufferSizePerSender { get; }

        /// <summary>
        ///     Take a new snapshot of our <see cref="IReceiverState" /> every N messages.
        ///     This is designed to help cap recovery times for this actor in the event of a restart.
        /// </summary>
        public int TakeSnapshotEveryNMessages { get; }

        /// <summary>
        ///     INTERNAL API.
        ///     Used to configure the <see cref="IReceiverState" /> time provider
        ///     for testing purposes.
        /// </summary>
        /// <remarks>
        ///     Defaults to <see cref="DateTimeOffsetNowTimeProvider" />, which is the safe default,
        ///     if this value is not set in the constructor.
        /// </remarks>
        public ITimeProvider TimeProvider { get; }
    }

    public abstract class DeDuplicatingReceiveActor : ReceivePersistentActor
    {
        private ICancelable _pruneTask;

        private IReceiverState _receiverState;

        protected DeDuplicatingReceiveActor() : this(new DeDuplicatingReceiverSettings()) { }

        protected DeDuplicatingReceiveActor(DeDuplicatingReceiverSettings settings)
        {
            Settings = settings;
            _receiverState = CreateInitialState(settings);
            _pruneTask = CreatePruneTask();

            BuiltInRecovers();
            BuiltInCommands();
        }

        /// <summary>
        ///     Is the message we are currently processing <see cref="IConfirmableMessage" />?
        /// </summary>
        public bool IsCurrentMessageConfirmable { get; private set; }

        /// <summary>
        ///     The current id of the most recent <see cref="IConfirmableMessage" />.
        ///     This value is <c>null</c> if <see cref="IsCurrentMessageConfirmable" /> is <c>false</c>.
        /// </summary>
        public long? CurrentConfirmationId { get; private set; }

        /// <summary>
        ///     The current sender id of the most recent <see cref="IConfirmableMessage" />.
        ///     This value is <c>null</c> if <see cref="IsCurrentMessageConfirmable" /> is <c>false</c>.
        /// </summary>
        public string CurrentSenderId { get; private set; }

        /// <summary>
        ///     The settings for this actor.
        /// </summary>
        public DeDuplicatingReceiverSettings Settings { get; }

        /// <inheritdoc />
        /// <summary>
        ///     Cancels the pruning task on the scheduler.
        /// </summary>
        protected override void PostStop()
        {
            _pruneTask?.Cancel();
            base.PostStop();
        }


        /// <summary>
        ///     INTERNAL API.
        ///     All of the built-in recovery methods needed to re-create this actor's state.
        /// </summary>
        private void BuiltInRecovers()
        {
            // Confirming a single message from a single sender
            Recover<Confirmation>(c => { _receiverState.ConfirmProcessing(c.ConfirmationId, c.SenderId); });
            Recover<SnapshotOffer>(snapshotOffer =>
            {
                if (snapshotOffer.Snapshot is IReceiverStateSnapshot receiverStateSnapshot)
                    _receiverState = _receiverState.FromSnapshot(receiverStateSnapshot);
                else
                    Log.Error("{0} should not be used to persist user-" +
                              "defined state under any circumstances. " +
                              "Tried to recover snapshot of type [{1}]. Read the documentation.", GetType(),
                        snapshotOffer.Snapshot.GetType());
            });
        }

        private void BuiltInCommands()
        {
            Command<SaveSnapshotSuccess>(snapshot =>
            {
                if (Log.IsDebugEnabled)
                    Log.Debug(
                        "Successfully saved snapshot with SeqNo {0} - purging older entries from journal and snapshotstore",
                        snapshot.Metadata.SequenceNr);

                DeleteMessages(snapshot.Metadata.SequenceNr);
                DeleteSnapshots(new SnapshotSelectionCriteria(snapshot.Metadata.SequenceNr - 1));
            });

            Command<SaveSnapshotFailure>(failure =>
            {
                Log.Error(failure.Cause, "Failed to save snapshot {0} - " +
                                         "refraining from deleting any messages from journal.",
                    failure.Metadata.SequenceNr);
            });

            Command<PruneSendersTick>(prune =>
            {
                var pruneResult = _receiverState.Prune(Settings.PruneInterval);
                _receiverState = pruneResult.newState;

                if (Log.IsDebugEnabled && pruneResult.prunedSenders.Count > 0)
                    Log.Debug("Pruned senders [{0}] from ReceiverState. Have not been active for [{1}]",
                        string.Join(",", pruneResult.prunedSenders), Settings.PruneInterval);
            });
        }

        protected override bool AroundReceive(Receive receive, object message)
        {
            ClearConfirmationData();

            var realMsg = message;
            /*
             * This is where duplicate detection and handling must be performed.
             */
            if (message is IConfirmableMessage confirmable)
            {
                IsCurrentMessageConfirmable = true;
                CurrentConfirmationId = confirmable.ConfirmationId;
                CurrentSenderId = confirmable.SenderId;

                if (confirmable is ConfirmableMessageEnvelope envelope) realMsg = envelope.Message;

                // automatic de-duplication
                if (IsDuplicate())
                {
                    HandleDuplicate(confirmable.ConfirmationId, confirmable.SenderId, realMsg);
                    return true;
                }
            }

            // let the end-user code handle it
            return base.AroundReceive(Receive, realMsg);
        }

        private void ClearConfirmationData()
        {
            IsCurrentMessageConfirmable = false;
            CurrentConfirmationId = null;
            CurrentSenderId = null;
        }

        protected bool IsDuplicate()
        {
            if (!IsCurrentMessageConfirmable)
                return false;

            Debug.Assert(CurrentConfirmationId != null, nameof(CurrentConfirmationId) + " != null");
            return _receiverState.AlreadyProcessed(CurrentConfirmationId.Value, CurrentSenderId);
        }

        protected void ConfirmDelivery()
        {
            if (!IsCurrentMessageConfirmable)
            {
                Log.Warning("Attempted to confirm non-confirmable message {0}",
                    Context.AsInstanceOf<ActorCell>().CurrentMessage);
                return;
            }

            Debug.Assert(CurrentConfirmationId != null, nameof(CurrentConfirmationId) + " != null");
            _receiverState = _receiverState.ConfirmProcessing(CurrentConfirmationId.Value, CurrentSenderId);


            // Persist the current confirmation state
            Persist(new Confirmation(CurrentConfirmationId.Value, CurrentSenderId), confirmation =>
            {
                if (LastSequenceNr % Settings.TakeSnapshotEveryNMessages == 0)
                    SaveSnapshot(_receiverState.ToSnapshot());
            });
        }

        /// <summary>
        ///     This method gets invoked when <see cref="IsDuplicate" /> has already returned <c>true</c>
        ///     for the current message. Can be overriden by end-users.
        ///     By default it automatically sends the message produced by <see cref="CreateConfirmationReplyMessage" />\
        ///     to the current <see cref="ActorBase.Sender" />.
        /// </summary>
        /// <param name="confirmationId">The correlation id of the current message.</param>
        /// <param name="senderId">The id of the sender of the current message.</param>
        /// <param name="duplicateMessage">The original message handled by this actor.</param>
        protected virtual void HandleDuplicate(long confirmationId, string senderId, object duplicateMessage)
        {
            var confirmationMessage = CreateConfirmationReplyMessage(confirmationId, senderId, duplicateMessage);
            Sender.Tell(confirmationMessage);
        }

        /// <summary>
        ///     Utility method for automatically calling <see cref="ConfirmDelivery" />
        ///     and sending a reply generated by <see cref="CreateConfirmationReplyMessage" />
        ///     to the current sender if an additional <see cref="replyTarget" /> is not specified.
        /// </summary>
        /// <param name="currentMessage">The current message processed by the actor.</param>
        /// <param name="replyTarget">Optional. A reference to the actor to whom we should reply.</param>
        protected virtual void ConfirmAndReply(object currentMessage, IActorRef replyTarget = null)
        {
            ConfirmDelivery();
            Debug.Assert(CurrentConfirmationId != null, nameof(CurrentConfirmationId) + " != null");
            var confirmationMessage =
                CreateConfirmationReplyMessage(CurrentConfirmationId.Value, CurrentSenderId, currentMessage);
            (replyTarget ?? Sender).Tell(confirmationMessage);
        }

        protected abstract object CreateConfirmationReplyMessage(long confirmationId, string senderId,
            object originalMessage);

        /// <summary>
        ///     INTERNAL API.
        ///     Used to trigger the pruning of senders from our <see cref="IReceiverState" />
        /// </summary>
        private sealed class PruneSendersTick : INotInfluenceReceiveTimeout
        {
            public static readonly PruneSendersTick Instance = new PruneSendersTick();

            private PruneSendersTick()
            {
            }
        }

        #region Utility Methods

        private ICancelable CreatePruneTask()
        {
            return _pruneTask = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(Settings.PruneInterval,
                Settings.PruneInterval, Self, PruneSendersTick.Instance, ActorRefs.NoSender);
        }

        internal static IReceiverState CreateInitialState(DeDuplicatingReceiverSettings settings)
        {
            // TODO: add support for StrictOrdering state
            return new UnorderedReceiverState(settings.TimeProvider, settings.BufferSizePerSender);
        }

        #endregion
    }
}