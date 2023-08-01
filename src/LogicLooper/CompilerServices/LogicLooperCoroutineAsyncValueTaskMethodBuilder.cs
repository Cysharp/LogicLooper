using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Cysharp.Threading.CompilerServices;

#if !DEBUG
[EditorBrowsable(EditorBrowsableState.Never)]
#endif
public struct LogicLooperCoroutineAsyncValueTaskMethodBuilder
{
    private readonly LogicLooperCoroutine _coroutine;

    public static LogicLooperCoroutineAsyncValueTaskMethodBuilder Create()
    {
        return new LogicLooperCoroutineAsyncValueTaskMethodBuilder(new LogicLooperCoroutine(LogicLooperCoroutineActionContext.Current!));
    }

    private LogicLooperCoroutineAsyncValueTaskMethodBuilder(LogicLooperCoroutine coroutine)
    {
        _coroutine = coroutine ?? throw new ArgumentNullException(nameof(coroutine));
    }

    public void Start<TStateMachine>(ref TStateMachine stateMachine) where TStateMachine : IAsyncStateMachine
    {
        stateMachine.MoveNext();
    }

    public void SetStateMachine(IAsyncStateMachine stateMachine)
    {
    }

    public void SetResult()
    {
        _coroutine.SetResult();
    }

    public void SetException(Exception exception)
    {
        _coroutine.SetException(exception);
    }

    public LogicLooperCoroutine Task => _coroutine;

    public void AwaitOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : INotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        if (!(awaiter is LogicLooperCoroutineFrameAwaitable))
            throw new InvalidOperationException($"Cannot use general-purpose awaitable in the Coroutine action. Use {nameof(LogicLooperCoroutineActionContext)}'s methods instead of {nameof(Task)}'s.");

        awaiter.OnCompleted(stateMachine.MoveNext);
    }

    public void AwaitUnsafeOnCompleted<TAwaiter, TStateMachine>(ref TAwaiter awaiter, ref TStateMachine stateMachine)
        where TAwaiter : ICriticalNotifyCompletion
        where TStateMachine : IAsyncStateMachine
    {
        if (!(awaiter is LogicLooperCoroutineFrameAwaitable))
            throw new InvalidOperationException($"Cannot use general-purpose awaitable in the Coroutine action. Use {nameof(LogicLooperCoroutineActionContext)}'s methods instead of {nameof(Task)}'s.");

        awaiter.UnsafeOnCompleted(stateMachine.MoveNext);
    }
}
