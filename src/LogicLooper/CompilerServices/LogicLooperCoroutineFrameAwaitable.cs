using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Cysharp.Threading.CompilerServices
{
#if !DEBUG
    [EditorBrowsable(EditorBrowsableState.Never)]
#endif
    public struct LogicLooperCoroutineFrameAwaitable : INotifyCompletion, ICriticalNotifyCompletion
    {
        private readonly int _waitFrames;
        private readonly LogicLooperCoroutine _coroutine;

        public LogicLooperCoroutineFrameAwaitable GetAwaiter() => this;

        public bool IsCompleted => false;
        public void GetResult()
        { }

        public LogicLooperCoroutineFrameAwaitable(LogicLooperCoroutine coroutine, int waitFrames)
        {
            _coroutine = coroutine ?? throw new ArgumentNullException(nameof(coroutine));
            _waitFrames = (waitFrames > 0) ? waitFrames - 1 : throw new ArgumentOutOfRangeException(nameof(waitFrames));
        }

        public void OnCompleted(Action continuation)
        {
            _coroutine.SetContinuation(_waitFrames, continuation);
        }

        public void UnsafeOnCompleted(Action continuation)
        {
            _coroutine.SetContinuation(_waitFrames, continuation);
        }
    }
}
