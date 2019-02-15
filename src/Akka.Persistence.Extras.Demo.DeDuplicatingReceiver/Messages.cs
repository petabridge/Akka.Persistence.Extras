// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

namespace Akka.Persistence.Extras.Demo.DeDuplicatingReceiver
{
    public class ReliableDeliveryEnvelope<TMessage>
    {
        public ReliableDeliveryEnvelope(TMessage message, long messageId)
        {
            Message = message;
            MessageId = messageId;
        }

        public TMessage Message { get; }

        public long MessageId { get; }
    }

    public class ReliableDeliveryAck
    {
        public ReliableDeliveryAck(long messageId)
        {
            MessageId = messageId;
        }

        public long MessageId { get; }
    }

    public class Write
    {
        public Write(string content)
        {
            Content = content;
        }

        public string Content { get; }
    }
}