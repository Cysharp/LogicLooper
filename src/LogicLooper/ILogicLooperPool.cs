using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cysharp.Threading.LogicLooper
{
    public interface ILogicLooperPool : IDisposable
    {
        /// <summary>
        /// Gets the pooled looper instances.
        /// </summary>
        IReadOnlyList<LogicLooper> Loopers { get; }

        /// <summary>
        /// Registers an loop-frame action to a pooled looper and returns <see cref="Task"/> to wait for completion.
        /// </summary>
        /// <param name="loopAction"></param>
        /// <returns></returns>
        Task RegisterActionAsync(LogicLooperActionDelegate loopAction);

        /// <summary>
        /// Registers an loop-frame action with state object to a pooled looper and returns <see cref="Task"/> to wait for completion.
        /// </summary>
        /// <param name="loopAction"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state);

        /// <summary>
        /// Stops all action loop of the loopers.
        /// </summary>
        /// <param name="shutdownDelay"></param>
        /// <returns></returns>
        Task ShutdownAsync(TimeSpan shutdownDelay);
    }
}