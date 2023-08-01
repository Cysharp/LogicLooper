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

        count.Should().Be(0);
        looper.Tick();
        count.Should().Be(3);
        looper.Tick();
        count.Should().Be(3);
        syncContext.Post(_ =>
        {
            count++;
        }, null);
        looper.Tick();
        count.Should().Be(4);
    }

    [Fact]
    public async Task LooperIntegration()
    {
        using var looper = new ManualLogicLooper(60);
        using var syncContext = new LogicLooperSynchronizationContext(looper);
        SynchronizationContext.SetSynchronizationContext(syncContext);

        var result = new List<string>();
        var task = looper.RegisterActionAsync(async (ctx) =>
        {
            result.Add("1"); // Frame: 1
            await Task.Delay(250);
            result.Add("2"); // Frame: 2
            return false;
        });

        looper.Tick();
        result.Should().BeEquivalentTo(new[] { "1" });

        await Task.Delay(500);

        looper.Tick();
        result.Should().BeEquivalentTo(new[] { "1", "2" });

        task.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task DequeueLoopAction_NotRegisteredWhenNonAsync()
    {
        using var looper = new ManualLogicLooper(60);
        using var syncContext = new LogicLooperSynchronizationContext(looper);
        SynchronizationContext.SetSynchronizationContext(syncContext); // This context is used when advancing frame within the Tick method.
        var t = looper.RegisterActionAsync((in LogicLooperActionContext ctx) => false);

        looper.ApproximatelyRunningActions.Should().Be(1);
        looper.Tick();
        looper.ApproximatelyRunningActions.Should().Be(0);
        t.IsCompleted.Should().BeTrue();
    }
    
    [Fact]
    public async Task DequeueLoopAction_RegisteredWhenHasAsyncAction()
    {
        using var looper = new ManualLogicLooper(60);
        using var syncContext = new LogicLooperSynchronizationContext(looper);
        SynchronizationContext.SetSynchronizationContext(syncContext); // This context is used when advancing frame within the Tick method.
        var t = looper.RegisterActionAsync(async (LogicLooperActionContext ctx) =>
        {
            await Task.Yield();
            return false;
        });

        looper.ApproximatelyRunningActions.Should().Be(1); // User-Action
        looper.Tick();
        await Task.Delay(100).ConfigureAwait(false);
        looper.ApproximatelyRunningActions.Should().Be(2); // User-Action + DequeLoopAction
        looper.Tick(); // Run continuation
        looper.Tick(); // Wait for complete action
        t.IsCompleted.Should().BeTrue();
        looper.ApproximatelyRunningActions.Should().Be(1); // DequeueLoopAction
    }

}
