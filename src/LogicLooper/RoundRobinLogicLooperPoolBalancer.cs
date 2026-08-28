namespace Cysharp.Threading;

public class RoundRobinLogicLooperPoolBalancer : ILogicLooperPoolBalancer
{
    private int _index = -1;

    public static ILogicLooperPoolBalancer Instance { get; } = new RoundRobinLogicLooperPoolBalancer();

    internal RoundRobinLogicLooperPoolBalancer()
    { }

    public ILogicLooper GetPooledLooper(ILogicLooper[] pooledLoopers)
    {
        var index = unchecked((uint)Interlocked.Increment(ref _index));
        return pooledLoopers[(int)(index % (uint)pooledLoopers.Length)];
    }
}
