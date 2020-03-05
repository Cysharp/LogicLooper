Provide a framework for building loop-action based server application on .NET Core.

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
// Create a looper.
const int targetFps = 60;
using var looper = new Cysharp.Threading.LogicLooper.LogicLooper(targetFps);

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
