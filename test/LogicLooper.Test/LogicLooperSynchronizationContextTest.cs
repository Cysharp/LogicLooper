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
}
