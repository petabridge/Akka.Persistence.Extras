// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using Akka.Actor;
using Akka.Configuration;

namespace Akka.Persistence.Extras.Demo.PersistenceSupervisor
{
    internal class Program
    {
        public static readonly Config Config = @"akka.persistence.journal.plugin = ""akka.persistence.journal.failure""
                           akka.persistence.journal.failure.recovery-event-timeout = 2s
                           akka.persistence.journal.failure.class = """ +
                                               typeof(FailingJournal).AssemblyQualifiedName + "\"";

        private static void Main(string[] args)
        {
            var actorSystem = ActorSystem.Create("MyActorSystem", Config);

            var childProps = Props.Create(() => new WorkingPersistentActor("fuber"));
            var supervisor = Extras.PersistenceSupervisor.PropsFor((o, l) =>
                {
                    if (o is int i)
                        return new WorkingPersistentActor.AddToCount(l, string.Empty, i);

                    return new ConfirmableMessageEnvelope(l, string.Empty, o);
                }, o => o is int, childProps, "myActor",
                strategy: SupervisorStrategy.StoppingStrategy.WithMaxNrOfRetries(100));

            var sup = actorSystem.ActorOf(supervisor, "fuber");

            // final sum should be 5050
            foreach (var i in Enumerable.Range(1, 100))
                sup.Tell(i);

            actorSystem.WhenTerminated.Wait();
        }
    }
}