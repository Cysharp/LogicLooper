using Cysharp.Threading;

namespace LogicLooper.Test;

public class LogicLooperPoolTest
{
    [Fact]
    public void Create()
    {
        using var pool = new LogicLooperPool(60, 4, RoundRobinLogicLooperPoolBalancer.Instance);
        Assert.Equal(4, pool.Loopers.Count());
        Assert.InRange(pool.Loopers[0].TargetFrameRate, 60, 60.1);
    }

    [Fact]
    public void Create_TimeSpan()
    {
        using var pool = new LogicLooperPool(TimeSpan.FromMilliseconds(16.666), 4, RoundRobinLogicLooperPoolBalancer.Instance);
        Assert.Equal(4, pool.Loopers.Count());
        Assert.InRange(pool.Loopers[0].TargetFrameRate, 60, 60.1);
    }

    [Fact]
    public void RegisterActionAsync()
    {
        using var pool = new LogicLooperPool(60, 4, RoundRobinLogicLooperPoolBalancer.Instance);

        var actionCount = 50000;
        var loopCount = 10;
        var executedCount = 0;

        Parallel.For(0, actionCount, _ =>
        {
            var loop = 0;
            pool.RegisterActionAsync((in LogicLooperActionContext ctx) =>
            {
                Interlocked.Increment(ref executedCount);
                return ++loop < loopCount;
            });
        });

        Thread.Sleep(1000);

        Assert.Equal(actionCount * loopCount, executedCount);
    }

    [Fact]
    public void GetLooper()
    {
        using var pool = new LogicLooperPool(60, 4, new FakeSequentialLogicLooperPoolBalancer());
        Assert.Equal(pool.Loopers[0], pool.GetLooper());
        Assert.Equal(pool.Loopers[1], pool.GetLooper());
        Assert.Equal(pool.Loopers[2], pool.GetLooper());
        Assert.Equal(pool.Loopers[3], pool.GetLooper());
        Assert.Equal(pool.Loopers[0], pool.GetLooper());
    }

    class FakeSequentialLogicLooperPoolBalancer : ILogicLooperPoolBalancer
    {
        private int _count;
        public Cysharp.Threading.LogicLooper GetPooledLooper(Cysharp.Threading.LogicLooper[] pooledLoopers)
        {
            return pooledLoopers[_count++ % pooledLoopers.Length];
        }
    }
}
