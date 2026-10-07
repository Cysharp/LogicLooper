using System.Diagnostics.Metrics;

namespace Cysharp.Threading.Internal;

internal class LogicLooperTracker
{
    private readonly HashSet<LogicLooper> _loopers = new();

    // NOTE: The same histogram instance can be registered more than once. For example, the IMeterFactory registered by AddMetrics
    //       returns the same meter for the same name, and the meter returns the same histogram for the same parameters.
    //       The registrations are reference-counted so that each histogram is recorded only once per frame
    //       and is not unregistered until all of its registrations are removed.
    private readonly Dictionary<Histogram<double>, int> _processingDurationHistogramRefCounts = new();

    // NOTE: Copy-on-write array to read it from loop threads without locking.
    private Histogram<double>[] _processingDurationHistograms = [];

    public static LogicLooperTracker Instance { get; } = new();

    internal /* for UnitTest */ int ProcessingDurationHistogramCount => Volatile.Read(ref _processingDurationHistograms).Length;

    public int Count
    {
        get
        {
            lock (_loopers)
            {
                return _loopers.Count;
            }
        }
    }

    public int ApproximatelyRunningActions
    {
        get
        {
            lock (_loopers)
            {
                return _loopers.Sum(x => x.ApproximatelyRunningActions);
            }
        }
    }

    public void Register(LogicLooper looper)
    {
        if (looper == null) throw new ArgumentNullException(nameof(looper));
        lock (_loopers)
        {
            _loopers.Add(looper);
        }
    }

    public void Unregister(LogicLooper looper)
    {
        if (looper == null) throw new ArgumentNullException(nameof(looper));
        lock (_loopers)
        {
            _loopers.Remove(looper);
        }
    }

    public void AddProcessingDurationHistogram(Histogram<double> histogram)
    {
        if (histogram == null) throw new ArgumentNullException(nameof(histogram));
        lock (_processingDurationHistogramRefCounts)
        {
            _processingDurationHistogramRefCounts.TryGetValue(histogram, out var refCount);
            _processingDurationHistogramRefCounts[histogram] = refCount + 1;
            if (refCount == 0)
            {
                Volatile.Write(ref _processingDurationHistograms, _processingDurationHistogramRefCounts.Keys.ToArray());
            }
        }
    }

    public void RemoveProcessingDurationHistogram(Histogram<double> histogram)
    {
        if (histogram == null) throw new ArgumentNullException(nameof(histogram));
        lock (_processingDurationHistogramRefCounts)
        {
            if (!_processingDurationHistogramRefCounts.TryGetValue(histogram, out var refCount)) return;

            if (refCount > 1)
            {
                _processingDurationHistogramRefCounts[histogram] = refCount - 1;
            }
            else
            {
                _processingDurationHistogramRefCounts.Remove(histogram);
                Volatile.Write(ref _processingDurationHistograms, _processingDurationHistogramRefCounts.Keys.ToArray());
            }
        }
    }

    /// <summary>
    /// Records the processing duration of a frame. This method is called from the loop thread on every frame.
    /// </summary>
    /// <param name="duration">The processing duration of the frame.</param>
    public void RecordProcessingDuration(TimeSpan duration)
    {
        var histograms = Volatile.Read(ref _processingDurationHistograms);
        if (histograms.Length == 0) return;

        var seconds = duration.TotalSeconds;
        foreach (var histogram in histograms)
        {
            try
            {
                histogram.Record(seconds);
            }
            catch
            {
                // NOTE: Catch all exceptions regardless of their cause, because an unhandled exception on the loop thread terminates the process.
                //       The main source is a MeterListener callback, whose exception propagates to the caller of Record.
                //       This keeps recording to the other histograms, but the other listeners of the same histogram
                //       may miss the measurement. This behavior is documented in the README.
            }
        }
    }
}
