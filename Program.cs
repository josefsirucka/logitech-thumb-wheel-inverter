namespace MxThumbWheelReader;

internal static class Program
{
    private static int Main(string[] args) => new LogitechReader().Run(args);
}
