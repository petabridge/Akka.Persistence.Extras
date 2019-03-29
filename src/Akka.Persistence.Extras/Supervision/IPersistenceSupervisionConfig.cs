using System;

namespace Akka.Persistence.Extras.Supervision
{
    /// <summary>
    /// Configuration used by <see cref="BackoffPersistenceSupervisor"/>.
    /// </summary>
    public interface IPersistenceSupervisionConfig
    {
        /// <summary>
        /// Tests to see if an incoming message is an event.
        ///
        /// Returns <c>true</c> if a message is an event that will be persisted
        /// by the child actor. <c>false</c> otherwise.
        /// </summary>
        Func<object, bool> IsEvent { get; }

        /// <summary>
        /// Packages the original message and a correlation id (a <c>long</c> integer)
        /// into a message of type <see cref="IConfirmableMessage"/>.
        ///
        /// We expect the underlying <see cref="PersistentActor"/> to send back a <see cref="Confirmation"/> with
        /// this information so we can mark the message as successfully processed.
        /// </summary>
        Func<object, long, IConfirmableMessage> MakeEventConfirmable { get; }

        /// <summary>
        /// Optional. Can be <c>null</c>. Used to indicate if the message
        /// is the final message that the underlying child actor will process before being shutdown.
        /// </summary>
        Func<object, bool> FinalStopMessage { get; }
    }

    /// <inheritdoc cref="IPersistenceSupervisionConfig"/>
    public sealed class PersistenceSupervisionConfig : IPersistenceSupervisionConfig
    {
        public PersistenceSupervisionConfig(Func<object, bool> isEvent, Func<object, long, IConfirmableMessage> makeEventConfirmable, 
            Func<object, bool> finalStopMessage = null)
        {
            IsEvent = isEvent ?? throw new ArgumentNullException(nameof(isEvent));
            MakeEventConfirmable = makeEventConfirmable ?? throw new ArgumentNullException(nameof(makeEventConfirmable));
            FinalStopMessage = finalStopMessage;
        }

        public Func<object, bool> IsEvent { get; }
        public Func<object, long, IConfirmableMessage> MakeEventConfirmable { get; }
        public Func<object, bool> FinalStopMessage { get; }
    }
}