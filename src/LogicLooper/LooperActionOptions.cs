namespace Cysharp.Threading;

/// <summary>
/// Provides options for the loop-action.
/// </summary>
/// <param name="TargetFrameRateOverride">Set a override value for the target frame rate. LogicLooper tries to get as close to the target value as possible, but it is not as accurate as the Looper's frame rate.</param>
public record LooperActionOptions(int? TargetFrameRateOverride = null)
{
    public static LooperActionOptions Default { get; } = new LooperActionOptions();
}
