using Cysharp.Threading;

namespace LogicLooper.Test;

public class StressTest
{
    [Theory]
    [InlineData(60, 1000000, 100)]
    [InlineData(120, 1000000, 1)]
    [InlineData(120, 10000, 1000)]
    public void LogicLooperPool_Stress_1(int targetFps, int actionCount, int loopCount)
    {
        using var pool = new LogicLooperPool(targetFps, 4, RoundRobinLogicLooperPoolBalancer.Instance);

        var executedCount = 0;
        var launchedCount = 0;

        var begin = DateTime.Now;
        Parallel.For(0, actionCount, _ =>
        {
            var firstTime = true;
            var loop = 0;
            pool.RegisterActionAsync((in LogicLooperActionContext ctx) =>
            {
                if (firstTime)
                {
                    Interlocked.Increment(ref launchedCount);
                    firstTime = false;
                }
                Interlocked.Increment(ref executedCount);
                return ++loop < loopCount;
            });
        });

        while (true)
        {
            var elapsed = DateTime.Now - begin;
            Assert.True(elapsed.TotalSeconds < 20, "Timed out");

            if (executedCount >= actionCount * loopCount)
            {
                break;
            }

            Thread.Sleep(100);
        }

        Assert.Equal(actionCount, launchedCount);
        Assert.Equal(actionCount * loopCount, executedCount);
    }

    [Theory]
    [InlineData(60, 1000000, 100)]
    [InlineData(120, 1000000, 1)]
    [InlineData(120, 10000, 1000)]
    public async Task LogicLooper_Stress_1(int targetFps, int actionCount, int loopCount)
    {
        using var looper = new Cysharp.Threading.LogicLooper(targetFps);

        Assert.Equal(0, looper.ApproximatelyRunningActions);
        Assert.InRange(looper.TargetFrameRate, targetFps - 1, targetFps + 1);

        var executedCount = 0;
        var launchedCount = 0;

        var begin = DateTime.Now;
        Parallel.For(0, actionCount, _ =>
        {
            var firstTime = true;
            var loop = 0;
            looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
            {
                // the looper uses fixed-thread and loop action in single-thread.
                loop++;
                executedCount++;

                if (firstTime)
                {
                    launchedCount++;
                    firstTime = false;
                }

                return loop < loopCount;
            });
        });

        while (true)
        {
            var elapsed = DateTime.Now - begin;
            Assert.True(elapsed.TotalSeconds < 20, "Timed out");

            if (executedCount >= actionCount * loopCount)
            {
                break;
            }
            Thread.Sleep(100);
        }

        Assert.Equal(actionCount, launchedCount);
        Assert.Equal(actionCount * loopCount, executedCount);
    }
}
