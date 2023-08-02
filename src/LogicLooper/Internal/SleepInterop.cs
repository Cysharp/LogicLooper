using System.Runtime.CompilerServices;
#if WINDOWS
using System.Runtime.InteropServices;
using Windows.Win32;
#endif

namespace Cysharp.Threading.Internal;

internal static class SleepInterop
{
#if WINDOWS
    private const uint CREATE_WAITABLE_TIMER_MANUAL_RESET = 0x00000001;
    private const uint CREATE_WAITABLE_TIMER_HIGH_RESOLUTION = 0x00000002;

    [ThreadStatic]
    private static SafeHandle? _timerHandle;

    public static unsafe void Sleep(int milliseconds)
    {
        _timerHandle ??= PInvoke.CreateWaitableTimerEx(null, default(string?), CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, 0x1F0003 /* TIMER_ALL_ACCESS */);
        var result = PInvoke.SetWaitableTimer(_timerHandle, milliseconds * -10000, 0, null, null, false);
        var resultWait = PInvoke.WaitForSingleObject(_timerHandle, 0xffffffff /* Infinite */);
    }
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Sleep(int milliseconds) => Thread.Sleep(milliseconds);
#endif
}
