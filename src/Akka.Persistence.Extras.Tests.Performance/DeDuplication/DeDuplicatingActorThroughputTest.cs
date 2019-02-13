// -----------------------------------------------------------------------
// <copyright file="UnitTest1.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using Akka.Actor;
using NBench;

namespace Akka.AtLeastOnceDeliveryJournaling.Tests.Performance
{
    public class DeDuplicatingActorThroughputTest
    {
        public const string MsgRcvCounter = "MessagesProcessed";
        private Counter _opsCounter;
        private ActorSystem _actorSystem;

        [PerfSetup]
        public void Setup(BenchmarkContext context)
        {
            _opsCounter = context.GetCounter(MsgRcvCounter);
        }

        [PerfBenchmark(NumberOfIterations = 5, RunMode = RunMode.Throughput, RunTimeMilliseconds = 1000)]
        [CounterMeasurement(MsgRcvCounter)]
        [GcMeasurement(GcMetric.TotalCollections, GcGeneration.AllGc)]
        [MemoryMeasurement(MemoryMetric.TotalBytesAllocated)]
        public void TestMethod1()
        {
            _opsCounter.Increment();
        }
    }
}