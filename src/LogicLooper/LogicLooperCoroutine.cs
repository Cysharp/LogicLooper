using System;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using Cysharp.Threading.CompilerServices;

namespace Cysharp.Threading
{
    /// <summary>
    /// Represents a coroutine-like operation.
    /// </summary>
    [AsyncMethodBuilder(typeof(LogicLooperCoroutineAsyncValueTaskMethodBuilder))]
    public class LogicLooperCoroutine
    {
        private readonly LogicLooperCoroutineActionContext _context;

        private (int WaitFrames, Action? Action) _next;
        private LogicLooperCoroutineStatus _status = LogicLooperCoroutineStatus.Created;

        /// <summary>
        /// Gets whether the coroutine-like operation has completed.
        /// </summary>
        public bool IsCompleted
            => _status == LogicLooperCoroutineStatus.RanToCompletion ||
               _status == LogicLooperCoroutineStatus.Faulted;

        /// <summary>
        /// Gets whether the coroutine-like operation has completed successfully.
        /// </summary>
        public bool IsCompletedSuccessfully
            => _status == LogicLooperCoroutineStatus.RanToCompletion;

        /// <summary>
        /// Gets whether the coroutine-like operation completed due to unhandled exception.
        /// </summary>
        public bool IsFaulted
            => _status == LogicLooperCoroutineStatus.Faulted;

        /// <summary>
        /// Gets an <see cref="System.Exception"/> that thrown while running in the update.
        /// </summary>
        public Exception? Exception { get; private set; }

        internal LogicLooperCoroutine(LogicLooperCoroutineActionContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _context.SetCoroutine(this);
        }

        internal void SetException(Exception exception)
        {
            if (IsCompleted) throw new InvalidOperationException("The coroutine has already been completed.");

            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            _status = LogicLooperCoroutineStatus.Faulted;
        }

        internal void SetResult()
        {
            if (IsCompleted) throw new InvalidOperationException("The coroutine has already been completed.");

            _status = LogicLooperCoroutineStatus.RanToCompletion;
        }

        internal void SetContinuation(int waitFrames, Action continuation)
        {
            _next = (waitFrames, continuation);
        }

        internal bool Update(in LogicLooperActionContext ctx)
        {
            _status = LogicLooperCoroutineStatus.Running;
            _context.SetActionContext(ctx);

            {
                var next = _next;
                _next = (0, null);

                if (next.WaitFrames == 0)
                {
                    next.Action!();
                }
                else
                {
                    _next = (next.WaitFrames - 1, next.Action);
                }

                if (Exception != null)
                {
                    throw Exception;
                }
            }

            return _next.Action != null;
        }
    }

    /// <summary>
    /// Represents a coroutine-like operation.
    /// </summary>
    [AsyncMethodBuilder(typeof(LogicLooperCoroutineAsyncValueTaskMethodBuilder<>))]
    public sealed class LogicLooperCoroutine<TResult> : LogicLooperCoroutine
    {
        private TResult _result;

        public TResult Result
        {
            get
            {
                if (IsFaulted && Exception != null) throw Exception;
                if (!IsCompleted) throw new InvalidOperationException("The coroutine is not completed yet.");

                return _result;
            }
        }

        internal void SetResult(TResult result)
        {
            if (IsCompleted) throw new InvalidOperationException("The coroutine has already been completed.");

            _result = result;

            base.SetResult(); // The coroutine should be RanToCompletion status.
        }

        internal LogicLooperCoroutine(LogicLooperCoroutineActionContext context)
            : base(context)
        {
            _result = default;
        }
    }

    internal enum LogicLooperCoroutineStatus
    {
        Created,
        Running,
        RanToCompletion,
        Faulted,
    }
}
