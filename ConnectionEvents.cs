using System.ComponentModel;
using System.Runtime.InteropServices;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace MxThumbWheelReader;

// Observe Windows' paired-device state, not advertisements or mouse input.
internal sealed class ConnectionEvents : IDisposable
{
    private readonly Action changed;
    private readonly List<DeviceWatcher> watchers = [];
    private readonly Dictionary<string, bool> connected = [];
    private readonly object gate = new();
    private volatile bool disposed;
    private readonly PowerCallback powerCallback;
    private IntPtr powerHandle;
    private const string IsConnected = "System.Devices.Aep.IsConnected";

    public ConnectionEvents(Action changed)
    {
        this.changed = changed;
        powerCallback = (_, type, _) =>
        {
            if (type is 7 or 18) Signal();
            return 0;
        };
        try
        {
            AddBluetoothWatcher(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true));
            AddBluetoothWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(true));
            var hid = DeviceInformation.CreateWatcher(
                "System.Devices.InterfaceClassGuid:=\"{4D1E55B2-F16F-11CF-88CB-001111000030}\"",
                null, DeviceInformationKind.DeviceInterface);
            hid.Added += (_, device) =>
            {
                if (device.Id.Contains("046d", StringComparison.OrdinalIgnoreCase)) Signal();
            };
            hid.Updated += (_, _) => { };
            hid.Removed += (_, _) => { };
            hid.Stopped += (_, _) => OnStopped(hid);
            watchers.Add(hid);
            var parameters = new PowerParameters { Callback = Marshal.GetFunctionPointerForDelegate(powerCallback) };
            uint error = PowerRegisterSuspendResumeNotification(2, ref parameters, out powerHandle);
            if (error != 0) throw new Win32Exception((int)error);
            foreach (var watcher in watchers) watcher.Start();
        }
        catch { Dispose(); throw; }
    }

    private void AddBluetoothWatcher(string selector)
    {
        var watcher = DeviceInformation.CreateWatcher(selector, [IsConnected], DeviceInformationKind.AssociationEndpoint);
        watcher.Added += (_, device) =>
        {
            if (device.Properties.TryGetValue(IsConnected, out object? value) && value is bool state)
            {
                lock (gate) connected[device.Id] = state;
                if (state) Signal();
            }
        };
        watcher.Updated += (_, update) =>
        {
            if (!update.Properties.TryGetValue(IsConnected, out object? value) || value is not bool state) return;
            bool newlyConnected;
            lock (gate)
            {
                newlyConnected = state && (!connected.TryGetValue(update.Id, out bool old) || !old);
                connected[update.Id] = state;
            }
            if (newlyConnected) Signal();
        };
        watcher.Removed += (_, update) => { lock (gate) connected.Remove(update.Id); };
        watcher.Stopped += (_, _) => OnStopped(watcher);
        watchers.Add(watcher);
    }

    private void OnStopped(DeviceWatcher watcher)
    {
        if (!disposed && watcher.Status == DeviceWatcherStatus.Aborted)
        {
            Faulted = true;
            Signal();
        }
    }

    public bool Faulted { get; private set; }

    private void Signal()
    {
        if (disposed) return;
        try { changed(); } catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        disposed = true;
        foreach (var watcher in watchers)
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
                watcher.Stop();
        if (powerHandle != IntPtr.Zero)
        {
            PowerUnregisterSuspendResumeNotification(powerHandle);
            powerHandle = IntPtr.Zero;
        }
        GC.KeepAlive(powerCallback);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint PowerCallback(IntPtr context, uint type, IntPtr setting);
    [StructLayout(LayoutKind.Sequential)]
    private struct PowerParameters { public IntPtr Callback; public IntPtr Context; }
    [DllImport("powrprof.dll")]
    private static extern uint PowerRegisterSuspendResumeNotification(uint flags, ref PowerParameters recipient, out IntPtr registration);
    [DllImport("powrprof.dll")]
    private static extern uint PowerUnregisterSuspendResumeNotification(IntPtr registration);
}
