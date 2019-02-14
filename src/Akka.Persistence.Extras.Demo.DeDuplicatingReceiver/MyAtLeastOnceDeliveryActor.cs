// -----------------------------------------------------------------------
// <copyright file="MyAtLeastOnceDeliveryActor.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;

namespace Akka.Persistence.Extras.Demo.DeDuplicatingReceiver
{
    public class MyAtLeastOnceDeliveryActor : AtLeastOnceDeliveryReceiveActor
    {
        private const string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private readonly IActorRef _targetActor;
        private int _counter;

        private ICancelable _recurringMessageSend;
        private ICancelable _recurringSnapshotCleanup;

        public MyAtLeastOnceDeliveryActor(IActorRef targetActor)
        {
            _targetActor = targetActor;

            // recover the most recent at least once delivery state
            Recover<SnapshotOffer>(offer => offer.Snapshot is AtLeastOnceDeliverySnapshot, offer =>
            {
                var snapshot = offer.Snapshot as AtLeastOnceDeliverySnapshot;
                SetDeliverySnapshot(snapshot);
            });

            Command<DoSend>(send => { Self.Tell(new Write("Message " + Characters[_counter++ % Characters.Length])); });

            Command<Write>(write =>
            {
                Deliver(_targetActor.Path,
                    messageId => new ConfirmableMessageEnvelope(messageId, PersistenceId, write));

                // save the full state of the at least once delivery actor
                // so we don't lose any messages upon crash
                SaveSnapshot(GetDeliverySnapshot());
            });

            Command<ReliableDeliveryAck>(ack => { ConfirmDelivery(ack.MessageId); });

            Command<CleanSnapshots>(clean =>
            {
                // save the current state (grabs confirmations)
                SaveSnapshot(GetDeliverySnapshot());
            });

            Command<SaveSnapshotSuccess>(saved =>
            {
                var seqNo = saved.Metadata.SequenceNr;
                DeleteSnapshots(new SnapshotSelectionCriteria(seqNo,
                    saved.Metadata.Timestamp.AddMilliseconds(-1))); // delete all but the most current snapshot
            });

            Command<SaveSnapshotFailure>(failure =>
            {
                // log or do something else
            });
        }

        // Going to use our name for persistence purposes
        public override string PersistenceId => Context.Self.Path.Name;

        protected override void PreStart()
        {
            _recurringMessageSend = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(10), Self, new DoSend(), Self);

            _recurringSnapshotCleanup =
                Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(10), Self, new CleanSnapshots(), ActorRefs.NoSender);

            base.PreStart();
        }

        protected override void PostStop()
        {
            _recurringSnapshotCleanup?.Cancel();
            _recurringMessageSend?.Cancel();

            base.PostStop();
        }

        private class DoSend
        {
        }

        private class CleanSnapshots
        {
        }
    }
}