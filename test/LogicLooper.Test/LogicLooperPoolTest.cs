using System;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.LogicLooper;
using FluentAssertions;
using Xunit;

namespace LogicLooper.Test
{
    public class LogicLooperPoolTest
    {
        [Fact]
        public void Create()
        {
            using var pool = new LogicLooperPool(60, 4, RoundRobinLogicLooperPoolBalancer.Instance);
            pool.Loopers.Should().HaveCount(4);
            pool.Loopers[0].TargetFrameRate.Should().BeInRange(60, 60.1);
        }

        [Fact]
        public void Create_TimeSpan()
        {
            using var pool = new LogicLooperPool(TimeSpan.FromMilliseconds(16.666), 4, RoundRobinLogicLooperPoolBalancer.Instance);
            pool.Loopers.Should().HaveCount(4);
            pool.Loopers[0].TargetFrameRate.Should().BeInRange(60, 60.1);
        }

        [Fact]
        public void RegisterActionAsync()
        {
            using var pool = new LogicLooperPool(60, 4, RoundRobinLogicLooperPoolBalancer.Instance);

            var actionCount = 50000;
            var loopCount = 10;
            var executedCount = 0;

            Parallel.For(0, actionCount, _ =>
            {
                var loop = 0;
                pool.RegisterActionAsync((in LogicLooperActionContext ctx) =>
                {
                    Interlocked.Increment(ref executedCount);
                    return ++loop < loopCount;
                });
            });

            Thread.Sleep(1000);

            executedCount.Should().Be(actionCount * loopCount);
        }
    }
}
