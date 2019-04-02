// -----------------------------------------------------------------------
// <copyright file="AckActor.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;

namespace Akka.Persistence.Extras.Tests.Supervision
{
    /// <summary>
    ///     Test actor used for sending back ACK messages as though it's been persisting them.
    /// </summary>
    public class AckActor : ReceiveActor
    {
        private readonly string _persistenceId;
        private readonly IActorRef _testActor;
        private bool _acking;

        public AckActor(IActorRef testActor, string persistenceId, bool acking = true)
        {
            _testActor = testActor;
            _persistenceId = persistenceId;
            _acking = acking;

            Receive<IConfirmableMessage>(msg =>
            {
                if (_acking)
                {
                    var confirmation = new Confirmation(msg.ConfirmationId, _persistenceId);
                    Context.Parent.Tell(confirmation);
                    _testActor.Tell(confirmation);
                }
            });

            Receive<ToggleAck>(ack => { _acking = !_acking; });

            Receive<Fail>(_ => { throw new ApplicationException("boom"); });

            ReceiveAny(_ => { _testActor.Tell(_); });
        }

        /// <summary>
        ///     Fail signal.
        /// </summary>
        public sealed class Fail
        {
            public static readonly Fail Instance = new Fail();

            private Fail()
            {
            }
        }

        public sealed class ToggleAck
        {
            public static readonly ToggleAck Instance = new ToggleAck();

            private ToggleAck()
            {
            }
        }
    }
}