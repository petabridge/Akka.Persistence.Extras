// -----------------------------------------------------------------------
// <copyright file="MyRecipientActor.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Akka.Persistence.Extras.Demo.DeDuplicatingReceiver
{
    public class MyRecipientActor : DeDuplicatingReceiveActor
    {
        public MyRecipientActor()
        {
            Command<Write>(write =>
            {
                Console.WriteLine("Received message {0} [id: {1}] from {2} - accept?", write.Content,
                    CurrentConfirmationId, CurrentSenderId);
                var response = Console.ReadLine()?.ToLowerInvariant();
                if (!string.IsNullOrEmpty(response) && (response.Equals("yes") || response.Equals("y")))
                {
                    // confirm delivery only if the user explicitly agrees
                    ConfirmAndReply(write);
                    Console.WriteLine("Confirmed delivery of message ID {0}", CurrentConfirmationId);
                }
                else
                {
                    Console.WriteLine("Did not confirm delivery of message ID {0}", CurrentConfirmationId);
                }
            });
        }

        public override string PersistenceId => Context.Self.Path.Name;

        protected override object CreateConfirmationReplyMessage(long confirmationId, string senderId,
            object originalMessage)
        {
            return new ReliableDeliveryAck(confirmationId);
        }

        protected override void HandleDuplicate(long confirmationId, string senderId, object duplicateMessage)
        {
            Console.WriteLine("Automatically de-duplicated message with ID {0} from {1}", confirmationId, senderId);
            base.HandleDuplicate(confirmationId, senderId, duplicateMessage);
        }
    }
}