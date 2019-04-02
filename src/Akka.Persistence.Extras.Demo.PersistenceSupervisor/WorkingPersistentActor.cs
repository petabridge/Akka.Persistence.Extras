// -----------------------------------------------------------------------
// <copyright file="WorkingPersistentActor.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Event;

namespace Akka.Persistence.Extras.Demo.PersistenceSupervisor
{
    /// <summary>
    ///     Persistent actor that is going to try to get its work done using the <see cref="FailingJournal" />
    /// </summary>
    public class WorkingPersistentActor : ReceivePersistentActor
    {
        private readonly ILoggingAdapter _log = Context.GetLogger();
        private int _currentCount;

        public WorkingPersistentActor(string persistenceId)
        {
            PersistenceId = persistenceId;

            Recover<int>(i =>
            {
                _log.Info("Recovery: Adding [{0}] to current count [{1}] - new count: [{2}]", i, _currentCount,
                    _currentCount + i);
                _currentCount += i;
            });

            Recover<SnapshotOffer>(o =>
            {
                if (o.Snapshot is int i)
                {
                    _log.Info("Recovery: Setting initial count to [{1}]", i);
                    _currentCount = i;
                }
            });

            Command<AddToCount>(e =>
            {
                Persist(e.CountValue, iN =>
                {
                    _log.Info("Command: Adding [{0}] to current count [{1}] - new count: [{2}]", iN, _currentCount,
                        _currentCount + iN);
                    _currentCount += iN;

                    // ACK the message back to parent
                    Context.Parent.Tell(new Confirmation(e.ConfirmationId, PersistenceId));
                });
            });

            Command<GetCount>(g => { Sender.Tell(_currentCount); });
        }

        public override string PersistenceId { get; }

        public class AddToCount : IConfirmableMessage
        {
            public AddToCount(long confirmationId, string senderId, int countValue)
            {
                ConfirmationId = confirmationId;
                SenderId = senderId;
                CountValue = countValue;
            }

            public int CountValue { get; }

            public long ConfirmationId { get; }
            public string SenderId { get; }
        }

        public class GetCount
        {
            public static readonly GetCount Instance = new GetCount();

            private GetCount()
            {
            }
        }
    }
}