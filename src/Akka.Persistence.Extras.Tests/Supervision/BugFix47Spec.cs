using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Extras.Tests.Supervision
{
    public class BugFix47Spec : TestKit.Xunit2.TestKit
    {
        public BugFix47Spec(ITestOutputHelper helper) : base(output: helper)
        {
        }

        [Fact(DisplayName = "PersistenceSupervisor should be able to deliver IConfirmable messages without having an explicit MakeConfirmableEvent or IsEvent method defined")]
        public void ShouldDeliverConfirmableMessagesToChildWithoutExplicitDecorators()
        {
            var ackActorProps = Props.Create(() => new AckActor(TestActor, "fuber", true));
            var ps = Sys.ActorOf(PersistenceSupervisor.PropsFor(ackActorProps, "test"));
            var confirmable = new ConfirmableMessage<int>(1, 1000, "t");
            ps.Tell(confirmable);

            // make sure the confirmation ID we provided isn't replaced by anything the PersistenceSupervisor does
            var c = ExpectMsg<Confirmation>();
            c.ConfirmationId.Should().Be(confirmable.ConfirmationId);
        }
    }
}
