using System.Diagnostics.Metrics;
using Cysharp.Threading.Internal;

namespace Cysharp.Threading.Diagnostics;

public class LogicLooperMetrics : IDisposable
{
    private readonly Meter _meter;
    private readonly ObservableUpDownCounter<int> _counterSharedPoolLoopersCounter;
    private readonly ObservableUpDownCounter<int> _counterSharedPoolRunningActions;
    private readonly ObservableUpDownCounter<int> _counterRunningLoopers;
    private readonly Histogram<double> _histogramProcessingDuration;

    private readonly TimeProvider _timeProvider;
    private readonly LogicLooperTracker _tracker;
    private readonly Task _monitorTask;
    private readonly CancellationTokenSource _shutdownTokenSource = new();
    private readonly TimeSpan _monitoringLoopInterval = TimeSpan.FromSeconds(1); // Adjust the interval as needed

    private const string LooperCountUnit = "loopers";
    private const string ActionCountUnit = "actions";
    public const string MeterName = "LogicLooper";

    public static class InstrumentNames
    {
        public const string SharedPoolLoopers = "shared_pool.loopers";
        public const string SharedPoolRunningActions = "shared_pool.running_actions";
        public const string RunningLoopers = "running_loopers";
        public const string ProcessingDuration = "processing_duration";
    }

    public LogicLooperMetrics()
        : this(new DefaultMeterFactory()) {}

    public LogicLooperMetrics(IMeterFactory meterFactory)
        : this(meterFactory, TimeProvider.System, LogicLooperTracker.Instance, static () => LogicLooperPool.Shared) {}

    internal LogicLooperMetrics(IMeterFactory meterFactory, TimeProvider timeProvider, LogicLooperTracker tracker, Func<ILogicLooperPool> sharedPoolAccessor)
    {
        _meter = meterFactory.Create(MeterName);
        _timeProvider = timeProvider;
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
        _histogramProcessingDuration = _meter.CreateHistogram<double>(
            InstrumentNames.ProcessingDuration,
            unit: "milliseconds",
            description: "Duration of processing actions in milliseconds"
        );


        _monitorTask = Task.Run(() => RunMonitoringLoopAsync(_shutdownTokenSource.Token));
    }

    private async Task RunMonitoringLoopAsync(CancellationToken shutdownToken)
    {
        while (!shutdownToken.IsCancellationRequested)
        {
            var loopers = _tracker.GetLoopersSnapshot();
            foreach (var looper in loopers)
            {
                var processingDuration = looper.LastProcessingDuration.TotalMilliseconds;
                _histogramProcessingDuration.Record(processingDuration, new KeyValuePair<string, object?>("looper_id", looper.Id));
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
}
