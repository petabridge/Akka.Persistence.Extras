using System;
using System.Collections.Generic;
using System.Text;
using Akka.Actor;
using Akka.Configuration;

namespace Akka.Persistence.Extras
{
    /// <summary>
    /// INTERNAL API.
    ///
    /// Used by the <see cref="ActorSystem"/> to create a singleton <see cref="ExtraPersistence"/>
    /// instance specific to the actor system who calls it.
    /// </summary>
    public sealed class ExtraPersistenceExt : ExtensionIdProvider<ExtraPersistence>
    {
        public override ExtraPersistence CreateExtension(ExtendedActorSystem system)
        {
            return new ExtraPersistence(system);
        }
    }

    /// <summary>
    /// <see cref="ActorSystem"/> extension used to inject all of the relevant settings.
    /// </summary>
    public sealed class ExtraPersistence : IExtension
    {
        /// <summary>
        /// Creates a new <see cref="ExtraPersistence"/> instance and injects all appropriate
        /// serialization settings into <see cref="system"/>.
        /// </summary>
        /// <param name="system">The actor system to which this instance will be bound.</param>
        public ExtraPersistence(ExtendedActorSystem system)
        {
            // forces the serialization bindings to become active
            system.Settings.InjectTopLevelFallback(DefaultConfig());

            Running = true;
        }

        /// <summary>
        /// Used to check if all of the <see cref="ExtraPersistence"/> settings are currently active.
        /// </summary>
        public bool Running { get; }

        /// <summary>
        /// Fetch the current <see cref="ExtraPersistence"/> instance.
        /// </summary>
        /// <param name="system">The <see cref="ActorSystem"/>.</param>
        /// <returns>The singleton <see cref="ExtraPersistence"/> instance associated with <see cref="system"/>.
        /// Will create the <see cref="ExtraPersistence"/> instance if one doesn't exist.</returns>
        public static ExtraPersistence For(ActorSystem system)
        {
            return system.WithExtension<ExtraPersistence, ExtraPersistenceExt>();
        }

        /// <summary>
        /// Returns the default HOCON configuration for Akka.Persistence.Extras.
        /// </summary>
        /// <returns>The built-in HOCON config.</returns>
        internal static Config DefaultConfig()
        {
            return ConfigurationFactory.FromResource<ExtraPersistence>(
                "Akka.Persistence.Extras.Config.akka.persistence.extras.conf");
        }
    }
}
