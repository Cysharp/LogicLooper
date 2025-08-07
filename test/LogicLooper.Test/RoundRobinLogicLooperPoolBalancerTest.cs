using Cysharp.Threading;

namespace LogicLooper.Test;

public class RoundRobinLogicLooperPoolBalancerTest
{
    [Fact]
    public void GetPooledLooper_ReturnsLoopersInRoundRobinOrder()
    {
        var balancer = RoundRobinLogicLooperPoolBalancer.Instance;
        var loopers = new ILogicLooper[]
        {
            new ManualLogicLooper(1),
            new ManualLogicLooper(2),
            new ManualLogicLooper(3)
        };
        Assert.Equal(loopers[0], balancer.GetPooledLooper(loopers));
        Assert.Equal(loopers[1], balancer.GetPooledLooper(loopers));
        Assert.Equal(loopers[2], balancer.GetPooledLooper(loopers));
        Assert.Equal(loopers[0], balancer.GetPooledLooper(loopers));
    }
}
