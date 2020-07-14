using System;
using System.IO;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Extras;
using Akka.Persistence.Serialization;
using Newtonsoft.Json;

namespace Deserializer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Read snapshot bytes
            var bytes = await File.ReadAllBytesAsync("0068_B_.BIN");
            
            // Get configured ActorSystem
            var persistenceConfig = ConfigurationFactory.FromResource<ExtraPersistence>("Akka.Persistence.Extras.Config.akka.persistence.extras.conf");
            var system = ActorSystem.Create("Deserializer", persistenceConfig);
            
            // Deserialize snapshot
            var persistenceMessageSerializer = new PersistenceSnapshotSerializer(system as ExtendedActorSystem);
            var snapshot = persistenceMessageSerializer.FromBinary<Snapshot>(bytes);
            
            // Print snapshot to the output
            Console.Write(JsonConvert.SerializeObject(snapshot.Data));
        }
    }
}