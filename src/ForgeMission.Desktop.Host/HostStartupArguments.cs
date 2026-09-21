namespace ForgeMission.Desktop.Host;

public sealed class HostStartupArguments
{
    private readonly string[] _values;

    public HostStartupArguments(string[] values) => _values = values.AsSpan().ToArray();

    public ReadOnlySpan<string> Values => _values;
}
