using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Extras.Tests.DeDuplication
{

    public class TestDeDuplicatingActor : DeDuplicatingReceiveActor
    {
        public class ConfirmableMsg : IConfirmableMessage
        {
            public ConfirmableMsg(long confirmationId, string senderId, string msg)
            {
                ConfirmationId = confirmationId;
                SenderId = senderId;
                Msg = msg;
            }

            public long ConfirmationId { get; }
            public string SenderId { get; }

            public string Msg { get; }
        }

        public class ReplyMessage
        {
            public ReplyMessage(long confirmationId, string senderId, object originalMessage)
            {
                ConfirmationId = confirmationId;
                SenderId = senderId;
                OriginalMessage = originalMessage;
            }

            public long ConfirmationId { get; }

            public string SenderId { get; }

            public object OriginalMessage { get; }
        }

        public List<string> ReceivedMessages { get; } = new List<string>();

        public TestDeDuplicatingActor() : this(new DeDuplicatingReceiverSettings(), null) { }

        public TestDeDuplicatingActor(DeDuplicatingReceiverSettings settings, string persistenceId) : base(settings)
        {
            PersistenceId = persistenceId ?? Uri.EscapeUriString(Self.Path.ToStringWithoutAddress());

            Command<ConfirmableMsg>(c =>
            {
                ReceivedMessages.Add(c.Msg);
                ConfirmAndReply(c);
            });

            Command<string>(str => str.Equals("crash"), str =>
            {
                Crash();
            });

            Command<string>(str => str.Equals("canConfirm"), str =>
            {
                Sender.Tell(IsCurrentMessageConfirmable);
            });

            Command<string>(str =>
            {
                if (IsCurrentMessageConfirmable)
                {
                    ReceivedMessages.Add(str);
                    ConfirmAndReply(str);
                }
            });
        }

        private void Crash()
        {
            throw new ApplicationException("HALP");
        }

        public override string PersistenceId { get; }
        protected override object CreateConfirmationReplyMessage(long confirmationId, string senderId, object originalMessage)
        {
            switch (originalMessage)
            {
                case ConfirmableMsg msg:
                    return new ReplyMessage(confirmationId, senderId, msg.Msg);
                default:
                    return new ReplyMessage(confirmationId, senderId, originalMessage);
            }
            
        }
    }

    public class DeDuplicatingActorSpecs : TestKit.Xunit2.TestKit
    {
        public DeDuplicatingActorSpecs(ITestOutputHelper output)
            : base("akka.loglevel = DEBUG",output: output) { }

        [Fact(DisplayName = "DeDuplicatingActor should handle non-IConfirmable messages normally")]
        public void DeDuplicatingActor_should_handle_nonConfirmable_message()
        {
            var dedup = Sys.ActorOf(Props.Create(() => new TestDeDuplicatingActor()));
            dedup.Tell("canConfirm");
            ExpectMsg<bool>().Should().BeFalse();
        }

        [Fact(DisplayName = "DeDuplicatingActor should handle confirmable messages normally")]
        public void DeDuplicatingActor_should_handle_confirmable_message()
        {
            var dedup = Sys.ActorOf(Props.Create(() => new TestDeDuplicatingActor()));
            dedup.Tell(new ConfirmableMessageEnvelope(100L, "fakeNews", "canConfirm"));
            ExpectMsg<bool>().Should().BeTrue();
        }

        [Fact(DisplayName = "A DeDuplicatingActor should confirm a message the first time it's " +
                            "processed and then de-duplicate it afterwards")]
        public void DeDuplicatingActor_should_confirm_and_dedupe_message()
        {
            var dedup = ActorOfAsTestActorRef<TestDeDuplicatingActor>(Props.Create(() => new TestDeDuplicatingActor()));
            var confirmableMessage = new TestDeDuplicatingActor.ConfirmableMsg(1L, "foo", "test1");
            dedup.Tell(confirmableMessage);

            // should get confirmation back
            var reply1 = ExpectMsg<TestDeDuplicatingActor.ReplyMessage>();

            reply1.ConfirmationId.Should().Be(confirmableMessage.ConfirmationId);
            reply1.SenderId.Should().Be(confirmableMessage.SenderId);
            reply1.OriginalMessage.Should().Be(confirmableMessage.Msg);
            dedup.UnderlyingActor.ReceivedMessages.Count.Should().Be(1);

            // now we send a duplicate
            dedup.Tell(confirmableMessage);
            var reply2 = ExpectMsg<TestDeDuplicatingActor.ReplyMessage>();

            // all assertions should be the same
            reply2.ConfirmationId.Should().Be(confirmableMessage.ConfirmationId);
            reply2.SenderId.Should().Be(confirmableMessage.SenderId);
            reply2.OriginalMessage.Should().Be(confirmableMessage.Msg);
            dedup.UnderlyingActor.ReceivedMessages.Count.Should().Be(1);
        }
    }
}
