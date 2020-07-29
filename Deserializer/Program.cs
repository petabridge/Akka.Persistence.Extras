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
            const string filename = "Snapshot";
            
            // Read snapshot bytes
            var bytes = await File.ReadAllBytesAsync($"{filename}.bin");

            // Get configured ActorSystem
            var persistenceConfig = ConfigurationFactory.FromResource<ExtraPersistence>("Akka.Persistence.Extras.Config.akka.persistence.extras.conf");
            var system = ActorSystem.Create("Deserializer", persistenceConfig);

            // Deserilize snapshot with DDUP serializer
            var snapshot = system.Serialization.Deserialize(bytes, 501, "DDUPSNAPSHOT");
            
            // Save data to json file
            await File.WriteAllTextAsync($"{filename}.data.json", JsonConvert.SerializeObject(snapshot, Formatting.Indented));
        }
    }
}