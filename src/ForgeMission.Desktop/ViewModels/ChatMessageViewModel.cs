using CommunityToolkit.Mvvm.ComponentModel;

namespace ForgeMission.Desktop.ViewModels;

public sealed partial class ChatMessageViewModel : ObservableObject
{
    public ChatMessageViewModel(string sender, string text)
    {
        Sender = sender;
        Text = text;
    }

    public string Sender { get; }

    [ObservableProperty]
    public partial string Text { get; set; }

    public void Append(string value) => Text += value;
}
