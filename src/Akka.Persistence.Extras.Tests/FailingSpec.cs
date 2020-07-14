using System;
using Xunit;

namespace Akka.Persistence.Extras.Tests
{
    public class FailingSpec
    {
        [Fact]
        public void Fail()
        {
            throw new Exception("This spec should fail");
        }
    }
}