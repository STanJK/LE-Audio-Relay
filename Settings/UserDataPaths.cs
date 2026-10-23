namespace LEAudioRelay.Settings;

internal static class UserDataPaths
{
    public static string ProductRoot { get; } =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "LEAudioLab",
            "LEAudioRelay");

    public static string Logs { get; } =
        Path.Combine(
            ProductRoot,
            "logs");
}
