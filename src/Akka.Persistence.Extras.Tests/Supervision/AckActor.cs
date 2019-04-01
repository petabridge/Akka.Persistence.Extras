using System;
using Akka.Actor;

namespace Akka.Persistence.Extras.Tests.Supervision
{
    /// <summary>
    /// Test actor used for sending back ACK messages as though it's been persisting them.
    /// </summary>
    public class AckActor : ReceiveActor
    {
        /// <summary>
        /// Fail signal.
        /// </summary>
        public sealed class Fail
        {
            public static readonly Fail Instance = new Fail();
            private Fail() { }
        }

        private readonly string _persistenceId;
        private readonly IActorRef _testActor;
        public AckActor(IActorRef testActor, string persistenceId)
        {
            _testActor = testActor;
            _persistenceId = persistenceId;

            Receive<IConfirmableMessage>(msg =>
            {
                var confirmation = new Confirmation(msg.ConfirmationId, _persistenceId);
                Context.Parent.Tell(confirmation);
                _testActor.Tell(confirmation);
            });

            Receive<Fail>(_ => { throw new ApplicationException("boom"); });

            ReceiveAny(_ =>
            {
                _testActor.Tell(_);
            });
        }
    }
}