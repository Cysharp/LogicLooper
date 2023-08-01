using Cysharp.Threading.CompilerServices;

namespace Cysharp.Threading;

/// <summary>
/// Represents the current coroutine-like contextual values.
/// </summary>
public sealed class LogicLooperCoroutineActionContext
{
    [ThreadStatic]
    private static LogicLooperCoroutineActionContext? _current;

    internal static LogicLooperCoroutineActionContext? Current => _current;

    // NOTE: the field will be set in LogicLooperCoroutine.Update.
    private LogicLooperActionContext _actionContext;

    // NOTE: the field will be set in LogicLooperCoroutine..ctor.
    private LogicLooperCoroutine _coroutine = default!;

    /// <summary>
    /// Gets a looper for the current action.
    /// </summary>
    public ILogicLooper Looper => _actionContext.Looper;

    /// <summary>
    /// Gets a current frame that elapsed since beginning the looper is started.
    /// </summary>
    public long CurrentFrame => _actionContext.CurrentFrame;

    /// <summary>
    /// Gets an elapsed time since the previous frame has proceeded.
    /// </summary>
    public TimeSpan ElapsedTimeFromPreviousFrame => _actionContext.ElapsedTimeFromPreviousFrame;

    /// <summary>
    /// Gets the cancellation token for the loop.
    /// </summary>
    public CancellationToken CancellationToken => _actionContext.CancellationToken;

    internal LogicLooperCoroutineActionContext(LogicLooperActionContext ctx)
    {
        _actionContext = ctx;
    }

    internal static void SetCurrent(LogicLooperCoroutineActionContext? context)
    {
        _current = context;
    }

    internal void SetCoroutine(LogicLooperCoroutine coroutine)
    {
        _coroutine = coroutine ?? throw new ArgumentNullException(nameof(coroutine));
    }

    internal void SetActionContext(in LogicLooperActionContext actionContext)
    {
        _actionContext = actionContext;
    }

    /// <summary>
    /// Creates an awaitable that resumes the coroutine until next update.
    /// </summary>
    /// <returns></returns>
    public LogicLooperCoroutineFrameAwaitable DelayNextFrame()
    {
        return new LogicLooperCoroutineFrameAwaitable(_coroutine, 1);
    }

    /// <summary>
    /// Creates an awaitable that resumes the coroutine after a specified frames.
    /// </summary>
    /// <returns></returns>
    public LogicLooperCoroutineFrameAwaitable DelayFrame(int waitFrames)
    {
        return new LogicLooperCoroutineFrameAwaitable(_coroutine, waitFrames);
    }

    /// <summary>
    /// Creates an awaitable that resumes the coroutine after a specified duration.
    /// </summary>
    /// <returns></returns>
    public LogicLooperCoroutineFrameAwaitable Delay(TimeSpan delay)
    {
        var frames = this.Looper.TargetFrameRate * delay.TotalSeconds;
        return new LogicLooperCoroutineFrameAwaitable(_coroutine, (int)frames);
    }
}
