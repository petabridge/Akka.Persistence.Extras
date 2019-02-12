// -----------------------------------------------------------------------
// <copyright file="FakeTimeProvider.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;

namespace Akka.Persistence.Extras.Tests
{
    /// <summary>
    ///     INTERNAL API.
    /// </summary>
    /// <remarks>
    ///     For testing purposes with <see cref="IReceiverState" />.
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