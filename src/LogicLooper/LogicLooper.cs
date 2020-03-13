using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Internal;

// ReSharper disable StringLiteralTypo
// ReSharper disable IdentifierTypo

namespace Cysharp.Threading
{
    public delegate bool LogicLooperActionDelegate(in LogicLooperActionContext ctx);
    public delegate bool LogicLooperActionWithStateDelegate<in T>(in LogicLooperActionContext ctx, T state);

    /// <summary>
    /// Provides update loop programming model. the looper ties thread and while-loop and call registered methods every frame.
    /// </summary>
    public class LogicLooper : IDisposable
    {
        private static int _looperSequence = 0;

        [ThreadStatic]
        private static LogicLooper? _threadLocalLooper = default;

        /// <summary>
        /// Gets a looper of the current thread.
        /// </summary>
        public static LogicLooper? Current => _threadLocalLooper;

        private readonly int _looperId;
        private readonly Thread _runLoopThread;
        private readonly CancellationTokenSource _ctsLoop;
        private readonly CancellationTokenSource _ctsAction;
        private readonly TaskCompletionSource<bool> _shutdownTaskAwaiter;
        private readonly double _targetFrameRate;
        private readonly int _targetFrameTimeMilliseconds;
        private readonly MinimumQueue<LooperAction> _registerQueue;
        private readonly object _lockActions = new object();
        private readonly object _lockQueue = new object();
        private readonly int _growFactor = 2;

        private int _tail = 0;
        private bool _isRunning = false;
        private LooperAction[] _actions;
        private long _lastProcessingDuration = 0;
        private int _isShuttingDown = 0;
        private long _frame = 0;

        /// <summary>
        /// Gets a unique identifier of the looper.
        /// </summary>
        public int Id => _looperId;

        /// <summary>
        /// Gets an approximately count of running actions.
        /// </summary>
        public int ApproximatelyRunningActions => _tail;

        /// <summary>
        /// Gets a duration of the last processed frame.
        /// </summary>
        public TimeSpan LastProcessingDuration => TimeSpan.FromMilliseconds(_lastProcessingDuration);

        /// <summary>
        /// Gets a target frame rate of the looper.
        /// </summary>
        public double TargetFrameRate => _targetFrameRate;

        public LogicLooper(int targetFrameRate, int initialActionsCapacity = 16)
            : this(TimeSpan.FromMilliseconds(1000 / (double)targetFrameRate), initialActionsCapacity)
        {
        }

        public LogicLooper(TimeSpan targetFrameTime, int initialActionsCapacity = 16)
        {
            _targetFrameRate = 1000 / targetFrameTime.TotalMilliseconds;
            _looperId = Interlocked.Increment(ref _looperSequence);
            _ctsLoop = new CancellationTokenSource();
            _ctsAction = new CancellationTokenSource();
            _targetFrameTimeMilliseconds = (int)targetFrameTime.TotalMilliseconds;
            _registerQueue = new MinimumQueue<LooperAction>(10);
            _runLoopThread = new Thread(StartRunLoop)
            {
                Name = $"{typeof(LogicLooper).Name}-{_looperId}",
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal,
            };
            _shutdownTaskAwaiter = new TaskCompletionSource<bool>();
            _actions = new LooperAction[initialActionsCapacity];

            _runLoopThread.Start(this);
        }

        /// <summary>
        /// Registers an loop-frame action to the looper and returns <see cref="Task"/> to wait for completion.
        /// </summary>
        /// <param name="loopAction"></param>
        /// <returns></returns>
        public Task RegisterActionAsync(LogicLooperActionDelegate loopAction)
        {
            var action = new LooperAction(DelegateHelper.GetWrapper(), loopAction, null);
            return RegisterActionAsyncCore(action);
        }

        /// <summary>
        /// Registers an loop-frame action with state object to the looper and returns <see cref="Task"/> to wait for completion.
        /// </summary>
        /// <param name="loopAction"></param>
        /// <param name="state"></param>
        /// <returns></returns>
        public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state)
        {
            var action = new LooperAction(DelegateHelper.GetWrapper<TState>(), loopAction, state);
            return RegisterActionAsyncCore(action);
        }

        /// <summary>
        /// Stops action loop of the looper.
        /// </summary>
        /// <param name="shutdownDelay"></param>
        public async Task ShutdownAsync(TimeSpan shutdownDelay)
        {
            if (Interlocked.CompareExchange(ref _isShuttingDown, 1, 0) == 0)
            {
                _ctsAction.Cancel();

                if (shutdownDelay == TimeSpan.Zero)
                {
                    _ctsLoop.Cancel();
                }
                else
                {
                    _ctsLoop.CancelAfter(shutdownDelay);
                }
            }

            await _shutdownTaskAwaiter.Task.ConfigureAwait(false);
        }

        public void Dispose()
        {
            ShutdownAsync(TimeSpan.Zero).GetAwaiter().GetResult();
        }

