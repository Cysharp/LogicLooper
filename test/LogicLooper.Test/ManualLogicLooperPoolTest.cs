using Cysharp.Threading;

namespace LogicLooper.Test;

public class ManualLogicLooperPoolTest
{
    [Fact]
    public void Create()
    {
        var pool = new ManualLogicLooperPool(60.0);
        Assert.Single(pool.Loopers);
        Assert.Equal(60.0, pool.FakeLooper.TargetFrameRate);
    }

    [Fact]
    public void RegisterActionAsync()
    {
        var pool = new ManualLogicLooperPool(60.0);

        var t1 = pool.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            return false;
        });
        Assert.Equal(1, pool.Loopers[0].ApproximatelyRunningActions);
        pool.Tick();
        Assert.Equal(0, pool.Loopers[0].ApproximatelyRunningActions);
        Assert.True(t1.IsCompletedSuccessfully);
    }

    [Fact]
    public void GetLooper()
    {
        var pool = new ManualLogicLooperPool(60.0);
        var looper = pool.GetLooper();

        Assert.Equal(pool.FakeLooper, looper);
    }
}
