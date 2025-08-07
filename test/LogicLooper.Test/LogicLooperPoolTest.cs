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

    [Fact]
    public void PooledLogicLooper_Dispose_Noop()
    {
        var looperFactory = new FakeLogicLooperPoolLooperFactory();
        using var pool = new LogicLooperPool(60, 4, RoundRobinLogicLooperPoolBalancer.Instance, looperFactory);
        var pooled1 = pool.GetLooper();
        var pooled2 = pool.GetLooper();

        Assert.Equal(4, looperFactory.CreatedLoopers.Count);
        Assert.False(looperFactory.CreatedLoopers[0].IsDisposed);
        Assert.False(looperFactory.CreatedLoopers[1].IsDisposed);
        Assert.False(looperFactory.CreatedLoopers[2].IsDisposed);
        Assert.False(looperFactory.CreatedLoopers[3].IsDisposed);

        pooled1.Dispose();
        pooled2.Dispose();

        Assert.False(looperFactory.CreatedLoopers[0].IsDisposed);
        Assert.False(looperFactory.CreatedLoopers[1].IsDisposed);
        Assert.False(looperFactory.CreatedLoopers[2].IsDisposed);
        Assert.False(looperFactory.CreatedLoopers[3].IsDisposed);
    }

    [Fact]
    public void PooledLogicLooper_DisposeFromLooperPool()
    {
        var looperFactory = new FakeLogicLooperPoolLooperFactory();
        var pool = new LogicLooperPool(60, 4, RoundRobinLogicLooperPoolBalancer.Instance, looperFactory);

        Assert.Equal(4, looperFactory.CreatedLoopers.Count);
        Assert.False(looperFactory.CreatedLoopers[0].IsDisposed);
        Assert.False(looperFactory.CreatedLoopers[1].IsDisposed);
        Assert.False(looperFactory.CreatedLoopers[2].IsDisposed);
        Assert.False(looperFactory.CreatedLoopers[3].IsDisposed);

        pool.Dispose();

        Assert.True(looperFactory.CreatedLoopers[0].IsDisposed);
        Assert.True(looperFactory.CreatedLoopers[1].IsDisposed);
        Assert.True(looperFactory.CreatedLoopers[2].IsDisposed);
        Assert.True(looperFactory.CreatedLoopers[3].IsDisposed);
    }

    [Fact]
    public async Task PooledLogicLooper_ShutdownAsync_NotSupported()
    {
        var looperFactory = new FakeLogicLooperPoolLooperFactory();
        using var pool = new LogicLooperPool(60, 4, RoundRobinLogicLooperPoolBalancer.Instance, looperFactory);
        var pooled1 = pool.GetLooper();
        var pooled2 = pool.GetLooper();

        Assert.Equal(4, looperFactory.CreatedLoopers.Count);
        Assert.False(looperFactory.CreatedLoopers[0].IsShutdownRequested);
        Assert.False(looperFactory.CreatedLoopers[1].IsShutdownRequested);
        Assert.False(looperFactory.CreatedLoopers[2].IsShutdownRequested);
        Assert.False(looperFactory.CreatedLoopers[3].IsShutdownRequested);

        await Assert.ThrowsAsync<NotSupportedException>(() => pooled1.ShutdownAsync(TimeSpan.FromSeconds(1)));
        await Assert.ThrowsAsync<NotSupportedException>(() => pooled2.ShutdownAsync(TimeSpan.FromSeconds(1)));
        Assert.False(looperFactory.CreatedLoopers[0].IsShutdownRequested);
        Assert.False(looperFactory.CreatedLoopers[1].IsShutdownRequested);
        Assert.False(looperFactory.CreatedLoopers[2].IsShutdownRequested);
        Assert.False(looperFactory.CreatedLoopers[3].IsShutdownRequested);
    }

    [Fact]
    public async Task PooledLogicLooper_ShutdownAsyncFromLooperPool()
    {
        var looperFactory = new FakeLogicLooperPoolLooperFactory();
        using var pool = new LogicLooperPool(60, 4, RoundRobinLogicLooperPoolBalancer.Instance, looperFactory);
        var pooled1 = pool.GetLooper();
        var pooled2 = pool.GetLooper();

        Assert.Equal(4, looperFactory.CreatedLoopers.Count);
        Assert.False(looperFactory.CreatedLoopers[0].IsShutdownRequested);
        Assert.False(looperFactory.CreatedLoopers[1].IsShutdownRequested);
        Assert.False(looperFactory.CreatedLoopers[2].IsShutdownRequested);
        Assert.False(looperFactory.CreatedLoopers[3].IsShutdownRequested);

        await pool.ShutdownAsync(TimeSpan.FromSeconds(1));
        Assert.True(looperFactory.CreatedLoopers[0].IsShutdownRequested);
        Assert.True(looperFactory.CreatedLoopers[1].IsShutdownRequested);
        Assert.True(looperFactory.CreatedLoopers[2].IsShutdownRequested);
        Assert.True(looperFactory.CreatedLoopers[3].IsShutdownRequested);
    }

    class FakeSequentialLogicLooperPoolBalancer : ILogicLooperPoolBalancer
    {
        private int _count;
        public Cysharp.Threading.ILogicLooper GetPooledLooper(Cysharp.Threading.ILogicLooper[] pooledLoopers)
        {
            return pooledLoopers[_count++ % pooledLoopers.Length];
        }
    }

    class FakeLogicLooperPoolLooperFactory : ILogicLooperPoolLooperFactory
    {
        public List<LogicLooper> CreatedLoopers { get; } = new();

        public ILogicLooper Create(TimeSpan targetFrameTime)
        {
            var looper = new LogicLooper();
            CreatedLoopers.Add(looper);
            return looper;
        }

        public class LogicLooper : ILogicLooper
        {
            public bool IsDisposed { get; set; }
            public bool IsShutdownRequested { get; set; }

            public void Dispose()
            {
                IsDisposed = true;
            }

            public int Id { get; }
            public int ApproximatelyRunningActions { get; }
            public TimeSpan LastProcessingDuration { get; }
            public double TargetFrameRate { get; }
            public long CurrentFrame { get; }
            public Task RegisterActionAsync(LogicLooperActionDelegate loopAction)
            {
                throw new NotImplementedException();
            }

            public Task RegisterActionAsync(LogicLooperActionDelegate loopAction, LooperActionOptions options)
            {
                throw new NotImplementedException();
            }

            public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state)
            {
                throw new NotImplementedException();
            }

            public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options)
            {
                throw new NotImplementedException();
            }

            public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction)
            {
                throw new NotImplementedException();
            }

            public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction, LooperActionOptions options)
            {
                throw new NotImplementedException();
            }

            public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state)
            {
                throw new NotImplementedException();
            }

            public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options)
            {
                throw new NotImplementedException();
            }

            public Task ShutdownAsync(TimeSpan shutdownDelay)
            {
                IsShutdownRequested = true;
                return Task.CompletedTask;
            }
        }
    }
}
