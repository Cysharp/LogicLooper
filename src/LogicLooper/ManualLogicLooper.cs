namespace Cysharp.Threading;

/// <summary>
/// Implements <see cref="ILogicLooper"/> to loop update frame manually.
/// </summary>
public sealed class ManualLogicLooper : ILogicLooper
{
    private readonly List<LogicLooper.LooperAction> _actions = new List<LogicLooper.LooperAction>();
    private readonly CancellationTokenSource _ctsAction = new CancellationTokenSource();
    private int _frame;

    /// <inheritdoc />
    public int Id => 0;

    /// <inheritdoc />
    public int ApproximatelyRunningActions => _actions.Count;

    /// <inheritdoc />
    public TimeSpan LastProcessingDuration => TimeSpan.Zero;

    /// <inheritdoc />
    public double TargetFrameRate { get; }

    public ManualLogicLooper(double targetFrameRate)
    {
        if (targetFrameRate == 0) throw new ArgumentOutOfRangeException(nameof(targetFrameRate), "TargetFrameRate must be greater than 0.");
        TargetFrameRate = targetFrameRate;
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <summary>
    /// Ticks the frame of the current looper.
    /// </summary>
    /// <param name="frameCount"></param>
    public bool Tick(int frameCount)
    {
        var result = true;
        for (var i = 0; i < frameCount; i++)
        {
            result &= Tick();
        }

        return result;
    }

    /// <summary>
    /// Ticks the frame of the current looper.
    /// </summary>
    /// <returns></returns>
    public bool Tick()
    {
        var ctx = new LogicLooperActionContext(this, _frame++, TimeSpan.FromMilliseconds(1000 / TargetFrameRate) /* Fixed Time */, _ctsAction.Token);
        var completed = new List<LogicLooper.LooperAction>();
        lock (_actions)
        {
            foreach (var action in _actions.ToArray())
            {
                if (!InvokeAction(ctx, action))
                {
                    completed.Add(action);
                }
            }

            foreach (var completedAction in completed)
            {
                _actions.Remove(completedAction);
            }

            return _actions.Count != 0;
        }
    }

    /// <summary>
    /// Ticks the frame of the current looper while the predicate returns <c>true</c>.
    /// </summary>
    public void TickWhile(Func<bool> predicate)
    {
        while (predicate())
        {
            Tick();
        }
    }

    /// <inheritdoc />
    public Task RegisterActionAsync(LogicLooperActionDelegate loopAction)
    {
        var action = new LogicLooper.LooperAction(LogicLooper.DelegateHelper.GetWrapper(), loopAction, default);
        lock (_actions)
        {
            _actions.Add(action);
        }
        return action.Future.Task;
    }

    /// <inheritdoc />
    public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state)
    {
        var action = new LogicLooper.LooperAction(LogicLooper.DelegateHelper.GetWrapper<TState>(), loopAction, state);
        lock (_actions)
        {
            _actions.Add(action);
        }
        return action.Future.Task;
    }
    
    /// <inheritdoc />
    public Task RegisterActionAsync(LogicLooperAsyncActionDelegate loopAction)
    {
        var action = new LogicLooper.LooperAction(LogicLooper.DelegateHelper.GetWrapper(), LogicLooper.DelegateHelper.ConvertAsyncToSync(loopAction), default);
        lock (_actions)
        {
            _actions.Add(action);
        }
        return action.Future.Task;
    }

    /// <inheritdoc />
    public Task RegisterActionAsync<TState>(LogicLooperAsyncActionWithStateDelegate<TState> loopAction, TState state)
    {
        var action = new LogicLooper.LooperAction(LogicLooper.DelegateHelper.GetWrapper<TState>(), LogicLooper.DelegateHelper.ConvertAsyncToSync(loopAction), state);
        lock (_actions)
        {
            _actions.Add(action);
        }
        return action.Future.Task;
    }

    /// <inheritdoc />
    public Task ShutdownAsync(TimeSpan shutdownDelay)
    {
        return Task.CompletedTask;
    }

    private static bool InvokeAction(in LogicLooperActionContext ctx, in LogicLooper.LooperAction action)
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
}
