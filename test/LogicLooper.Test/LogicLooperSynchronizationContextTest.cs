using Cysharp.Threading;
using Cysharp.Threading.Internal;

namespace LogicLooper.Test;

public class LogicLooperSynchronizationContextTest
{
    [Fact]
    public async Task Post()
    {
        using var looper = new ManualLogicLooper(60);
        using var syncContext = new LogicLooperSynchronizationContext(looper);

        var count = 0;
        syncContext.Post(_ =>
        {
            count++;
        }, null);
        syncContext.Post(_ =>
        {
            count++;
        }, null);
        syncContext.Post(_ =>
        {
            count++;
        }, null);

        Assert.Equal(0, count);
        looper.Tick();
        Assert.Equal(3, count);
        looper.Tick();
        Assert.Equal(3, count);
        syncContext.Post(_ =>
        {
            count++;
        }, null);
        looper.Tick();
        Assert.Equal(4, count);
    }

    [Fact]
    public async Task LooperIntegration()
    {
        using var looper = new ManualLogicLooper(60);
        using var syncContext = new LogicLooperSynchronizationContext(looper);
        SynchronizationContext.SetSynchronizationContext(syncContext); // This context is used when advancing frame within the Tick method. Use `ConfigureAwait(false)` in the following codes.

        var result = new List<string>();
        var task = looper.RegisterActionAsync(async (ctx) =>
        {
            result.Add("1"); // Frame: 1
            await Task.Delay(250);
            result.Add("2"); // Frame: 2
            return false;
        });

        looper.Tick();
        Assert.Equal(new[] { "1" }, result);

        await Task.Delay(500).ConfigureAwait(false);

        looper.Tick(); // Run continuation
        looper.Tick(); // Wait for complete action
        Assert.Equal(new[] { "1", "2" }, result);

        Assert.True(task.IsCompleted);
    }

    [Fact]
    public async Task DequeueLoopAction_NotRegisteredWhenNonAsync()
    {
        using var looper = new ManualLogicLooper(60);
        using var syncContext = new LogicLooperSynchronizationContext(looper);
        SynchronizationContext.SetSynchronizationContext(syncContext); // This context is used when advancing frame within the Tick method. Use `ConfigureAwait(false)` in the following codes.
        var t = looper.RegisterActionAsync((in LogicLooperActionContext ctx) => false);

        Assert.Equal(1, looper.ApproximatelyRunningActions);
        looper.Tick();
        Assert.Equal(0, looper.ApproximatelyRunningActions);
        Assert.True(t.IsCompleted);
    }
    
    [Fact]
    public async Task DequeueLoopAction_RegisteredWhenHasAsyncAction()
    {
        using var looper = new ManualLogicLooper(60);
        using var syncContext = new LogicLooperSynchronizationContext(looper);
        SynchronizationContext.SetSynchronizationContext(syncContext); // This context is used when advancing frame within the Tick method. Use `ConfigureAwait(false)` in the following codes.
        var t = looper.RegisterActionAsync(async (LogicLooperActionContext ctx) =>
        {
            await Task.Yield();
            return false;
        });

        Assert.Equal(1, looper.ApproximatelyRunningActions); // User-Action
        looper.Tick();
        await Task.Delay(100).ConfigureAwait(false);
        Assert.Equal(2, looper.ApproximatelyRunningActions); // User-Action + DequeLoopAction
        looper.Tick(); // Run continuation
        looper.Tick(); // Wait for complete action
        Assert.True(t.IsCompleted);
        Assert.Equal(1, looper.ApproximatelyRunningActions); // DequeueLoopAction
    }

}
