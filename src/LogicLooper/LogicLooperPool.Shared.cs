using Cysharp.Threading.Internal;

namespace Cysharp.Threading;

public sealed partial class LogicLooperPool
{
    private static readonly ILogicLooperPool _notInitializedSharedPool = new NotInitializedLogicLooperPool();
    private static ILogicLooperPool _shared = _notInitializedSharedPool;

    /// <summary>
    /// Gets the shared pool of loopers. Requires to call <see cref="InitializeSharedPool"/> method before use.
    /// </summary>
    public static ILogicLooperPool Shared => Volatile.Read(ref _shared);

    /// <summary>
    /// Initializes the shared pool of loopers with specified options.
    /// </summary>
    /// <param name="targetFrameRate"></param>
    /// <param name="looperCount"></param>
    /// <param name="balancer"></param>
    /// <param name="looperFactory"></param>
    /// <exception cref="InvalidOperationException">The shared pool has already been initialized.</exception>
    public static void InitializeSharedPool(int targetFrameRate, int looperCount = 0, ILogicLooperPoolBalancer? balancer = null, ILogicLooperPoolLooperFactory? looperFactory = null)
    {
        if (!ReferenceEquals(Volatile.Read(ref _shared), _notInitializedSharedPool))
        {
            throw new InvalidOperationException("LogicLooperPool.Shared has already been initialized.");
        }

        if (looperCount == 0)
        {
            looperCount = Math.Max(1, Environment.ProcessorCount - 1);
        }

        var sharedPool = new LogicLooperPool(
            targetFrameRate,
            looperCount,
            balancer ?? RoundRobinLogicLooperPoolBalancer.Instance,
            looperFactory ?? DefaultLogicLooperPoolLooperFactory.Instance
        );

        if (!ReferenceEquals(Interlocked.CompareExchange(ref _shared, sharedPool, _notInitializedSharedPool), _notInitializedSharedPool))
        {
            sharedPool.Dispose();
            throw new InvalidOperationException("LogicLooperPool.Shared has already been initialized.");
        }
    }
}
