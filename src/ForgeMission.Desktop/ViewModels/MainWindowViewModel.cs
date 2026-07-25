using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ForgeMission.Core.Runtime;
using ForgeMission.Core.Tools;
using ForgeMission.Desktop.Services;

namespace ForgeMission.Desktop.ViewModels;

public partial class MainWindowViewModel(VanillaMissionSessionFactory sessionFactory) : ViewModelBase
{
    private LocalDiskWorkspace? workspace;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } =
    [
        new("Forge", "Open a folder to begin."),
    ];

    [ObservableProperty]
    public partial string ComposeText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsWorkspaceOpen { get; set; }

    [ObservableProperty]
    public partial string WorkspaceLabel { get; set; } = "No folder open";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsSending { get; set; }

    private bool CanSend() => IsWorkspaceOpen && !IsSending && !string.IsNullOrWhiteSpace(ComposeText);

    partial void OnComposeTextChanged(string value) => SendCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private async Task OpenFolderAsync(TopLevel topLevel)
    {
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open workspace folder",
            AllowMultiple = false,
        });

        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(path))
            return;

        workspace = new LocalDiskWorkspace(path);
        WorkspaceLabel = workspace.Roots[0];
        IsWorkspaceOpen = true;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (workspace is null)
            return;

        var goal = ComposeText.Trim();
        Messages.Add(new ChatMessageViewModel("You", goal));
        ComposeText = string.Empty;

        var assistantMessage = new ChatMessageViewModel("Forge", string.Empty);
        Messages.Add(assistantMessage);
        IsSending = true;

        try
        {
            // Each Send is a fresh one-shot run. Visible earlier messages are not mission context.
            var session = sessionFactory.Create(workspace);
            var result = await session.AgenticSession.RunAsync(new PipelineRunOptions(
                session.MissionName,
                new Dictionary<string, string> { ["goal"] = goal },
                ContentWriter: new UiStreamingTextWriter(assistantMessage)));

            assistantMessage.Text = result.Status == MissionStatus.Fail
                ? $"Mission failed: {result.FailReason}"
                : result.Text;
        }
        catch (Exception ex)
        {
            assistantMessage.Text = ex.Message;
        }
        finally
        {
            IsSending = false;
        }
    }
}
