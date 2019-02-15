// -----------------------------------------------------------------------
// <copyright file="DeDuplicatingReceiverSettingsSpecs.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Akka.Persistence.Extras.Tests.DeDuplication
{
    public class DeDuplicatingReceiverSettingsSpecs
    {
        public static IEnumerable<object[]> GetTimeSpans()
        {
            yield return new object[] {TimeSpan.Zero, true};
            yield return new object[] {TimeSpan.MaxValue, true};
            yield return new object[] {TimeSpan.MinValue, true};
            yield return new object[] {TimeSpan.FromSeconds(30), false};
        }

        [Theory]
        [MemberData(nameof(GetTimeSpans))]
        public void DeDuplicatingReceiverSettings_should_reject_illegal_PruneIntervals(TimeSpan pruneInterval,
            bool shouldThrow)
        {
            Action createSettings = () =>
            {
                var settings = new DeDuplicatingReceiverSettings(ReceiveOrdering.AnyOrder, pruneInterval, 1000, 100);
            };

            if (shouldThrow)
                createSettings.Should().Throw<ArgumentOutOfRangeException>();
            else
                createSettings.Should().NotThrow();
        }

        [Theory]
        [InlineData(0, true)]
        [InlineData(1, true)]
        [InlineData(-1, true)]
        [InlineData(10, false)]
        public void DeDuplicatingReceiverSettings_should_reject_illegal_BufferSizeValues(int bufferSize,
            bool shouldThrow)
        {
            Action createSettings = () =>
            {
                var settings = new DeDuplicatingReceiverSettings(ReceiveOrdering.AnyOrder, TimeSpan.FromMinutes(30),
                    bufferSize, 100);
            };

            if (shouldThrow)
                createSettings.Should().Throw<ArgumentOutOfRangeException>();
            else
                createSettings.Should().NotThrow();
        }

        [Fact(DisplayName = "Default DeDuplicatingReceiverSettings should match the expected values")]
        public void DefaultDeDuplicatingReceiverSettings_should_be_as_Expected()
        {
            var setting = new DeDuplicatingReceiverSettings();
            setting.PruneInterval.Should().Be(TimeSpan.FromMinutes(30));
            setting.BufferSizePerSender.Should().Be(1000);
            setting.TakeSnapshotEveryNMessages.Should().Be(100);
            setting.ReceiverType.Should().Be(ReceiveOrdering.AnyOrder);
        }
    }
}