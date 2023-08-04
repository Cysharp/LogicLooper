using Cysharp.Threading;

namespace LogicLooper.Test;

public class LogicLooperPoolTest
{
    [Fact]
    public void Create()
    {
        using var pool = new LogicLooperPool(60, 4, RoundRobinLogicLooperPoolBalancer.Instance);
        pool.Loopers.Should().HaveCount(4);
        pool.Loopers[0].TargetFrameRate.Should().BeInRange(60, 60.1);
    }

    [Fact]
    public void Create_TimeSpan()
    {
        using var pool = new LogicLooperPool(TimeSpan.FromMilliseconds(16.666), 4, RoundRobinLogicLooperPoolBalancer.Instance);
        pool.Loopers.Should().HaveCount(4);
        pool.Loopers[0].TargetFrameRate.Should().BeInRange(60, 60.1);
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

        executedCount.Should().Be(actionCount * loopCount);
    }

    [Fact]
    public void GetLooper()
    {
        using var pool = new LogicLooperPool(60, 4, new FakeSequentialLogicLooperPoolBalancer());
        pool.GetLooper().Should().Be(pool.Loopers[0]);
        pool.GetLooper().Should().Be(pool.Loopers[1]);
        pool.GetLooper().Should().Be(pool.Loopers[2]);
        pool.GetLooper().Should().Be(pool.Loopers[3]);
        pool.GetLooper().Should().Be(pool.Loopers[0]);
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
