using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Akka.Persistence.Extras.Tests
{
    public class ExtraPersistenceSpecs : TestKit.Xunit2.TestKit
    {
        public ExtraPersistenceSpecs(ITestOutputHelper helper) : base(output:helper) { }

        [Fact(DisplayName = "ExtraPersistence embedded HOCON should load correctly")]
        public void Should_load_default_ExtraPersistence_HOCON()
        {
            ExtraPersistence.DefaultConfig().Should().NotBeNull();
        }
    }
}
