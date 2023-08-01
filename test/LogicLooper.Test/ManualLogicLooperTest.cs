using Cysharp.Threading;

namespace LogicLooper.Test;

public class ManualLogicLooperTest
{
    [Fact]
    public void Elapsed()
    {
        var looper = new ManualLogicLooper(60.0);
        looper.TargetFrameRate.Should().Be(60.0);

        var elapsed = default(TimeSpan);
        looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            elapsed = ctx.ElapsedTimeFromPreviousFrame;
            return false;
        });
        looper.Tick();

        elapsed.Should().BeCloseTo(TimeSpan.FromMilliseconds(1000 / 60), precision: 1);
    }

    [Fact]
    public void Elapsed_2()
    {
        var looper = new ManualLogicLooper(30.0);
        looper.TargetFrameRate.Should().Be(30.0);

        var elapsed = default(TimeSpan);
        looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            elapsed = ctx.ElapsedTimeFromPreviousFrame;
            return false;
        });
        looper.Tick();

        elapsed.Should().BeCloseTo(TimeSpan.FromMilliseconds(1000 / 30), precision: 1);
    }

    [Fact]
    public void Tick()
    {
        var looper = new ManualLogicLooper(60.0);
        looper.ApproximatelyRunningActions.Should().Be(0);

        var count = 0;
        var t1 = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            count++;
            return count != 3;
        });
        looper.ApproximatelyRunningActions.Should().Be(1);

        count.Should().Be(0);
        looper.Tick().Should().BeTrue();
        count.Should().Be(1);
        looper.Tick().Should().BeTrue();
        count.Should().Be(2);
        looper.Tick().Should().BeFalse();
        count.Should().Be(3);

        looper.ApproximatelyRunningActions.Should().Be(0);
        t1.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void Tick_Multiple()
    {
        var looper = new ManualLogicLooper(60.0);
        looper.ApproximatelyRunningActions.Should().Be(0);

        var count = 0;
        var t1 = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            count++;
            return count != 5;
        });
        var t2 = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            count++;
            return count != 7;
        });
        looper.ApproximatelyRunningActions.Should().Be(2);

        count.Should().Be(0);
        looper.Tick().Should().BeTrue();
        count.Should().Be(2);
        looper.Tick().Should().BeTrue();
        count.Should().Be(4);
        looper.Tick().Should().BeTrue();
        count.Should().Be(6);
        looper.ApproximatelyRunningActions.Should().Be(1);
        t1.IsCompletedSuccessfully.Should().BeTrue();

        looper.Tick().Should().BeFalse();
        count.Should().Be(7);

        looper.ApproximatelyRunningActions.Should().Be(0);
        t2.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void Tick_Count()
    {
        var looper = new ManualLogicLooper(60.0);
        looper.ApproximatelyRunningActions.Should().Be(0);

        var count = 0;
        var t1 = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            count++;
            return count != 3;
        });
        looper.ApproximatelyRunningActions.Should().Be(1);

        looper.Tick(3).Should().BeFalse();

        looper.ApproximatelyRunningActions.Should().Be(0);
        t1.IsCompletedSuccessfully.Should().BeTrue();
    }

    [Fact]
    public void TickWhile()
    {
        var looper = new ManualLogicLooper(60.0);
        looper.ApproximatelyRunningActions.Should().Be(0);

        var count = 0;
        var t1 = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            count++;
            return true;
        });
        looper.ApproximatelyRunningActions.Should().Be(1);

        count.Should().Be(0);

        looper.TickWhile(() => count != 6);

        count.Should().Be(6);

        looper.ApproximatelyRunningActions.Should().Be(1);
        t1.IsCompletedSuccessfully.Should().BeFalse();
    }


    [Fact]
    public void RegisterActionAsync_State()
    {
        var looper = new ManualLogicLooper(60.0);
        looper.ApproximatelyRunningActions.Should().Be(0);

        var count = 0;
        var tuple = Tuple.Create("Foo", 123);
        var receivedState = default(Tuple<string, int>);
        var t1 = looper.RegisterActionAsync((in LogicLooperActionContext ctx, Tuple<string, int> state) =>
        {
            receivedState = state;
            count++;
            return count != 3;
        }, tuple);
        looper.ApproximatelyRunningActions.Should().Be(1);

        looper.Tick().Should().BeTrue();
        looper.Tick().Should().BeTrue();
        looper.Tick().Should().BeFalse();
        count.Should().Be(3);
        receivedState.Should().Be(tuple);

        looper.ApproximatelyRunningActions.Should().Be(0);
        t1.IsCompletedSuccessfully.Should().BeTrue();
    }
}
