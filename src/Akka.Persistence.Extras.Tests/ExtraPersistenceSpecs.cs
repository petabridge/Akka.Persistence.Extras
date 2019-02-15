using System;
using System.Collections.Generic;
using System.Text;
using Akka.Persistence.Extras.Serialization;
using Akka.Serialization;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Extras.Tests
{
    public class ExtraPersistenceSpecs : TestKit.Xunit2.TestKit
    {
        public ExtraPersistenceSpecs(ITestOutputHelper helper) : base(output: helper) { }

        [Fact(DisplayName = "ExtraPersistence embedded HOCON should load correctly")]
        public void Should_load_default_ExtraPersistence_HOCON()
        {
            var defaultConfig = ExtraPersistence.DefaultConfig();
            defaultConfig.Should().NotBeNull();
        }

        [Fact(DisplayName = "ExtraPersistence class should inject DeDuplicatingMessageSerializer configuration into ActorSystem")]
        public void Should_inject_DeDuplicatingSerializer_config_into_ActorSystem()
        {
            ExtraPersistence.For(Sys).Running.Should().BeTrue();

            // Test with confirmation
            var confirmation = new Confirmation(1L, "fuber");
            var serializer = (SerializerWithStringManifest)Sys.Serialization.FindSerializerFor(confirmation);
            serializer.Should().BeOfType<DeDuplicatingMessageSerializer>();

            // Test with envelope
            Sys.Serialization.FindSerializerForType(typeof(ConfirmableMessageEnvelope))
                .Should().BeOfType<DeDuplicatingMessageSerializer>();

            // Test with snapshot
            Sys.Serialization.FindSerializerForType(typeof(IReceiverStateSnapshot))
                .Should().BeOfType<DeDuplicatingMessageSerializer>();
            Sys.Serialization.FindSerializerForType(typeof(ReceiverStateSnapshot))
                .Should().BeOfType<DeDuplicatingMessageSerializer>();
        }
    }
}
