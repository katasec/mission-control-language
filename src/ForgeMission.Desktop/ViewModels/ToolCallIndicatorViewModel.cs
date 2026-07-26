using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;

namespace ForgeMission.Desktop.ViewModels;

public sealed partial class ToolCallIndicatorViewModel : ObservableObject
{
    private const int MaxTargetLength = 80;

    private readonly string toolName;
    private readonly string target;

    public ToolCallIndicatorViewModel(FunctionCallContent call)
    {
        toolName = call.Name;
        target = TargetFor(call);
        Refresh();
    }

    [ObservableProperty]
    public partial bool IsRunning { get; set; } = true;

    [ObservableProperty]
    public partial string Glyph { get; set; } = "○";

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    public void MarkDone()
    {
        IsRunning = false;
        Refresh();
    }

    private void Refresh()
    {
        Glyph = IsRunning ? "○" : "✓";
        Text = IsRunning
            ? $"{RunningVerb(toolName)} {target}…"
            : $"{DoneVerb(toolName)} {target}";
    }

    private static string RunningVerb(string name) => name switch
    {
        "Read" => "Reading",
        "Edit" => "Editing",
        "Write" => "Writing",
        "Bash" => "Running",
        _ => "Using",
    };

    private static string DoneVerb(string name) => name switch
    {
        "Read" => "Read",
        "Edit" => "Edited",
        "Write" => "Wrote",
        "Bash" => "Ran",
        _ => "Used",
    };

    private static string TargetFor(FunctionCallContent call)
    {
        var key = call.Name == "Bash" ? "command" : "file_path";
        return Truncate(TryGetString(call.Arguments, key) ?? call.Name);
    }

    private static string? TryGetString(IDictionary<string, object?>? arguments, string key)
    {
        if (arguments is null || !arguments.TryGetValue(key, out var value))
            return null;

        return value switch
        {
            string text when !string.IsNullOrWhiteSpace(text) => text,
            _ when value is not null => value.ToString(),
            _ => null,
        };
    }

    private static string Truncate(string value)
    {
        if (value.Length <= MaxTargetLength)
            return value;

        return value[..(MaxTargetLength - 1)] + "…";
    }
}
