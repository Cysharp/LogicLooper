using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Cysharp.Threading.Internal;

internal class LogicLooperSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly ILogicLooper _logicLooper;
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue;
    private readonly CancellationTokenSource _cancellationTokenSource;

    private int _initialized;
    private Task? _loopTask;

    public LogicLooperSynchronizationContext(ILogicLooper logicLooper)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _queue = new ConcurrentQueue<(SendOrPostCallback Callback, object? State)>();
        _logicLooper = logicLooper;
        
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureRunDequeueLoop()
    {
        if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0) return; // the dequeue loop has already started.
        StartDequeueLoop();
    }

    private void StartDequeueLoop()
    {
        if (_loopTask != null)
        {
            throw new InvalidOperationException("The dequeue loop has already started.");
        }

        _loopTask = _logicLooper.RegisterActionAsync((in LogicLooperActionContext ctx, CancellationToken cancellationToken) =>
        {
            while (_queue.TryDequeue(out var action))
            {
                action.Callback(action.State);
            }

            return !cancellationToken.IsCancellationRequested;
        }, _cancellationTokenSource.Token);
    }

    public override void Post(SendOrPostCallback d, object? state)
    {
        EnsureRunDequeueLoop();

        _queue.Enqueue((d, state));
    }

    public override void Send(SendOrPostCallback d, object? state)
    {
        throw new NotSupportedException();
    }

    public void Dispose()
    {
        _cancellationTokenSource.Cancel();
    }
}
