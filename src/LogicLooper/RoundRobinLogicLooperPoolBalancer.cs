using System.Threading;

namespace Cysharp.Threading.LogicLooper
{
    public class RoundRobinLogicLooperPoolBalancer : ILogicLooperPoolBalancer
    {
        private int _index = 0;

        public static ILogicLooperPoolBalancer Instance { get; } = new RoundRobinLogicLooperPoolBalancer();

        public LogicLooper GetPooledLooper(LogicLooper[] pooledLoopers)
        {
            return pooledLoopers[Interlocked.Increment(ref _index) % pooledLoopers.Length];
        }
    }
}