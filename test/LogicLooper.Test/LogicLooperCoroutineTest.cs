using Cysharp.Threading;

namespace LogicLooper.Test;

public class LogicLooperCoroutineTest
{
    [Fact]
    public async Task RunCoroutineNonGeneric()
    {
        using var looper = new Cysharp.Threading.LogicLooper(60);

        var coroutine = default(LogicLooperCoroutine);
        var startFrame = 0L;
        await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            if (coroutine == null)
            {
                startFrame = ctx.CurrentFrame;
                coroutine = ctx.RunCoroutine(async ctx2 =>
                {
                    Assert.Equal(startFrame, ctx2.CurrentFrame);
                        
                    await ctx2.DelayFrame(60);

                    Assert.Equal(startFrame + 60, ctx2.CurrentFrame);

                    await ctx2.DelayNextFrame();

                    Assert.Equal(startFrame + 61, ctx2.CurrentFrame);

                    await ctx2.Delay(TimeSpan.FromMilliseconds(16.66666));

                    Assert.Equal(startFrame + 62, ctx2.CurrentFrame);
                });
            }

            return !coroutine.IsCompleted;
        });

        if (coroutine.Exception != null)
            throw coroutine.Exception;

        Assert.True(coroutine.IsCompleted);
        Assert.True(coroutine.IsCompletedSuccessfully);
        Assert.False(coroutine.IsFaulted);
    }

    [Fact]
    public async Task RunCoroutineGeneric()
    {
        using var looper = new Cysharp.Threading.LogicLooper(60);

        var coroutine = default(LogicLooperCoroutine<int>);
        var startFrame = 0L;
        await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            if (coroutine == null)
            {
                startFrame = ctx.CurrentFrame;
                coroutine = ctx.RunCoroutine(async ctx2 =>
                {
                    Assert.Equal(startFrame, ctx2.CurrentFrame);

                    await ctx2.DelayFrame(60);

                    Assert.Equal(startFrame + 60, ctx2.CurrentFrame);

                    await ctx2.DelayNextFrame();

                    Assert.Equal(startFrame + 61, ctx2.CurrentFrame);

                    await ctx2.Delay(TimeSpan.FromMilliseconds(16.66666));

                    Assert.Equal(startFrame + 62, ctx2.CurrentFrame);

                    return 12345;
                });
            }

            return !coroutine.IsCompleted;
        });

        if (coroutine.Exception != null)
            throw coroutine.Exception;

        Assert.True(coroutine.IsCompleted);
        Assert.True(coroutine.IsCompletedSuccessfully);
        Assert.False(coroutine.IsFaulted);

        Assert.Equal(12345, coroutine.Result);
    }

    [Fact]
    public async Task ExceptionNonGeneric()
    {
        using var looper = new Cysharp.Threading.LogicLooper(60);

        var coroutine = default(LogicLooperCoroutine);
        var startFrame = 0L;
        await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            if (coroutine == null)
            {
                startFrame = ctx.CurrentFrame;
                coroutine = ctx.RunCoroutine(async ctx2 =>
                {
                    Assert.Equal(startFrame, ctx2.CurrentFrame);

                    await ctx2.DelayFrame(5);

                    throw new Exception("ThrownFromCoroutine");
                });
            }

            return !coroutine.IsCompleted;
        });

        Assert.True(coroutine.IsCompleted);
        Assert.False(coroutine.IsCompletedSuccessfully);
        Assert.True(coroutine.IsFaulted);

        Assert.NotNull(coroutine.Exception);
        Assert.Equal("ThrownFromCoroutine", coroutine.Exception.Message);
    }

    [Fact]
    public async Task ExceptionGeneric()
    {
        using var looper = new Cysharp.Threading.LogicLooper(60);

        var coroutine = default(LogicLooperCoroutine<int>);
        var startFrame = 0L;
        await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            if (coroutine == null)
            {
                startFrame = ctx.CurrentFrame;
                coroutine = ctx.RunCoroutine(async ctx2 =>
                {
                    Assert.Equal(startFrame, ctx2.CurrentFrame);

                    await ctx2.DelayFrame(5);

                    throw new Exception("ThrownFromCoroutine");

#pragma warning disable CS0162 // Unreachable code detected
                    return 1;
#pragma warning restore CS0162 // Unreachable code detected
                });
            }

            return !coroutine.IsCompleted;
        });

        Assert.True(coroutine.IsCompleted);
        Assert.False(coroutine.IsCompletedSuccessfully);
        Assert.True(coroutine.IsFaulted);

        Assert.NotNull(coroutine.Exception);
        Assert.Equal("ThrownFromCoroutine", coroutine.Exception.Message);
    }


    [Fact]
    public async Task CoroutineLooperAlwaysSameAsParentAction()
    {
        using var looperPool = new Cysharp.Threading.LogicLooperPool(60, Environment.ProcessorCount, RoundRobinLogicLooperPoolBalancer.Instance);

        var loopsCount = 100;
        var coroutineCountPerLoop = 100;
        var coroutineLoopCount = 240;

        var tasks = new List<Task>();
        var count = 0;
        for (var i = 0; i < loopsCount; i++)
        {
            var coroutines = default(LogicLooperCoroutine[]);
            var startFrame = 0L;
            var task = looperPool.RegisterActionAsync((in LogicLooperActionContext ctx) =>
            {
                if (coroutines == null)
                {
                    coroutines = new LogicLooperCoroutine[coroutineCountPerLoop];
                    var looper = ctx.Looper;
                    startFrame = ctx.CurrentFrame;
                    for (var j = 0; j < coroutineCountPerLoop; j++)
                    {
                        coroutines[j] = ctx.RunCoroutine(async ctx2 =>
                        {
                            for (var k = 0; k < coroutineLoopCount; k++)
                            {
                                Assert.Equal(looper, ctx2.Looper);

                                await ctx2.DelayFrame(1);

                                Assert.Equal(looper, ctx2.Looper);

                                Interlocked.Increment(ref count);
                            }
                        });
                    }
                }

                var faulted = coroutines.FirstOrDefault(x => x.IsFaulted);
                if (faulted != null)
                {
                    throw faulted.Exception;
                }

                return !coroutines.All(x => x.IsCompleted);
            });

            tasks.Add(task);
        }

        await Task.WhenAll(tasks);

        Assert.Equal(loopsCount * coroutineCountPerLoop * coroutineLoopCount, count);
    }

    [Fact]
    public async Task DelayNextFrame()
    {
        using var looper = new Cysharp.Threading.LogicLooper(60);

        var coroutine = default(LogicLooperCoroutine);
        var startFrame = 0L;
        await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            if (coroutine == null)
            {
                startFrame = ctx.CurrentFrame;
                coroutine = ctx.RunCoroutine(async ctx2 =>
                {
                    Assert.Equal(startFrame, ctx2.CurrentFrame);

                    await ctx2.DelayNextFrame();

                    Assert.Equal(startFrame + 1, ctx2.CurrentFrame);

                    await ctx2.DelayNextFrame();

                    Assert.Equal(startFrame + 2, ctx2.CurrentFrame);
                });
            }

            return !coroutine.IsCompleted;
        });

        if (coroutine.Exception != null)
            throw coroutine.Exception;

        Assert.True(coroutine.IsCompleted);
        Assert.True(coroutine.IsCompletedSuccessfully);
        Assert.False(coroutine.IsFaulted);
    }

    [Fact]
    public async Task DelayFrame()
    {
        using var looper = new Cysharp.Threading.LogicLooper(60);

        var coroutine = default(LogicLooperCoroutine);
        var startFrame = 0L;
        await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            if (coroutine == null)
            {
                startFrame = ctx.CurrentFrame;
                coroutine = ctx.RunCoroutine(async ctx2 =>
                {
                    Assert.Equal(startFrame, ctx2.CurrentFrame);

                    await ctx2.DelayFrame(30);

                    Assert.Equal(startFrame + 30, ctx2.CurrentFrame);

                    await ctx2.DelayFrame(30);

                    Assert.Equal(startFrame + 30 + 30, ctx2.CurrentFrame);
                });
            }

            return !coroutine.IsCompleted;
        });

        if (coroutine.Exception != null)
            throw coroutine.Exception;

        Assert.True(coroutine.IsCompleted);
        Assert.True(coroutine.IsCompletedSuccessfully);
        Assert.False(coroutine.IsFaulted);
    }

    [Fact]
    public async Task Delay()
    {
        using var looper = new Cysharp.Threading.LogicLooper(60);

        var coroutine = default(LogicLooperCoroutine);
        var startFrame = 0L;
        await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
        {
            if (coroutine == null)
            {
                startFrame = ctx.CurrentFrame;
                coroutine = ctx.RunCoroutine(async ctx2 =>
                {
                    Assert.Equal(startFrame, ctx2.CurrentFrame);

                    await ctx2.Delay(TimeSpan.FromMilliseconds(16.66666));

                    Assert.Equal(startFrame + 1, ctx2.CurrentFrame);

                    await ctx2.Delay(TimeSpan.FromMilliseconds(33.33333));

                    Assert.Equal(startFrame + 1 + 2, ctx2.CurrentFrame);
                });
            }

            return !coroutine.IsCompleted;
        });

        if (coroutine.Exception != null)
            throw coroutine.Exception;

        Assert.True(coroutine.IsCompleted);
        Assert.True(coroutine.IsCompletedSuccessfully);
        Assert.False(coroutine.IsFaulted);
    }
}
