using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Akka.Actor;
using Akka.Pattern;
using Akka.Persistence.Extras.Supervision;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using static Akka.Persistence.Extras.Tests.Supervision.PersistenceSupervisorHelpers;

namespace Akka.Persistence.Extras.Tests.Supervision
{
    public class PersistenceSupervisorSpecs : TestKit.Xunit2.TestKit
    {
        public PersistenceSupervisorSpecs(ITestOutputHelper helper) : base(output: helper)
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

        [Fact(DisplayName = "PersistenceSupervisor should buffer messages while child is restarting")]
        public void PersistenceSupervisor_should_buffer_messages_while_ActorIsRestarting()
        {
            var childProps = Props.Create(() => new AckActor(TestActor, "fuber", true));

            // make the backoff window huge, so we have to manually restart it
            var supervisorConfig = new PersistenceSupervisionConfig(o => o is string, ToConfirmableMessage,
                new ManualReset(), TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(10));

            var supervisorProps = Props.Create(() =>
                new PersistenceSupervisor(childProps, "myPersistentActor", supervisorConfig, null));
            var actor = Sys.ActorOf(supervisorProps);

            actor.Tell(BackoffSupervisor.GetCurrentChild.Instance);
            var child = ExpectMsg<BackoffSupervisor.CurrentChild>().Ref;
            Watch(child);

            // send some events
            actor.Tell("event1");
            actor.Tell("event2");
            ExpectMsg<Confirmation>().ConfirmationId.Should().Be(1L);
            ExpectMsg<Confirmation>().ConfirmationId.Should().Be(2L);

            // kill the child
            EventFilter.Exception<ApplicationException>("boom").ExpectOne(() =>
            {
                actor.Tell(AckActor.Fail.Instance);
                ExpectTerminated(child); // child should be stopped by default parent supervisor strategy
            });

            // send a blend of events and other messages prior to restart
            actor.Tell("event3");
            actor.Tell(1);
            actor.Tell(2);
            actor.Tell("event4");
            actor.Tell(true);

            // shouldn't hear anything back
            ExpectNoMsg(250);

            // create the child manually (timer is still running in background)
            actor.Tell(BackoffSupervisor.StartChild.Instance);

            ExpectMsg<Confirmation>().ConfirmationId.Should().Be(3L);
            ExpectMsg(1);
            ExpectMsg(2);
            ExpectMsg<Confirmation>().ConfirmationId.Should().Be(4L);
            ExpectMsg(true);
        }

        [Fact(DisplayName = "PersistentSupervisor should kill itself if child RestartCount exceeded")]
        public void PersistentSupervisor_should_kill_child_and_self_if_RestartCount_Exceeded()
        {
            var childProps = Props.Create(() => new AckActor(TestActor, "fuber", true));
            var supervisorConfig = new PersistenceSupervisionConfig(o => o is string, ToConfirmableMessage,
                new ManualReset(), TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(10));

            var supervisorProps = Props.Create(() =>
                new PersistenceSupervisor(childProps, "myPersistentActor", supervisorConfig, SupervisorStrategy.StoppingStrategy.WithMaxNrOfRetries(1)));
            var actor = Sys.ActorOf(supervisorProps);

            Watch(actor);

            actor.Tell(BackoffSupervisor.GetCurrentChild.Instance);
            var child = ExpectMsg<BackoffSupervisor.CurrentChild>().Ref;
            Watch(child);

            actor.Tell(AckActor.Fail.Instance); // forces the actor to fail
            ExpectTerminated(child);
            
            AwaitCondition(() =>
            {
                actor.Tell(BackoffSupervisor.GetCurrentChild.Instance);
                child = ExpectMsg<BackoffSupervisor.CurrentChild>().Ref;
                if (child.IsNobody())
                    return false;

                Watch(child);
                actor.Tell(AckActor.Fail.Instance); // forces the actor to fail
                ExpectTerminated(child);
                return true;
            });

            ExpectTerminated(actor);
        }
    }
}
