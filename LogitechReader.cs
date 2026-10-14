using System.Diagnostics;
using System.Text;
using HidSharp;

namespace MxThumbWheelReader;

public sealed class LogitechReader
{
    private const int LogitechVendorId = 0x046D;

    // HID++ 2.0 long report.
    private const byte LongReportId = 0x11;

    // Libovolné nenulové Software ID v rozsahu 1–15.
    private const byte SoftwareId = 0x0A;

    // Logitech HID++ feature pro horizontální palcové kolečko.
    private const ushort ThumbWheelFeatureId = 0x2150;

    // 0xFF = zařízení připojené přímo přes Bluetooth/USB.
    // 1–6 = zařízení připojená přes Logitech receiver.
    private static readonly byte[] DeviceIndices =
    [
        0xFF,
        0x01,
        0x02,
        0x03,
        0x04,
        0x05,
        0x06
    ];

    public int Run(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        ThumbWheelCommand? command = ParseCommand(args);

        if (command is null)
        {
            PrintUsage();
            return 64;
        }

        Console.WriteLine("MX Thumb Wheel Configurator");
        Console.WriteLine("===========================");
        Console.WriteLine($"Příkaz: {FormatCommand(command.Value)}");
        Console.WriteLine();
        Console.WriteLine("Vyhledávám Logitech HID zařízení...");
        Console.WriteLine();

        var settingsStore = new AppSettingsStore();
        AppSettings savedSettings;
        try
        {
            savedSettings = settingsStore.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Nelze načíst appsettings.json: {ex.Message}");
            return 6;
        }

        MouseSettings? preferredMouse =
            savedSettings.ComputerName == Environment.MachineName
                ? savedSettings.Mouse
                : null;

        HidDevice[] devices = DeviceList.Local
            .GetHidDevices(LogitechVendorId)
            .OrderByDescending(device =>
                preferredMouse is not null &&
                device.ProductID == preferredMouse.ProductId &&
                device.DevicePath == preferredMouse.DevicePath)
            .ToArray();

        if (devices.Length == 0)
        {
            Console.WriteLine("Nebyla nalezena žádná Logitech HID zařízení.");
            return 1;
        }

        Console.WriteLine($"Nalezeno Logitech HID rozhraní: {devices.Length}");
        Console.WriteLine();

        foreach (HidDevice device in devices)
        {
            string productName = GetProductNameSafely(device);
            int inputLength = GetInputLengthSafely(device);
            int outputLength = GetOutputLengthSafely(device);

            Console.WriteLine(
                $"VID={device.VendorID:X4}, PID={device.ProductID:X4}, " +
                $"IN={inputLength}, OUT={outputLength}, " +
                $"Product=\"{productName}\"");

            Console.WriteLine($"Path: {device.DevicePath}");

            if (inputLength < 20 || outputLength < 20)
            {
                Console.WriteLine(
                    "  Přeskakuji: rozhraní nepodporuje 20B HID++ report.");

                Console.WriteLine();
                continue;
            }

            if (!device.TryOpen(out HidStream stream))
            {
                Console.WriteLine("  Rozhraní nelze otevřít.");
                Console.WriteLine();
                continue;
            }

            using (stream)
            {
                stream.ReadTimeout = 150;
                stream.WriteTimeout = 500;

                IEnumerable<byte> indices = preferredMouse is not null &&
                    device.ProductID == preferredMouse.ProductId &&
                    device.DevicePath == preferredMouse.DevicePath
                    ? new[] { preferredMouse.DeviceIndex }.Concat(DeviceIndices).Distinct()
                    : DeviceIndices;

                foreach (byte deviceIndex in indices)
                {
                    ThumbWheelResult? currentState = TryReadThumbWheel(
                        stream,
                        outputLength,
                        deviceIndex);

                    if (currentState is null)
                    {
                        continue;
                    }

                    PrintFoundDevice(
                        productName,
                        deviceIndex,
                        currentState);

                    if (!SaveSettings(settingsStore, device, productName,
                            deviceIndex, currentState.DirectionInverted))
                    {
                        return 6;
                    }

                    if (command == ThumbWheelCommand.Status)
                    {
                        return 0;
                    }

                    bool targetInverted =
                        command == ThumbWheelCommand.Inverted;

                    if (currentState.DirectionInverted == targetInverted)
                    {
                        Console.WriteLine();
                        Console.WriteLine(
                            "Požadovaný směr už je nastavený. " +
                            "Není potřeba nic zapisovat.");

                        return 0;
                    }

                    Console.WriteLine();
                    Console.WriteLine(
                        $"Nastavuji směr: " +
                        $"{(targetInverted ? "INVERTED" : "NORMAL")}");

                    bool writeSucceeded = TrySetThumbWheelDirection(
                        stream,
                        outputLength,
                        deviceIndex,
                        currentState.FeatureIndex,
                        currentState.ReportingMode,
                        targetInverted,
                        out byte[]? writeResponse);

                    if (!writeSucceeded)
                    {
                        Console.WriteLine();
                        Console.WriteLine(
                            "CHYBA: Myš na nastavovací příkaz neodpověděla.");

                        return 3;
                    }

                    Console.WriteLine(
                        $"Odpověď zápisu: {ToHex(writeResponse!)}");

                    ThumbWheelResult? verifiedState =
                        VerifyThumbWheelDirection(
                            stream,
                            outputLength,
                            deviceIndex,
                            targetInverted);

                    if (verifiedState is null)
                    {
                        Console.WriteLine();
                        Console.WriteLine(
                            "CHYBA: Po zápisu se nepodařilo stav znovu přečíst.");

                        return 4;
                    }

                    Console.WriteLine();
                    Console.WriteLine("Ověření po zápisu:");
                    Console.WriteLine(
                        $"  Reporting mode:     " +
                        $"{FormatMode(verifiedState.ReportingMode)}");

                    Console.WriteLine(
                        $"  Direction inverted: " +
                        $"{(verifiedState.DirectionInverted ? "ANO" : "NE")}");

                    Console.WriteLine(
                        $"  Status flags:       0x{verifiedState.Flags:X2}");

                    Console.WriteLine(
                        $"  HID++ odpověď:      " +
                        $"{ToHex(verifiedState.RawResponse)}");

                    if (verifiedState.DirectionInverted != targetInverted)
                    {
                        Console.WriteLine();
                        Console.WriteLine(
                            "CHYBA: Myš odpověděla, ale požadovaná hodnota " +
                            "se neuložila.");

                        return 5;
                    }

                    if (!SaveSettings(settingsStore, device, productName,
                            deviceIndex, verifiedState.DirectionInverted))
                    {
                        return 6;
                    }

                    Console.WriteLine();
                    Console.WriteLine("Nastavení bylo úspěšně změněno a ověřeno.");

                    return 0;
                }
            }

            Console.WriteLine(
                "  Feature 0x2150 nebyla na tomto rozhraní nalezena.");

            Console.WriteLine();
        }

        Console.WriteLine();
        Console.WriteLine(
            "Logitech zařízení byla nalezena, ale žádné neodpovědělo");

        Console.WriteLine(
            "jako HID++ myš s feature 0x2150.");

        Console.WriteLine();
        Console.WriteLine(
            "Před dalším spuštěním pohněte myší, aby nebyla uspána.");

        return 2;
    }

