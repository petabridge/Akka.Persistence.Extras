using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Akka.Pattern;

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
        public sealed class Reset
        {
            public static readonly Reset Instance = new Reset();
            private Reset() { }
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
        public sealed class StartChild : IDeadLetterSuppression
        {
            /// <summary>
            /// TBD
            /// </summary>
            public static readonly StartChild Instance = new StartChild();
            private StartChild() { }
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

        private readonly TimeSpan _minBackoff;
        private readonly TimeSpan _maxBackoff;
        private readonly double _randomFactor;
        private readonly SupervisorStrategy _strategy;

        public PersistenceSupervisor(Props childProps, string childName,
            IBackoffReset reset,
            TimeSpan minBackoff, 
            TimeSpan maxBackoff, 
            double randomFactor, SupervisorStrategy strategy = null, Func<object, bool> finalStopMessage = null)
        {
            ChildProps = childProps;
            ChildName = childName;
            Reset = reset;
            _minBackoff = minBackoff;
            _maxBackoff = maxBackoff;
            _randomFactor = randomFactor;
            _strategy = strategy ?? Actor.SupervisorStrategy.StoppingStrategy;
            FinalStopMessage = finalStopMessage;
        }

        protected Props ChildProps { get; }
        protected string ChildName { get; }
        protected IBackoffReset Reset { get; }
        protected Func<object, bool> FinalStopMessage { get; }

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
            base.PreStart();
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
    }
}
