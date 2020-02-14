using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.LogicLooper;
using FluentAssertions;
using Xunit;

namespace LogicLooper.Test
{
    public class LogicLooperTest
    {
        [Theory]
        [InlineData(60)]
        [InlineData(30)]
        [InlineData(20)]
        public async Task TargetFrameRate_1(int targetFps)
        {
            using var looper = new Cysharp.Threading.LogicLooper.LogicLooper(targetFps);

            looper.ApproximatelyRunningActions.Should().Be(0);
            looper.TargetFrameRate.Should().Be(targetFps);

            var beginTimestamp = Stopwatch.GetTimestamp();
            var lastTimestamp = beginTimestamp;
            var fps = 0d;
            var task = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
            {
                var now = Stopwatch.GetTimestamp();
                var elapsedFromBeginMilliseconds = (now - beginTimestamp) / 10000d;
                var elapsedFromPreviousFrameMilliseconds = (now - lastTimestamp) / 10000d;

                fps = (fps == 0) ? (1000 / elapsedFromPreviousFrameMilliseconds) : (fps + (1000 / elapsedFromPreviousFrameMilliseconds)) / 2d;

                lastTimestamp = now;

                return elapsedFromBeginMilliseconds < 3000; // 3 seconds
            });

            // wait for moving action from queue to actions.
            await Task.Delay(100);

            looper.ApproximatelyRunningActions.Should().Be(1);

            await task;

            await Task.Delay(100);

            looper.ApproximatelyRunningActions.Should().Be(0);

            fps.Should().BeInRange(targetFps-2, targetFps);
        }
    }
}
