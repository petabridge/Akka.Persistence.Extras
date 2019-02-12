using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;

namespace Akka.Persistence.Extras.Tests
{
    /// <summary>
    /// INTERNAL API.
    /// </summary>
    /// <remarks>
    /// For testing purposes with <see cref="IReceiverState"/>.
    /// </remarks>
    public class FakeTimeProvider : ITimeProvider
    {
        private DateTime _now;

        public FakeTimeProvider(DateTime now)
        {
            _now = now;
        }

        public DateTimeOffset Now => _now;
        public TimeSpan MonotonicClock => TimeSpan.Zero;
        public TimeSpan HighResMonotonicClock => TimeSpan.Zero;

        public void SetTime(DateTime newTime)
        {
            _now = newTime;
        }

        public void SetTime(TimeSpan additionalTime)
        {
            _now = _now + additionalTime;
        }
    }
}
