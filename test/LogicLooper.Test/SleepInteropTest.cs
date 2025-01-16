using System.Diagnostics;
using Cysharp.Threading.Internal;

namespace LogicLooper.Test;

public class SleepInteropTest
{
    [Fact]
    public void LessThan16Milliseconds()
    {
        var begin = Stopwatch.GetTimestamp();
        SleepInterop.Sleep(1);
        var end = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(begin, end);
        Assert.True(elapsed.TotalMilliseconds < 16);
    }

    [Fact]
    public void GreaterThan16Milliseconds()
    {
        var begin = Stopwatch.GetTimestamp();
        SleepInterop.Sleep(17);
        var end = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(begin, end);
        Assert.True(elapsed.TotalMilliseconds > 16);
    }

    [Fact]
    public void ThreadSafety()
    {
        var threads = Enumerable.Range(0, 10).Select(x =>
        {
            var t = new Thread(() =>
            {
                for (var i = 0; i < 10; i++)
                {
                    SleepInterop.Sleep(100);
                }
            });
            t.Start();
            return t;
        }).ToArray();
        foreach (var thread in threads)
        {
            Assert.True(thread.Join(TimeSpan.FromSeconds(10)));
        }
    }
}
