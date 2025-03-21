using Cysharp.Threading.Internal;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace Cysharp.Threading;

public delegate bool LogicLooperActionDelegate(in LogicLooperActionContext ctx);
public delegate bool LogicLooperActionWithStateDelegate<in T>(in LogicLooperActionContext ctx, T state);
public delegate ValueTask<bool> LogicLooperAsyncActionDelegate(LogicLooperActionContext ctx);
public delegate ValueTask<bool> LogicLooperAsyncActionWithStateDelegate<in T>(LogicLooperActionContext ctx, T state);

/// <summary>
/// Provides update loop programming model. the looper ties thread and while-loop and call registered methods every frame.
/// </summary>
public sealed class LogicLooper : ILogicLooper, IDisposable
{
    private static int _looperSequence = 0;

    [ThreadStatic]
    private static ILogicLooper? _threadLocalLooper = default;

    /// <summary>
    /// Gets a looper of the current thread.
    /// </summary>
    public static ILogicLooper? Current
    {
        get => _threadLocalLooper;

        // NOTE: Setter to set from ManualLogicLooper for testing purposes
        internal set => _threadLocalLooper = value;
    }

    private readonly int _looperId;
    private readonly Thread _runLoopThread;
    private readonly CancellationTokenSource _ctsLoop;
    private readonly CancellationTokenSource _ctsAction;
    private readonly TaskCompletionSource<bool> _shutdownTaskAwaiter;
    private readonly double _targetFrameRate;
    private readonly int _targetFrameTimeMilliseconds;
    private readonly MinimumQueue<LooperAction> _registerQueue;
    private readonly object _lockActions = new object();
    private readonly object _lockQueue = new object();
    private readonly int _growFactor = 2;
    private readonly TimeProvider _timeProvider;

    private int _tail = 0;
    private bool _isRunning = false;
    private LooperAction[] _actions;
    private long _lastProcessingDuration = 0;
    private int _isShuttingDown = 0;
    private long _frame = 0;

    /// <inheritdoc/>
    public int Id => _looperId;

    /// <inheritdoc/>
    public int ApproximatelyRunningActions => _tail;

    /// <inheritdoc/>
    public TimeSpan LastProcessingDuration => TimeSpan.FromMilliseconds(_lastProcessingDuration);

    /// <inheritdoc/>
    public double TargetFrameRate => _targetFrameRate;

    /// <summary>
    /// Gets a unique identifier of the managed thread.
    /// </summary>
    internal int ThreadId => _runLoopThread.ManagedThreadId;

    /// <inheritdoc/>
    public long CurrentFrame => _frame;

    public LogicLooper(int targetFrameRate, int initialActionsCapacity = 16)
        : this(TimeSpan.FromMilliseconds(1000 / (double)targetFrameRate), initialActionsCapacity)
    {
    }

    public LogicLooper(TimeSpan targetFrameTime, int initialActionsCapacity = 16)
    {
        _targetFrameRate = 1000 / targetFrameTime.TotalMilliseconds;
        _looperId = Interlocked.Increment(ref _looperSequence);
        _ctsLoop = new CancellationTokenSource();
        _ctsAction = new CancellationTokenSource();
        _targetFrameTimeMilliseconds = (int)targetFrameTime.TotalMilliseconds;
        _registerQueue = new MinimumQueue<LooperAction>(10);
        _runLoopThread = new Thread(StartRunLoop)
        {
            Name = $"{typeof(LogicLooper).Name}-{_looperId}",
            IsBackground = true,
            Priority = ThreadPriority.AboveNormal,
        };
        _shutdownTaskAwaiter = new TaskCompletionSource<bool>();
        _actions = new LooperAction[initialActionsCapacity];
        _timeProvider = TimeProvider.System;

        _runLoopThread.Start(this);
    }

    /// <summary>
    /// Registers a loop-frame action to the looper and returns <see cref="Task"/> to wait for completion.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <returns></returns>
    public Task RegisterActionAsync(LogicLooperActionDelegate loopAction)
        => RegisterActionAsync(loopAction, LooperActionOptions.Default);

