# LogicLooper
A library for building server application using loop-action programming model on .NET Core. This library focuses on building game servers with server-side logic.

## Installation
```powershell
PS> Install-Package LogicLooper
```
```bash
$ dotnet add package LogicLooper
```

## Usage
### Single-loop application
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

## Advanced
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