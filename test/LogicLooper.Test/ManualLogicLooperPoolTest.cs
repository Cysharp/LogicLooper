using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading;
using FluentAssertions;
using Xunit;

namespace LogicLooper.Test
{
    public class ManualLogicLooperPoolTest
    {
        [Fact]
        public void Create()
        {
            var pool = new ManualLogicLooperPool(60.0);
            pool.Loopers.Should().HaveCount(1);
            pool.FakeLooper.TargetFrameRate.Should().Be(60.0);
        }

        [Fact]
        public void RegisterActionAsync()
        {
            var pool = new ManualLogicLooperPool(60.0);

            var t1 = pool.RegisterActionAsync((in LogicLooperActionContext ctx) =>
            {
                return false;
            });
            pool.Loopers[0].ApproximatelyRunningActions.Should().Be(1);
            pool.Tick();
            pool.Loopers[0].ApproximatelyRunningActions.Should().Be(0);
            t1.IsCompletedSuccessfully.Should().BeTrue();
        }
    }
}
