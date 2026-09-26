using System.Reflection;
using System.Text;

namespace LEAudioRelay.Telemetry;

/// <summary>
/// Low-volume persistent lifecycle journal.
/// Deliberately excludes heartbeat, ring warnings, overflow warnings, and
/// per-second telemetry.
/// </summary>
internal sealed class LifecycleEventLog
{
    private readonly object _gate =
        new();

    private readonly string _directory;

    public LifecycleEventLog()
    {
        _directory =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "LEAudioRelay",
                "logs");
    }

    public string DirectoryPath =>
        _directory;

    public static string ApplicationVersion
    {
        get
        {
            Assembly assembly =
                Assembly.GetEntryAssembly() ??
                Assembly.GetExecutingAssembly();

            return assembly
                       .GetCustomAttribute<
                           AssemblyInformationalVersionAttribute>()
                       ?.InformationalVersion
                   ?? assembly
                       .GetName()
                       .Version
                       ?.ToString()
                   ?? "unknown";
        }
    }

    public void Write(
        string eventName,
        string? detail = null)
    {
        try
        {
            lock (_gate)
            {
                Directory.CreateDirectory(
                    _directory);

                DateTimeOffset now =
                    DateTimeOffset.Now;

                string path =
                    Path.Combine(
                        _directory,
                        $"lifecycle-{now:yyyy-MM-dd}.log");

                string cleanDetail =
                    Sanitize(
                        detail);

                string line =
                    string.IsNullOrEmpty(
                        cleanDetail)
                        ? $"{now:O}\t{eventName}"
                        : $"{now:O}\t{eventName}\t{cleanDetail}";

                File.AppendAllText(
                    path,
                    line +
                    Environment.NewLine,
                    new UTF8Encoding(
                        encoderShouldEmitUTF8Identifier: false));
            }
        }
        catch
        {
            // Logging must never affect routing or lifecycle recovery.
        }
    }

    private static string Sanitize(
        string? value)
    {
        if (string.IsNullOrWhiteSpace(
                value))
        {
            return string.Empty;
        }

        return value
            .Replace(
                '\r',
                ' ')
            .Replace(
                '\n',
                ' ')
            .Replace(
                '\t',
                ' ')
            .Trim();
    }
}
