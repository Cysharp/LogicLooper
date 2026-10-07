using System.Diagnostics.Metrics;
using Cysharp.Threading.Internal;

namespace LogicLooper.Test;

public class LogicLooperTrackerTest
{
    [Fact]
    public void Register_Unregister()
    {
        var tracker = new LogicLooperTracker();
        using (var looper = new Cysharp.Threading.LogicLooper(TimeSpan.FromMilliseconds(100), 16, TimeProvider.System, tracker))
        {
            Assert.Equal(1, tracker.Count);
        }
        Assert.Equal(0, tracker.Count);
    }

    /// <summary>
    /// Ensures that a histogram registered more than once is recorded only once per frame,
    /// and keeps being recorded until all of its registrations are removed.
    /// </summary>
    [Fact]
    public void ProcessingDurationHistogram_RegisteredMoreThanOnce()
    {
        // Arrange
        var tracker = new LogicLooperTracker();
        using var meter = new Meter($"{nameof(LogicLooperTrackerTest)}-{Guid.NewGuid()}");
        var histogram = meter.CreateHistogram<double>("processing_duration");
        using var recorder = new HistogramRecorder(meter);

        // Act & Assert
        tracker.AddProcessingDurationHistogram(histogram);
        tracker.AddProcessingDurationHistogram(histogram);
        tracker.RecordProcessingDuration(TimeSpan.FromMilliseconds(1));
        Assert.Equal(1, recorder.Count);

        tracker.RemoveProcessingDurationHistogram(histogram);
        tracker.RecordProcessingDuration(TimeSpan.FromMilliseconds(1));
        Assert.Equal(2, recorder.Count);

        tracker.RemoveProcessingDurationHistogram(histogram);
        tracker.RecordProcessingDuration(TimeSpan.FromMilliseconds(1));
        Assert.Equal(2, recorder.Count);
    }

    /// <summary>
    /// Ensures that removing one histogram does not stop recording to the other registered histograms.
    /// </summary>
    [Fact]
    public void ProcessingDurationHistogram_RemoveOne()
    {
        // Arrange
        var tracker = new LogicLooperTracker();
        using var meter1 = new Meter($"{nameof(LogicLooperTrackerTest)}-{Guid.NewGuid()}");
        using var meter2 = new Meter($"{nameof(LogicLooperTrackerTest)}-{Guid.NewGuid()}");
        var histogram1 = meter1.CreateHistogram<double>("processing_duration");
        var histogram2 = meter2.CreateHistogram<double>("processing_duration");
        using var recorder1 = new HistogramRecorder(meter1);
        using var recorder2 = new HistogramRecorder(meter2);
        tracker.AddProcessingDurationHistogram(histogram1);
        tracker.AddProcessingDurationHistogram(histogram2);

        // Act
        tracker.RemoveProcessingDurationHistogram(histogram1);
        tracker.RecordProcessingDuration(TimeSpan.FromMilliseconds(1));

        // Assert
        Assert.Equal(0, recorder1.Count);
        Assert.Equal(1, recorder2.Count);
    }

    /// <summary>
    /// Ensures that a histogram registered again after all of its registrations are removed is recorded again.
    /// </summary>
    [Fact]
    public void ProcessingDurationHistogram_AddAfterRemoved()
    {
        // Arrange
        var tracker = new LogicLooperTracker();
        using var meter = new Meter($"{nameof(LogicLooperTrackerTest)}-{Guid.NewGuid()}");
        var histogram = meter.CreateHistogram<double>("processing_duration");
        using var recorder = new HistogramRecorder(meter);
        tracker.AddProcessingDurationHistogram(histogram);
        tracker.RemoveProcessingDurationHistogram(histogram);

        // Act
        tracker.AddProcessingDurationHistogram(histogram);
        tracker.RecordProcessingDuration(TimeSpan.FromMilliseconds(1));

        // Assert
        Assert.Equal(1, recorder.Count);
    }

    /// <summary>
    /// Ensures that removing a histogram that is not registered does nothing,
    /// and in particular does not affect the registration of the other histograms.
    /// </summary>
    [Fact]
    public void ProcessingDurationHistogram_RemoveNotRegistered()
    {
        // Arrange
        var tracker = new LogicLooperTracker();
        using var meter = new Meter($"{nameof(LogicLooperTrackerTest)}-{Guid.NewGuid()}");
        var registeredHistogram = meter.CreateHistogram<double>("registered");
        var notRegisteredHistogram = meter.CreateHistogram<double>("not_registered");
        using var recorder = new HistogramRecorder(meter);
        tracker.AddProcessingDurationHistogram(registeredHistogram);

        // Act
        tracker.RemoveProcessingDurationHistogram(notRegisteredHistogram);
        tracker.RecordProcessingDuration(TimeSpan.FromMilliseconds(1));

        // Assert
        Assert.Equal(1, recorder.Count);
    }

    /// <summary>
    /// Ensures that an exception thrown by a <see cref="MeterListener"/> callback does not propagate to the caller of
    /// <see cref="LogicLooperTracker.RecordProcessingDuration"/>, which is the loop thread in production,
    /// and does not prevent recording to the histograms registered before and after the one whose listener throws.
    /// </summary>
    [Fact]
    public void RecordProcessingDuration_ListenerThrows()
    {
        // Arrange
        var tracker = new LogicLooperTracker();
        using var meterBefore = new Meter($"{nameof(LogicLooperTrackerTest)}-{Guid.NewGuid()}");
        using var throwingMeter = new Meter($"{nameof(LogicLooperTrackerTest)}-{Guid.NewGuid()}");
        using var meterAfter = new Meter($"{nameof(LogicLooperTrackerTest)}-{Guid.NewGuid()}");
        // NOTE: Register the histogram whose listener throws between the others, so that one of them is recorded after the exception
        //       regardless of the order in which the tracker records to the histograms.
        tracker.AddProcessingDurationHistogram(meterBefore.CreateHistogram<double>("processing_duration"));
        tracker.AddProcessingDurationHistogram(throwingMeter.CreateHistogram<double>("processing_duration"));
        tracker.AddProcessingDurationHistogram(meterAfter.CreateHistogram<double>("processing_duration"));
        using var recorderBefore = new HistogramRecorder(meterBefore);
        using var throwingRecorder = new HistogramRecorder(throwingMeter, () => throw new InvalidOperationException());
        using var recorderAfter = new HistogramRecorder(meterAfter);

        // Act
        var exception = Record.Exception(() => tracker.RecordProcessingDuration(TimeSpan.FromMilliseconds(1)));

        // Assert
        Assert.Null(exception);
        Assert.Equal(1, throwingRecorder.Count);
        Assert.Equal(1, recorderBefore.Count);
        Assert.Equal(1, recorderAfter.Count);
    }

    private sealed class HistogramRecorder : IDisposable
    {
        private readonly MeterListener _listener = new();
        private int _count;

        public int Count => _count;

        public HistogramRecorder(Meter meter, Action onMeasurement = null)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter == meter) listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<double>((_, _, _, _) =>
            {
                Interlocked.Increment(ref _count);
                onMeasurement?.Invoke();
            });
            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }
}
