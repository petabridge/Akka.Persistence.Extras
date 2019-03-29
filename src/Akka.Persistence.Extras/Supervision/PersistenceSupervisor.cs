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

        private readonly SortedDictionary<long, PersistentEvent> _buffer = new SortedDictionary<long, PersistentEvent>();
        private readonly IPersistenceSupervisionConfig _config;
        private readonly SupervisorStrategy _strategy;
        private long _currentDeliveryId = 0;
        protected readonly ILoggingAdapter Log = Context.GetLogger();

        public PersistenceSupervisor(Props childProps, string childName,
            IPersistenceSupervisionConfig config, SupervisorStrategy strategy = null)
        {
            ChildProps = childProps;
            ChildName = childName;
            _config = config;
            _strategy = strategy ?? Actor.SupervisorStrategy.StoppingStrategy;
        }

        protected Props ChildProps { get; }
        protected string ChildName { get; }
        protected TimeSpan ResetBackoff => _config.ResetBackoff;

        protected IActorRef Child { get; set; }
        protected int RestartCountN { get; set; }
        protected bool FinalStopMessageReceived { get; set; }

        protected Func<object, bool> FinalStopMessage => _config.FinalStopMessage;
        protected Func<object, bool> IsEvent => _config.IsEvent;
        protected Func<object, long, IConfirmableMessage> MakeEventConfirmable => _config.MakeEventConfirmable;


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

        protected bool HandleBackoff(object message)
        {
            switch (message)
            {
                case BackoffSupervisor.StartChild _:
                    {
                        StartChild();
                        Context.System.Scheduler.ScheduleTellOnce(ResetBackoff, Self, new BackoffSupervisor.ResetRestartCount(RestartCountN), Self);
                        break;
                    }
                case BackoffSupervisor.ResetRestartCount count:
                    {
                        if (count.Current == RestartCountN)
                        {
                            RestartCountN = 0;
                        }
                        break;
                    }
                case BackoffSupervisor.GetRestartCount _:
                    Sender.Tell(new BackoffSupervisor.RestartCount(RestartCountN));
                    break;
                case BackoffSupervisor.GetCurrentChild _:
                    Sender.Tell(new BackoffSupervisor.CurrentChild(Child));
                    break;
                default:
                    {
                        if (Child != null)
                        {
                            if (Child.Equals(Sender))
                            {
                                // use the BackoffSupervisor as sender
                                Context.Parent.Tell(message);
                            }
                            else
                            {
                                Child.Forward(message);
                                if (!FinalStopMessageReceived && FinalStopMessage != null)
                                {
                                    FinalStopMessageReceived = FinalStopMessage(message);
                                }
                            }
                        }
                        else
                        {
                            if (ReplyWhileStopped != null)
                            {
                                Sender.Tell(ReplyWhileStopped);
                            }
                            else
                            {
                                Context.System.DeadLetters.Forward(message);
                            }

                            if (FinalStopMessage != null && FinalStopMessage(message))
                            {
                                Context.Stop(Self);
                            }
                        }
                        break;
                    }
            }

            return true;
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
