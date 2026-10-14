using System.Text.Json;

namespace MxThumbWheelReader;

internal sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _path = System.IO.Path.Combine(AppContext.BaseDirectory, "appsettings.json");

    public AppSettings Load()
    {
        if (!File.Exists(_path)) return new AppSettings();

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_path), JsonOptions)
                   ?? new AppSettings();
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Neplatný JSON v {_path}: {ex.Message}", ex);
        }
    }

    public void Save(MouseSettings mouse)
    {
        var settings = new AppSettings
        {
            ComputerName = Environment.MachineName,
            Mouse = mouse
        };

        string temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(settings, JsonOptions));
        File.Move(temporaryPath, _path, overwrite: true);
    }

    public string Path => _path;
}

internal sealed class AppSettings
{
    public string ComputerName { get; set; } = "";
    public MouseSettings? Mouse { get; set; }
}

internal sealed class MouseSettings
{
    public int VendorId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string DevicePath { get; set; } = "";
    public byte DeviceIndex { get; set; }
    public bool DirectionInverted { get; set; }
}
