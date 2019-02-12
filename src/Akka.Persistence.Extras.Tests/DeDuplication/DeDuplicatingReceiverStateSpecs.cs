using System;
using System.Collections.Immutable;
using FsCheck;
using FsCheck.Experimental;
using FsCheck.Xunit;
using Xunit;

namespace Akka.Persistence.Extras.Tests.DeDuplication
{
    public class DeDuplicatingReceiverStateSpecs
    {
        [Property]
        public Property DeDuplicatingReceiverStateModel_should_hold_for_AnyOrderReceiverState()
        {
            var model = new DeDuplicatingReceiverStateModel(new AnyOrderReceiverStateSetup());
            return model.ToProperty();
        }

        [Fact(DisplayName = "UnorderedReceiverState should prune its records correctly")]
        public void UnorderedReceiverState_should_prune_correctly()
        {

        }
    }

    public class AnyOrderReceiverStateSetup : Setup<IReceiverState, DeDuplicatingReceiverModelState>
    {
        public readonly DateTime StartTime = DateTime.UtcNow;

        public override IReceiverState Actual()
        {
            return new UnorderedReceiverState(new FakeTimeProvider(StartTime));
        }

        public override DeDuplicatingReceiverModelState Model()
        {
            return new DeDuplicatingReceiverModelState(ImmutableDictionary<string, DateTime>.Empty, ImmutableDictionary<string, ImmutableHashSet<long>>.Empty, StartTime);
        }
    }
}
