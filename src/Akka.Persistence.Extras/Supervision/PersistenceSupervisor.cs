// -----------------------------------------------------------------------
// <copyright file="PersistenceSupervisor.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using Akka.Actor;
using Akka.Event;
using Akka.Pattern;
using Akka.Util;

namespace Akka.Persistence.Extras
{
    /// <inheritdoc />
    /// <summary>
    ///     <see cref="T:Akka.Pattern.BackoffSupervisor" /> implementation that buffers un-delivered and un-persisted
    ///     messages down to a <see cref="T:Akka.Persistence.PersistentActor" /> child.
    /// </summary>
    public class PersistenceSupervisor : ActorBase
    {
        private readonly Queue<Envelope> _allMsgBuffer = new Queue<Envelope>();
        private readonly IPersistenceSupervisionConfig _config;

        private readonly Dictionary<long, PersistentEvent> _eventBuffer = new Dictionary<long, PersistentEvent>();
        private readonly SupervisorStrategy _strategy;
        protected readonly ILoggingAdapter Log = Context.GetLogger();
        private long _currentDeliveryId;

        public PersistenceSupervisor(Props childProps, string childName,
            IPersistenceSupervisionConfig config, SupervisorStrategy strategy = null)
            : this(i => childProps, childName, config, strategy) { }

        public PersistenceSupervisor(Func<IActorRef, Props> childProps, string childName,
            IPersistenceSupervisionConfig config, SupervisorStrategy strategy = null)
        {
            ChildProps = childProps;
            ChildName = childName;
            _config = config;
            _strategy = strategy ?? Actor.SupervisorStrategy.StoppingStrategy;
            
            // use built-in defaults if unavailable
            IsEvent = _config.IsEvent ?? PersistenceSupervisionConfig.DefaultIsEvent;
            MakeEventConfirmable = _config.MakeEventConfirmable ??
                                   PersistenceSupervisionConfig.DefaultMakeEventConfirmable(childName);
        }

        protected Func<IActorRef, Props> ChildProps { get; }
        protected string ChildName { get; }
        protected IBackoffReset Reset => _config.Reset;

        protected IActorRef Child { get; set; }
        protected int RestartCountN { get; set; }
        protected bool FinalStopMessageReceived { get; set; }

        protected Func<object, bool> FinalStopMessage => _config.FinalStopMessage;
        protected Func<object, bool> IsEvent { get; }
        protected Func<object, long, IConfirmableMessage> MakeEventConfirmable { get; }

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
                Child = Context.Watch(Context.ActorOf(ChildProps(Context.Self), ChildName));
        }

        protected virtual void OnChildRecreate()
        {
            /*
             * Drain all internal buffers and try to get the child actor back
             * into the state it would have been if there were no issues with recovery
             * or writing to the Akka.Persistence journal.
             */
            foreach (var e in _eventBuffer.OrderBy(x => x.Key))
                Child.Tell(e.Value.Msg, e.Value.Sender);

            // Drain the normal operational buffer
            foreach (var m in _allMsgBuffer)
                HandleMsg(m.Message, m.Sender);

            _allMsgBuffer.Clear();
        }

        protected override bool Receive(object message)
        {
            return OnTerminated(message) || HandleBackoff(message);
        }

        protected virtual bool CheckIsEvent(object message)
        {
            return IsEvent(message);
        }

        protected virtual IConfirmableMessage DoMakeEventConfirmable(object message, long deliveryId)
        {
            return MakeEventConfirmable(message, deliveryId);
        }

        protected void HandleMsg(object message, IActorRef sender = null)
        {
            sender = sender ?? Sender;
            if (CheckIsEvent(message))
            {
                var confirmable = DoMakeEventConfirmable(message, ++_currentDeliveryId);
                _eventBuffer[confirmable.ConfirmationId] = new PersistentEvent(confirmable, Sender);
                Child.Tell(confirmable, sender);
            }
            else if (message is Confirmation confirmation)
            {
                if (Log.IsDebugEnabled)
                    Log.Debug("Confirming delivery of event [{0}] from [{1}]", confirmation.ConfirmationId,
                        confirmation.SenderId);

                if (!_eventBuffer.Remove(confirmation.ConfirmationId))
                    Log.Warning("Received confirmation for unknown event [{0}] from persistent entity [{1}]",
                        confirmation.ConfirmationId, confirmation.SenderId);
            }
            else if (sender.Equals(Child))
            {
                // use the BackoffSupervisor as sender
                Context.Parent.Tell(message);
            }
            else
            {
                Child.Tell(message, sender);
                if (!FinalStopMessageReceived && FinalStopMessage != null)
                    FinalStopMessageReceived = FinalStopMessage(message);
            }
        }

