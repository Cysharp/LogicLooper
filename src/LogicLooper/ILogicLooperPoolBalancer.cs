namespace Cysharp.Threading;

/// <summary>
/// Defines a strategy for selecting an <see cref="ILogicLooper"/> instance from a pool of available loopers.
/// </summary>
/// <remarks>Implementations of this interface determine how a looper is chosen from the provided pool, which may
/// affect load distribution, performance, or resource utilization.</remarks>
public interface ILogicLooperPoolBalancer
{
    /// <summary>
    /// Gets an available <see cref="ILogicLooper"/> instance from the specified pool.
    /// </summary>
    ILogicLooper GetPooledLooper(ILogicLooper[] pooledLoopers);
}
