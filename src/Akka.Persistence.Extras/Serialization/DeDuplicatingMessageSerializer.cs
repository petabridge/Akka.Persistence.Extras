using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Akka.Serialization;

namespace Akka.Persistence.Extras.Serialization
{
    public sealed class DeDuplicatingMessageSerializer : SerializerWithStringManifest
    {
        public DeDuplicatingMessageSerializer(ExtendedActorSystem system) : base(system)
        {
        }

        public override byte[] ToBinary(object obj)
        {
            throw new NotImplementedException();
        }

        public override object FromBinary(byte[] bytes, string manifest)
        {
            throw new NotImplementedException();
        }

        public override string Manifest(object o)
        {
            throw new NotImplementedException();
        }
    }
}
