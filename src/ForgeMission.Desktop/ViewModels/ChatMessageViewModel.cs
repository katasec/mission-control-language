namespace ForgeMission.Desktop.ViewModels;

public sealed class ChatMessageViewModel(string sender, string text)
{
    public string Sender { get; } = sender;

    public string Text { get; } = text;
}
