using Cysharp.Threading;

namespace LogicLooper.Test;

public class ManualLogicLooperTest
{
    [Fact]
    public void Elapsed()
    {
        var looper = new ManualLogicLooper(60.0);
        Assert.Equal(60.0, looper.TargetFrameRate);

        var elapsed = default(TimeSpan);
        looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            elapsed = ctx.ElapsedTimeFromPreviousFrame;
            return false;
        });
        looper.Tick();

        Assert.InRange(elapsed.TotalMilliseconds, 16.6666, 16.9999);
    }

    [Fact]
    public void Elapsed_2()
    {
        var looper = new ManualLogicLooper(30.0);
        Assert.Equal(30.0, looper.TargetFrameRate);

        var elapsed = default(TimeSpan);
        looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            elapsed = ctx.ElapsedTimeFromPreviousFrame;
            return false;
        });
        looper.Tick();

        Assert.InRange(elapsed.TotalMilliseconds, 33.3333, 33.9999);
    }

    [Fact]
    public void Tick()
    {
        var looper = new ManualLogicLooper(60.0);
        Assert.Equal(0, looper.ApproximatelyRunningActions);

        var count = 0;
        var t1 = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            count++;
            return count != 3;
        });
        Assert.Equal(1, looper.ApproximatelyRunningActions);

        Assert.Equal(0, count);
        Assert.Equal(0, looper.CurrentFrame);
        Assert.True(looper.Tick());
        Assert.Equal(1, count);
        Assert.Equal(1, looper.CurrentFrame);
        Assert.True(looper.Tick());
        Assert.Equal(2, count);
        Assert.Equal(2, looper.CurrentFrame);
        Assert.False(looper.Tick());
        Assert.Equal(3, count);
        Assert.Equal(3, looper.CurrentFrame);

        Assert.Equal(0, looper.ApproximatelyRunningActions);
        Assert.True(t1.IsCompletedSuccessfully);
    }

    [Fact]
    public void Tick_Multiple()
    {
        var looper = new ManualLogicLooper(60.0);
        Assert.Equal(0, looper.ApproximatelyRunningActions);

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
        Assert.Equal(2, looper.ApproximatelyRunningActions);

        Assert.Equal(0, count);
        Assert.True(looper.Tick());
        Assert.Equal(2, count);
        Assert.True(looper.Tick());
        Assert.Equal(4, count);
        Assert.True(looper.Tick());
        Assert.Equal(6, count);
        Assert.Equal(1, looper.ApproximatelyRunningActions);
        Assert.True(t1.IsCompletedSuccessfully);

        Assert.False(looper.Tick());
        Assert.Equal(7, count);

        Assert.Equal(0, looper.ApproximatelyRunningActions);
        Assert.True(t2.IsCompletedSuccessfully);
    }

    [Fact]
    public void Tick_Count()
    {
        var looper = new ManualLogicLooper(60.0);
        Assert.Equal(0, looper.ApproximatelyRunningActions);

        var count = 0;
        var t1 = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            count++;
            return count != 3;
        });
        Assert.Equal(1, looper.ApproximatelyRunningActions);

        Assert.False(looper.Tick(3));

        Assert.Equal(0, looper.ApproximatelyRunningActions);
        Assert.True(t1.IsCompletedSuccessfully);
    }

    [Fact]
    public void TickWhile()
    {
        var looper = new ManualLogicLooper(60.0);
        Assert.Equal(0, looper.ApproximatelyRunningActions);

        var count = 0;
        var t1 = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            count++;
            return true;
        });
        Assert.Equal(1, looper.ApproximatelyRunningActions);

        Assert.Equal(0, count);

        looper.TickWhile(() => count != 6);

        Assert.Equal(6, count);

        Assert.Equal(1, looper.ApproximatelyRunningActions);
        Assert.False(t1.IsCompletedSuccessfully);
    }


    [Fact]
    public void RegisterActionAsync_State()
    {
        var looper = new ManualLogicLooper(60.0);
        Assert.Equal(0, looper.ApproximatelyRunningActions);

        var count = 0;
        var tuple = Tuple.Create("Foo", 123);
        var receivedState = default(Tuple<string, int>);
        var t1 = looper.RegisterActionAsync((in LogicLooperActionContext ctx, Tuple<string, int> state) =>
        {
            receivedState = state;
            count++;
            return count != 3;
        }, tuple);
        Assert.Equal(1, looper.ApproximatelyRunningActions);

        Assert.True(looper.Tick());
        Assert.True(looper.Tick());
        Assert.False(looper.Tick());
        Assert.Equal(3, count);
        Assert.Equal(tuple, receivedState);

        Assert.Equal(0, looper.ApproximatelyRunningActions);
        Assert.True(t1.IsCompletedSuccessfully);
    }

    [Fact]
    public void LogicLooper_Current()
    {
        Assert.Null(Cysharp.Threading.LogicLooper.Current);

        var looper = new ManualLogicLooper(60.0);
        Assert.Equal(60.0, looper.TargetFrameRate);

        var currentLogicLooperInAction = default(ILogicLooper);
        looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            currentLogicLooperInAction = Cysharp.Threading.LogicLooper.Current;
            return false;
        });
        looper.Tick();

        Assert.Equal(looper, currentLogicLooperInAction);
        Assert.Null(Cysharp.Threading.LogicLooper.Current);
    }
}