        protected bool HandleBackoff(object message)
        {
            switch (message)
            {
                case BackoffSupervisor.StartChild _:
                {
                    StartChild();
                    OnChildRecreate(); // replay all messages
                    if (Reset is AutoReset auto)
                        Context.System.Scheduler.ScheduleTellOnce(auto.ResetBackoff, Self,
                            new BackoffSupervisor.ResetRestartCount(RestartCountN), Self);
                    break;
                }
                case BackoffSupervisor.ResetRestartCount count:
                {
                    if (count.Current == RestartCountN)
                        RestartCountN = 0;
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
                        HandleMsg(message);
                    }
                    else
                    {
                        if (FinalStopMessage != null && FinalStopMessage(message))
                        {
                            Context.System.DeadLetters.Forward(message);
                            Context.Stop(Self);
                        }
                        else
                        {
                            // buffer the messages while we recreate the actor
                            _allMsgBuffer.Enqueue(new Envelope(message, Sender));
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
                        var restartDelay = CalculateDelay(RestartCountN, _config.MinBackoff, _config.MaxBackoff,
                            _config.RandomFactor);
                        Context.System.Scheduler.ScheduleTellOnce(restartDelay, Self,
                            BackoffSupervisor.StartChild.Instance, Self);
                        RestartCountN = nextRestartCount;
                    }
                    else
                    {
                        Log.Error(
                            $"Terminating on restart #{nextRestartCount} which exceeds max allowed restarts ({maxNrOfRetries})");
                        Context.Stop(Self);
                    }
                }
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Calculates an exponential back off delay.
        /// </summary>
        internal static TimeSpan CalculateDelay(
            int restartCount,
            TimeSpan minBackoff,
            TimeSpan maxBackoff,
            double randomFactor)
        {
            var rand = 1.0 + ThreadLocalRandom.Current.NextDouble() * randomFactor;
            var calculateDuration = Math.Min(maxBackoff.Ticks, minBackoff.Ticks * Math.Pow(2, restartCount)) * rand;
            return calculateDuration < 0d || calculateDuration >= long.MaxValue
                ? maxBackoff
                : new TimeSpan((long) calculateDuration);
        }

        public static Props PropsFor(Func<object, long, IConfirmableMessage> makeConfirmable,
            Func<object, bool> isEvent,
            Func<IActorRef, Props> childPropsFactory, string childName, IBackoffReset reset = null, Func<object, bool> finalStopMsg = null,
            SupervisorStrategy strategy = null)
        {
            var config =
                new PersistenceSupervisionConfig(isEvent, makeConfirmable, reset, finalStopMessage: finalStopMsg);
            return Props.Create(() => new PersistenceSupervisor(childPropsFactory, childName, config,
                strategy ?? Actor.SupervisorStrategy.StoppingStrategy));
        }

        public static Props PropsFor(Func<object, long, IConfirmableMessage> makeConfirmable,
            Func<object, bool> isEvent,
            Props childProps, string childName, IBackoffReset reset = null, Func<object, bool> finalStopMsg = null,
            SupervisorStrategy strategy = null)
        {
            var config =
                new PersistenceSupervisionConfig(isEvent, makeConfirmable, reset, finalStopMessage: finalStopMsg);
            return Props.Create(() => new PersistenceSupervisor(childProps, childName, config,
                strategy ?? Actor.SupervisorStrategy.StoppingStrategy));
        }

        /// <summary>
        /// Overload for users who ARE ALREADY USING <see cref="IConfirmableMessage"/> by the time the message
        /// is received by the <see cref="PersistenceSupervisor"/>.
        ///
        /// If a message is received by the <see cref="PersistenceSupervisor"/> without being decorated by <see cref="IConfirmableMessage"/>,
        /// under this configuration we will automatically package your message inside a <see cref="ConfirmableMessageEnvelope"/>.
        /// </summary>
        /// <param name="childProps"></param>
        /// <param name="childName"></param>
        /// <param name="reset"></param>
        /// <param name="finalStopMsg"></param>
        /// <param name="strategy"></param>
        /// <remarks>
        /// Read the manual. Seriously. 
        /// </remarks>
        /// <returns></returns>
        public static Props PropsFor(Props childProps, string childName, IBackoffReset reset = null,
            Func<object, bool> finalStopMsg = null,
            SupervisorStrategy strategy = null)
        {
            var config =
                new PersistenceSupervisionConfig(null, null, reset, finalStopMessage: finalStopMsg);
            return Props.Create(() => new PersistenceSupervisor(childProps, childName, config,
                strategy ?? Actor.SupervisorStrategy.StoppingStrategy));
        }

        /// <summary>
        /// Overload for users who ARE ALREADY USING <see cref="IConfirmableMessage"/> by the time the message
        /// is received by the <see cref="PersistenceSupervisor"/>.
        ///
        /// If a message is received by the <see cref="PersistenceSupervisor"/> without being decorated by <see cref="IConfirmableMessage"/>,
        /// under this configuration we will automatically package your message inside a <see cref="ConfirmableMessageEnvelope"/>.
        /// </summary>
        /// <param name="childPropsFactory"></param>
        /// <param name="childName"></param>
        /// <param name="reset"></param>
        /// <param name="finalStopMsg"></param>
        /// <param name="strategy"></param>
        /// <remarks>
        /// Read the manual. Seriously. 
        /// </remarks>
        /// <returns></returns>
        public static Props PropsFor(Func<IActorRef, Props> childPropsFactory, string childName, IBackoffReset reset = null,
            Func<object, bool> finalStopMsg = null,
            SupervisorStrategy strategy = null)
        {
            var config =
                new PersistenceSupervisionConfig(null, null, reset, finalStopMessage: finalStopMsg);
            return Props.Create(() => new PersistenceSupervisor(childPropsFactory, childName, config,
                strategy ?? Actor.SupervisorStrategy.StoppingStrategy));
        }

        /// <summary>
        ///     In-memory state representation of an un-ACKed message
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
    }
}