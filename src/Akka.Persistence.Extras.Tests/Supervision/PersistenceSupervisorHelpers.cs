// -----------------------------------------------------------------------
// <copyright file="PersistenceSupervisorHelpers.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using Akka.Actor;

namespace Akka.Persistence.Extras.Tests.Supervision
{
    public static class PersistenceSupervisorHelpers
    {
        public static IConfirmableMessage ToConfirmableMessage(object msg, long confirmationId)
        {
            return new ConfirmableMessageEnvelope(confirmationId, string.Empty, msg);
        }

        public static Props PersistenceSupervisorFor(Func<object, bool> isEvent,
            Func<IActorRef, Props> childPropsFactory, string childName)
        {
            return PersistenceSupervisor.PropsFor(ToConfirmableMessage, isEvent, childPropsFactory, childName,
                new ManualReset());
        }
    }
}