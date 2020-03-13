using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Cysharp.Threading
{
    /// <summary>
    /// Represents the current loop-action contextual values.
    /// </summary>
    public readonly struct LogicLooperActionContext
    {
        /// <summary>
        /// Gets a looper for the current action.
        /// </summary>
        public LogicLooper Looper { get; }

        /// <summary>
        /// Gets a current frame that elapsed since beginning the looper is started.
        /// </summary>
        public long CurrentFrame { get; }

        /// <summary>
        /// Gets an elapsed time since the previous frame has proceeded.
        /// </summary>
        public TimeSpan ElapsedTimeFromPreviousFrame { get; }

        /// <summary>
        /// Gets the cancellation token for the loop.
        /// </summary>
        public CancellationToken CancellationToken { get; }

        public LogicLooperActionContext(LogicLooper looper, long currentFrame, TimeSpan elapsedTimeFromPreviousFrame, CancellationToken cancellationToken)
        {
            Looper = looper ?? throw new ArgumentNullException(nameof(looper));
            CurrentFrame = currentFrame;
            ElapsedTimeFromPreviousFrame = elapsedTimeFromPreviousFrame;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Launch the specified action as a new coroutine-like operation in the current looper.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public LogicLooperCoroutine RunCoroutine(Func<LogicLooperCoroutineActionContext, LogicLooperCoroutine> action)
        {
            LogicLooperCoroutineActionContext.SetCurrent(new LogicLooperCoroutineActionContext(this));
            var coroutineTask = action(LogicLooperCoroutineActionContext.Current!);

            if (coroutineTask.IsCompleted)
            {
                return coroutineTask;
            }

            this.Looper.RegisterActionAsync((in LogicLooperActionContext ctx2, LogicLooperCoroutine state) => state.Update(ctx2), coroutineTask);
            return coroutineTask;
        }

        /// <summary>
        /// Launch the specified action as a new coroutine-like operation in the current looper.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public LogicLooperCoroutine<TResult> RunCoroutine<TResult>(Func<LogicLooperCoroutineActionContext, LogicLooperCoroutine<TResult>> action)
        {
            LogicLooperCoroutineActionContext.SetCurrent(new LogicLooperCoroutineActionContext(this));
            var coroutineTask = action(LogicLooperCoroutineActionContext.Current!);

            if (coroutineTask.IsCompleted)
            {
                return coroutineTask;
            }

            this.Looper.RegisterActionAsync((in LogicLooperActionContext ctx2, LogicLooperCoroutine state) => state.Update(ctx2), coroutineTask);
            return coroutineTask;
        }
    }
}
