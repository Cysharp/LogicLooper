using System.Diagnostics.Metrics;
using Cysharp.Threading;
using Cysharp.Threading.Diagnostics;
using Cysharp.Threading.Internal;
using Microsoft.Extensions.DependencyInjection;
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
        using var metrics = new LogicLooperMetrics(testMeterFactory, tracker, () => throw new NotSupportedException());
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
        using var metrics = new LogicLooperMetrics(testMeterFactory, tracker, () => pool);

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
        using var metrics = new LogicLooperMetrics(testMeterFactory, tracker, () => pool);
        pool.RegisterActionAsync((in LogicLooperActionContext ctx) => true);
        pool.RegisterActionAsync((in LogicLooperActionContext ctx) => true);

        // Assert
        collector.RecordObservableInstruments();
        var values = collector.GetMeasurementSnapshot();

        Assert.Single(values);
        Assert.Equal(2, values[0].Value);
    }

    /// <summary>
    /// Ensures that the processing duration is recorded once for every frame processed by the looper,
    /// and that it is recorded in seconds with sub-millisecond precision, excluding the time spent waiting for the next frame
    /// (the frame time of the looper is 10 ms, while the expected value is less than 1 ms).
    /// </summary>
    [Fact]
    public async Task ProcessingDuration()
    {
        // Arrange
        using var testMeterFactory = new TestMeterFactory();
        var tracker = new LogicLooperTracker();
        using var collector = new MetricCollector<double>(testMeterFactory, LogicLooperMetrics.MeterName, LogicLooperMetrics.InstrumentNames.ProcessingDuration);
        using var metrics = new LogicLooperMetrics(testMeterFactory, tracker, () => throw new NotSupportedException());
        using var looper = new Cysharp.Threading.LogicLooper(TimeSpan.FromMilliseconds(10), 16, TimeProvider.System, tracker);

        // Act
        _ = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            while (stopwatch.Elapsed < TimeSpan.FromMicroseconds(200)) { }
            return !ctx.CancellationToken.IsCancellationRequested;
        });
        await Task.Delay(500);
        await looper.ShutdownAsync(TimeSpan.Zero);

        // Assert
        var values = collector.GetMeasurementSnapshot();

        Assert.Equal("s", collector.Instrument!.Unit);
        Assert.Equal(looper.CurrentFrame, values.Count);
        Assert.Contains(values, x => x.Value >= 0.0002 && x.Value < 0.001);
    }

    /// <summary>
    /// Ensures that each measurement is tagged with the target frame rate of the looper rounded to two decimal places,
    /// so that the tag value is not affected by the error in calculating the frame rate from the frame time.
    /// </summary>
    [Theory]
    [InlineData(1000.0 / 60, 60.0)]
    [InlineData(1000.0 / 30, 30.0)]
    [InlineData(15.0, 66.67)]
    public async Task ProcessingDuration_TargetFrameRateTag(double targetFrameTimeMilliseconds, double expectedTagValue)
    {
        // Arrange
        using var testMeterFactory = new TestMeterFactory();
        var tracker = new LogicLooperTracker();
        using var collector = new MetricCollector<double>(testMeterFactory, LogicLooperMetrics.MeterName, LogicLooperMetrics.InstrumentNames.ProcessingDuration);
        using var metrics = new LogicLooperMetrics(testMeterFactory, tracker, () => throw new NotSupportedException());
        using var looper = new Cysharp.Threading.LogicLooper(TimeSpan.FromMilliseconds(targetFrameTimeMilliseconds), 16, TimeProvider.System, tracker);

        // Act
        await Task.Delay(200);
        await looper.ShutdownAsync(TimeSpan.Zero);

        // Assert
        var values = collector.GetMeasurementSnapshot();

        Assert.NotEmpty(values);
        Assert.All(values, x => Assert.Equal(expectedTagValue, x.Tags[LogicLooperMetrics.TagNames.TargetFrameRate]));
    }

    /// <summary>
    /// Ensures that the bucket boundaries include the frame times of 60 fps and 30 fps,
    /// so that frames exceeding the frame time of those common frame rates can be counted.
    /// </summary>
    [Fact]
    public void ProcessingDuration_BucketBoundaries()
    {
        // Arrange
        using var testMeterFactory = new TestMeterFactory();
        var tracker = new LogicLooperTracker();
        using var collector = new MetricCollector<double>(testMeterFactory, LogicLooperMetrics.MeterName, LogicLooperMetrics.InstrumentNames.ProcessingDuration);

        // Act
        using var metrics = new LogicLooperMetrics(testMeterFactory, tracker, () => throw new NotSupportedException());

        // Assert
        var boundaries = ((Histogram<double>)collector.Instrument!).Advice!.HistogramBucketBoundaries!;

        Assert.Contains(1.0 / 60, boundaries);
        Assert.Contains(1.0 / 30, boundaries);
    }

    /// <summary>
    /// Ensures that the processing duration is recorded to each of multiple <see cref="LogicLooperMetrics"/> instances
    /// created from different meter factories, and that disposing one of them stops recording to it
    /// while recording to the others continues.
    /// The meters of <see cref="TestMeterFactory"/> stop recording when disposed, so this test cannot tell whether
    /// <see cref="LogicLooperMetrics.Dispose"/> unregisters the histogram from the tracker.
    /// That is verified by <see cref="ProcessingDuration_DisposeUnregistersHistogram"/>.
    /// </summary>
    [Fact]
    public async Task ProcessingDuration_MultipleInstances()
    {
        // Arrange
        using var testMeterFactory1 = new TestMeterFactory();
        using var testMeterFactory2 = new TestMeterFactory();
        var tracker = new LogicLooperTracker();
        using var collector1 = new MetricCollector<double>(testMeterFactory1, LogicLooperMetrics.MeterName, LogicLooperMetrics.InstrumentNames.ProcessingDuration);
        using var collector2 = new MetricCollector<double>(testMeterFactory2, LogicLooperMetrics.MeterName, LogicLooperMetrics.InstrumentNames.ProcessingDuration);
        using var metrics1 = new LogicLooperMetrics(testMeterFactory1, tracker, () => throw new NotSupportedException());
        using var metrics2 = new LogicLooperMetrics(testMeterFactory2, tracker, () => throw new NotSupportedException());
        using var looper = new Cysharp.Threading.LogicLooper(TimeSpan.FromMilliseconds(10), 16, TimeProvider.System, tracker);

        // Act
        await Task.Delay(200);
        metrics1.Dispose();
        // NOTE: A frame that has already started before Dispose may still record a measurement after Dispose returns.
        //       Wait for a few frames so that such a measurement is included in the count.
        await Task.Delay(50);
        var countAfterDispose = collector1.GetMeasurementSnapshot().Count;
        var count2AfterDispose = collector2.GetMeasurementSnapshot().Count;
        await Task.Delay(200);

        // Assert
        Assert.True(countAfterDispose > 0, $"Count: {countAfterDispose}");
        Assert.Equal(countAfterDispose, collector1.GetMeasurementSnapshot().Count);
        Assert.True(collector2.GetMeasurementSnapshot().Count > count2AfterDispose);
    }

    /// <summary>
    /// Ensures that the processing duration of a looper that is already running when <see cref="LogicLooperMetrics"/> is created
    /// is recorded from then on. For example, the loopers of <see cref="LogicLooperPool.Shared"/> usually start before the host
    /// creates <see cref="LogicLooperMetrics"/>.
    /// </summary>
    [Fact]
    public async Task ProcessingDuration_LooperStartedBeforeMetrics()
    {
        // Arrange
        using var testMeterFactory = new TestMeterFactory();
        var tracker = new LogicLooperTracker();
        using var collector = new MetricCollector<double>(testMeterFactory, LogicLooperMetrics.MeterName, LogicLooperMetrics.InstrumentNames.ProcessingDuration);
        using var looper = new Cysharp.Threading.LogicLooper(TimeSpan.FromMilliseconds(10), 16, TimeProvider.System, tracker);
        await Task.Delay(100);

        // Act
        using var metrics = new LogicLooperMetrics(testMeterFactory, tracker, () => throw new NotSupportedException());
        await Task.Delay(200);

        // Assert
        var values = collector.GetMeasurementSnapshot();

        Assert.NotEmpty(values);
    }

    /// <summary>
    /// Ensures that <see cref="LogicLooperMetrics.Dispose"/> unregisters the histogram from the tracker.
    /// The meters created by the <see cref="IMeterFactory"/> registered by <c>AddMetrics</c> keep recording even after they are disposed,
    /// so unregistering is the only thing that stops recording in that configuration.
    /// </summary>
    [Fact]
    public async Task ProcessingDuration_DisposeUnregistersHistogram()
    {
        // Arrange
        using var serviceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var tracker = new LogicLooperTracker();
        using var collector = new MetricCollector<double>(meterFactory, LogicLooperMetrics.MeterName, LogicLooperMetrics.InstrumentNames.ProcessingDuration);
        var metrics = new LogicLooperMetrics(meterFactory, tracker, () => throw new NotSupportedException());
        using var looper = new Cysharp.Threading.LogicLooper(TimeSpan.FromMilliseconds(10), 16, TimeProvider.System, tracker);
        await Task.Delay(100);

        // Act
        metrics.Dispose();
        // NOTE: A frame that has already started before Dispose may still record a measurement after Dispose returns.
        await Task.Delay(50);
        var countAfterDispose = collector.GetMeasurementSnapshot().Count;
        await Task.Delay(200);

        // Assert
        Assert.True(collector.Instrument!.Enabled, "The meter is expected to keep recording after it is disposed.");
        Assert.True(countAfterDispose > 0, $"Count: {countAfterDispose}");
        Assert.Equal(countAfterDispose, collector.GetMeasurementSnapshot().Count);
    }

    /// <summary>
    /// Ensures that multiple <see cref="LogicLooperMetrics"/> instances created from the same caching <see cref="IMeterFactory"/>,
    /// which share the same histogram, record each frame only once, and that disposing one of them, even twice,
    /// does not stop recording for the other.
    /// </summary>
    [Fact]
    public async Task ProcessingDuration_SharedMeterFactory()
    {
        // Arrange
        using var serviceProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = serviceProvider.GetRequiredService<IMeterFactory>();
        var tracker = new LogicLooperTracker();
        using var collector = new MetricCollector<double>(meterFactory, LogicLooperMetrics.MeterName, LogicLooperMetrics.InstrumentNames.ProcessingDuration);
        using var metrics1 = new LogicLooperMetrics(meterFactory, tracker, () => throw new NotSupportedException());
        using var metrics2 = new LogicLooperMetrics(meterFactory, tracker, () => throw new NotSupportedException());
        using var looper = new Cysharp.Threading.LogicLooper(TimeSpan.FromMilliseconds(10), 16, TimeProvider.System, tracker);

        // NOTE: This test is meaningful only if the instances share the same histogram, which depends on the caching of the meter factory.
        Assert.Equal(1, tracker.ProcessingDurationHistogramCount);

        // Act & Assert: each frame is recorded only once.
        await Task.Delay(200);
        var countBeforeDispose = collector.GetMeasurementSnapshot().Count;
        var frameBeforeDispose = looper.CurrentFrame;
        Assert.True(countBeforeDispose > 0, $"Count: {countBeforeDispose}");
        Assert.True(countBeforeDispose <= frameBeforeDispose, $"Count: {countBeforeDispose}, Frame: {frameBeforeDispose}");

        // Act & Assert: disposing one of them twice does not stop recording for the other.
        metrics1.Dispose();
        metrics1.Dispose();
        await Task.Delay(50);
        var countAfterDispose = collector.GetMeasurementSnapshot().Count;
        await Task.Delay(200);
        Assert.True(collector.GetMeasurementSnapshot().Count > countAfterDispose);
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
