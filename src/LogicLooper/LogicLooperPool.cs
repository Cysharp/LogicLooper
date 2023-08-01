namespace Cysharp.Threading;

/// <summary>
/// Provides a pool of loopers that can be register loop-action into the pooled looper.
/// </summary>
public sealed partial class LogicLooperPool : ILogicLooperPool, IDisposable
{
    private readonly LogicLooper[] _loopers;
    private readonly ILogicLooperPoolBalancer _balancer;

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
    {
        if (looperCount <= 0) throw new ArgumentOutOfRangeException(nameof(looperCount), "LooperCount must be more than zero.");

        _loopers = new LogicLooper[looperCount];
        for (var i = 0; i < looperCount; i++)
        {
            _loopers[i] = new LogicLooper(targetFrameTime);
        }
        _balancer = balancer ?? throw new ArgumentNullException(nameof(balancer));
    }

    /// <inheritdoc />
    public Task RegisterActionAsync(LogicLooperActionDelegate loopAction)
        => _balancer.GetPooledLooper(_loopers).RegisterActionAsync(loopAction);

    /// <inheritdoc />
    public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state)
        => _balancer.GetPooledLooper(_loopers).RegisterActionAsync(loopAction, state);

    /// <inheritdoc />
    public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction)
        => _balancer.GetPooledLooper(_loopers).RegisterActionAsync(loopAction);

    /// <inheritdoc />
    public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state)
        => _balancer.GetPooledLooper(_loopers).RegisterActionAsync(loopAction, state);

    /// <inheritdoc />
    public async Task ShutdownAsync(TimeSpan shutdownDelay)
    {
        await Task.WhenAll(_loopers.Select(x => x.ShutdownAsync(shutdownDelay)));
    }

    public void Dispose()
    {
        foreach (var looper in _loopers)
        {
            try
            {
                looper.Dispose();
            }
            catch
            {
            }
        }
    }
}
