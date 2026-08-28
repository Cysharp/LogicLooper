using Cysharp.Threading;

namespace LogicLooper.Test;

public class RoundRobinLogicLooperPoolBalancerTest
{
    [Fact]
    public void GetPooledLooper_ReturnsLoopersInRoundRobinOrder()
    {
        var balancer = new RoundRobinLogicLooperPoolBalancer();
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

    [Fact]
    public void GetPooledLooper_ReturnsLoopersInRoundRobinOrder_WhenIndexOverflows()
    {
        var balancer = new RoundRobinLogicLooperPoolBalancer();
        var indexField = typeof(RoundRobinLogicLooperPoolBalancer).GetField("_index", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(indexField);
        indexField.SetValue(balancer, int.MaxValue - 1);

        var loopers = new ILogicLooper[]
        {
            new ManualLogicLooper(1),
            new ManualLogicLooper(2),
            new ManualLogicLooper(3)
        };

        Assert.Equal(loopers[1], balancer.GetPooledLooper(loopers));
        Assert.Equal(loopers[2], balancer.GetPooledLooper(loopers));
        Assert.Equal(loopers[0], balancer.GetPooledLooper(loopers));
    }
}
