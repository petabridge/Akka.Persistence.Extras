// -----------------------------------------------------------------------
// <copyright file="DeDuplicatingMessageSerializer.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Persistence.Extras.Serialization.Msgs;
using Akka.Serialization;
using Akka.Util;
using Google.Protobuf;

namespace Akka.Persistence.Extras.Serialization
{
    /// <summary>
    ///     <see cref="Serializer" /> implementation for working with types used by the
    ///     <see cref="DeDuplicatingReceiveActor" />.
    /// </summary>
    public sealed class DeDuplicatingMessageSerializer : SerializerWithStringManifest
    {
        public const string ConfirmationManifest = "DDUPCONFIRM";
        public const string ReceiveStateSnapshotManifest = "DDUPSNAPSHOT";
        public const string ConfirmableEnvelopeManifest = "DDUPENVELOPE";

        private readonly WrappedPayloadSupport _wrappedPayloadSupport;

        public DeDuplicatingMessageSerializer(ExtendedActorSystem system) : base(system)
        {
            _wrappedPayloadSupport = new WrappedPayloadSupport(system);
        }

        /// <inheritdoc />
        public override byte[] ToBinary(object obj)
        {
            switch (obj)
            {
                case ConfirmableMessageEnvelope cme:
                    return ConfirmableEnvelopeToProto(cme);
                case Confirmation confirmation:
                    return ConfirmationToProto(confirmation);
                case IReceiverStateSnapshot snapshot:
                    return SnapshotToProto(snapshot);
                default:
                    throw new ArgumentException(
                        $"Cannot serialize object of type [{obj.GetType().TypeQualifiedName()}]");
            }
        }

        /// <inheritdoc />
        public override object FromBinary(byte[] bytes, string manifest)
        {
            switch (manifest)
            {
                case ConfirmableEnvelopeManifest:
                    return ConfirmableEnvelopeFromProto(bytes);
                case ConfirmationManifest:
                    return ConfirmationFromProto(bytes);
                case ReceiveStateSnapshotManifest:
                    return SnapshotFromProto(bytes);
                default:
                    throw new ArgumentException($"Cannot deserialize object with manifest [{manifest}]");
            }
        }

        /// <inheritdoc />
        public override string Manifest(object o)
        {
            if (o is ConfirmableMessageEnvelope) return ConfirmableEnvelopeManifest;
            if (o is Confirmation) return ConfirmationManifest;
            if (o is IReceiverStateSnapshot snapshot) return ReceiveStateSnapshotManifest;

            throw new ArgumentException($"Cannot deserialize object of type [{o.GetType().TypeQualifiedName()}]");
        }

        private byte[] ConfirmationToProto(Confirmation c)
        {
            var p = new Msgs.Confirmation
            {
                SenderId = c.SenderId,
                ConfirmationId = c.ConfirmationId
            };

            return p.ToByteArray();
        }

        private Confirmation ConfirmationFromProto(byte[] bytes)
        {
            var cP = Msgs.Confirmation.Parser.ParseFrom(bytes);

            return new Confirmation(cP.ConfirmationId, cP.SenderId);
        }

        private byte[] SnapshotToProto(IReceiverStateSnapshot snapshot)
        {
            var sP = new Msgs.ReceiverStateSnapshot();

            foreach (var trackedSender in snapshot.TrackedSenders)
                sP.TrackedSenders.Add(trackedSender.Key, trackedSender.Value.Ticks);

            foreach (var trackerIds in snapshot.TrackedIds)
            {
                var rcp = new ReceivedMessageCollection();
                rcp.ConfirmationIds.Add(trackerIds.Value);
                sP.TrackedIds.Add(trackerIds.Key, rcp);
            }

            return sP.ToByteArray();
        }

        private IReceiverStateSnapshot SnapshotFromProto(byte[] bytes)
        {
            var sP = Msgs.ReceiverStateSnapshot.Parser.ParseFrom(bytes);

            var trackedIds = new Dictionary<string, List<long>>();
            var trackedSenders = new Dictionary<string, DateTime>();

            foreach (var trackedId in sP.TrackedIds)
                trackedIds[trackedId.Key] = trackedId.Value.ConfirmationIds.ToList();

            foreach (var trackedSender in sP.TrackedSenders)
                trackedSenders[trackedSender.Key] = new DateTime(trackedSender.Value, DateTimeKind.Utc);

            return new ReceiverStateSnapshot(trackedIds.ToDictionary(x => x.Key, y => (IReadOnlyList<long>) y.Value),
                trackedSenders);
        }

        private byte[] ConfirmableEnvelopeToProto(ConfirmableMessageEnvelope e)
        {
            var eP = new Msgs.ConfirmableMessageEnvelope();
            eP.SenderId = e.SenderId;
            eP.ConfirmationId = e.ConfirmationId;
            eP.Msg = _wrappedPayloadSupport.PayloadToProto(e.Message);

            return eP.ToByteArray();
        }

        private ConfirmableMessageEnvelope ConfirmableEnvelopeFromProto(byte[] bytes)
        {
            var eP = Msgs.ConfirmableMessageEnvelope.Parser.ParseFrom(bytes);

            var payload = _wrappedPayloadSupport.PayloadFrom(eP.Msg);

            return new ConfirmableMessageEnvelope(eP.ConfirmationId, eP.SenderId, payload);
        }
    }
}