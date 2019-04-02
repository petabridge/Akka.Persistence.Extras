// -----------------------------------------------------------------------
// <copyright file="AkkaPersistenceSupervisionEnd2EndSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using Akka.Actor;
using Akka.Configuration;
using Akka.Event;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Extras.Tests.Supervision
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

    public class AkkaPersistenceSupervisionEnd2EndSpecs : TestKit.Xunit2.TestKit
    {
        public AkkaPersistenceSupervisionEnd2EndSpecs(ITestOutputHelper output)
            : base(Config, output: output)
        {
        }

        public static readonly Config Config = @"akka.persistence.journal.plugin = ""akka.persistence.journal.failure""
                           akka.persistence.journal.failure.recovery-event-timeout = 2s
                           akka.persistence.journal.failure.class = """ +
                                               typeof(FailingJournal).AssemblyQualifiedName + "\"";

        [Fact(DisplayName = "End2End: PersistenceSupervisor should ensure delivery of all events")]
        public void PersistenceSupervisor_should_ensure_delivery_of_all_events()
        {
            var childProps = Props.Create(() => new WorkingPersistentActor("fuber"));
            var supervisor = PersistenceSupervisor.PropsFor((o, l) =>
                {
                    if (o is int i)
                        return new WorkingPersistentActor.AddToCount(l, string.Empty, i);

                    return new ConfirmableMessageEnvelope(l, string.Empty, o);
                }, o => o is int, childProps, "myActor",
                strategy: SupervisorStrategy.StoppingStrategy.WithMaxNrOfRetries(100));

            var sup = Sys.ActorOf(supervisor, "fuber");
            Watch(sup);

            foreach (var i in Enumerable.Range(1, 100))
                sup.Tell(i);

            Within(TimeSpan.FromMinutes(2), () =>
            {
                AwaitAssert(() =>
                {
                    sup.Tell(WorkingPersistentActor.GetCount.Instance);
                    ExpectMsg(5050, TimeSpan.FromMilliseconds(25)); // sum should be 5051
                }, interval: TimeSpan.FromMilliseconds(40));
            });
        }
    }
}