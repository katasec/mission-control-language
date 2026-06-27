namespace ForgeMission.Core.Experts;

public class ExpertLoadException(
    string message,
    string? filePath  = null,
    int     line      = 0,
    int     column    = 0,
    int     endColumn = -1)
    : Exception(message)
{
    public string? FilePath  { get; } = filePath;
    public int     Line      { get; } = line;
    public int     Column    { get; } = column;
    public int     EndColumn { get; } = endColumn;
}
