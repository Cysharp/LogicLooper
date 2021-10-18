using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Cysharp.Threading
{
    /// <summary>
    /// Provides a pool of <see cref="ManualLogicLooper"/>.
    /// </summary>
    public sealed class ManualLogicLooperPool : ILogicLooperPool
    {
        public ManualLogicLooperPool(double targetFrameRate)
        {
            if (targetFrameRate == 0) throw new ArgumentOutOfRangeException(nameof(targetFrameRate), "TargetFrameRate must be greater than 0.");

            FakeLooper = new ManualLogicLooper(targetFrameRate);
            Loopers = new[] { FakeLooper };
        }

        /// <inheritdoc />
        public IReadOnlyList<ILogicLooper> Loopers { get; }

        /// <summary>
        /// Gets the fake-looper in this pool.
        /// </summary>
        public ManualLogicLooper FakeLooper { get; }

        /// <summary>
        /// Ticks the frame of the loopers.
        /// </summary>
        /// <param name="frameCount"></param>
        public void Tick(int frameCount)
            => FakeLooper.Tick(frameCount);

        /// <summary>
        /// Ticks the frame of the loopers.
        /// </summary>
        /// <returns></returns>
        public bool Tick()
            => FakeLooper.Tick();

        /// <summary>
        /// Ticks the frame of the loopers while the predicate returns <c>true</c>.
        /// </summary>
        public void TickWhile(Func<bool> predicate)
            => FakeLooper.TickWhile(predicate);

        /// <inheritdoc />
        public void Dispose()
        {
            Loopers[0].Dispose();
        }

        /// <inheritdoc />
        public Task RegisterActionAsync(LogicLooperActionDelegate loopAction)
        {
            return Loopers[0].RegisterActionAsync(loopAction);
        }

        /// <inheritdoc />
        public Task RegisterActionAsync<TState>(LogicLooperActionWithStateDelegate<TState> loopAction, TState state)
        {
            return Loopers[0].RegisterActionAsync(loopAction, state);
        }

        /// <inheritdoc />
        public Task ShutdownAsync(TimeSpan shutdownDelay)
        {
            return Loopers[0].ShutdownAsync(shutdownDelay);
        }
    }
}
