using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Serialization;
using FluentAssertions;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Extras.Tests.Deserialization
{
    public class SnapshotDeserializationSpec : Akka.TestKit.Xunit2.TestKit
    {
        private const string TargetDir = "target/snapshots";
        
        public SnapshotDeserializationSpec(ITestOutputHelper output)
            : base(ConfigurationFactory.ParseString(
                    @"akka.test.timefactor = 3
                      akka.persistence.snapshot-store.plugin = ""akka.persistence.snapshot-store.local""
                      akka.persistence.snapshot-store.local.dir = """ + TargetDir +  @"""")
                    .WithFallback(ConfigurationFactory.FromResource<ExtraPersistence>("Akka.Persistence.Extras.Config.akka.persistence.extras.conf")),
                "SnapshotDeserializationSpec", output)
        {
            EnsureEmptyDirectoryExists();
        }

        [Fact]
        public async Task Should_deserialize_saved_snapshot()
        {
            // Start receiving process
            var recipientActor = Sys.ActorOf(Props.Create(() => new MyRecipientActor()), "receiver");
            var atLeastOnceDeliveryActor = Sys.ActorOf(Props.Create(() => new AtLeastOnceDeliverySender(recipientActor)), "delivery1");

            // Wait until de-duplication actor snapshot is done
            AwaitCondition(() => Directory.GetFiles(TargetDir, "snapshot-receiver-*").Any(), TimeSpan.FromSeconds(60));
            
            // Stop actors
            Sys.Terminate().Wait();

            // Load snapshot bytes
            var snapshotPath = Directory.GetFiles(TargetDir, "snapshot-receiver-*").First();
            var snapshotBytes = await File.ReadAllBytesAsync(snapshotPath);
            
            // Get configured ActorSystem
            var persistenceConfig = ConfigurationFactory.FromResource<ExtraPersistence>("Akka.Persistence.Extras.Config.akka.persistence.extras.conf");
            var system = ActorSystem.Create("Deserializer", persistenceConfig);
            
            // Deserialize snapshot
            var persistenceMessageSerializer = new PersistenceSnapshotSerializer(system as ExtendedActorSystem);
            var snapshot = persistenceMessageSerializer.FromBinary<Akka.Persistence.Serialization.Snapshot>(snapshotBytes);
            
            // Assert that snapshot contains real data
            snapshot.Data.Should().BeOfType<ReceiverStateSnapshot>().Which.TrackedIds.Should().NotBeEmpty();

            EnsureDirectoryDeleted();
        }

        private void EnsureEmptyDirectoryExists()
        {
            EnsureDirectoryDeleted();
            Directory.CreateDirectory(TargetDir);
        }
        
        private void EnsureDirectoryDeleted()
        {
            if (Directory.Exists(TargetDir))
                Directory.Delete(TargetDir, true);
        }
        
        public class AtLeastOnceDeliverySender : AtLeastOnceDeliveryReceiveActor
        {
            private const string Characters = "AB";
            private int _counter;

            private ICancelable _recurringMessageSend;

            public AtLeastOnceDeliverySender(IActorRef targetActor)
            {
                Command<DoSend>(send =>
                {
                    var write = new Write("Message " + Characters[_counter++ % Characters.Length]);
                    Deliver(targetActor.Path, messageId => new ConfirmableMessageEnvelope(messageId, PersistenceId, write));
                });

                Command<ReliableDeliveryAck>(ack => { ConfirmDelivery(ack.MessageId); });
            }

            // Going to use our name for persistence purposes
            public override string PersistenceId => Context.Self.Path.Name;

            protected override void PreStart()
            {
                _recurringMessageSend = Context.System.Scheduler.ScheduleTellRepeatedlyCancelable(
                    TimeSpan.Zero,
                    TimeSpan.FromMilliseconds(100), 
                    Self, 
                    new DoSend(), 
                    Self);

                base.PreStart();
            }

            protected override void PostStop()
            {
                _recurringMessageSend?.Cancel();

                base.PostStop();
            }

            private class DoSend { }
        }
        
        public class MyRecipientActor : DeDuplicatingReceiveActor
        {
            public MyRecipientActor()
            {
                Command<Write>(write => ConfirmAndReply(write));
            }

            public override string PersistenceId => Context.Self.Path.Name;

            protected override object CreateConfirmationReplyMessage(long confirmationId, string senderId, object originalMessage)
            {
                return new ReliableDeliveryAck(confirmationId);
            }
        }
        
        public class ReliableDeliveryAck
        {
            public ReliableDeliveryAck(long messageId)
            {
                MessageId = messageId;
            }

            public long MessageId { get; }
        }

        public class Write
        {
            public Write(string content)
            {
                Content = content;
            }

            public string Content { get; }
        }
    }
}