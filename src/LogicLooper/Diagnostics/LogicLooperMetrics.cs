using System.Diagnostics.Metrics;
using Cysharp.Threading.Internal;

namespace Cysharp.Threading.Diagnostics;

public class LogicLooperMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly ObservableUpDownCounter<int> _counterSharedPoolLoopersCounter;
    private readonly ObservableUpDownCounter<int> _counterSharedPoolRunningActions;
    private readonly ObservableUpDownCounter<int> _counterRunningLoopers;
    private readonly ObservableUpDownCounter<int> _counterRunningActions;
    private readonly Histogram<double> _histogramProcessingDuration;

    private readonly LogicLooperTracker _tracker;
    private int _disposed;

    private const string LooperCountUnit = "{looper}";
    private const string ActionCountUnit = "{action}";
    private const string ProcessingDurationUnit = "s";

    // NOTE: The default bucket boundaries of OpenTelemetry (0, 5, 10, 25, ..., 10000) assume values in milliseconds,
    //       so they do not fit this histogram, whose unit is seconds.
    private static readonly double[] ProcessingDurationBucketBoundaries =
    [
        0.0001, 0.00025, 0.0005, 0.00075,
        0.001, 0.0025, 0.005, 0.0075,
        0.01, 0.025, 0.05, 0.075,
        0.1, 0.25, 0.5, 0.75,
        1,
    ];

    public const string MeterName = "LogicLooper";

    public static class InstrumentNames
    {
        public const string SharedPoolLoopers = "shared_pool.loopers";
        public const string SharedPoolRunningActions = "shared_pool.running_actions";
        public const string RunningLoopers = "running_loopers";
        public const string RunningActions = "running_actions";
        public const string ProcessingDuration = "processing_duration";
    }

    public LogicLooperMetrics()
        : this(new DefaultMeterFactory()) {}

    public LogicLooperMetrics(IMeterFactory meterFactory)
        : this(meterFactory, LogicLooperTracker.Instance, static () => LogicLooperPool.Shared) {}

    internal LogicLooperMetrics(IMeterFactory meterFactory, LogicLooperTracker tracker, Func<ILogicLooperPool> sharedPoolAccessor)
    {
        _meter = meterFactory.Create(MeterName);
        _tracker = tracker;

        _counterSharedPoolLoopersCounter = _meter.CreateObservableUpDownCounter(
            InstrumentNames.SharedPoolLoopers,
            () => sharedPoolAccessor().Loopers.Count,
            unit: LooperCountUnit
        );
        _counterSharedPoolRunningActions = _meter.CreateObservableUpDownCounter(
            InstrumentNames.SharedPoolRunningActions,
            () => sharedPoolAccessor().Loopers.Sum(x => x.ApproximatelyRunningActions),
            unit: ActionCountUnit
        );

        _counterRunningLoopers = _meter.CreateObservableUpDownCounter(
            InstrumentNames.RunningLoopers,
            () => _tracker.Count,
            unit: LooperCountUnit,
            "Number of currently running loopers in the process"
        );
        _counterRunningActions = _meter.CreateObservableUpDownCounter(
            InstrumentNames.RunningActions,
            () => _tracker.ApproximatelyRunningActions,
            unit: ActionCountUnit,
            "Number of currently running actions in the process"
        );

        _histogramProcessingDuration = _meter.CreateHistogram<double>(
            InstrumentNames.ProcessingDuration,
            unit: ProcessingDurationUnit,
            description: "Duration of processing one frame of a looper",
            tags: null,
            advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = ProcessingDurationBucketBoundaries }
        );
        _tracker.AddProcessingDurationHistogram(_histogramProcessingDuration);
    }

    public void Dispose()
    {
        // NOTE: The histogram may be shared with other instances, and its registration is reference-counted.
        //       Unregister it only once so that disposing this instance twice does not stop recording for the others.
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        _tracker.RemoveProcessingDurationHistogram(_histogramProcessingDuration);
        _meter.Dispose();
    }

    private class DefaultMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = new();

        public Meter Create(MeterOptions options)
        {
            var meter = new Meter(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (var meter in _meters)
            {
                meter.Dispose();
            }
        }
    }
}
