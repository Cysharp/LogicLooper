namespace Cysharp.Threading;

public interface ILogicLooperPoolBalancer
{
    LogicLooper GetPooledLooper(LogicLooper[] pooledLoopers);
}
