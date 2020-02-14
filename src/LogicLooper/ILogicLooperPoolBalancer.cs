namespace Cysharp.Threading.LogicLooper
{
    public interface ILogicLooperPoolBalancer
    {
        LogicLooper GetPooledLooper(LogicLooper[] pooledLoopers);
    }
}