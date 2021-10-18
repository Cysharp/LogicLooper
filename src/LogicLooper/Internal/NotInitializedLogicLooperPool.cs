using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cysharp.Threading.Internal
{
    internal class NotInitializedLogicLooperPool : ILogicLooperPool
    {
        IReadOnlyList<ILogicLooper> ILogicLooperPool.Loopers => throw new NotImplementedException();

        public Task RegisterActionAsync(LogicLooperActionDelegate loopAction)
            => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

        public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state)
            => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

        public Task ShutdownAsync(TimeSpan shutdownDelay)
            => throw new InvalidOperationException("LogicLooper.Shared is not initialized yet.");

        public void Dispose() { }
    }
}
