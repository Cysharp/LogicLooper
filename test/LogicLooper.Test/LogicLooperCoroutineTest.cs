using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading;
using FluentAssertions;
using Xunit;
using Xunit.Sdk;

namespace LogicLooper.Test
{
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
                        ctx2.CurrentFrame.Should().Be(startFrame);
                        
                        await ctx2.DelayFrame(60);

                        ctx2.CurrentFrame.Should().Be(startFrame + 60);

                        await ctx2.DelayNextFrame();

                        ctx2.CurrentFrame.Should().Be(startFrame + 61);

                        await ctx2.Delay(TimeSpan.FromMilliseconds(16.66666));

                        ctx2.CurrentFrame.Should().Be(startFrame + 62);
                    });
                }

                return !coroutine.IsCompleted;
            });

            if (coroutine.Exception != null)
                throw coroutine.Exception;

            coroutine.IsCompleted.Should().BeTrue();
            coroutine.IsCompletedSuccessfully.Should().BeTrue();
            coroutine.IsFaulted.Should().BeFalse();
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
                        ctx2.CurrentFrame.Should().Be(startFrame);

                        await ctx2.DelayFrame(60);

                        ctx2.CurrentFrame.Should().Be(startFrame + 60);

                        await ctx2.DelayNextFrame();

                        ctx2.CurrentFrame.Should().Be(startFrame + 61);

                        await ctx2.Delay(TimeSpan.FromMilliseconds(16.66666));

                        ctx2.CurrentFrame.Should().Be(startFrame + 62);

                        return 12345;
                    });
                }

                return !coroutine.IsCompleted;
            });

            if (coroutine.Exception != null)
                throw coroutine.Exception;

            coroutine.IsCompleted.Should().BeTrue();
            coroutine.IsCompletedSuccessfully.Should().BeTrue();
            coroutine.IsFaulted.Should().BeFalse();

            coroutine.Result.Should().Be(12345);
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
                        ctx2.CurrentFrame.Should().Be(startFrame);

                        await ctx2.DelayFrame(5);

                        throw new Exception("ThrownFromCoroutine");
                    });
                }

                return !coroutine.IsCompleted;
            });

            coroutine.IsCompleted.Should().BeTrue();
            coroutine.IsCompletedSuccessfully.Should().BeFalse();
            coroutine.IsFaulted.Should().BeTrue();

            coroutine.Exception.Should().NotBeNull();
            coroutine.Exception.Message.Should().Be("ThrownFromCoroutine");
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
                        ctx2.CurrentFrame.Should().Be(startFrame);

                        await ctx2.DelayFrame(5);

                        throw new Exception("ThrownFromCoroutine");

#pragma warning disable CS0162 // Unreachable code detected
                        return 1;
#pragma warning restore CS0162 // Unreachable code detected
                    });
                }

                return !coroutine.IsCompleted;
            });

            coroutine.IsCompleted.Should().BeTrue();
            coroutine.IsCompletedSuccessfully.Should().BeFalse();
            coroutine.IsFaulted.Should().BeTrue();

            coroutine.Exception.Should().NotBeNull();
            coroutine.Exception.Message.Should().Be("ThrownFromCoroutine");
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
                                    ctx2.Looper.Should().Be(looper);

                                    await ctx2.DelayFrame(1);

                                    ctx2.Looper.Should().Be(looper);

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

            count.Should().Be(loopsCount * coroutineCountPerLoop * coroutineLoopCount);
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
                        ctx2.CurrentFrame.Should().Be(startFrame);

                        await ctx2.DelayNextFrame();

                        ctx2.CurrentFrame.Should().Be(startFrame + 1);

                        await ctx2.DelayNextFrame();

                        ctx2.CurrentFrame.Should().Be(startFrame + 2);
                    });
                }

                return !coroutine.IsCompleted;
            });

            if (coroutine.Exception != null)
                throw coroutine.Exception;

            coroutine.IsCompleted.Should().BeTrue();
            coroutine.IsCompletedSuccessfully.Should().BeTrue();
            coroutine.IsFaulted.Should().BeFalse();
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
                        ctx2.CurrentFrame.Should().Be(startFrame);

                        await ctx2.DelayFrame(30);

                        ctx2.CurrentFrame.Should().Be(startFrame + 30);

                        await ctx2.DelayFrame(30);

                        ctx2.CurrentFrame.Should().Be(startFrame + 30 + 30);
                    });
                }

                return !coroutine.IsCompleted;
            });

            if (coroutine.Exception != null)
                throw coroutine.Exception;

            coroutine.IsCompleted.Should().BeTrue();
            coroutine.IsCompletedSuccessfully.Should().BeTrue();
            coroutine.IsFaulted.Should().BeFalse();
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
                        ctx2.CurrentFrame.Should().Be(startFrame);

                        await ctx2.Delay(TimeSpan.FromMilliseconds(16.66666));

                        ctx2.CurrentFrame.Should().Be(startFrame + 1);

                        await ctx2.Delay(TimeSpan.FromMilliseconds(33.33333));

                        ctx2.CurrentFrame.Should().Be(startFrame + 1 + 2);
                    });
                }

                return !coroutine.IsCompleted;
            });

            if (coroutine.Exception != null)
                throw coroutine.Exception;

            coroutine.IsCompleted.Should().BeTrue();
            coroutine.IsCompletedSuccessfully.Should().BeTrue();
            coroutine.IsFaulted.Should().BeFalse();
        }
    }
}