        private Task RegisterActionAsyncCore(LooperAction action)
        {
            lock (_lockQueue)
            {
                if (_isRunning)
                {
                    _registerQueue.Enqueue(action);
                    return action.Future.Task;
                }
            }

            lock (_lockActions)
            {
                if (_actions.Length == _tail)
                {
                    Array.Resize(ref _actions, checked(_tail * _growFactor));
                }
                _actions[_tail++] = action;
            }

            return action.Future.Task;
        }

        private static void StartRunLoop(object? state)
        {
            _threadLocalLooper = ((LogicLooper)state!);
            _threadLocalLooper.RunLoop();
        }

        private void RunLoop()
        {
            var lastTimestamp = Stopwatch.GetTimestamp();

            while (!_ctsLoop.IsCancellationRequested)
            {
                var begin = Stopwatch.GetTimestamp();

                lock (_lockQueue)
                {
                    _isRunning = true;
                }

                lock (_lockActions)
                {
                    var elapsedTimeFromPreviousFrame = TimeSpan.FromTicks(begin - lastTimestamp);
                    lastTimestamp = begin;

                    var ctx = new LogicLooperActionContext(this, _frame++, elapsedTimeFromPreviousFrame, _ctsAction.Token);

                    var j = _tail - 1;
                    for (var i = 0; i < _actions.Length; i++)
                    {
                        ref var action = ref _actions[i];

                        // Found an action and invoke it.
                        if (action.Action != null)
                        {
                            if (!InvokeAction(ctx, ref action))
                            {
                                action = default;
                            }
                            continue;
                        }

                        // Invoke actions from tail.
                        while (i < j)
                        {
                            ref var actionFromTail = ref _actions[j];

                            j--; // consumed

                            if (actionFromTail.Action != null)
                            {
                                if (!InvokeAction(ctx, ref actionFromTail))
                                {
                                    actionFromTail = default;
                                    continue; // Continue the reverse loop flow.
                                }
                                else
                                {
                                    action = actionFromTail; // Swap the null element and the action.
                                    actionFromTail = default;
                                    goto NextActionLoop; // Resume to the regular flow.
                                }
                            }
                        }

                        _tail = i;
                        break;

                        NextActionLoop:
                        continue;
                    }

                    lock (_lockQueue)
                    {
                        _isRunning = false;

                        while (_registerQueue.Count != 0)
                        {
                            if (_actions.Length == _tail)
                            {
                                Array.Resize(ref _actions, checked(_tail * _growFactor));
                            }
                            _actions[_tail++] = _registerQueue.Dequeue();
                        }
                    }
                }

                var now = Stopwatch.GetTimestamp();
                var elapsedMilliseconds = (now - begin) / 10000;
                _lastProcessingDuration = elapsedMilliseconds;

                var waitForNextFrameMilliseconds = (int)(_targetFrameTimeMilliseconds - elapsedMilliseconds);
                if (waitForNextFrameMilliseconds > 0)
                {
                    Thread.Sleep(waitForNextFrameMilliseconds);
                }
            }

            _shutdownTaskAwaiter.SetResult(true);
        }

        private static bool InvokeAction(in LogicLooperActionContext ctx, ref LooperAction action)
        {
            try
            {
                var hasNext = action.Invoke(ctx);
                if (!hasNext)
                {
                    action.Future.SetResult(true);
                }

                return hasNext;
            }
            catch (Exception ex)
            {
                action.Future.SetException(ex);
            }

            return false;
        }

        internal class DelegateHelper
        {
            private static InternalLogicLooperActionDelegate _wrapper => (object wrappedDelegate, in LogicLooperActionContext ctx, object? state) => ((LogicLooperActionDelegate)wrappedDelegate)(ctx);

            public static InternalLogicLooperActionDelegate GetWrapper<T>() => Cache<T>.Wrapper;
            public static InternalLogicLooperActionDelegate GetWrapper() => _wrapper;

            static class Cache<T>
            {
                public static InternalLogicLooperActionDelegate Wrapper => (object wrappedDelegate, in LogicLooperActionContext ctx, object? state) => ((LogicLooperActionWithStateDelegate<T>) wrappedDelegate)(ctx, (T) state!);
            }
        }

        internal delegate bool InternalLogicLooperActionDelegate(object wrappedDelegate, in LogicLooperActionContext ctx, object? state);

        internal readonly struct LooperAction
        {
            public long BeginAt { get; }
            public object? State { get; }
            public Delegate Action { get; }
            public InternalLogicLooperActionDelegate ActionWrapper { get; }
            public TaskCompletionSource<bool> Future { get; }

            public LooperAction(InternalLogicLooperActionDelegate actionWrapper, Delegate action, object? state)
            {
                BeginAt = Stopwatch.GetTimestamp();
                ActionWrapper = actionWrapper ?? throw new ArgumentNullException(nameof(actionWrapper));
                Action = action ?? throw new ArgumentNullException(nameof(action));
                State = state;
                Future = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public bool Invoke(in LogicLooperActionContext ctx)
            {
                return ActionWrapper(Action, ctx, State);
            }
        }
    }
}
