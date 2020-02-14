using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace Cysharp.Threading.LogicLooper
{
    public readonly struct LogicLooperActionContext
    {
        public long CurrentFrame { get; }
        public TimeSpan ElapsedTimeFromPreviousFrame { get; }
        public CancellationToken CancellationToken { get; }

        public LogicLooperActionContext(long currentFrame, TimeSpan elapsedTimeFromPreviousFrame, CancellationToken cancellationToken)
        {
            CurrentFrame = currentFrame;
            ElapsedTimeFromPreviousFrame = elapsedTimeFromPreviousFrame;
            CancellationToken = cancellationToken;
        }
    }
}
