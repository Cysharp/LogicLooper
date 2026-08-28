using Cysharp.Threading;

namespace LogicLooper.Test;

public class LogicLooperPoolSharedTest
{
    [Fact]
    public void InitializeSharedPool_CanOnlySucceedOnce()
    {
        const int concurrentCalls = 8;
        using var factory = new TrackingLogicLooperFactory(concurrentCalls);
        using var start = new ManualResetEventSlim();
        var results = new Exception[concurrentCalls];

        var tasks = Enumerable.Range(0, concurrentCalls)
            .Select(index => Task.Factory.StartNew(
                () =>
                {
                    start.Wait();
                    try
                    {
                        LogicLooperPool.InitializeSharedPool(60, 1, looperFactory: factory);
                    }
                    catch (Exception ex)
                    {
                        results[index] = ex;
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        start.Set();
        Task.WaitAll(tasks);

        try
        {
            Assert.Single(results, static x => x == null);
            Assert.Equal(concurrentCalls - 1, results.Count(static x => x is InvalidOperationException));
            Assert.Equal(concurrentCalls, factory.CreatedCount);
            Assert.Equal(concurrentCalls - 1, factory.DisposedCount);

            Assert.Throws<InvalidOperationException>(() =>
                LogicLooperPool.InitializeSharedPool(60, 1, looperFactory: factory));
            Assert.Equal(concurrentCalls, factory.CreatedCount);
        }
        finally
        {
            LogicLooperPool.Shared.Dispose();
        }

        Assert.Equal(concurrentCalls, factory.DisposedCount);
    }

    private sealed class TrackingLogicLooperFactory(int expectedCreateCount) : ILogicLooperPoolLooperFactory, IDisposable
    {
        private readonly Barrier _createBarrier = new(expectedCreateCount);
        private int _createdCount;
        private int _disposedCount;

        public int CreatedCount => Volatile.Read(ref _createdCount);
        public int DisposedCount => Volatile.Read(ref _disposedCount);

        public ILogicLooper Create(TimeSpan targetFrameTime)
        {
            Interlocked.Increment(ref _createdCount);
            if (!_createBarrier.SignalAndWait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Timed out waiting for concurrent pool creation.");
            }
            return new TrackingLogicLooper(this);
        }

        public void Dispose() => _createBarrier.Dispose();

        private sealed class TrackingLogicLooper(TrackingLogicLooperFactory owner) : ILogicLooper
        {
            public int Id => 0;
            public int ApproximatelyRunningActions => 0;
            public TimeSpan LastProcessingDuration => TimeSpan.Zero;
            public double TargetFrameRate => 60;
            public long CurrentFrame => 0;

            public Task RegisterActionAsync(LogicLooperActionDelegate loopAction) => throw new NotSupportedException();
            public Task RegisterActionAsync(LogicLooperActionDelegate loopAction, LooperActionOptions options) => throw new NotSupportedException();
            public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state) => throw new NotSupportedException();
            public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options) => throw new NotSupportedException();
            public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction) => throw new NotSupportedException();
            public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction, LooperActionOptions options) => throw new NotSupportedException();
            public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state) => throw new NotSupportedException();
            public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options) => throw new NotSupportedException();
            public Task ShutdownAsync(TimeSpan shutdownDelay) => Task.CompletedTask;
            public void Dispose() => Interlocked.Increment(ref owner._disposedCount);
        }
    }
}
