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
                Arb.Default.NonWhiteSpaceString().Generator.Select(x => x.Get),
                Arb.Default.Int64().Generator.Where(x => x > 0)));
        }
    }
}
