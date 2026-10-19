namespace MxThumbWheelReader;

internal sealed class WatchRunner
{
    public int Run()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        using var single = new Mutex(false, @"Local\MxThumbWheelReader.Watch");
        bool owned;
        try { owned = single.WaitOne(0); }
        catch (AbandonedMutexException) { owned = true; }
        if (!owned) { Console.Error.WriteLine("Hlídání už běží."); return 7; }
        using var stop = new ManualResetEvent(false);
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; stop.Set(); };
        Console.CancelKeyPress += cancel;
        using var operation = new Mutex(false, @"Local\MxThumbWheelReader.Operation");
        var store = new AppSettingsStore();
        int? previous = null;
        int failures = 0;
        try
        {
            Console.WriteLine("Hlídání nastavení myši. Ukončení: Ctrl+C.");
            while (!stop.WaitOne(0))
            {
                int result = 6;
                int interval = 5;
                bool acquired = false;
                try
                {
                    try { acquired = operation.WaitOne(0); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (acquired)
                    {
                        var settings = store.Load();
                        interval = Math.Clamp(settings.WatchIntervalSeconds, 5, 300);
                        if (!settings.DesiredDirectionInverted.HasValue)
                        {
                            Console.Error.WriteLine("Nejdřív zvolte --inverted nebo --normal. Starý přečtený stav není uložený požadavek.");
                            return 8;
                        }
                        // No repeated verbose output or log writes on healthy polling cycles.
                        TextWriter output = Console.Out;
                        TextWriter error = Console.Error;
                        try
                        {
                            Console.SetOut(TextWriter.Null);
                            Console.SetError(TextWriter.Null);
                            result = new LogitechReader().RunOnce(["status"], watch: true);
                        }
                        finally { Console.SetOut(output); Console.SetError(error); }
                    }
                    else result = 7;
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException)
                { result = 6; }
                finally { if (acquired) operation.ReleaseMutex(); }

                if (result != previous)
                {
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss} " + (result == 0
                        ? "Myš dostupná, požadovaný směr ověřen."
                        : $"Myš není připravena nebo došlo k chybě (kód {result}); zkusím znovu."));
                    previous = result;
                }
                failures = result == 0 ? 0 : Math.Min(failures + 1, 3);
                int delay = failures == 0 ? interval : Math.Max(interval, Math.Min(30, 5 * (1 << failures)));
                // Kernel wait: no busy loop, interruptible by Ctrl+C.
                stop.WaitOne(TimeSpan.FromSeconds(delay));
            }
            return 0;
        }
        finally { Console.CancelKeyPress -= cancel; single.ReleaseMutex(); }
    }
}
