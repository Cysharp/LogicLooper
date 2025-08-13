namespace Cysharp.Threading.Internal;

internal class LogicLooperTracker
{
    private readonly HashSet<LogicLooper> _loopers = new();

    public static LogicLooperTracker Instance { get; } = new();

    public int Count
    {
        get
        {
            lock (_loopers)
            {
                return _loopers.Count;
            }
        }
    }

    public IReadOnlyList<LogicLooper> GetLoopersSnapshot()
    {
        lock (_loopers)
        {
            return _loopers.ToArray();
        }
    }

    public void Register(LogicLooper looper)
    {
        if (looper == null) throw new ArgumentNullException(nameof(looper));
        lock (_loopers)
        {
            _loopers.Add(looper);
        }
    }

    public void Unregister(LogicLooper looper)
    {
        if (looper == null) throw new ArgumentNullException(nameof(looper));
        lock (_loopers)
        {
            _loopers.Remove(looper);
        }
    }
}
