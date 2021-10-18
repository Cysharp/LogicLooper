# LogicLooper
.NET Core でのサーバーアプリケーションでループを使用したプログラミングモデルを実装するためのライブラリです。
主にサーバーサイドにロジックがあるゲームサーバーのようなユースケースにフォーカスしています。

例えば次のようなゲームループがある場合、これらを集約して素朴な `Task` による駆動よりも効率の良い形で動かす方法を提供します。

```csharp
while (true)
{
    // 1フレームで行う処理いろいろ
    network.Receive();
    world.Update();
    players.Update();
    network.Send();
    // ...他の何か処理 ...

    // 次のフレームを待つ
    await Task.Delay(16);
}
```

```csharp
using var looper = new LogicLooper(60);
await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
{
    // 1フレームで行う処理いろいろ
    network.Receive();
    world.Update();
    players.Update();
    network.Send();
    // ...他の何か処理 ...

    return true; // 次のアップデート待ち
});
```

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
## Table of Contents

- [インストール](#%E3%82%A4%E3%83%B3%E3%82%B9%E3%83%88%E3%83%BC%E3%83%AB)
- [使い方](#%E4%BD%BF%E3%81%84%E6%96%B9)
  - [一つのループ](#%E4%B8%80%E3%81%A4%E3%81%AE%E3%83%AB%E3%83%BC%E3%83%97)
  - [LooperPool を使用した複数の Looper](#looperpool-%E3%82%92%E4%BD%BF%E7%94%A8%E3%81%97%E3%81%9F%E8%A4%87%E6%95%B0%E3%81%AE-looper)
  - [Microsoft.Extensions.Hosting との統合](#microsoftextensionshosting-%E3%81%A8%E3%81%AE%E7%B5%B1%E5%90%88)
- [上級編](#%E4%B8%8A%E7%B4%9A%E7%B7%A8)
  - [Coroutine](#coroutine)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

## インストール
```powershell
PS> Install-Package LogicLooper
```
```bash
$ dotnet add package LogicLooper
```

## 使い方
### 一つのループ
一つの Looper は一つのスレッドを占有し、ループを開始します。その Looper に対して複数のループアクションを登録できます。
これはゲームエンジンの一フレームで複数の `Update` メソッドが呼び出されるようなものと似ています。

```csharp
using Cysharp.Threading;

// 指定したフレームレートで Looper を起動します
const int targetFps = 60;
using var looper = new LogicLooper(targetFps);

// ループのアクションを登録して、完了を待機します
await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
{
    // ループを完了するとき(もう呼ばれる必要がないとき)は `false` を返します
    if (...) { return false; }

    // 1フレームの何か処理 ...

    return true; // 次のアップデート待ち
});
```

### LooperPool を使用した複数の Looper
例えば、サーバーに複数のコアがある場合は複数のループとスレッドをホストすることでより効率的に処理を行えます。
`LooperPool` は複数の Looper を束ね、それらの入り口となる API を提供します。この場合 Looper を直接操作する必要がありません。

```csharp
using Cysharp.Threading;

// Looper のプールを作成します
// もし4コアのマシンであれば、LooperPool は4つの Looper のインスタンスを保持します
const int targetFps = 60;
var looperCount = Environment.ProcessorCount;
using var looperPool = new LogicLooperPool(targetFps, looperCount, RoundRobinLogicLooperPoolBalancer.Instance);

// ループのアクションを登録して、完了を待機します
await looperPool.RegisterActionAsync((in LogicLooperActionContext ctx) =>
{
    // ループを完了するとき(もう呼ばれる必要がないとき)は `false` を返します
    if (...) { return false; }

    // 1フレームの何か処理 ...

    return true; // 次のアップデート待ち
});
```

### Microsoft.Extensions.Hosting との統合
[samples/LoopHostingApp](samples/LoopHostingApp) をご覧ください。`IHostedService` と組み合わせることでサーバーのライフサイクルなどを考慮した実装を行えます。

## 上級編
### ユニットテスト / フレーム単位実行
LogicLooper と共にユニットテストを記述する場合やフレームを手動で更新したいような場合には `ManualLogicLooper` / `ManualLogicLooperPool` を利用できます。

```csharp
var looper = new ManualLogicLooper(60.0); // `ElapsedTimeFromPreviousFrame` は `1000 / FrameTargetFrameRate` に固定されます

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

looper.Tick(); // Update frame (t1 が完了します)
Console.WriteLine(count); // => 3

looper.Tick(); // Update frame (実行するアクションはありません)
Console.WriteLine(count); // => 3
```

### Coroutine
LogicLooper はコルーチンのような操作もサポートしています。Unity を利用したことがあればなじみのあるコルーチンパターンです。

```csharp
using var looper = new LogicLooper(60);

var coroutine = default(LogicLooperCoroutine);
await looper.RegisterActionAsync((in LogicLooperActionContext ctx) =>
{
    if (/* ... */)
    {
        // コルーチンを現在のループアクションが実行されている Looper で起動する
        coroutine = ctx.RunCoroutine(async coCtx =>
        {
            // NOTE: コルーチンの中では `DelayFrame`, `DelayNextFrame`, `Delay` メソッドのみを待機(`await`)可能です
            // もし `Task` や `Task-like` をコルーチン内で `await` した場合、例外を送出します
            await coCtx.DelayFrame(60); // 60フレームを待つ

            // 何か処理 …

            await coCtx.DelayNextFrame(); // 次のフレームを待つ

            // 何か処理 …

            await coCtx.Delay(TimeSpan.FromMilliseconds(16.66666)); // 約16ミリ秒待つ (=1f)
        });
    }

    // コルーチンが終わったかどうかをチェックする
    if (coroutine != null && coroutine.IsCompleted)
    {
        // コルーチンが終わった場合、何か処理する…
    }

    return true;
});
```