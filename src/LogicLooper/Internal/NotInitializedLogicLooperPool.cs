namespace Cysharp.Threading.Internal;

internal class NotInitializedLogicLooperPool : ILogicLooperPool
{
    IReadOnlyList<ILogicLooper> ILogicLooperPool.Loopers => throw new NotImplementedException();

    public Task RegisterActionAsync(LogicLooperActionDelegate loopAction)
        => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

    public Task RegisterActionAsync(LogicLooperActionDelegate loopAction, LooperActionOptions options)
        => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

    public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state)
        => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

    public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options)
        => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

    public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction)
        => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

    public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction, LooperActionOptions options)
        => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

    public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state)
        => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

    public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options)
        => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

    public Task ShutdownAsync(TimeSpan shutdownDelay)
        => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

    public ILogicLooper GetLooper()
        => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

    public void Dispose() { }
}
