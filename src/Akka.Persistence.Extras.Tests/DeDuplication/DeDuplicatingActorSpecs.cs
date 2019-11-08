// -----------------------------------------------------------------------
// <copyright file="DeDuplicatingActorSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Extras.Tests.DeDuplication
{
    public class TestDeDuplicatingActor : DeDuplicatingReceiveActor
    {
        public TestDeDuplicatingActor(string persistenceId) : this(new DeDuplicatingReceiverSettings(), persistenceId)
        {
        }

        public TestDeDuplicatingActor(DeDuplicatingReceiverSettings settings, string persistenceId) : base(settings)
        {
            PersistenceId = persistenceId ?? Uri.EscapeUriString(Self.Path.ToStringWithoutAddress());

            Command<ConfirmableMsg>(c =>
            {
                ReceivedMessages.Add(c.Msg);
                ConfirmAndReply(c);
            });

            Command<string>(str => str.Equals("crash"), str => { Crash(); });

            Command<string>(str => str.Equals("canConfirm"), str => { Sender.Tell(IsCurrentMessageConfirmable); });

            Command<string>(str =>
            {
                if (IsCurrentMessageConfirmable)
                {
                    ReceivedMessages.Add(str);
                    ConfirmAndReply(str);
                }
            });
        }

        public List<string> ReceivedMessages { get; } = new List<string>();

        public override string PersistenceId { get; }

        private void Crash()
        {
            throw new ApplicationException("HALP");
        }

        protected override object CreateConfirmationReplyMessage(long confirmationId, string senderId,
            object originalMessage)
        {
            switch (originalMessage)
            {
                case ConfirmableMsg msg:
                    return new ReplyMessage(confirmationId, senderId, msg.Msg);
                default:
                    return new ReplyMessage(confirmationId, senderId, originalMessage);
            }
        }

        public class ConfirmableMsg : IConfirmableMessage
        {
            public ConfirmableMsg(long confirmationId, string senderId, string msg)
            {
                ConfirmationId = confirmationId;
                SenderId = senderId;
                Msg = msg;
            }

            public string Msg { get; }

            public long ConfirmationId { get; }
            public string SenderId { get; }
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
    }

    public class DeDuplicatingActorSpecs : TestKit.Xunit2.TestKit
    {
        public DeDuplicatingActorSpecs(ITestOutputHelper output)
            : base(@"akka.loglevel = DEBUG
                     akka.persistence.snapshot-store.plugin = ""akka.persistence.snapshot-store.inmem""", output)
        {
        }

        [Fact(DisplayName = "A DeDuplicatingActor should confirm a message the first time it's " +
                            "processed and then de-duplicate it afterwards")]
        public void DeDuplicatingActor_should_confirm_and_dedup_message()
        {
            var dedup = ActorOfAsTestActorRef<TestDeDuplicatingActor>(Props.Create(() => new TestDeDuplicatingActor("uno")));
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

        [Fact(DisplayName = "DeDuplicatingActor should handle confirmable messages normally")]
        public void DeDuplicatingActor_should_handle_confirmable_message()
        {
            var dedup = Sys.ActorOf(Props.Create(() => new TestDeDuplicatingActor("dos")));
            dedup.Tell(new ConfirmableMessageEnvelope(100L, "fakeNews", "canConfirm"));
            ExpectMsg<bool>().Should().BeTrue();
        }

        [Fact(DisplayName = "DeDuplicatingActor should handle non-IConfirmable messages normally")]
        public void DeDuplicatingActor_should_handle_nonConfirmable_message()
        {
            var dedup = Sys.ActorOf(Props.Create(() => new TestDeDuplicatingActor("tres")));
            dedup.Tell("canConfirm");
            ExpectMsg<bool>().Should().BeFalse();
        }

        [Fact(DisplayName = "A DeDuplicatingActor should be able to recover its prior state upon crashing" +
                            "and restarting.")]
        public void DeDuplicatingActor_should_recover_prior_deduping_state()
        {
            var dedup = ActorOfAsTestActorRef<TestDeDuplicatingActor>(Props.Create(() => new TestDeDuplicatingActor("quattro")));

            ConfirmableMessageEnvelope CreateNewMsg(long seqNo)
            {
                return new ConfirmableMessageEnvelope(seqNo, "fuber", "no" + seqNo);
            }

            void ShouldConfirm(long seqNo, object rcvdMsg)
            {
                rcvdMsg.Should().BeOfType<TestDeDuplicatingActor.ReplyMessage>();
                var replyMsg = (TestDeDuplicatingActor.ReplyMessage) rcvdMsg;

                replyMsg.ConfirmationId.Should().Be(seqNo);
                replyMsg.SenderId.Should().Be("fuber");
            }

            var msgCount = 120;

            // should be enough to generate 1 snapshot and 20 additional journal msgs
            foreach (var seqNo in Enumerable.Range(0, msgCount).Select(x => (long) x)) dedup.Tell(CreateNewMsg(seqNo));

            var confirmations = ReceiveN(msgCount);
            long currentMsgId = 0;
            foreach (var c in confirmations)
            {
                ShouldConfirm(currentMsgId, c);
                currentMsgId++;
            }

            dedup.UnderlyingActor.ReceivedMessages.Count.Should().Be(msgCount);

            // validate that the snapshot has been saved
            AwaitCondition(() => dedup.UnderlyingActor.SnapshotSequenceNr > 0);

            // time to crash the actor
            EventFilter.Exception<ApplicationException>("HALP").ExpectOne(() => { dedup.Tell("crash"); });

            // validate that the actor's user-defined state is fresh
            AwaitCondition(() => dedup.UnderlyingActor.ReceivedMessages.Count == 0);

            // send the actor a duplicate
            dedup.Tell(CreateNewMsg(1L));
            var reply = ExpectMsg<TestDeDuplicatingActor.ReplyMessage>();
            reply.ConfirmationId.Should().Be(1L);

            // validate that the state was not modified (because: duplicate)
            dedup.UnderlyingActor.ReceivedMessages.Count.Should().Be(0);
        }

        [Fact(DisplayName = "A DeDuplicatingActor should be able to recover its prior state upon crashing" +
                            "and restarting without any snapshots.")]
        public void DeDuplicatingActor_should_recover_prior_deduping_state_without_snapshots()
        {
            var dedup = ActorOfAsTestActorRef<TestDeDuplicatingActor>(Props.Create(() => new TestDeDuplicatingActor("m")));

            ConfirmableMessageEnvelope CreateNewMsg(long seqNo)
            {
                return new ConfirmableMessageEnvelope(seqNo, "fuber", "no" + seqNo);
            }

            void ShouldConfirm(long seqNo, object rcvdMsg)
            {
                rcvdMsg.Should().BeOfType<TestDeDuplicatingActor.ReplyMessage>();
                var replyMsg = (TestDeDuplicatingActor.ReplyMessage)rcvdMsg;

                replyMsg.ConfirmationId.Should().Be(seqNo);
                replyMsg.SenderId.Should().Be("fuber");
            }

            var msgCount = 12;

            // should be enough to generate 12 msgs
            foreach (var seqNo in Enumerable.Range(0, msgCount).Select(x => (long)x)) dedup.Tell(CreateNewMsg(seqNo));

            var confirmations = ReceiveN(msgCount);
            long currentMsgId = 0;
            foreach (var c in confirmations)
            {
                ShouldConfirm(currentMsgId, c);
                currentMsgId++;
            }

            dedup.UnderlyingActor.ReceivedMessages.Count.Should().Be(msgCount);

            // terminate the deduplicating receive actor
            Watch(dedup);
            dedup.Tell(PoisonPill.Instance);
            ExpectTerminated(dedup);

            var dedup2 = ActorOfAsTestActorRef<TestDeDuplicatingActor>(Props.Create(() => new TestDeDuplicatingActor("m")));

            // send the actor a duplicate
            dedup2.Tell(CreateNewMsg(1L));
            var reply = ExpectMsg<TestDeDuplicatingActor.ReplyMessage>();
            reply.ConfirmationId.Should().Be(1L);

            // validate that the state was not modified (because: duplicate)
            dedup2.UnderlyingActor.ReceivedMessages.Count.Should().Be(0);
        }
    }
}