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
