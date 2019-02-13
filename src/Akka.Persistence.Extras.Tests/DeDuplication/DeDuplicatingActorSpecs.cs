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
            return new ReplyMessage(confirmationId, senderId, originalMessage);
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
    }
}
