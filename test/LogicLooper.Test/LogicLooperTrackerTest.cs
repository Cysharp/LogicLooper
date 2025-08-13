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
}