    private static bool SaveSettings(
        AppSettingsStore store,
        HidDevice device,
        string productName,
        byte deviceIndex,
        bool directionInverted)
    {
        try
        {
            store.Save(new MouseSettings
            {
                VendorId = device.VendorID,
                ProductId = device.ProductID,
                ProductName = productName,
                DevicePath = device.DevicePath,
                DeviceIndex = deviceIndex,
                DirectionInverted = directionInverted
            });
            Console.WriteLine($"Konfigurace uložena: {store.Path}");
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Konfiguraci nelze uložit: {ex.Message}");
            return false;
        }
    }

    private static ThumbWheelCommand? ParseCommand(string[] args)
    {
        if (args.Length == 0)
        {
            return ThumbWheelCommand.Status;
        }

        if (args.Length != 1)
        {
            return null;
        }

        return args[0].Trim().ToLowerInvariant() switch
        {
            "status" => ThumbWheelCommand.Status,

            "normal" => ThumbWheelCommand.Normal,

            "inverted" => ThumbWheelCommand.Inverted,
            "invert" => ThumbWheelCommand.Inverted,

            _ => null
        };
    }

    private static ThumbWheelResult? VerifyThumbWheelDirection(
        HidStream stream,
        int outputLength,
        byte deviceIndex,
        bool expectedInverted)
    {
        // Firmware může potřebovat krátkou chvíli na aplikování změny.
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            Thread.Sleep(100);

            ThumbWheelResult? state = TryReadThumbWheel(
                stream,
                outputLength,
                deviceIndex);

            if (state is null)
            {
                continue;
            }

            if (state.DirectionInverted == expectedInverted)
            {
                return state;
            }

            // Poslední přečtený stav vrátíme i v případě,
            // že se hodnota nezměnila.
            if (attempt == 3)
            {
                return state;
            }
        }

