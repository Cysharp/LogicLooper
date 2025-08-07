using Cysharp.Threading.Internal;

namespace Cysharp.Threading;

public sealed partial class LogicLooperPool
{
    /// <summary>
    /// Gets the shared pool of loopers. Requires to call <see cref="InitializeSharedPool"/> method before use.
    /// </summary>
    public static ILogicLooperPool Shared { get; private set; } = new NotInitializedLogicLooperPool();

    /// <summary>
    /// Initializes the shared pool of loopers with specified options.
    /// </summary>
    /// <param name="targetFrameRate"></param>
    /// <param name="looperCount"></param>
    /// <param name="balancer"></param>
    /// <param name="looperFactory"></param>
    public static void InitializeSharedPool(int targetFrameRate, int looperCount = 0, ILogicLooperPoolBalancer? balancer = null, ILogicLooperPoolLooperFactory? looperFactory = null)
    {
        if (looperCount == 0)
        {
            looperCount = Math.Max(1, Environment.ProcessorCount - 1);
        }

        Shared = new LogicLooperPool(
            targetFrameRate,
            looperCount,
            balancer ?? RoundRobinLogicLooperPoolBalancer.Instance,
            looperFactory ?? DefaultLogicLooperPoolLooperFactory.Instance
        );
    }
}
