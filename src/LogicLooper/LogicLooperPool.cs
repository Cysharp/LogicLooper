using Cysharp.Threading.Internal;

namespace Cysharp.Threading;

/// <summary>
/// Provides a pool of loopers that can be register loop-action into the pooled looper.
/// </summary>
public sealed partial class LogicLooperPool : ILogicLooperPool, IDisposable
{
    private readonly PooledLogicLooper[] _loopers;
    private readonly ILogicLooperPoolBalancer _balancer;
    private readonly CancellationTokenSource _shutdownTokenSource = new();

    /// <inheritdoc />
    public IReadOnlyList<ILogicLooper> Loopers => _loopers;

    /// <summary>
    /// Initialize the looper pool with specified configurations.
    /// </summary>
    /// <param name="targetFrameRate"></param>
    /// <param name="looperCount"></param>
    /// <param name="balancer"></param>
    public LogicLooperPool(int targetFrameRate, int looperCount, ILogicLooperPoolBalancer balancer)
        : this(TimeSpan.FromMilliseconds(1000 / (double)targetFrameRate), looperCount, balancer)
    { }


    /// <summary>
    /// Initialize the looper pool with specified configurations.
    /// </summary>
    /// <param name="targetFrameTime"></param>
    /// <param name="looperCount"></param>
    /// <param name="balancer"></param>
    public LogicLooperPool(TimeSpan targetFrameTime, int looperCount, ILogicLooperPoolBalancer balancer)
        : this(targetFrameTime, looperCount, balancer, DefaultLogicLooperPoolLooperFactory.Instance)
    {
    }

    /// <summary>
    /// Initialize the looper pool with specified configurations.
    /// </summary>
    /// <param name="targetFrameRate"></param>
    /// <param name="looperCount"></param>
    /// <param name="balancer"></param>
    /// <param name="looperFactory"></param>
    public LogicLooperPool(int targetFrameRate, int looperCount, ILogicLooperPoolBalancer balancer, ILogicLooperPoolLooperFactory looperFactory)
        : this(TimeSpan.FromMilliseconds(1000 / (double)targetFrameRate), looperCount, balancer, looperFactory)
    { }

    /// <summary>
    /// Initialize the looper pool with specified configurations.
    /// </summary>
    /// <param name="targetFrameTime"></param>
    /// <param name="looperCount"></param>
    /// <param name="balancer"></param>
    /// <param name="looperFactory"></param>
    public LogicLooperPool(TimeSpan targetFrameTime, int looperCount, ILogicLooperPoolBalancer balancer, ILogicLooperPoolLooperFactory looperFactory)
    {
        if (looperCount <= 0) throw new ArgumentOutOfRangeException(nameof(looperCount), "LooperCount must be more than zero.");

        _loopers = new PooledLogicLooper[looperCount];
        for (var i = 0; i < looperCount; i++)
        {
            _loopers[i] = new PooledLogicLooper(looperFactory.Create(targetFrameTime));
        }
        _balancer = balancer ?? throw new ArgumentNullException(nameof(balancer));
    }

    /// <inheritdoc />
    public Task RegisterActionAsync(LogicLooperActionDelegate loopAction)
        => GetLooper().RegisterActionAsync(loopAction);

    /// <inheritdoc />
    public Task RegisterActionAsync(LogicLooperActionDelegate loopAction, LooperActionOptions options)
        => GetLooper().RegisterActionAsync(loopAction, options);

    /// <inheritdoc />
    public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state)
        => GetLooper().RegisterActionAsync(loopAction, state);

    /// <inheritdoc />
    public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options)
        => GetLooper().RegisterActionAsync(loopAction, state, options);

    /// <inheritdoc />
    public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction)
        => GetLooper().RegisterActionAsync(loopAction);

    /// <inheritdoc />
    public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction, LooperActionOptions options)
        => GetLooper().RegisterActionAsync(loopAction, options);

    /// <inheritdoc />
    public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state)
        => GetLooper().RegisterActionAsync(loopAction, state);

    /// <inheritdoc />
    public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options)
        => GetLooper().RegisterActionAsync(loopAction, state, options);

    /// <inheritdoc />
    public async Task ShutdownAsync(TimeSpan shutdownDelay)
    {
        await Task.WhenAll(_loopers.Select(x => x.WrappedLooper.ShutdownAsync(shutdownDelay)));
    }

    /// <inheritdoc />
    public ILogicLooper GetLooper()
        => _balancer.GetPooledLooper(_loopers);

    public void Dispose()
    {
        foreach (var looper in _loopers)
        {
            try
            {
                looper.WrappedLooper.Dispose();
            }
            catch
            {
            }
        }
        _shutdownTokenSource.Cancel();
    }

    private class PooledLogicLooper : ILogicLooper
    {
        private readonly ILogicLooper _looper;

        public ILogicLooper WrappedLooper => _looper;

        public PooledLogicLooper(ILogicLooper looper)
        {
            _looper = looper ?? throw new ArgumentNullException(nameof(looper));
        }

        public int Id => _looper.Id;
        public int ApproximatelyRunningActions => _looper.ApproximatelyRunningActions;
        public TimeSpan LastProcessingDuration => _looper.LastProcessingDuration;
        public double TargetFrameRate => _looper.TargetFrameRate;
        public long CurrentFrame => _looper.CurrentFrame;
        public Task RegisterActionAsync(LogicLooperActionDelegate loopAction)
            => _looper.RegisterActionAsync(loopAction);
        public Task RegisterActionAsync(LogicLooperActionDelegate loopAction, LooperActionOptions options)
            => _looper.RegisterActionAsync(loopAction, options);
        public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state)
            => _looper.RegisterActionAsync(loopAction, state);
        public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options)
            => _looper.RegisterActionAsync(loopAction, state, options);
        public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction)
            => _looper.RegisterActionAsync(loopAction);
        public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction, LooperActionOptions options)
            => _looper.RegisterActionAsync(loopAction, options);
        public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state)
            => _looper.RegisterActionAsync(loopAction, state);
        public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options)
            => _looper.RegisterActionAsync(loopAction, state, options);

        public Task ShutdownAsync(TimeSpan shutdownDelay)
            => throw new NotSupportedException("PooledLogicLooper does not support ShutdownAsync. Use the LogicLooperPool to shutdown all loopers.");
        public void Dispose()
        {}
    }
}
