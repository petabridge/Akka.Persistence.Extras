// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System.IO;
using Akka.Actor;
using Akka.Configuration;

namespace Akka.Persistence.Extras.Demo.DeDuplicatingReceiver
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var config = ConfigurationFactory.ParseString(File.ReadAllText("sample.conf"));
            using (var actorSystem = ActorSystem.Create("AtLeastOnceDeliveryDemo", config))
            {
                var recipientActor = actorSystem.ActorOf(Props.Create(() => new MyRecipientActor()), "receiver");
                var atLeastOnceDeliveryActor =
                    actorSystem.ActorOf(Props.Create(() => new MyAtLeastOnceDeliveryActor(recipientActor)),
                        "delivery1");

                var atLeastOnceDeliveryActor2 =
                    actorSystem.ActorOf(Props.Create(() => new MyAtLeastOnceDeliveryActor(recipientActor)),
                        "delivery2");

                var atLeastOnceDeliveryActor3 =
                    actorSystem.ActorOf(Props.Create(() => new MyAtLeastOnceDeliveryActor(recipientActor)),
                        "delivery3");

                actorSystem.WhenTerminated.Wait();
            }
        }
    }
}