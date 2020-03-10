using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Cysharp.Threading
{
    public readonly struct LogicLooperActionContext
    {
        public LogicLooper Looper { get; }
        public long CurrentFrame { get; }
        public TimeSpan ElapsedTimeFromPreviousFrame { get; }
        public CancellationToken CancellationToken { get; }

        public LogicLooperActionContext(LogicLooper looper, long currentFrame, TimeSpan elapsedTimeFromPreviousFrame, CancellationToken cancellationToken)
        {
            Looper = looper ?? throw new ArgumentNullException(nameof(looper));
            CurrentFrame = currentFrame;
            ElapsedTimeFromPreviousFrame = elapsedTimeFromPreviousFrame;
            CancellationToken = cancellationToken;
        }
    }
}
