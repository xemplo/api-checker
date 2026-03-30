namespace Xemplo.ApiChecker.Cli;

public static class CliArgumentParser
{
    public static CliParseResult Parse(string[] args)
    {
        ArgumentNullException.ThrowIfNull(args);

        string? oldSource = null;
        string? newSource = null;

        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];

            switch (arg)
            {
                case "--old":
                    if (!TryReadValue(args, ref index, out oldSource))
                    {
                        return CliParseResult.Fail("Missing value for --old.");
                    }

                    break;

                case "--new":
                    if (!TryReadValue(args, ref index, out newSource))
                    {
                        return CliParseResult.Fail("Missing value for --new.");
                    }

                    break;

                default:
                    return CliParseResult.Fail($"Unknown argument '{arg}'.");
            }
        }

        if (string.IsNullOrWhiteSpace(oldSource) || string.IsNullOrWhiteSpace(newSource))
        {
            return CliParseResult.Fail("Both --old and --new are required.");
        }

        return CliParseResult.Success(new CliOptions(oldSource, newSource));
    }

    private static bool TryReadValue(string[] args, ref int index, out string? value)
    {
        var nextIndex = index + 1;
        if (nextIndex >= args.Length)
        {
            value = null;
            return false;
        }

        value = args[nextIndex];
        index = nextIndex;
        return true;
    }
}