        return null;
    }

    private static bool TrySetThumbWheelDirection(
        HidStream stream,
        int outputLength,
        byte deviceIndex,
        byte featureIndex,
        byte reportingMode,
        bool inverted,
        out byte[]? response)
    {
        /*
         * Feature 0x2150, function 2:
         *
         * byte 0 = reporting mode
         * byte 1 = invert direction
         * byte 2 = reserved
         *
         * Zachováváme aktuální reporting mode.
         */
        response = HidppCall(
            stream,
            outputLength,
            deviceIndex,
            featureIndex,
            functionId: 0x02,
            parameters:
            [
                reportingMode,
                inverted ? (byte)0x01 : (byte)0x00,
                0x00
            ]);

        return response is not null;
    }

    private static ThumbWheelResult? TryReadThumbWheel(
        HidStream stream,
        int outputLength,
        byte deviceIndex)
    {
        // IRoot.GetFeature(0x2150)
        byte[]? featureResponse = HidppCall(
            stream,
            outputLength,
            deviceIndex,
            featureIndex: 0x00,
            functionId: 0x00,
            parameters:
            [
                (byte)(ThumbWheelFeatureId >> 8),
                (byte)(ThumbWheelFeatureId & 0xFF),
                0x00
            ]);

        if (featureResponse is null || featureResponse.Length < 6)
        {
            return null;
        }

        byte thumbWheelFeatureIndex = featureResponse[4];

        if (thumbWheelFeatureIndex == 0x00)
        {
            return null;
        }

        // Feature 0x2150, function 1 = getThumbwheelStatus.
        byte[]? statusResponse = HidppCall(
            stream,
            outputLength,
            deviceIndex,
            thumbWheelFeatureIndex,
            functionId: 0x01,
            parameters:
            [
                0x00,
                0x00,
                0x00
            ]);

        if (statusResponse is null || statusResponse.Length < 6)
        {
            return null;
        }

        byte reportingMode = statusResponse[4];
        byte flags = statusResponse[5];

        return new ThumbWheelResult(
            FeatureIndex: thumbWheelFeatureIndex,
            ReportingMode: reportingMode,
            Flags: flags,
            DirectionInverted: (flags & 0x01) != 0,
            RawResponse: statusResponse);
    }

    private static byte[]? HidppCall(
        HidStream stream,
        int outputLength,
        byte deviceIndex,
        byte featureIndex,
        byte functionId,
        byte[] parameters)
    {
        if (outputLength < 20)
        {
            return null;
        }

        byte[] request = new byte[outputLength];

        request[0] = LongReportId;
        request[1] = deviceIndex;
        request[2] = featureIndex;

        // Horní 4 bity = function ID.
        // Dolní 4 bity = software ID.
        request[3] = (byte)((functionId << 4) | SoftwareId);

        int parameterCount = Math.Min(
            parameters.Length,
            request.Length - 4);

        Array.Copy(
            parameters,
            sourceIndex: 0,
            request,
            destinationIndex: 4,
            length: parameterCount);

        try
        {
            stream.Write(request);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is TimeoutException)
        {
            return null;
        }

        Stopwatch timeout = Stopwatch.StartNew();

        while (timeout.ElapsedMilliseconds < 900)
        {
            byte[] response =
                new byte[Math.Max(
                    stream.Device.GetMaxInputReportLength(),
                    20)];

            int count;

            try
            {
                count = stream.Read(
                    response,
                    offset: 0,
                    count: response.Length);
            }
            catch (TimeoutException)
            {
                continue;
            }
            catch (IOException)
            {
                return null;
            }

            if (count < 5)
            {
                continue;
            }

            Array.Resize(ref response, count);

            // Odpověď jiného zařízení za receiverem.
            if (response[1] != deviceIndex)
            {
                continue;
            }

            // HID++ 1.0 / receiver error.
            if (response[0] == 0x10 &&
                response[2] == 0x8F)
            {
                return null;
            }

            // HID++ 2.0 error.
            if (response[0] == 0x11 &&
                response[2] == 0xFF)
            {
                return null;
            }

            if (response[0] != LongReportId)
            {
                continue;
            }

            if (response[2] != featureIndex)
            {
                continue;
            }

            // Události nebo odpovědi určené jiné aplikaci ignorujeme.
            if ((response[3] & 0x0F) != SoftwareId)
            {
                continue;
            }

            if ((response[3] >> 4) != functionId)
            {
                continue;
            }

            return response;
        }

        return null;
    }

    private static void PrintFoundDevice(
        string productName,
        byte deviceIndex,
        ThumbWheelResult result)
    {
        Console.WriteLine();
        Console.WriteLine(
            "======================================");

        Console.WriteLine(
            "MX myš s horizontálním kolečkem nalezena");

        Console.WriteLine(
            "======================================");

        Console.WriteLine(
            $"HID product:        {productName}");

        Console.WriteLine(
            $"Device index:       0x{deviceIndex:X2}");

        Console.WriteLine(
            $"Feature 0x2150:     index 0x{result.FeatureIndex:X2}");

        Console.WriteLine(
            $"Reporting mode:     {FormatMode(result.ReportingMode)}");

        Console.WriteLine(
            $"Direction inverted: " +
            $"{(result.DirectionInverted ? "ANO" : "NE")}");

        Console.WriteLine(
            $"Status flags:       0x{result.Flags:X2}");

        Console.WriteLine();
        Console.WriteLine(
            $"Celá HID++ odpověď: {ToHex(result.RawResponse)}");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Neplatný příkaz.");
        Console.WriteLine();
        Console.WriteLine("Použití:");
        Console.WriteLine("  dotnet run -- status");
        Console.WriteLine("  dotnet run -- normal");
        Console.WriteLine("  dotnet run -- inverted");
    }

    private static string FormatCommand(ThumbWheelCommand command)
    {
        return command switch
        {
            ThumbWheelCommand.Status => "zjistit aktuální stav",
            ThumbWheelCommand.Normal => "nastavit NORMAL",
            ThumbWheelCommand.Inverted => "nastavit INVERTED",
            _ => command.ToString()
        };
    }

    private static string FormatMode(byte mode)
    {
        return mode switch
        {
            0x00 => "Native / standardní HID (0)",
            0x01 => "Diverted / události přes HID++ (1)",
            _ => $"Neznámý režim 0x{mode:X2}"
        };
    }

    private static string ToHex(byte[] bytes)
    {
        return Convert.ToHexString(bytes);
    }

    private static string GetProductNameSafely(HidDevice device)
    {
        try
        {
            return device.GetProductName() ?? "(bez názvu)";
        }
        catch
        {
            return "(název nelze přečíst)";
        }
    }

    private static int GetInputLengthSafely(HidDevice device)
    {
        try
        {
            return device.GetMaxInputReportLength();
        }
        catch
        {
            return 0;
        }
    }

    private static int GetOutputLengthSafely(HidDevice device)
    {
        try
        {
            return device.GetMaxOutputReportLength();
        }
        catch
        {
            return 0;
        }
    }

    private enum ThumbWheelCommand
    {
        Status,
        Normal,
        Inverted
    }

    private sealed record ThumbWheelResult(
        byte FeatureIndex,
        byte ReportingMode,
        byte Flags,
        bool DirectionInverted,
        byte[] RawResponse);
}
