namespace Cysharp.Threading;

public record LooperActionOptions(int? TargetFrameRateOverride = null)
{
    public static LooperActionOptions Default { get; } = new LooperActionOptions();
}
