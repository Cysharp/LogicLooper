[![Build-Master](https://github.com/Cysharp/LogicLooper/actions/workflows/build-master.yaml/badge.svg)](https://github.com/Cysharp/LogicLooper/actions/workflows/build-master.yaml) [![Releases](https://img.shields.io/github/release/Cysharp/LogicLooper.svg)](https://github.com/Cysharp/LogicLooper/releases)

# LogicLooper

[日本語](README.ja.md)

A library is for building server application using loop-action programming model on .NET. This library focuses on building game servers with server-side logic.

For example, if you have the following game loops, the library will provide a way to aggregate and process in a more efficient way than driving with a simple `Task`.

```csharp
while (true)
{
    // some stuff to do ...
    network.Receive();
    world.Update();
    players.Update();
    network.Send();
    // some stuff to do ...

    // wait for next frame
    await Task.Delay(16);
}
```

```csharp
using var looper = new LogicLooper(60);
await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
{
    // The action will be called by looper every frame.
    // some stuff to do ...
    network.Receive();
    world.Update();
    players.Update();
    network.Send();
    // some stuff to do ...

    return true; // wait for next update
});
```

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
## Table of Contents

- [Installation](#installation)
- [Usage](#usage)
  - [Single-loop application](#single-loop-application)
  - [Multiple-loop application using LooperPool](#multiple-loop-application-using-looperpool)
  - [Integrate with Microsoft.Extensions.Hosting](#integrate-with-microsoftextensionshosting)
- [Advanced](#advanced)
  - [Unit tests / Frame-by-Frame execution](#unit-tests--frame-by-frame-execution)
  - [Coroutine](#coroutine)
  - [TargetFrameRateOverride](#targetframerateoverride)
- [Experimental](#experimental)
  - [async-aware loop actions](#async-aware-loop-actions)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

## Installation
```powershell
PS> Install-Package LogicLooper
```
```bash
$ dotnet add package LogicLooper
```

## Usage
### Single-loop application
A Looper bound one thread and begin a main-loop. You can register multiple loop actions for the Looper.
It's similar to be multiple `Update` methods called in one frame of the game engine.

```csharp
using Cysharp.Threading;

// Create a looper.
const int targetFps = 60;
using var looper = new LogicLooper(targetFps);

// Register a action to the looper and wait for completion.
await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
{
    // If you want to stop/complete the loop, return false to stop.
    if (...) { return false; }

    // some stuff to do ...

    return true; // wait for a next update.
});
```

### Multiple-loop application using LooperPool
For example, if your server has many cores, it is more efficient running multiple loops. `LooperPool` provides multiple loopers and facade for using them.

```csharp
using Cysharp.Threading;

// Create a looper pool.
// If your machine has 4-cores, the LooperPool creates 4-Looper instances.
const int targetFps = 60;
var looperCount = Environment.ProcessorCount;
using var looperPool = new LogicLooperPool(targetFps, looperCount, RoundRobinLogicLooperPoolBalancer.Instance);

// Register a action to the looper and wait for completion.
await looperPool.RegisterActionAsync((in LogicLooperActionContext ctx) =>
{
    // If you want to stop/complete the loop, return false to stop.
    if (...) { return false; }

    // some stuff to do ...

    return true; // wait for a next update.
});
```

### Integrate with Microsoft.Extensions.Hosting
See [samples/LoopHostingApp](samples/LoopHostingApp).

### Metrics

LogicLooper provides metrics using the Metric API. This allows you to obtain the number of LogicLoopers and registered actions, as well as the execution time per loop.

To enable metrics collection, install the `LogicLooper.Diagnostics` package and call `AddLogicLooperMetrics`.

```csharp
services.AddLogicLooperMetrics();
```

|Instrumentation|Unit|Description|
|---|---|---|
|LogicLooper.shared_pool.loopers|`{looper}`|Number of LogicLooper instances running in LogicLooperPool.Shared|
|LogicLooper.shared_pool.running_actions|`{action}`|Number of actions registered in LogicLooperPool.Shared that are running in LogicLooper|
|LogicLooper.running_loopers|`{looper}`|Number of LogicLooper instances running in the process|
|LogicLooper.running_actions|`{action}`|Number of actions registered in LogicLooper that are running in the process|
|LogicLooper.processing_duration_min|`ms`|Minimum execution time of one loop in the process for LogicLooper|
|LogicLooper.processing_duration_max|`ms`|Maximum execution time of one loop in the process for LogicLooper|
|LogicLooper.processing_duration_avg|`ms`|Average execution time of one loop in the process for LogicLooper|


## Advanced
### Unit tests / Frame-by-Frame execution
If you want to write unit tests with LogicLooper or update frames manually, you can use `ManualLogicLooper` / `ManualLogicLooperPool`.

```csharp
var looper = new ManualLogicLooper(60.0); // `ElapsedTimeFromPreviousFrame` will be fixed to `1000 / FrameTargetFrameRate`.

var count = 0;
var t1 = looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
{
    count++;
    return count != 3;
});

looper.Tick(); // Update frame
Console.WriteLine(count); // => 1

looper.Tick(); // Update frame
Console.WriteLine(count); // => 2

looper.Tick(); // Update frame (t1 will be completed)
Console.WriteLine(count); // => 3

looper.Tick(); // Update frame (no action)
Console.WriteLine(count); // => 3
```

### Coroutine
LogicLooper has support for the coroutine-like operation. If you are using Unity, you are familiar with the coroutine pattern.

```csharp
using var looper = new LogicLooper(60);

var coroutine = default(LogicLooperCoroutine);
await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
{
    if (/* ... */)
    {
        // Launch a coroutine in the looper that same as the loop action.
        coroutine = ctx.RunCoroutine(async coCtx =>
        {
            // NOTE: `DelayFrame`, `DelayNextFrame`, `Delay` methods are allowed and awaitable in the coroutine.
            // If you await a Task or Task-like, the coroutine throws an exception.
            await coCtx.DelayFrame(60);

            // some stuff to do ...

            await coCtx.DelayNextFrame();

            // some stuff to do ...

            await coCtx.Delay(TimeSpan.FromMilliseconds(16.66666));
        });
    }

    if (coroutine.IsCompleted)
    {
        // When the coroutine has completed, you can do some stuff ...
    }

    return true;
});
```

### TargetFrameRateOverride

`TargetFrameRateOverride` option allows to override the frame rate for each action. This can be useful in cases where you want to mix multiple frame rates, such as expecting the main loop to run at 30fps, but wanting some actions to be called at 5fps.

You can also set the frame rate for each Looper that executes the loops, but the design of LogicLooper is 1-loop per thread, so in principle we expect a number of Loopers in accordance with the number of cores. By setting the frame rate for each action, you can keep the number of Loopers fixed even if the workload changes.

```csharp
using var looper = new LogicLooper(60); // 60 fps

await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
{
    // Something to do ...
    return true;
}); // The action will be called at 60fps.

await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
{
    // Something to do (low priority) ...
    return true;
}, LooperActionOptions.Default with { TargetFrameRateOverride = 10 }); // The action will be called at 10fps.
```

The granularity of action execution changes based on the execution frequency of the main loop itself. This means that the accuracy may be inferior to the target frame rate of the Looper.

## Experimental
### async-aware loop actions
Experimental support for loop actions that can await asynchronous events.

With SynchronizationContext, all asynchronous continuations are executed on the loop thread.
Please beware that asynchronous actions are executed across multiple frames, unlike synchronous actions.

```csharp
await looper.RegisterActionAsync(static async (ctx, state) =>
{
    state.Add("1"); // Frame: 1
    await Task.Delay(250);
    state.Add("2"); // Frame: 2 or later
    return false;
});
```

> [!WARNING]
> If an action completes immediately (`ValueTask.IsCompleted = true`), there's no performance difference from non-async version. But it is very slow if there's a need to await. This asynchronous support provides as an emergency hatch when it becomes necessary to communicate with the outside at a low frequency. We do not recommended to perform asynchronous processing at a high frequency.
