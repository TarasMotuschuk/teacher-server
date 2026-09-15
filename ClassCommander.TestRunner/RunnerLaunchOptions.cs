namespace ClassCommander.TestRunner;

internal sealed class RunnerLaunchOptions
{
    public static RunnerLaunchOptions Current { get; private set; } = new();

    public string? ServerUrl { get; private set; }

    public string? Surname { get; private set; }

    public string? Name { get; private set; }

    public string? ClassName { get; private set; }

    public string? DeviceId { get; private set; }

    public bool AutoContinue { get; private set; }

    public string? Language { get; private set; }

    public static void Apply(string[] args)
    {
        var options = new RunnerLaunchOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (TryReadValue(args, ref i, arg, "--server-url", "--server", out var server))
            {
                options.ServerUrl = server;
            }
            else if (TryReadValue(args, ref i, arg, "--surname", out var surname))
            {
                options.Surname = surname;
            }
            else if (TryReadValue(args, ref i, arg, "--name", out var name))
            {
                options.Name = name;
            }
            else if (TryReadValue(args, ref i, arg, "--class", out var className))
            {
                options.ClassName = className;
            }
            else if (TryReadValue(args, ref i, arg, "--device-id", "--device", out var deviceId))
            {
                options.DeviceId = deviceId;
            }
            else if (TryReadValue(args, ref i, arg, "--language", "--lang", out var language))
            {
                options.Language = language;
            }
            else if (string.Equals(arg, "--auto-continue", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(arg, "--auto", StringComparison.OrdinalIgnoreCase))
            {
                options.AutoContinue = true;
            }
        }

        Current = options;
    }

    private static bool TryReadValue(string[] args, ref int index, string arg, string option, out string value)
        => TryReadValue(args, ref index, arg, option, null, out value);

    private static bool TryReadValue(string[] args, ref int index, string arg, string option, string? alias, out string value)
    {
        value = string.Empty;
        if (string.Equals(arg, option, StringComparison.OrdinalIgnoreCase)
            || (alias is not null && string.Equals(arg, alias, StringComparison.OrdinalIgnoreCase)))
        {
            if (index + 1 >= args.Length)
            {
                return false;
            }

            index++;
            value = args[index].Trim().Trim('"');
            return !string.IsNullOrWhiteSpace(value);
        }

        var prefixes = alias is null ? new[] { option + "=" } : new[] { option + "=", alias + "=" };
        foreach (var prefix in prefixes)
        {
            if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = arg[prefix.Length..].Trim().Trim('"');
                return !string.IsNullOrWhiteSpace(value);
            }
        }

        return false;
    }
}
