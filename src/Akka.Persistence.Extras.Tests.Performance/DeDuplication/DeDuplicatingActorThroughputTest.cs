// -----------------------------------------------------------------------
// <copyright file="UnitTest1.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Extras;
using Akka.Util.Internal;
using NBench;

namespace Akka.AtLeastOnceDeliveryJournaling.Tests.Performance
{
    public class TestDeDuplicatingActor : DeDuplicatingReceiveActor
    {
        public long CounterValue = 0;

        public TestDeDuplicatingActor() : this(new DeDuplicatingReceiverSettings(), null)
        {
        }

        public TestDeDuplicatingActor(DeDuplicatingReceiverSettings settings, string persistenceId) : base(settings)
        {
            PersistenceId = persistenceId ?? Uri.EscapeUriString(Self.Path.ToStringWithoutAddress());

            Command<ConfirmableMsg>(c =>
            {
                CounterValue++;
                ConfirmAndReply(c);
            });

            Command<string>(str => str.Equals("getCounter"), str => { Sender.Tell(CounterValue); });

            Command<string>(str =>
            {
                if (IsCurrentMessageConfirmable)
                {
                    CounterValue++;
                    ConfirmAndReply(str);
                }
            });
        }

        protected override void HandleDuplicate(long confirmationId, string senderId, object duplicateMessage)
        {
            CounterValue++;
            base.HandleDuplicate(confirmationId, senderId, duplicateMessage);
        }

        public override string PersistenceId { get; }

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

    public class DeDuplicatingActorThroughputTest
    {
        public const string MsgRcvCounter = "MessagesProcessed";
        private Counter _opsCounter;
        private ActorSystem _actorSystem;
        private IActorRef _dedup;

        private static readonly AtomicCounter ActorSystemId = new AtomicCounter(0);

        public static readonly Config Config =
            @"akka.persistence.snapshot-store.plugin = ""akka.persistence.snapshot-store.inmem""";

        [PerfSetup]
        public void Setup(BenchmarkContext context)
        {
            _opsCounter = context.GetCounter(MsgRcvCounter);
            _actorSystem = ActorSystem.Create("SpecActor" + ActorSystemId.IncrementAndGet(), Config);
            _dedup = _actorSystem.ActorOf(Props.Create(() => new TestDeDuplicatingActor()), "deduper");

            // need to do a warm-up (ensures the actor is fully initialized before test begins)
            context.Trace.Debug("START: Warming up DeDuplicatingActor");
            _dedup.Ask<ActorIdentity>(new Identify(null)).Wait();
            context.Trace.Debug("FINISH: Warming up DeDuplicatingActor");
        }

        [PerfBenchmark(NumberOfIterations = 5, RunMode = RunMode.Iterations, RunTimeMilliseconds = 1000)]
        [CounterMeasurement(MsgRcvCounter)]
        public void DeDuplicatingActorThroughputSpec(BenchmarkContext context)
        {
            // do this N times (in order to create duplicates)
            for (var y = 0; y < 10; y++)
            {
                // keep track of up to 1000 messages
                for (var i = 0L; i < 1050L; i++)
                {
                    _dedup.Tell(new TestDeDuplicatingActor.ConfirmableMsg(i, "foo", "bar"));
                }
            }

            var runTask = _dedup.Ask<long>("getCounter", TimeSpan.FromSeconds(60));
            runTask.Wait();
            _opsCounter.Increment(runTask.Result);
        }

        [PerfCleanup]
        public void CleanUp()
        {
            _actorSystem.Terminate().Wait();
        }
    }
}