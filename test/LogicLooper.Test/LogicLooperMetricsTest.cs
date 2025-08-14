using System.Diagnostics.Metrics;
using Cysharp.Threading;
using Cysharp.Threading.Diagnostics;
using Cysharp.Threading.Internal;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace LogicLooper.Test;

public class LogicLooperMetricsTest
{
    [Fact]
    public void RunningLoopers()
    {
        // Arrange
        using var testMeterFactory = new TestMeterFactory();
        using var collector = new MetricCollector<int>(testMeterFactory, LogicLooperMetrics.MeterName, LogicLooperMetrics.InstrumentNames.RunningLoopers);
        var tracker = new LogicLooperTracker();

        // Act
        using var metrics = new LogicLooperMetrics(testMeterFactory, TimeProvider.System, tracker, () => throw new NotSupportedException(), 1, 10);
        using var logicLooper1 = new Cysharp.Threading.LogicLooper(TimeSpan.FromMilliseconds(100), 16, TimeProvider.System, tracker);
        using var logicLooper2 = new Cysharp.Threading.LogicLooper(TimeSpan.FromMilliseconds(100), 16, TimeProvider.System, tracker);

        // Assert
        collector.RecordObservableInstruments();
        var values = collector.GetMeasurementSnapshot();

        Assert.Single(values);
        Assert.Equal(2, values[0].Value);
    }

    [Fact]
    public void SharedPoolLoopers()
    {
        // Arrange
        using var testMeterFactory = new TestMeterFactory();
        var tracker = new LogicLooperTracker();
        using var collector = new MetricCollector<int>(testMeterFactory, LogicLooperMetrics.MeterName, LogicLooperMetrics.InstrumentNames.SharedPoolLoopers);
        using var pool = new LogicLooperPool(1000, 4, RoundRobinLogicLooperPoolBalancer.Instance,
            new AnonymousLogicLooperPoolLooperFactory(x => new Cysharp.Threading.LogicLooper(x,16, TimeProvider.System, tracker)));

        // Act
        using var metrics = new LogicLooperMetrics(testMeterFactory, TimeProvider.System, tracker, () => pool, 1, 10);

        // Assert
        collector.RecordObservableInstruments();
        var values = collector.GetMeasurementSnapshot();

        Assert.Single(values);
        Assert.Equal(4, values[0].Value);
    }

    [Fact]
    public void SharedPoolRunningActions()
    {
        // Arrange
        using var testMeterFactory = new TestMeterFactory();
        var tracker = new LogicLooperTracker();
        using var collector = new MetricCollector<int>(testMeterFactory, LogicLooperMetrics.MeterName, LogicLooperMetrics.InstrumentNames.SharedPoolRunningActions);
        using var pool = new LogicLooperPool(1000, 4, RoundRobinLogicLooperPoolBalancer.Instance,
            new AnonymousLogicLooperPoolLooperFactory(x => new Cysharp.Threading.LogicLooper(x, 16, TimeProvider.System, tracker)));

        // Act
        using var metrics = new LogicLooperMetrics(testMeterFactory, TimeProvider.System, tracker, () => pool, 1, 10);
        pool.RegisterActionAsync((in LogicLooperActionContext ctx) => true);
        pool.RegisterActionAsync((in LogicLooperActionContext ctx) => true);

        // Assert
        collector.RecordObservableInstruments();
        var values = collector.GetMeasurementSnapshot();

        Assert.Single(values);
        Assert.Equal(2, values[0].Value);
    }

}

class AnonymousLogicLooperPoolLooperFactory(Func<TimeSpan, ILogicLooper> factory) : ILogicLooperPoolLooperFactory
{
    public ILogicLooper Create(TimeSpan targetFrameTime)
    {
        return factory(targetFrameTime);
    }
}

class TestMeterFactory : IMeterFactory
{
    public List<Meter> Meters { get; } = new List<Meter>();

    public void Dispose()
    {
        foreach (var meter in Meters)
        {
            meter.Dispose();
        }
        Meters.Clear();
    }

    public Meter Create(MeterOptions options)
    {
        var meter = new Meter(options.Name, options.Version, options.Tags, scope: this);
        Meters.Add(meter);
        return meter;
    }
}
