// -----------------------------------------------------------------------
// <copyright file="DeDuplicatingMessageSerializerSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using Akka.Persistence.Extras.Serialization;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Extras.Tests.Serialization
{
    public class DeDuplicatingMessageSerializerSpecs : TestKit.Xunit2.TestKit
    {
        public DeDuplicatingMessageSerializerSpecs(ITestOutputHelper helper)
            : base(output: helper)
        {
            _deDuplicatingMessageSerializer = new DeDuplicatingMessageSerializer((ExtendedActorSystem) Sys);
        }

        private readonly DeDuplicatingMessageSerializer _deDuplicatingMessageSerializer;

        [Fact(DisplayName =
            "DeDuplicatingMessageSerializer should serialize and deserialize ConfirmableMessageEnvelope messages")]
        public void Should_serialize_and_deserialize_ConfirmableMessageEnvelope()
        {
            var cme = new ConfirmableMessageEnvelope(100L, "fuber", "noooooooo");

            var bytes = _deDuplicatingMessageSerializer.ToBinary(cme);
            var cme2 =
                (ConfirmableMessageEnvelope) _deDuplicatingMessageSerializer.FromBinary(bytes,
                    _deDuplicatingMessageSerializer.Manifest(cme));

            cme2.ConfirmationId.Should().Be(cme.ConfirmationId);
            cme2.SenderId.Should().Be(cme.SenderId);
            cme2.Message.Should().Be(cme.Message);
        }

        [Fact(DisplayName = "DeDuplicatingMessageSerializer should serialize and deserialize Confirmation messages")]
        public void Should_serialize_and_deserialize_Confirmation()
        {
            var confirmation = new Confirmation(100L, "fuber");
            var bytes = _deDuplicatingMessageSerializer.ToBinary(confirmation);
            var confirmation2 =
                (Confirmation) _deDuplicatingMessageSerializer.FromBinary(bytes,
                    _deDuplicatingMessageSerializer.Manifest(confirmation));

            confirmation2.ConfirmationId.Should().Be(confirmation.ConfirmationId);
            confirmation2.SenderId.Should().Be(confirmation.SenderId);
        }

        [Fact(DisplayName =
            "DeDuplicatingMessageSerializer should serialize and deserialize IReceiverStateSnapshot messages")]
        public void Should_serialize_and_deserialize_IReceiverStateSnapshot()
        {
            var anyOrderReceiverState = new UnorderedReceiverState();
            anyOrderReceiverState.ConfirmProcessing(100L, "fuber");
            anyOrderReceiverState.ConfirmProcessing(101L, "fuber");
            anyOrderReceiverState.ConfirmProcessing(1L, "foo");
            anyOrderReceiverState.ConfirmProcessing(1L, "bar");
            anyOrderReceiverState.ConfirmProcessing(2L, "foo");
            var snapshot = anyOrderReceiverState.ToSnapshot();

            var bytes = _deDuplicatingMessageSerializer.ToBinary(snapshot);
            var snapshot2 =
                (IReceiverStateSnapshot) _deDuplicatingMessageSerializer.FromBinary(bytes,
                    _deDuplicatingMessageSerializer.Manifest(snapshot));

            snapshot.TrackedIds.Should().BeEquivalentTo(snapshot2.TrackedIds);
            snapshot.TrackedSenders.Should().BeEquivalentTo(snapshot2.TrackedSenders);
        }
    }
}