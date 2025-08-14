using Microsoft.Extensions.Hosting;

// ReSharper disable once CheckNamespace
namespace Cysharp.Threading.Diagnostics;

#pragma warning disable CS9113 // Parameter is unread.
public class LogicLooperMetricsInitializationHostedService(LogicLooperMetrics metrics) : IHostedService
#pragma warning restore CS9113 // Parameter is unread.
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
