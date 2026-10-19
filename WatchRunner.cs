namespace MxThumbWheelReader;

internal sealed class WatchRunner
{
    internal const string ChangedEventName = @"Local\MxThumbWheelReader.Changed";

    public int Run(bool hidden = false)
    {
        if (hidden)
        {
            // Detach only this process, leaving a caller's terminal intact.
            if (!FreeConsole()) return 6;
            Console.SetOut(TextWriter.Null);
            Console.SetError(TextWriter.Null);
        }
        else Console.OutputEncoding = System.Text.Encoding.UTF8;
        using var single = new Mutex(false, @"Local\MxThumbWheelReader.Watch");
        bool owned;
        try { owned = single.WaitOne(0); }
        catch (AbandonedMutexException) { owned = true; }
        if (!owned) { Console.Error.WriteLine("Hlídání už běží."); return 7; }
        using var stop = new ManualResetEvent(false);
        using var changed = new EventWaitHandle(false, EventResetMode.AutoReset, ChangedEventName);
        ConsoleCancelEventHandler cancel = (_, e) => { e.Cancel = true; stop.Set(); };
        if (!hidden) Console.CancelKeyPress += cancel;
        using var operation = new Mutex(false, @"Local\MxThumbWheelReader.Operation");
        var store = new AppSettingsStore();
        int? previous = null;
        int exitCode = 0;
        try
        {
            using var events = new ConnectionEvents(() => changed.Set());
            Console.WriteLine("Čekám na události Bluetooth / HID / probuzení. Bez periodických dotazů. Ctrl+C ukončí hlídání.");
            new EventReconciler().Run(stop, changed, () =>
            {
                if (events.Faulted)
                {
                    Console.Error.WriteLine("Windows ukončil sledování zařízení. Restartujte hlídání.");
                    exitCode = 6; stop.Set(); return 6;
                }
                bool acquired = false;
                try
                {
                    try { acquired = operation.WaitOne(0); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) return 7;
                    var settings = store.Load();
                    if (!settings.DesiredDirectionInverted.HasValue)
                    {
                        Console.Error.WriteLine("Nejdřív zvolte --inverted nebo --normal.");
                        exitCode = 8; stop.Set(); return 8;
                    }
                    if (settings.ComputerName == Environment.MachineName && settings.Mouse is { DeviceIndex: >= 1 and <= 6 })
                    {
                        Console.Error.WriteLine("Tato verze hlídání je určena pro přímé Bluetooth. Připojení myši za USB receiverem nehlásí Windows jako připojení Bluetooth.");
                        exitCode = 8; stop.Set(); return 8;
                    }
                    TextWriter output = Console.Out;
                    TextWriter error = Console.Error;
                    try
                    {
                        Console.SetOut(TextWriter.Null);
                        Console.SetError(TextWriter.Null);
                        return new LogitechReader().RunOnce(["status"], watch: true);
                    }
                    finally { Console.SetOut(output); Console.SetError(error); }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or TimeoutException)
                { return 6; }
                finally { if (acquired) operation.ReleaseMutex(); }
            }, result =>
            {
                if (result == previous) return;
                Console.WriteLine($"{DateTime.Now:HH:mm:ss} " + (result == 0
                    ? "Myš dostupná, požadovaný směr ověřen."
                    : $"Myš není připravena (kód {result}). Omezené opakování, poté čekání na novou událost."));
                previous = result;
            });
            return exitCode;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or System.ComponentModel.Win32Exception)
        {
            Console.Error.WriteLine($"Nelze přihlásit odběr událostí Windows: {ex.Message}");
            return 6;
        }
        finally { if (!hidden) Console.CancelKeyPress -= cancel; single.ReleaseMutex(); }
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool FreeConsole();
}
