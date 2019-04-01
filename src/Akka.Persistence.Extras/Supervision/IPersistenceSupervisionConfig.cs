using System;
using Akka.Pattern;

namespace Akka.Persistence.Extras.Supervision
{
    /// <summary>
    /// Used to reset a <see cref="PersistenceSupervisor"/> manually,
    /// often for testing purposes.
    /// </summary>
    public sealed class ManualReset : IBackoffReset
    {

    }

    /// <summary>
    /// Used to reset a <see cref="PersistenceSupervisor"/> automatically on a timer.
    /// </summary>
    public sealed class AutoReset : IBackoffReset
    {
        public static readonly TimeSpan DefaultResetBackoff = TimeSpan.FromMilliseconds(5000);

        /// <summary>
        /// Default <see cref="AutoReset"/> instance.
        /// </summary>
        public static readonly AutoReset Default = new AutoReset(DefaultResetBackoff);

        public AutoReset(TimeSpan resetBackoff)
        {
            ResetBackoff = resetBackoff;
        }

        public TimeSpan ResetBackoff { get; }
    }

    /// <summary>
    /// Configuration used by <see cref="PersistenceSupervisor"/>.
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

        /// <summary>
        /// The strategy to use to reset the <see cref="PersistenceSupervisor"/>'s reset clock.
        /// </summary>
        IBackoffReset Reset { get; }

        TimeSpan MinBackoff { get; }
        TimeSpan MaxBackoff { get; }
        double RandomFactor { get; }
    }

    /// <inheritdoc cref="IPersistenceSupervisionConfig"/>
    public sealed class PersistenceSupervisionConfig : IPersistenceSupervisionConfig
    {
        public static readonly TimeSpan DefaultMinBackoff = TimeSpan.FromMilliseconds(100);
        public static readonly TimeSpan DefaultMaxBackoff = TimeSpan.FromMilliseconds(2000);
        
        public const double DefaultRandomFactor = 0.2d;

        public PersistenceSupervisionConfig(Func<object, bool> isEvent, Func<object, long, IConfirmableMessage> makeEventConfirmable, 
            IBackoffReset resetBackoff = null, 
            TimeSpan? minBackoff = null, 
            TimeSpan? maxBackoff = null, 
            double? randomFactor = null, Func<object, bool> finalStopMessage = null)
        {
            IsEvent = isEvent ?? throw new ArgumentNullException(nameof(isEvent));
            MakeEventConfirmable = makeEventConfirmable ?? throw new ArgumentNullException(nameof(makeEventConfirmable));
            Reset = resetBackoff ?? AutoReset.Default;
            MinBackoff = minBackoff ?? DefaultMinBackoff;
            MaxBackoff = maxBackoff ?? DefaultMaxBackoff;
            RandomFactor = randomFactor ?? DefaultRandomFactor;
            FinalStopMessage = finalStopMessage;
        }

        public Func<object, bool> IsEvent { get; }
        public Func<object, long, IConfirmableMessage> MakeEventConfirmable { get; }
        public Func<object, bool> FinalStopMessage { get; }
        public IBackoffReset Reset { get; }
        public TimeSpan MinBackoff { get; }
        public TimeSpan MaxBackoff { get; }
        public double RandomFactor { get; }
    }
}