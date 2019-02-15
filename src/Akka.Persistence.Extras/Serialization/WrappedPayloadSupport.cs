using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Akka.Persistence.Extras.Serialization.Msgs;
using Akka.Serialization;
using Akka.Util;
using Google.Protobuf;

namespace Akka.Persistence.Extras.Serialization
{
    /// <summary>
    ///     INTERNAL API.
    ///     Used to help serialize inner payloads while constructing the envelope.
    /// </summary>
    internal sealed class WrappedPayloadSupport
    {
        private readonly ExtendedActorSystem _system;

        public WrappedPayloadSupport(ExtendedActorSystem system)
        {
            _system = system;
        }

        public Payload PayloadToProto(object payload)
        {
            if (payload == null) // TODO: handle null messages
                throw new ArgumentNullException(nameof(payload), "payload cannot be null!");

            var payloadProto = new Payload();
            var serializer = _system.Serialization.FindSerializerFor(payload);

            payloadProto.Message = ByteString.CopyFrom(serializer.ToBinary(payload));
            payloadProto.SerializerId = serializer.Identifier;

            // get manifest

            if (serializer is SerializerWithStringManifest manifestSerializer)
            {
                var manifest = manifestSerializer.Manifest(payload);
                if (!string.IsNullOrEmpty(manifest))
                    payloadProto.MessageManifest = ByteString.CopyFromUtf8(manifest);
            }
            else
            {
                if (serializer.IncludeManifest)
                    payloadProto.MessageManifest = ByteString.CopyFromUtf8(payload.GetType().TypeQualifiedName());
            }

            return payloadProto;
        }

        public object PayloadFrom(Payload payload)
        {
            var manifest = !payload.MessageManifest.IsEmpty
                ? payload.MessageManifest.ToStringUtf8()
                : string.Empty;

            return _system.Serialization.Deserialize(
                payload.Message.ToByteArray(),
                payload.SerializerId,
                manifest);
        }
    }
}
