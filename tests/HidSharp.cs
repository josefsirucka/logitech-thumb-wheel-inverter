namespace HidSharp;

public sealed class DeviceList
{
    public static DeviceList Local { get; } = new();
    public static bool Online;
    public static bool Inverted;
    public static int Writes;
    public IEnumerable<HidDevice> GetHidDevices(int vendorId) => Online ? [new()] : [];
}
public sealed class HidDevice
{
    public int VendorID => 0x046D;
    public int ProductID => 123;
    public string DevicePath => "test-mouse";
    public string GetProductName() => "Simulated MX";
    public int GetMaxInputReportLength() => 20;
    public int GetMaxOutputReportLength() => 20;
    public bool TryOpen(out HidStream stream) { stream = new(); return true; }
}
public sealed class HidStream : IDisposable
{
    private byte[] response = new byte[20];
    public HidDevice Device { get; } = new();
    public int ReadTimeout { get; set; }
    public int WriteTimeout { get; set; }
    public void Write(byte[] request)
    {
        response = (byte[])request.Clone();
        if (request[2] == 0) response[4] = 3;
        else if ((request[3] >> 4) == 1)
        { response[4] = 0; response[5] = DeviceList.Inverted ? (byte)1 : (byte)0; }
        else if ((request[3] >> 4) == 2)
        { DeviceList.Inverted = request[5] == 1; DeviceList.Writes++; }
    }
    public int Read(byte[] buffer, int offset, int count)
    { response.CopyTo(buffer, offset); return response.Length; }
    public void Dispose() { }
}
