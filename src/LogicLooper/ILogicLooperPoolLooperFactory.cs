namespace Cysharp.Threading;

/// <summary>
/// Defines a factory for creating <see cref="ILogicLooper"/> instances.
/// </summary>
public interface ILogicLooperPoolLooperFactory
{
    /// <summary>
    /// Creates a new <see cref="ILogicLooper"/> instance configured to operate with the specified target frame time.
    /// </summary>
    ILogicLooper Create(TimeSpan targetFrameTime);
}

/// <summary>
/// Provides a default implementation of <see cref="ILogicLooperPoolLooperFactory"/> for creating logic loopers with a
/// specified target frame time.
/// </summary>
public class DefaultLogicLooperPoolLooperFactory : ILogicLooperPoolLooperFactory
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="ILogicLooperPoolLooperFactory"/> used to create logic looper pool
    /// looper objects.
    /// </summary>
    public static ILogicLooperPoolLooperFactory Instance { get; } = new DefaultLogicLooperPoolLooperFactory();

    private DefaultLogicLooperPoolLooperFactory() { }

    public ILogicLooper Create(TimeSpan targetFrameTime)
        => new LogicLooper(targetFrameTime);
}
