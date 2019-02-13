// -----------------------------------------------------------------------
// <copyright file="IReceiverStateSnapshot.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Akka.Persistence.Extras
{
    /// <summary>
    ///     INTERNAL API.
    ///     Used for writing out <see cref="IReceiverState" />  data to the
    ///     snapshot store.
    /// </summary>
    public interface IReceiverStateSnapshot
    {
        IReadOnlyDictionary<string, IReadOnlyList<long>> TrackedIds { get; }

        IReadOnlyDictionary<string, DateTime> TrackedSenders { get; }
    }

    /// <summary>
    ///     INTERNAL API.
    ///     Default snapshot implementation.
    /// </summary>
    public sealed class ReceiverStateSnapshot : IReceiverStateSnapshot
    {
        public ReceiverStateSnapshot(IReadOnlyDictionary<string, IReadOnlyList<long>> trackedIds,
            IReadOnlyDictionary<string, DateTime> trackedSenders)
        {
            TrackedIds = trackedIds;
            TrackedSenders = trackedSenders;
        }

        public IReadOnlyDictionary<string, IReadOnlyList<long>> TrackedIds { get; }
        public IReadOnlyDictionary<string, DateTime> TrackedSenders { get; }
    }
}