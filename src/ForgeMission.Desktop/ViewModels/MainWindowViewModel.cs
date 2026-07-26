using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ForgeMission.Core.Runtime;
using ForgeMission.Core.Tools;
using ForgeMission.Desktop.Services;

namespace ForgeMission.Desktop.ViewModels;

public partial class MainWindowViewModel(VanillaMissionSessionFactory sessionFactory) : ViewModelBase
{
    private LocalDiskWorkspace? workspace;

    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    [ObservableProperty]
    public partial string ComposeText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    [NotifyPropertyChangedFor(nameof(ComposePlaceholder))]
    [NotifyPropertyChangedFor(nameof(IsWorkspaceClosed))]
    [NotifyPropertyChangedFor(nameof(IsWorkspaceLabelVisible))]
    public partial bool IsWorkspaceOpen { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsWorkspaceLabelVisible))]
    public partial string WorkspaceLabel { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SendCommand))]
    public partial bool IsSending { get; set; }

    public string ComposePlaceholder => IsWorkspaceOpen
        ? "Describe what you want to build"
        : "Add a folder to start";

    public bool IsWorkspaceClosed => !IsWorkspaceOpen;

    public bool IsWorkspaceLabelVisible => IsWorkspaceOpen && !string.IsNullOrWhiteSpace(WorkspaceLabel);

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
            var session = sessionFactory.Create(
                workspace,
                (notification, ct) => UpdateToolCallAsync(assistantMessage, notification, ct));
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

    private static Task UpdateToolCallAsync(
        ChatMessageViewModel message,
        ToolCallNotification notification,
        CancellationToken ct)
    {
        return Dispatcher.UIThread.InvokeAsync(() =>
        {
            ct.ThrowIfCancellationRequested();
            if (notification.State == ToolCallNotificationState.Running)
                message.StartToolCall(notification.Call);
            else
                message.FinishToolCall(notification.Call);
        }).GetTask();
    }
}
