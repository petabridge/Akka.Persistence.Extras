using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Akka.Actor;
using Akka.Configuration;
using Akka.Persistence.Extras;
using Akka.Persistence.Serialization;
using Akka.Persistence.Serialization.Proto.Msg;
using Newtonsoft.Json;

namespace Deserializer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Read snapshot bytes
            var bytes = await File.ReadAllBytesAsync("Snapshot.bin");

            // Get configured ActorSystem
            var persistenceConfig = ConfigurationFactory.FromResource<ExtraPersistence>("Akka.Persistence.Extras.Config.akka.persistence.extras.conf");
            var system = ActorSystem.Create("Deserializer", persistenceConfig);
            
            // Deserialize snapshot
            var persistenceMessageSerializer = new PersistenceSnapshotSerializer(system as ExtendedActorSystem);
            var snapshot = persistenceMessageSerializer.FromBinary<Snapshot>(bytes);
            
            // Print snapshot to the output
            Console.Write(JsonConvert.SerializeObject(snapshot.Data));
            
            /*PersistentPayload payload = PersistentPayload.Parser.ParseFrom(bytes);

            string manifest = "";
            if (payload.PayloadManifest != null) manifest = payload.PayloadManifest.ToStringUtf8();

            var data = system.Serialization.Deserialize(payload.Payload.ToByteArray(), 501, "");
            
            // Print snapshot to the output
            Console.Write(JsonConvert.SerializeObject(data));*/
        }
        
        
    }
}