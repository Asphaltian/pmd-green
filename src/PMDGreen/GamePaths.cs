namespace PMDGreen;

internal static class GamePaths
{
    private const string PortableMarker = "portable.txt";

    public static string UserData { get; } = File.Exists(PortableMarker)
        ? Directory.GetCurrentDirectory()
        : Path.Combine(
            Environment.GetFolderPath(
                OperatingSystem.IsLinux() ? Environment.SpecialFolder.ApplicationData : Environment.SpecialFolder.LocalApplicationData,
                Environment.SpecialFolderOption.Create),
            "Green Rescue Team");
}
