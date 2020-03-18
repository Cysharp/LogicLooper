using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading;
using FluentAssertions;
using Xunit;

namespace LogicLooper.Test
{
    public class LogicLooperTest
    {
        [Theory]
        [InlineData(16.6666)] // 60fps
        [InlineData(33.3333)] // 30fps
        public async Task TargetFrameTime(double targetFrameTimeMs)
        {
            using var looper = new Cysharp.Threading.LogicLooper(TimeSpan.FromMilliseconds(targetFrameTimeMs));

            looper.ApproximatelyRunningActions.Should().Be(0);
            looper.TargetFrameRate.Should().Be(1000 / (double)targetFrameTimeMs);

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

            fps.Should().BeInRange(looper.TargetFrameRate - 2, looper.TargetFrameRate + 2);
        }

        [Theory]
        [InlineData(60)]
        [InlineData(30)]
        [InlineData(20)]
        public async Task TargetFrameRate_1(int targetFps)
        {
            using var looper = new Cysharp.Threading.LogicLooper(targetFps);

            looper.ApproximatelyRunningActions.Should().Be(0);
            ((int)looper.TargetFrameRate).Should().Be(targetFps);

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

            fps.Should().BeInRange(targetFps-2, targetFps + 2);
        }

        [Fact]
        public async Task Exit()
        {
            using var looper = new Cysharp.Threading.LogicLooper(60);

            var count = 0;
            await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
            {
                count++;
                return false;
            });

            await Task.Delay(100);
            count.Should().Be(1);
        }

        [Fact]
        public async Task Throw()
        {
            using var looper = new Cysharp.Threading.LogicLooper(60);

            var count = 0;
            await Assert.ThrowsAsync<Exception>(() => looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
            {
                count++;
                throw new Exception("Throw from inside loop");
            }));

            await Task.Delay(100);
            count.Should().Be(1);
        }

        [Fact]
        public async Task CurrentFrame()
        {
            using var looper = new Cysharp.Threading.LogicLooper(60);

            var currentFrame = 0L;
            await  looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
            {
                currentFrame = ctx.CurrentFrame;
                return currentFrame != 10;
            });

            await Task.Delay(100);
            currentFrame.Should().Be(10);
        }

        [Fact]
        public async Task Shutdown_Delay_Cancel()
        {
            using var looper = new Cysharp.Threading.LogicLooper(60);

            var runLoopTask = looper.RegisterActionAsync((in LogicLooperActionContext ctx) => !ctx.CancellationToken.IsCancellationRequested);
            runLoopTask.IsCompleted.Should().BeFalse();

            var shutdownTask = looper.ShutdownAsync(TimeSpan.FromMilliseconds(500));
            await Task.Delay(50);
            runLoopTask.IsCompleted.Should().BeTrue();
            shutdownTask.IsCompleted.Should().BeFalse();

            await shutdownTask;
        }


        [Fact]
        public async Task Shutdown_Delay_Cancel_2()
        {
            using var looper = new Cysharp.Threading.LogicLooper(60);

            var count = 0;
            var runLoopTask = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
            {
                count++;
                return !ctx.CancellationToken.IsCancellationRequested;
            });
            runLoopTask.IsCompleted.Should().BeFalse();

            var shutdownTask = looper.ShutdownAsync(TimeSpan.FromMilliseconds(500));
            await Task.Delay(50);
            runLoopTask.IsCompleted.Should().BeTrue();
            var count2 = count;
            shutdownTask.IsCompleted.Should().BeFalse();

            await shutdownTask;
            count.Should().Be(count);
        }

        [Fact]
        public async Task Shutdown_Immediately()
        {
            using var looper = new Cysharp.Threading.LogicLooper(1);

            var signal = new ManualResetEventSlim();
            var runLoopTask = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
            {
                signal.Set();
                return !ctx.CancellationToken.IsCancellationRequested;
            });
            runLoopTask.IsCompleted.Should().BeFalse();

            signal.Wait();
            var shutdownTask = looper.ShutdownAsync(TimeSpan.Zero);
            await shutdownTask;

            //runLoopTask.IsCompleted.Should().BeFalse(); // When the looper thread is waiting for next cycle, the loop task should not be completed.
        }


        [Fact]
        public async Task LastProcessingDuration()
        {
            using var looper = new Cysharp.Threading.LogicLooper(60);

            var runLoopTask = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
            {
                Thread.Sleep(100);
                return !ctx.CancellationToken.IsCancellationRequested;
            });

            await Task.Delay(1000);
            await looper.ShutdownAsync(TimeSpan.Zero);

            looper.LastProcessingDuration.TotalMilliseconds.Should().BeInRange(95, 105);
        }
    }
}
