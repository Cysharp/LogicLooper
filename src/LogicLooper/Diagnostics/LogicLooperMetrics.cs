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
    private readonly Histogram<double> _histogramProcessingDurationAvg;
    private readonly Histogram<double> _histogramProcessingDurationMin;
    private readonly Histogram<double> _histogramProcessingDurationMax;
    private readonly PointBuffer _pointBuffer;

    private readonly TimeProvider _timeProvider;
    private readonly LogicLooperTracker _tracker;
    private readonly Task _monitorTask;
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    private readonly TimeSpan _monitoringLoopInterval;

    private const string LooperCountUnit = "{looper}";
    private const string ActionCountUnit = "{action}";
    public const string MeterName = "LogicLooper";

    public static class InstrumentNames
    {
        public const string SharedPoolLoopers = "shared_pool.loopers";
        public const string SharedPoolRunningActions = "shared_pool.running_actions";
        public const string RunningLoopers = "running_loopers";
        public const string RunningActions = "running_actions";
        public const string ProcessingDurationAvg = "processing_duration_avg";
        public const string ProcessingDurationMin = "processing_duration_min";
        public const string ProcessingDurationMax = "processing_duration_max";
    }

    public LogicLooperMetrics()
        : this(new DefaultMeterFactory()) {}

    public LogicLooperMetrics(IMeterFactory meterFactory)
        : this(meterFactory, TimeProvider.System, LogicLooperTracker.Instance, static () => LogicLooperPool.Shared, monitorInterval: 1, countBufferingInterval: 10) {}

    internal LogicLooperMetrics(IMeterFactory meterFactory, TimeProvider timeProvider, LogicLooperTracker tracker, Func<ILogicLooperPool> sharedPoolAccessor, int monitorInterval, int countBufferingInterval)
    {
        if (monitorInterval <= 0) throw new ArgumentOutOfRangeException(nameof(monitorInterval), "Monitor interval must be greater than zero.");
        if (countBufferingInterval <= 0) throw new ArgumentOutOfRangeException(nameof(monitorInterval), "Count buffering interval must be greater than zero.");

        _meter = meterFactory.Create(MeterName);
        _timeProvider = timeProvider;
        _tracker = tracker;
        _monitoringLoopInterval = TimeSpan.FromSeconds(monitorInterval);
        _pointBuffer = new PointBuffer(countBufferingInterval / monitorInterval);

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

        _histogramProcessingDurationAvg = _meter.CreateHistogram<double>(
            InstrumentNames.ProcessingDurationAvg,
            unit: "ms",
            description: "Duration of processing actions in milliseconds (Average)"
        );
        _histogramProcessingDurationMin = _meter.CreateHistogram<double>(
            InstrumentNames.ProcessingDurationMin,
            unit: "ms",
            description: "Duration of processing actions in milliseconds (Min)"
        );
        _histogramProcessingDurationMax = _meter.CreateHistogram<double>(
            InstrumentNames.ProcessingDurationMax,
            unit: "ms",
            description: "Duration of processing actions in milliseconds (Max)"
        );

        _monitorTask = Task.Run(() => RunMonitoringLoopAsync(_shutdownTokenSource.Token));
    }

    private async Task RunMonitoringLoopAsync(CancellationToken shutdownToken)
    {
        while (!shutdownToken.IsCancellationRequested)
        {
            var loopers = _tracker.GetLoopersSnapshot();
            var min = 0.0d;
            var max = 0.0d;
            var sum = 0.0d;
            var count = 0;
            foreach (var looper in loopers)
            {
                if (looper.LastProcessingDuration.TotalMilliseconds == 0) continue;

                var processingDuration = looper.LastProcessingDuration.TotalMilliseconds;
                if (min > processingDuration || count == 0)
                {
                    min = processingDuration;
                }
                if (max < processingDuration || count == 0)
                {
                    max = processingDuration;
                }
                sum += processingDuration;
                count++;
            }

            var avg = (count > 0) ? sum / count : 0.0d;
            _pointBuffer.Add(min, max, avg);
            if (_pointBuffer.ShouldFlush())
            {
                (min, max, avg) = _pointBuffer.Flush();
                _histogramProcessingDurationAvg.Record(avg);
                _histogramProcessingDurationMin.Record(min);
                _histogramProcessingDurationMax.Record(max);
            }

            await Task.Delay(_monitoringLoopInterval,
#if NET8_0_OR_GREATER
                _timeProvider,
#endif
                shutdownToken);
        }
    }

    public void Dispose()
    {
        _shutdownTokenSource.Cancel();
        try
        {
            _monitorTask.GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            /* Ignore */
        }
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

    private class PointBuffer(int size)
    {
        private readonly double[] _min = new double[size];
        private readonly double[] _max = new double[size];
        private readonly double[] _avg = new double[size];
        private int _index;

        public bool ShouldFlush()
        {
            return (_index == size);
        }

        public void Add(double min, double max, double avg)
        {
            if (_index < size)
            {
                _min[_index] = min;
                _max[_index] = max;
                _avg[_index] = avg;
            }
            _index++;
        }

        public (double Min, double Max, double Avg) Flush()
        {
            if (_index == 0) return (0, 0, 0);
            var min = _min.Min();
            var max = _max.Max();
            var avg = _avg.Average();
            _index = 0;

            Array.Clear(_min, 0, size);
            Array.Clear(_max, 0, size);
            Array.Clear(_avg, 0, size);

            return (min, max, avg);
        }
    }
}
