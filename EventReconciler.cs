namespace MxThumbWheelReader;

// One immediate attempt per event, then only a bounded readiness retry burst.
// There is no periodic timeout after success or after the burst is exhausted.
internal sealed class EventReconciler
{
    internal static readonly int[] RetryDelays = [250, 750, 2000];

    internal void Run(WaitHandle stop, WaitHandle changed, Func<int> apply, Action<int> report)
    {
        int attempt = 0;
        while (!stop.WaitOne(0))
        {
            int result = apply();
            report(result);
            int delay = result != 0 && result != 8 && attempt < RetryDelays.Length
                ? RetryDelays[attempt++] : Timeout.Infinite;
            int signal = WaitHandle.WaitAny([stop, changed], delay);
            if (signal == 0) return;
            if (signal == 1) attempt = 0;
        }
    }
}
