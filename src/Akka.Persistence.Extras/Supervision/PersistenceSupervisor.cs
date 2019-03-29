using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Akka.Event;
using Akka.Pattern;
using Akka.Util;

namespace Akka.Persistence.Extras.Supervision
{
    /// <summary>
    /// <see cref="BackoffSupervisor"/> implementation that buffers un-delivered and un-persisted
    /// messages down to a <see cref="PersistentActor"/> child.
    /// </summary>
    public class PersistenceSupervisor : ActorBase
    {
        #region Messages

        /// <summary>
        /// Send this message to the <see cref="BackoffSupervisor"/> and it will reply with <see cref="CurrentChild"/> containing the `ActorRef` of the current child, if any.
        /// </summary>
        [Serializable]
        public sealed class GetCurrentChild
        {
            /// <summary>
            /// TBD
            /// </summary>
            public static readonly GetCurrentChild Instance = new GetCurrentChild();
            private GetCurrentChild() { }
        }

        /// <summary>
        /// Send this message to the <see cref="BackoffSupervisor"/> and it will reply with <see cref="CurrentChild"/> containing the `ActorRef` of the current child, if any.
        /// </summary>
        [Serializable]
        public sealed class CurrentChild
        {
            /// <summary>
            /// TBD
            /// </summary>
            /// <param name="ref">TBD</param>
            public CurrentChild(IActorRef @ref)
            {
                Ref = @ref;
            }

            public IActorRef Ref { get; }
        }

        /// <summary>
        /// Send this message to the <see cref="BackoffSupervisor"/> and it will reset the back-off. This should be used in conjunction with `withManualReset` in <see cref="BackoffOptionsImpl"/>.
        /// </summary>
        [Serializable]
        public sealed class DoReset
        {
            public static readonly DoReset Instance = new DoReset();
            private DoReset() { }
        }

        [Serializable]
        public sealed class GetRestartCount
        {
            public static readonly GetRestartCount Instance = new GetRestartCount();
            private GetRestartCount() { }
        }

        [Serializable]
        public sealed class RestartCount
        {
            public RestartCount(int count)
            {
                Count = count;
            }

            public int Count { get; }
        }

        [Serializable]
        public sealed class DoStartChild : IDeadLetterSuppression
        {
            /// <summary>
            /// TBD
            /// </summary>
            public static readonly DoStartChild Instance = new DoStartChild();
            private DoStartChild() { }
        }

        [Serializable]
        public sealed class ResetRestartCount : IDeadLetterSuppression
        {
            public ResetRestartCount(int current)
            {
                Current = current;
            }

            public int Current { get; }
        }

        #endregion

        /// <summary>
        /// In-memory state representation of an un-ACKed message
        /// </summary>
        internal struct PersistentEvent
        {
            public PersistentEvent(IConfirmableMessage msg, IActorRef sender)
            {
                Msg = msg;
                Sender = sender;
            }

            public IConfirmableMessage Msg { get; }
            public IActorRef Sender { get; }
        }


        private readonly IPersistenceSupervisionConfig _config;
        private readonly SupervisorStrategy _strategy;
        private long _currentDeliveryId = 0;
        protected readonly ILoggingAdapter Log = Context.GetLogger();

        public PersistenceSupervisor(Props childProps, string childName,
            IBackoffReset reset,
            IPersistenceSupervisionConfig config, SupervisorStrategy strategy = null)
        {
            ChildProps = childProps;
            ChildName = childName;
            Reset = reset;
            _config = config;
            _strategy = strategy ?? Actor.SupervisorStrategy.StoppingStrategy;
        }

        protected Props ChildProps { get; }
        protected string ChildName { get; }
        protected IBackoffReset Reset { get; }

        protected IActorRef Child { get; set; }
        protected int RestartCountN { get; set; }
        protected bool FinalStopMessageReceived { get; set; }

        protected override SupervisorStrategy SupervisorStrategy()
        {
            return _strategy;
        }

        protected override void PreStart()
        {
            StartChild();
        }

        private void StartChild()
        {
            if (Child == null)
            {
                Child = Context.Watch(Context.ActorOf(ChildProps, ChildName));
            }
        }

        protected override bool Receive(object message)
        {
            throw new NotImplementedException();
        }

        private bool OnTerminated(object message)
        {
            if (message is Terminated terminated && terminated.ActorRef.Equals(Child))
            {
                Child = null;
                if (FinalStopMessageReceived)
                {
                    Context.Stop(Self);
                }
                else
                {
                    var maxNrOfRetries = _strategy is OneForOneStrategy oneForOne ? oneForOne.MaxNumberOfRetries : -1;
                    var nextRestartCount = RestartCountN + 1;
                    if (maxNrOfRetries == -1 || nextRestartCount <= maxNrOfRetries)
                    {
                        var restartDelay = CalculateDelay(RestartCountN, _config.MinBackoff, _config.MaxBackoff, _config.RandomFactor);
                        Context.System.Scheduler.ScheduleTellOnce(restartDelay, Self, DoStartChild.Instance, Self);
                        RestartCountN = nextRestartCount;
                    }
                    else
                    {
                        Log.Debug($"Terminating on restart #{nextRestartCount} which exceeds max allowed restarts ({maxNrOfRetries})");
                        Context.Stop(Self);
                    }
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// Calculates an exponential back off delay.
        /// </summary>
        internal static TimeSpan CalculateDelay(
            int restartCount,
            TimeSpan minBackoff,
            TimeSpan maxBackoff,
            double randomFactor)
        {
            var rand = 1.0 + ThreadLocalRandom.Current.NextDouble() * randomFactor;
            var calculateDuration = Math.Min(maxBackoff.Ticks, minBackoff.Ticks * Math.Pow(2, restartCount)) * rand;
            return calculateDuration < 0d || calculateDuration >= long.MaxValue ? maxBackoff : new TimeSpan((long)calculateDuration);
        }
    }
}
