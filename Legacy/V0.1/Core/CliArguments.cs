namespace LEKeepAliveRelay.Core;

internal static class CliArguments
{
    public static bool Has(string[] args, string name) =>
        args.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));

    public static string Get(string[] args, string name, string fallback) =>
        GetNullable(args, name) ?? fallback;

    public static string GetRequired(string[] args, string name)
    {
        string? value = GetNullable(args, name);

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"Missing required argument {name}.");
        }

        return value;
    }

    public static int GetInt(
        string[] args,
        string name,
        int fallback,
        int min,
        int max)
    {
        string? raw = GetNullable(args, name);

        if (raw is not null && int.TryParse(raw, out int value))
        {
            return Math.Clamp(value, min, max);
        }

        return fallback;
    }

    private static string? GetNullable(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return args[i + 1];
            }
        }

        return null;
    }
}
