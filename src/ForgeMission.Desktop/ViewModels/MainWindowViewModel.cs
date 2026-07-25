using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ForgeMission.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public ObservableCollection<ChatMessageViewModel> Messages { get; } =
    [
        new("Forge", "Choose a mission and describe the work you want to do."),
    ];

    [ObservableProperty]
    public partial string ComposeText { get; set; } = string.Empty;

    [RelayCommand]
    private void Send()
    {
        if (string.IsNullOrWhiteSpace(ComposeText))
        {
            return;
        }

        Messages.Add(new ChatMessageViewModel("You", ComposeText));
        ComposeText = string.Empty;
    }
}
