using ForgeMission.ClientRuntime.Services;
using ForgeMission.Conversations.Contracts;

return await ProjectStoreProbe.RunAsync(args);

internal static class ProjectStoreProbe
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 4 || !Guid.TryParse(args[2], out var containerId) || !int.TryParse(args[3], out var count))
            return 2;

        if (TryParseCrashBoundary(args[1], out var crashBoundary))
        {
            var crashStore = new ProjectStore(
                args[0],
                new ProjectManifestFile(publicationBoundary: reached =>
                {
                    if (reached == crashBoundary)
                        Environment.FailFast($"Crash probe at {reached}.");
                }));
            await crashStore.SetProjectMissionContainerIdAsync(
                Path.Combine(args[0], "process-0"), containerId, CancellationToken.None);
            return 3;
        }

        var store = new ProjectStore(args[0]);
        for (var index = 0; index < count; index++)
        {
            var home = Path.Combine(args[0], $"process-{index}");
            if (args[1] == "select")
                await store.SelectMissionAsync(home, "Naive", CancellationToken.None);
            else if (args[1] == "container")
                await store.SetProjectMissionContainerIdAsync(home, containerId, CancellationToken.None);
            else if (args[1] == "receipt")
            {
                var commandId = store.ReadForHome(home).Manifest.Submission?.CommandId
                    ?? throw new InvalidOperationException("The receipt probe requires a prepared submission.");
                await store.RecordSubmissionAcceptedAsync(
                    home,
                    commandId,
                    new ProjectSubmissionAcceptance(containerId, Guid.NewGuid(), index + 1, ConversationRunStatus.Queued),
                    CancellationToken.None);
            }
            else
                return 2;
        }

        return 0;
    }

    private static bool TryParseCrashBoundary(string operation, out ProjectManifestPublicationBoundary boundary)
    {
        boundary = operation switch
        {
            "crash-before-flush" => ProjectManifestPublicationBoundary.BeforeFlush,
            "crash-before-rename" => ProjectManifestPublicationBoundary.BeforeRename,
            "crash-after-rename" => ProjectManifestPublicationBoundary.AfterRename,
            _ => default,
        };
        return operation.StartsWith("crash-", StringComparison.Ordinal);
    }
}
