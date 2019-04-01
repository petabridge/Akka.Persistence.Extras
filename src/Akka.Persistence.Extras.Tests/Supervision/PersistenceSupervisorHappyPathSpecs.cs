using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Akka.Pattern;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Akka.Persistence.Extras.Tests.Supervision.PersistenceSupervisorHelpers;

namespace Akka.Persistence.Extras.Tests.Supervision
{
    public class PersistenceSupervisorHappyPathSpecs : TestKit.Xunit2.TestKit
    {
        public PersistenceSupervisorHappyPathSpecs(ITestOutputHelper helper) : base(output: helper)
        {

        }

        [Fact(DisplayName = "PersistenceSupervisor should forward messages to child")]
        public void PersistenceSupervisor_should_forward_normal_msgs()
        {
            var childProps = Props.Create(() => new AckActor(TestActor, "fuber", true));
            var supervisorProps = PersistenceSupervisorFor(o => o is string, childProps, "myPersistentActor");
            var actor = Sys.ActorOf(supervisorProps);

            actor.Tell(BackoffSupervisor.GetCurrentChild.Instance);
            var child = ExpectMsg<BackoffSupervisor.CurrentChild>().Ref;
            Watch(child);

            // sanity check to ensure that the two actors are different
            child.Equals(actor).Should().BeFalse();

            // send a non-persisted message
            actor.Tell(1);
            ExpectMsg(1);

            // send a persisted message
            actor.Tell("string");
            ExpectMsg<Confirmation>().ConfirmationId.Should().Be(1L);
            actor.Tell("string1");
            ExpectMsg<Confirmation>().ConfirmationId.Should().Be(2L);

            // shutdown the parent
            actor.Tell(PoisonPill.Instance);
            ExpectTerminated(child);
        }

        [Fact(DisplayName = "PersistenceSupervisor should buffer and re-deliver un-ACKed events for child after restart")]
        public void PersistenceSupervisor_should_buffer_unAcked_messages()
        {
            var childProps = Props.Create(() => new AckActor(TestActor, "fuber", true));
            var supervisorProps = PersistenceSupervisorFor(o => o is string, childProps, "myPersistentActor");
            var actor = Sys.ActorOf(supervisorProps);

            actor.Tell(BackoffSupervisor.GetCurrentChild.Instance);
            var child = ExpectMsg<BackoffSupervisor.CurrentChild>().Ref;
            Watch(child);

            // disable ACK-ing - will be turned on automatically via restart
            actor.Tell(AckActor.ToggleAck.Instance);

            // send a non-persisted message
            actor.Tell(1);
            ExpectMsg(1);

            // send some events
            actor.Tell("event1");
            actor.Tell("event2");
            ExpectNoMsg(200); // no replies back

            EventFilter.Exception<ApplicationException>("boom").ExpectOne(() =>
            {
                actor.Tell(AckActor.Fail.Instance);
                ExpectTerminated(child); // child should be stopped by default parent supervisor strategy
            });

            ExpectMsg<Confirmation>().ConfirmationId.Should().Be(1L);
            ExpectMsg<Confirmation>().ConfirmationId.Should().Be(2L);
        }
    }
}
