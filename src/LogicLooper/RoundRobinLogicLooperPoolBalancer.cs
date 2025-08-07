namespace Cysharp.Threading;

public class RoundRobinLogicLooperPoolBalancer : ILogicLooperPoolBalancer
{
    private int _index = -1;

    public static ILogicLooperPoolBalancer Instance { get; } = new RoundRobinLogicLooperPoolBalancer();

    protected RoundRobinLogicLooperPoolBalancer()
    { }

    public ILogicLooper GetPooledLooper(ILogicLooper[] pooledLoopers)
    {
        return pooledLoopers[Interlocked.Increment(ref _index) % pooledLoopers.Length];
    }
}
