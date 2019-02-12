using System;
using System.Collections.Generic;
using System.Text;
using FsCheck;
using Microsoft.FSharp.Core;

namespace Akka.Persistence.Extras.Tests
{
    /// <summary>
    /// Used to generate <see cref="Arbitrary{a}"/> data for <see cref="IConfirmableMessage"/> types.
    /// </summary>
    public class ConfirmableGenerators
    {
        public static Arbitrary<IConfirmableMessage> CreateConfirmableMessage()
        {
            Func<string, long, IConfirmableMessage> combiner = 
                (senderId, confirmationId) => new ConfirmableMessageEnvelope(confirmationId, senderId, string.Empty);

            return Arb.From(Gen.Map2(FuncConvert.FromFunc(combiner),
                Gen.Elements("a", "b", "c", "d", "e", "f", "g"), // restrict the number of possible senders to something small
                Arb.Default.Int64().Generator.Where(x => x > 0)));

            //return Arb.From(Arb.Default.Int64().Generator.Where(x => x > 0)
            //    .Select(x => (IConfirmableMessage)new ConfirmableMessageEnvelope(x, "foo", string.Empty)));
        }
    }
}
