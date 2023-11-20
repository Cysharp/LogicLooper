namespace Cysharp.Threading;

public interface ILogicLooperPool : IDisposable
{
    /// <summary>
    /// Gets the pooled looper instances.
    /// </summary>
    IReadOnlyList<ILogicLooper> Loopers { get; }

    /// <summary>
    /// Registers a loop-frame action to a pooled looper and returns <see cref="Task"/> to wait for completion.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <returns></returns>
    Task RegisterActionAsync(LogicLooperActionDelegate loopAction);

    /// <summary>
    /// Registers a loop-frame action to a pooled looper and returns <see cref="Task"/> to wait for completion.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task RegisterActionAsync(LogicLooperActionDelegate loopAction, LooperActionOptions options);

    /// <summary>
    /// Registers a loop-frame action with state object to a pooled looper and returns <see cref="Task"/> to wait for completion.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state);

    /// <summary>
    /// Registers a loop-frame action with state object to a pooled looper and returns <see cref="Task"/> to wait for completion.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <param name="state"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options);

    /// <summary>
    /// [Experimental] Registers an async-aware loop-frame action to a pooled looper and returns <see cref="Task"/> to wait for completion.
    /// An asynchronous action is executed across multiple frames, differ from the synchronous version.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <returns></returns>
    Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction);

    /// <summary>
    /// [Experimental] Registers an async-aware loop-frame action to a pooled looper and returns <see cref="Task"/> to wait for completion.
    /// An asynchronous action is executed across multiple frames, differ from the synchronous version.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction, LooperActionOptions options);

    /// <summary>
    /// [Experimental] Registers an async-aware loop-frame action with state object to a pooled looper and returns <see cref="Task"/> to wait for completion.
    /// An asynchronous action is executed across multiple frames, differ from the synchronous version.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state);

    /// <summary>
    /// [Experimental] Registers an async-aware loop-frame action with state object to a pooled looper and returns <see cref="Task"/> to wait for completion.
    /// An asynchronous action is executed across multiple frames, differ from the synchronous version.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <param name="state"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options);

    /// <summary>
    /// Stops all action loop of the loopers.
    /// </summary>
    /// <param name="shutdownDelay"></param>
    /// <returns></returns>
    Task ShutdownAsync(TimeSpan shutdownDelay);

    /// <summary>
    /// Gets a <see cref="ILogicLooper"/> instance from the pool. This is useful when you want to explicitly register multiple actions on the same loop thread.
    /// </summary>
    /// <returns></returns>
    ILogicLooper GetLooper();
}
