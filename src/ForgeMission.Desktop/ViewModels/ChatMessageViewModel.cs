using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;

namespace ForgeMission.Desktop.ViewModels;

public sealed partial class ChatMessageViewModel : ObservableObject
{
    private readonly Dictionary<string, ToolCallIndicatorViewModel> toolCallsById = [];

    public ChatMessageViewModel(string sender, string text)
    {
        Sender = sender;
        Text = text;
    }

    public string Sender { get; }

    public ObservableCollection<ToolCallIndicatorViewModel> ToolCalls { get; } = [];

    [ObservableProperty]
    public partial string Text { get; set; }

    public void Append(string value) => Text += value;

    public void StartToolCall(FunctionCallContent call)
    {
        if (toolCallsById.ContainsKey(call.CallId))
            return;

        var row = new ToolCallIndicatorViewModel(call);
        toolCallsById[call.CallId] = row;
        ToolCalls.Add(row);
    }

    public void FinishToolCall(FunctionCallContent call)
    {
        if (toolCallsById.TryGetValue(call.CallId, out var row))
            row.MarkDone();
    }
}
