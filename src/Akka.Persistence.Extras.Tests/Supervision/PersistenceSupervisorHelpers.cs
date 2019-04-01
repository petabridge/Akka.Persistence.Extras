using System;
using Akka.Actor;
using Akka.Persistence.Extras.Supervision;

namespace Akka.Persistence.Extras.Tests.Supervision
{
    public static class PersistenceSupervisorHelpers
    {
        public static IConfirmableMessage ToConfirmableMessage(object msg, long confirmationId)
        {
            return new ConfirmableMessageEnvelope(confirmationId, string.Empty, msg);
        }

        public static Props PersistenceSupervisorFor(Func<object, bool> isEvent,
            Props childProps, string childName)
        {
            return PersistenceSupervisor.PropsFor(ToConfirmableMessage, isEvent, childProps, childName, new ManualReset());
        }
    }
}