using System.Text;
using Avalonia.Threading;
using ForgeMission.Desktop.ViewModels;

namespace ForgeMission.Desktop.Services;

internal sealed class UiStreamingTextWriter(ChatMessageViewModel message) : TextWriter
{
    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(char value) => Append(value.ToString());

    public override Task WriteAsync(string? value)
    {
        if (!string.IsNullOrEmpty(value)) Append(value);
        return Task.CompletedTask;
    }

    public override Task WriteAsync(ReadOnlyMemory<char> buffer, CancellationToken ct = default)
    {
        Append(buffer.ToString());
        return Task.CompletedTask;
    }

    private void Append(string value) => Dispatcher.UIThread.Post(() => message.Append(value));
}
