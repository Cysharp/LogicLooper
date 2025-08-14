using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Cysharp.Threading.Diagnostics;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class LogicLooperDiagnosticsExtensions
{
    /// <summary>
    /// Adds services required for LogicLooper metrics to the specified service collection.
    /// </summary>
    public static IServiceCollection AddLogicLooperMetrics(this IServiceCollection services)
    {
        services.AddMetrics();
        services.TryAddSingleton<LogicLooperMetrics>(x => new LogicLooperMetrics(x.GetRequiredService<IMeterFactory>()));
        services.AddHostedService<LogicLooperMetricsInitializationHostedService>();
        return services;
    }
}
