using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Cysharp.Threading
{
    /// <summary>
    /// Provides interface for update loop programming model.
    /// </summary>
    public interface ILogicLooper : IDisposable
    {
        /// <summary>
        /// Gets a unique identifier of the looper.
        /// </summary>
        int Id { get; }

        /// <summary>
        /// Gets an approximately count of running actions.
        /// </summary>
        int ApproximatelyRunningActions { get; }

        /// <summary>
        /// Gets a duration of the last processed frame.
        /// </summary>
        TimeSpan LastProcessingDuration { get; }

        /// <summary>
        /// Gets a target frame rate of the looper.
        /// </summary>
        double TargetFrameRate { get; }

        /// <summary>
        /// Registers a loop-frame action to the looper and returns <see cref="Task"/> to wait for completion.
        /// </summary>
        /// <param name="loopAction"></param>
        /// <returns></returns>
        Task RegisterActionAsync(LogicLooperActionDelegate loopAction);

        /// <summary>
        /// Registers a loop-frame action with state object to the looper and returns <see cref="Task"/> to wait for completion.
        /// </summary>
        /// <param name="loopAction"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state);

        /// <summary>
        /// Stops the action loop of the looper.
        /// </summary>
        /// <param name="shutdownDelay"></param>
        Task ShutdownAsync(TimeSpan shutdownDelay);
    }
}
