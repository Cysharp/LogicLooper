using System.Collections.Concurrent;

namespace Cysharp.Threading.Internal;

internal class LogicLooperSynchronizationContext : SynchronizationContext, IDisposable
{
    private readonly Task _loopTask;
    private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _queue;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public LogicLooperSynchronizationContext(ILogicLooper logicLooper)
    {
        _cancellationTokenSource = new CancellationTokenSource();
        _queue = new ConcurrentQueue<(SendOrPostCallback Callback, object? State)>();
        _loopTask = logicLooper.RegisterActionAsync((in LogicLooperActionContext ctx, CancellationToken cancellationToken) =>
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
