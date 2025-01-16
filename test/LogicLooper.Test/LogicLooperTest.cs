using Cysharp.Threading;
using Cysharp.Threading.Internal;

namespace LogicLooper.Test;

public class LogicLooperTest
{
    [Theory]
    [InlineData(16.6666)] // 60fps
    [InlineData(33.3333)] // 30fps
    public async Task TargetFrameTime(double targetFrameTimeMs)
    {
        using var looper = new Cysharp.Threading.LogicLooper(TimeSpan.FromMilliseconds(targetFrameTimeMs));

        Assert.Equal(0, looper.ApproximatelyRunningActions);
        Assert.Equal(1000 / (double)targetFrameTimeMs, looper.TargetFrameRate);

        var beginTimestamp = DateTime.Now.Ticks;
        var lastTimestamp = beginTimestamp;
        var fps = 0d;
        var task = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            var now = DateTime.Now.Ticks;
            var elapsedFromBeginMilliseconds = (now - beginTimestamp) / TimeSpan.TicksPerMillisecond;
            var elapsedFromPreviousFrameMilliseconds = (now - lastTimestamp) / TimeSpan.TicksPerMillisecond;

            if (elapsedFromPreviousFrameMilliseconds == 0) return true;

            fps = (fps + (1000 / elapsedFromPreviousFrameMilliseconds)) / 2d;

            lastTimestamp = now;

            return elapsedFromBeginMilliseconds < 3000; // 3 seconds
        });

        // wait for moving action from queue to actions.
        await Task.Delay(100);

        Assert.Equal(1, looper.ApproximatelyRunningActions);

        await task;

        await Task.Delay(100);

        Assert.Equal(0, looper.ApproximatelyRunningActions);

        Assert.InRange(fps, looper.TargetFrameRate - 2, looper.TargetFrameRate + 2);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(30)]
    [InlineData(20)]
    public async Task TargetFrameRate_1(int targetFps)
    {
        using var looper = new Cysharp.Threading.LogicLooper(targetFps);

        Assert.Equal(0, looper.ApproximatelyRunningActions);
        Assert.Equal(targetFps, ((int)looper.TargetFrameRate));

        var beginTimestamp = DateTime.Now.Ticks;
        var lastTimestamp = beginTimestamp;
        var fps = 0d;
        var task = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            var now = DateTime.Now.Ticks;
            var elapsedFromBeginMilliseconds = (now - beginTimestamp) / TimeSpan.TicksPerMillisecond;
            var elapsedFromPreviousFrameMilliseconds = (now - lastTimestamp) / TimeSpan.TicksPerMillisecond;

            if (elapsedFromPreviousFrameMilliseconds == 0) return true;

            fps = (fps + (1000 / elapsedFromPreviousFrameMilliseconds)) / 2d;

            lastTimestamp = now;

            return elapsedFromBeginMilliseconds < 3000; // 3 seconds
        });

        // wait for moving action from queue to actions.
        await Task.Delay(100);

        Assert.Equal(1, looper.ApproximatelyRunningActions);

        await task;

        await Task.Delay(100);

        Assert.Equal(0, looper.ApproximatelyRunningActions);

        Assert.InRange(fps, targetFps - 2, targetFps + 2);
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
        Assert.Equal(1, count);
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
        Assert.Equal(1, count);
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
        Assert.Equal(10, currentFrame);
    }

    [Fact]
    public async Task Shutdown_Delay_Cancel()
    {
        using var looper = new Cysharp.Threading.LogicLooper(60);

        var runLoopTask = looper.RegisterActionAsync((in LogicLooperActionContext ctx) => !ctx.CancellationToken.IsCancellationRequested);
        Assert.False(runLoopTask.IsCompleted);

        var shutdownTask = looper.ShutdownAsync(TimeSpan.FromMilliseconds(500));
        await Task.Delay(50);
        Assert.True(runLoopTask.IsCompleted);
        Assert.False(shutdownTask.IsCompleted);

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
        Assert.False(runLoopTask.IsCompleted);

        var shutdownTask = looper.ShutdownAsync(TimeSpan.FromMilliseconds(500));
        await Task.Delay(50);
        Assert.True(runLoopTask.IsCompleted);
        var count2 = count;
        Assert.False(shutdownTask.IsCompleted);

        await shutdownTask;
        Assert.Equal(count, count);
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
        Assert.False(runLoopTask.IsCompleted);

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
            SleepInterop.Sleep(100);
            return !ctx.CancellationToken.IsCancellationRequested;
        });

        await Task.Delay(1000);
        await looper.ShutdownAsync(TimeSpan.Zero);

        Assert.InRange(looper.LastProcessingDuration.TotalMilliseconds, 95, 105);
    }

    [Fact]
    public async Task AsyncAction()
    {
        using var looper = new Cysharp.Threading.LogicLooper(60);
        var count = 0;
        var managedThreadId = 0;
        await looper.RegisterActionAsync(((in LogicLooperActionContext ctx) =>
        {
            managedThreadId = Thread.CurrentThread.ManagedThreadId;
            return false;
        }));

        var results = new List<int>();
        var runLoopTask = looper.RegisterActionAsync(async (LogicLooperActionContext ctx) =>
        {
            results.Add(Thread.CurrentThread.ManagedThreadId);
            await Task.Delay(100);
            results.Add(Thread.CurrentThread.ManagedThreadId);
            return ++count < 3;
        });

        await runLoopTask;

        // 2 x 3
        Assert.Equal(new[] { managedThreadId, managedThreadId, managedThreadId, managedThreadId, managedThreadId, managedThreadId }, results);
    }

    [Fact]
    public async Task AsyncAction_WithState()
    {
        using var looper = new Cysharp.Threading.LogicLooper(60);
        var managedThreadId = 0;
        await looper.RegisterActionAsync(((in LogicLooperActionContext ctx) =>
        {
            managedThreadId = Thread.CurrentThread.ManagedThreadId;
            return false;
        }));

        var results = new List<int>();
        var runLoopTask = looper.RegisterActionAsync(static async (LogicLooperActionContext ctx, List<int> results) =>
        {
            await Task.Delay(100);
            results.Add(Thread.CurrentThread.ManagedThreadId);
            return results.Count < 3;
        }, results);

        await runLoopTask;

        Assert.Equal(new[] { managedThreadId, managedThreadId, managedThreadId }, results);
    }
    
    [Fact]
    public async Task AsyncAction_Fault()
    {
        using var looper = new Cysharp.Threading.LogicLooper(60);
        var count = 0;
        var runLoopTask = looper.RegisterActionAsync(async (LogicLooperActionContext ctx) =>
        {
            count++;
            await Task.Delay(100);
            throw new InvalidOperationException();
        });

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await runLoopTask);
        await Task.Delay(100);
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData(60, 10)]
    [InlineData(30, 10)]
    [InlineData(20, 10)]
    public async Task TargetFrameRateOverride_1(int targetFps, int overrideTargetFps)
    {
        using var looper = new Cysharp.Threading.LogicLooper(targetFps);

        Assert.Equal(0, looper.ApproximatelyRunningActions);
        Assert.Equal(targetFps, ((int)looper.TargetFrameRate));

        var beginTimestamp = DateTime.Now.Ticks;
        var lastTimestamp = beginTimestamp;
        var fps = 0d;

        var lastFrameNum = 0L;
        var frameCount = -1L; // CurrentFrame will be started from 0
        var task = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            frameCount++;
            lastFrameNum = ctx.CurrentFrame;
            
            var now = DateTime.Now.Ticks;
            var elapsedFromBeginMilliseconds = (now - beginTimestamp) / TimeSpan.TicksPerMillisecond;
            var elapsedFromPreviousFrameMilliseconds = (now - lastTimestamp) / TimeSpan.TicksPerMillisecond;

            if (elapsedFromPreviousFrameMilliseconds == 0) return true;

            fps = (fps + (1000 / elapsedFromPreviousFrameMilliseconds)) / 2d;

            lastTimestamp = now;

            return elapsedFromBeginMilliseconds < 3000; // 3 seconds
        }, LooperActionOptions.Default with { TargetFrameRateOverride = overrideTargetFps });

        // wait for moving action from queue to actions.
        await Task.Delay(100);

        Assert.Equal(1, looper.ApproximatelyRunningActions);

        await task;

        await Task.Delay(100);

        Assert.Equal(0, looper.ApproximatelyRunningActions);

        Assert.Equal(lastFrameNum, frameCount);
        Assert.InRange(fps, overrideTargetFps - 2, overrideTargetFps + 2);
    }

    [Fact]
    public async Task TargetFrameRateOverride_Invalid()
    {
        using var looper = new Cysharp.Threading.LogicLooper(30);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await looper.RegisterActionAsync((in LogicLooperActionContext _) => false, LooperActionOptions.Default with { TargetFrameRateOverride = 31 }));
    }

    [Fact]
    public async Task TargetFrameRateOverride_2()
    {
        using var looper = new Cysharp.Threading.LogicLooper(30);

        Assert.Equal(0, looper.ApproximatelyRunningActions);

        var lastFrameNum = 0L;
        var overriddenFrameCount = -1L;
        var cts = new CancellationTokenSource();

        _ = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            return ctx.CurrentFrame != 4;
        });
        _ = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            return ctx.CurrentFrame != 3;
        });
        _ = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            return ctx.CurrentFrame != 2;
        });
        _ = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            return ctx.CurrentFrame != 1;
        });

        var task = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            overriddenFrameCount++;
            return !cts.IsCancellationRequested;
        }, LooperActionOptions.Default with { TargetFrameRateOverride = 1 /* 1 frame per second */ });

        // wait for moving action from queue to actions.
        await Task.Delay(1100);
        cts.Cancel();

        Assert.Equal(1, looper.ApproximatelyRunningActions);

        Assert.Equal(1, overriddenFrameCount);
    }
}
