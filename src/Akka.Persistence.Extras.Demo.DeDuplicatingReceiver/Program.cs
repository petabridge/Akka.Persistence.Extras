using Akka.Actor;

namespace Akka.Persistence.Extras.Demo.DeDuplicatingReceiver
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var actorSystem = ActorSystem.Create("AtLeastOnceDeliveryDemo"))
            {
                var recipientActor = actorSystem.ActorOf(Props.Create(() => new MyRecipientActor()), "receiver");
                var atLeastOnceDeliveryActor =
                    actorSystem.ActorOf(Props.Create(() => new MyAtLeastOnceDeliveryActor(recipientActor)), "delivery1");

                var atLeastOnceDeliveryActor2 =
                    actorSystem.ActorOf(Props.Create(() => new MyAtLeastOnceDeliveryActor(recipientActor)), "delivery2");

                var atLeastOnceDeliveryActor3 =
                    actorSystem.ActorOf(Props.Create(() => new MyAtLeastOnceDeliveryActor(recipientActor)), "delivery3");

                actorSystem.WhenTerminated.Wait();
            }
        }
    }
}