    /// <summary>
    /// Registers a loop-frame action to the looper and returns <see cref="Task"/> to wait for completion.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public Task RegisterActionAsync(LogicLooperActionDelegate loopAction, LooperActionOptions options)
    {
        var action = new LooperAction(DelegateHelper.GetWrapper(), loopAction, null, options, _timeProvider);
        return RegisterActionAsyncCore(action);
    }

    /// <summary>
    /// Registers a loop-frame action with state object to the looper and returns <see cref="Task"/> to wait for completion.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state)
        => RegisterActionAsync(loopAction, state, LooperActionOptions.Default);

    /// <summary>
    /// Registers a loop-frame action with state object to the looper and returns <see cref="Task"/> to wait for completion.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <param name="state"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options)
    {
        var action = new LooperAction(DelegateHelper.GetWrapper<TState>(), loopAction, state, options, _timeProvider);
        return RegisterActionAsyncCore(action);
    }

    /// <summary>
    /// [Experimental] Registers a async-aware loop-frame action to the looper and returns <see cref="Task"/> to wait for completion.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <returns></returns>
    public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction)
        => RegisterActionAsync(loopAction, LooperActionOptions.Default);

    /// <summary>
    /// [Experimental] Registers a async-aware loop-frame action to the looper and returns <see cref="Task"/> to wait for completion.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public async Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction, LooperActionOptions options)
    {
        var action = new LooperAction(DelegateHelper.GetWrapper(), DelegateHelper.ConvertAsyncToSync(loopAction), null, options, _timeProvider);
        await RegisterActionAsyncCore(action);
    }

    /// <summary>
    /// [Experimental] Registers a async-aware loop-frame action with state object to the looper and returns <see cref="Task"/> to wait for completion.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <param name="state"></param>
    /// <returns></returns>
    public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state)
        => RegisterActionAsync<TState>(loopAction, state, LooperActionOptions.Default);

    /// <summary>
    /// [Experimental] Registers a async-aware loop-frame action with state object to the looper and returns <see cref="Task"/> to wait for completion.
    /// </summary>
    /// <param name="loopAction"></param>
    /// <param name="state"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public async Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state, LooperActionOptions options)
    {
        var action = new LooperAction(DelegateHelper.GetWrapper<TState>(), DelegateHelper.ConvertAsyncToSync(loopAction), state, options, _timeProvider);
        await RegisterActionAsyncCore(action);
    }

    /// <summary>
    /// Stops the action loop of the looper.
    /// </summary>
    /// <param name="shutdownDelay"></param>
    public async Task ShutdownAsync(TimeSpan shutdownDelay)
    {
        if (Interlocked.CompareExchange(ref _isShuttingDown, 1, 0) == 0)
        {
            _ctsAction.Cancel();

            if (shutdownDelay == TimeSpan.Zero)
            {
                _ctsLoop.Cancel();
            }
            else
            {
                _ctsLoop.CancelAfter(shutdownDelay);
            }
        }

        await _shutdownTaskAwaiter.Task.ConfigureAwait(false);
    }

    public void Dispose()
    {
        ShutdownAsync(TimeSpan.Zero).GetAwaiter().GetResult();
    }

    private Task RegisterActionAsyncCore(LooperAction action)
    {
        if (action.Options.TargetFrameRateOverride is { } targetFrameRateOverride && targetFrameRateOverride > this.TargetFrameRate)
        {
            throw new InvalidOperationException("Option 'TargetFrameRateOverride' must be less than Looper's target frame rate.");
        }

        lock (_lockQueue)
        {
            if (_isRunning)
            {
                _registerQueue.Enqueue(action);
                return action.Future.Task;
            }
        }

        lock (_lockActions)
        {
            if (_actions.Length == _tail)
            {
                Array.Resize(ref _actions, checked(_tail * _growFactor));
            }
            _actions[_tail++] = action;
        }

        return action.Future.Task;
    }

    private static void StartRunLoop(object? state)
    {
        var logicLooper = ((LogicLooper)state!);
        _threadLocalLooper = logicLooper;
        logicLooper.RunLoop();
    }

    private void RunLoop()
    {
        var lastTimestamp = _timeProvider.GetTimestamp();

        var syncContext = new LogicLooperSynchronizationContext(this);
        SynchronizationContext.SetSynchronizationContext(syncContext);

        while (!_ctsLoop.IsCancellationRequested)
        {
            var begin = _timeProvider.GetTimestamp();

            lock (_lockQueue)
            {
                _isRunning = true;
            }

            lock (_lockActions)
            {
                var elapsedTimeFromPreviousFrame = _timeProvider.GetElapsedTime(lastTimestamp, begin);
                lastTimestamp = begin;

                var ctx = new LogicLooperActionContext(this, _frame++, begin, elapsedTimeFromPreviousFrame, _ctsAction.Token);

                var j = _tail - 1;
                for (var i = 0; i < _actions.Length; i++)
                {
                    ref var action = ref _actions[i];

                    // Found an action and invoke it.
                    if (action.Action != null)
                    {
                        if (!InvokeAction(ctx, ref action, begin, _ctsAction.Token))
                        {
                            action = default;
                        }
                        continue;
                    }

                    // Invoke actions from tail.
                    while (i < j)
                    {
                        ref var actionFromTail = ref _actions[j];

                        j--; // consumed

                        if (actionFromTail.Action != null)
                        {
                            if (!InvokeAction(ctx, ref actionFromTail, begin, _ctsAction.Token))
                            {
                                actionFromTail = default;
                                continue; // Continue the reverse loop flow.
                            }
                            else
                            {
                                action = actionFromTail; // Swap the null element and the action.
                                actionFromTail = default;
                                goto NextActionLoop; // Resume to the regular flow.
                            }
                        }
                    }

                    _tail = i;
                    break;

NextActionLoop:
                    continue;
                }

                lock (_lockQueue)
                {
                    _isRunning = false;

                    while (_registerQueue.Count != 0)
                    {
                        if (_actions.Length == _tail)
                        {
                            Array.Resize(ref _actions, checked(_tail * _growFactor));
                        }
                        _actions[_tail++] = _registerQueue.Dequeue();
                    }
                }
            }

            var now = _timeProvider.GetTimestamp();
            var elapsedMilliseconds = (int)(_timeProvider.GetElapsedTime(begin, now).TotalMilliseconds);
            _lastProcessingDuration = elapsedMilliseconds;

            var waitForNextFrameMilliseconds = (int)(_targetFrameTimeMilliseconds - elapsedMilliseconds);
            if (waitForNextFrameMilliseconds > 0)
            {
                SleepInterop.Sleep(waitForNextFrameMilliseconds);
            }
        }

        _shutdownTaskAwaiter.SetResult(true);
    }

    private bool InvokeAction(in LogicLooperActionContext ctx, ref LooperAction action, long begin, CancellationToken actionStoppingToken)
    {
        var hasNext = true;
        if (action.LoopActionOverrideState.IsOverridden)
        {
            // If the action with fps override haven't reached the next invoke timestamp, we don't need to perform the action.
            if (action.LoopActionOverrideState.NextScheduledTimestamp > ctx.FrameBeginTimestamp)
            {
                return true;
            }

            var exceededTimestamp = action.LoopActionOverrideState.NextScheduledTimestamp == 0 ? 0 : ctx.FrameBeginTimestamp - action.LoopActionOverrideState.NextScheduledTimestamp;
            var elapsedTimeFromPreviousFrameOverride = _timeProvider.GetElapsedTime(begin, action.LoopActionOverrideState.LastInvokedAt);
            var overrideCtx = new LogicLooperActionContext(this, action.LoopActionOverrideState.Frame++, begin, elapsedTimeFromPreviousFrameOverride, actionStoppingToken);

            hasNext = InvokeActionCore(overrideCtx, ref action);

            action.LoopActionOverrideState.LastInvokedAt = begin;
            action.LoopActionOverrideState.NextScheduledTimestamp = begin + action.LoopActionOverrideState.TargetFrameTimeTimestamp - exceededTimestamp;
        }
        else
        {
            hasNext = InvokeActionCore(ctx, ref action);
        }

        return hasNext;
    }

    private static bool InvokeActionCore(in LogicLooperActionContext ctx, ref LooperAction action)
    {
        try
        {
            var hasNext = action.Invoke(ctx);

            if (!hasNext)
            {
                action.Future.SetResult(true);
            }

            return hasNext;
        }
        catch (Exception ex)
        {
            action.Future.SetException(ex);
        }

        return false;
    }

    internal class DelegateHelper
    {
        private static InternalLogicLooperActionDelegate _wrapper => (object wrappedDelegate, in LogicLooperActionContext ctx, object? state) => ((LogicLooperActionDelegate)wrappedDelegate)(ctx);

        public static InternalLogicLooperActionDelegate GetWrapper<T>() => Cache<T>.Wrapper;
        public static InternalLogicLooperActionDelegate GetWrapper() => _wrapper;

        public static LogicLooperActionDelegate ConvertAsyncToSync(LogicLooperAsyncActionDelegate loopAction)
        {
            // TODO: perf
            var runningTask = default(ValueTask<bool>?);
            return LoopActionSync;

            bool LoopActionSync(in LogicLooperActionContext ctx)
            {
                runningTask ??= loopAction(ctx);

                if (runningTask is { IsCompleted: true } completedTask)
                {
                    var result = completedTask.GetAwaiter().GetResult();
                    runningTask = null;
                    return result;
                }

                return true;
            }
        }

        public static LogicLooperActionWithStateDelegate<TState> ConvertAsyncToSync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction)
        {
            // TODO: perf
            var runningTask = default(ValueTask<bool>?);
            return LoopActionSync;

            bool LoopActionSync(in LogicLooperActionContext ctx, TState state)
            {
                runningTask ??= loopAction(ctx, state);

                if (runningTask is { IsCompleted: true } completedTask)
                {
                    var result = completedTask.GetAwaiter().GetResult();
                    runningTask = null;
                    return result;
                }

                return true;
            }
        }

        static class Cache<T>
        {
            public static InternalLogicLooperActionDelegate Wrapper => (object wrappedDelegate, in LogicLooperActionContext ctx, object? state) => ((LogicLooperActionWithStateDelegate<T>)wrappedDelegate)(ctx, (T)state!);
        }
    }

    internal delegate bool InternalLogicLooperActionDelegate(object wrappedDelegate, in LogicLooperActionContext ctx, object? state);

    internal struct LooperAction
    {
        public DateTimeOffset BeginAt { get; }
        public object? State { get; }
        public Delegate? Action { get; }
        public InternalLogicLooperActionDelegate ActionWrapper { get; }
        public TaskCompletionSource<bool> Future { get; }
        public LooperActionOptions Options { get; }

        public LoopActionOverrideState LoopActionOverrideState;

        public LooperAction(InternalLogicLooperActionDelegate actionWrapper, Delegate action, object? state, LooperActionOptions options, TimeProvider timeProvider)
        {
            BeginAt = DateTimeOffset.Now;
            ActionWrapper = actionWrapper ?? throw new ArgumentNullException(nameof(actionWrapper));
            Action = action ?? throw new ArgumentNullException(nameof(action));
            State = state;
            Future = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Options = options;
            LoopActionOverrideState = default;

            if (options.TargetFrameRateOverride is { } targetFrameRateOverride)
            {
                var timestampsToTicks = TimeSpan.TicksPerSecond / (double)timeProvider.TimestampFrequency;
                LoopActionOverrideState = new LoopActionOverrideState(isOverridden: true)
                {
                    TargetFrameTimeTimestamp = (long)(TimeSpan.FromMilliseconds(1000 / (double)targetFrameRateOverride).Ticks / timestampsToTicks),
                };
            }
        }

        public bool Invoke(in LogicLooperActionContext ctx)
        {
            return ActionWrapper(Action!, ctx, State);
        }
    }

    internal struct LoopActionOverrideState
    {
        public bool IsOverridden { get; }

        public int Frame { get; set; }
        public long NextScheduledTimestamp { get; set; }
        public long LastInvokedAt { get; set; }
        public long TargetFrameTimeTimestamp { get; set; }

        public LoopActionOverrideState(bool isOverridden)
        {
            IsOverridden = isOverridden;
        }
    }
}
