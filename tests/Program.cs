using HidSharp;
using MxThumbWheelReader;

static void Check(bool condition, string message)
{ if (!condition) throw new Exception(message); }
var store = new AppSettingsStore();
store.Save(new AppSettings());
var reader = new LogitechReader();
Check(reader.RunOnce(["--inverted"]) == 1, "Offline command should report no device");
Check(store.Load().DesiredDirectionInverted == true, "Offline preference lost");
DeviceList.Online = true;
Check(reader.RunOnce(["status"], watch: true) == 0 && DeviceList.Inverted, "Reconnect did not apply preference");
Check(DeviceList.Writes == 1, "Expected one hardware write");
var timestamp = File.GetLastWriteTimeUtc(store.Path);
reader.RunOnce(["status"], watch: true);
Check(DeviceList.Writes == 1, "Healthy check wrote to device again");
Check(File.GetLastWriteTimeUtc(store.Path) == timestamp, "Healthy check rewrote config");
DeviceList.Online = false;
Check(reader.RunOnce(["status"], watch: true) == 1, "Disconnect not detected");
DeviceList.Inverted = false;
DeviceList.Online = true;
reader.RunOnce(["--status"]);
Check(store.Load().DesiredDirectionInverted == true, "Status overwrote desired direction");
reader.RunOnce(["status"], watch: true);
Check(DeviceList.Inverted && DeviceList.Writes == 2, "Reset after reconnect was not repaired");
reader.RunOnce(["--normal"]);
Check(store.Load().DesiredDirectionInverted == false && !DeviceList.Inverted, "Normal preference failed");
store.Save(new AppSettings { Mouse = new MouseSettings { DirectionInverted = true } });
Check(reader.RunOnce(["status"], watch: true) == 8, "Legacy observation must not become desired state");
Console.WriteLine("PASS: offline preference, reconnect/reset, no redundant writes, status preservation, normal and legacy config.");

// An event must wake the controller; time passing alone must not query HID.
using (var stop = new ManualResetEvent(false))
using (var changed = new AutoResetEvent(false))
using (var applied = new AutoResetEvent(false))
{
    int calls = 0;
    var task = Task.Run(() => new EventReconciler().Run(stop, changed,
        () => { Interlocked.Increment(ref calls); return 0; }, _ => applied.Set()));
    try
    {
        Check(applied.WaitOne(3000), "Missing initial application");
        Check(!applied.WaitOne(5500) && calls == 1, "Idle watcher polled without an event");
        changed.Set();
        Check(applied.WaitOne(2000) && calls == 2, "Reconnect event did not trigger immediate work");
    }
    finally { stop.Set(); Check(task.Wait(3000), "Idle shutdown blocked"); }
}
using (var stop = new ManualResetEvent(false))
using (var changed = new AutoResetEvent(false))
using (var exhausted = new ManualResetEvent(false))
{
    int calls = 0;
    var task = Task.Run(() => new EventReconciler().Run(stop, changed,
        () => { Interlocked.Increment(ref calls); return 1; }, _ => { if (calls == 4) exhausted.Set(); }));
    try
    {
        Check(exhausted.WaitOne(6000), "Readiness retry burst did not complete");
        Thread.Sleep(2500);
        Check(calls == 4, "Offline watcher continued polling after retry limit");
    }
    finally { stop.Set(); Check(task.Wait(3000), "Offline shutdown blocked"); }
}
Console.WriteLine("PASS: event wakeup, no idle polling, bounded readiness retries, interruptible shutdown.");

using (var stop = new ManualResetEvent(false))
using (var changed = new AutoResetEvent(false))
{
    var task = Task.Run(() => new EventReconciler().Run(stop, changed, () => 0, _ => { }));
    using (var tray = new WatchTray(() => stop.Set()))
    {
        Check(!stop.WaitOne(100), "Tray stopped watcher during startup");
        tray.ClickExit();
        Check(task.Wait(3000), "Tray Exit did not stop watcher");
    }
}
Console.WriteLine("PASS: tray Exit stops watcher and UI thread disposes cleanly.");
