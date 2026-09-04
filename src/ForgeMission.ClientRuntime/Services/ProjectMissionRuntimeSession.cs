using System.Net;
using ForgeMission.ClientRuntime.Transport;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Tools;

namespace ForgeMission.ClientRuntime.Services;

// Owns one Project's Mission container on the Client Runtime side (43.21 task 1): resolving the
// Project from the session's own root, creating the container only when the manifest holds no ID
// yet, writing that ID back after Host acceptance, following the durable stream, and starting one
// child Mission Run per submitted instruction.
//
// The whole point of this class is that it derives everything a run needs and a surface supplies
// almost nothing. A caller sends a command ID and the person's text; the mission comes from the
// persisted selection, the Project goal from the container's pinned Host state, and the
// capabilities from what the session actually authorizes. There is no parameter here through which
// a provider, model, expert, path, or credential could arrive.
internal sealed class ProjectMissionRuntimeSession : IAsyncDisposable
{
    /// <summary>A bound on the instruction, not on the answer. It exists so an accidental paste of
    /// a whole file is refused with a named error instead of being sent to a provider and billed.</summary>
    private const int MaxInputCharacters = 32_000;

    private readonly string _projectHome;
    private readonly ProjectStore _projects;
    private readonly ConversationHostClient _hostClient;
    private readonly ConversationToolHandOff _tools;
    private readonly ConversationTailReader _tail;

    private Guid? _containerId;

    public ProjectMissionRuntimeSession(
        string sessionId,
        string projectHome,
        ProjectStore projects,
        ConversationHostClient hostClient,
        CapabilityRegistry capabilities,
        ICapabilityDispatcher dispatcher,
        Action<ClientRuntimeEvent> publish,
        CancellationToken applicationStopping)
    {
        _projectHome = projectHome;
        _projects = projects;
        _hostClient = hostClient;
        _tools = new ConversationToolHandOff(sessionId, hostClient, capabilities, dispatcher, publish);
        // A Janus child run can request a tool, so this session — unlike the legacy control one —
        // does supply the hand-off hook. A Naive run never reaches it: it is declared zero
        // capabilities and its executor refuses a tool request outright.
        _tail = new ConversationTailReader(sessionId, hostClient, publish, applicationStopping, OnTailEventAsync);
    }

    /// <summary>Opens the Project's Mission container and starts its replay/tail. A stored ID takes
    /// the replay path with no create; a null ID takes the idempotent create path and persists the
    /// returned ID. Idempotent within a session: a second call returns the already-opened container
    /// without touching the Host.</summary>
    public async Task<Guid> OpenAsync(CancellationToken ct)
    {
        if (_containerId is { } alreadyOpen)
            return alreadyOpen;

        var project = ReadProject();
        var containerId = project.Manifest.ProjectMissionContainerId ?? await CreateAndRecordAsync(project, ct);

        _containerId = containerId;
        _tail.Start(containerId);
        return containerId;
    }

    /// <summary>Persists the Project's mission selection and returns the canonical value. The
    /// allow-list is enforced by <see cref="ProjectStore"/>, which is the only writer, so this is a
    /// pass-through rather than a second place the rule could drift.</summary>
    public string SelectMission(string mission) =>
        ProjectMissions.RequireSelected(_projects.SelectMissionFor(_projectHome, mission));

    /// <summary>
    /// Starts one child Mission Run of the Project's SELECTED mission.
    ///
    /// The mission is read from the manifest rather than accepted from the caller — that is what
    /// makes "Presentation never branches execution" structural: there is no argument here it could
    /// pass to choose one. Capabilities are per-run and derived from the mission: Janus is offered
    /// what this session authorizes, Naive is offered nothing at all, so its zero-tool contract
    /// holds before any provider sees a declaration rather than only at the executor's guard.
    /// </summary>
    public async Task<Conversations.Contracts.StartProjectMissionRunResponse> StartRunAsync(
        Guid commandId, string input, CancellationToken ct)
    {
        var containerId = _containerId ?? throw new ProjectMissionNotOpenedException();
        ValidateSubmission(commandId, input);

        var mission = ProjectMissions.RequireSelected(ReadProject().Manifest.SelectedMission);

        return await _hostClient.StartProjectMissionRunAsync(
            new Conversations.Contracts.StartProjectMissionRunRequest(
                containerId, commandId, mission, input, CapabilitiesFor(mission)), ct);
    }

    /// <summary>
    /// What a submission must satisfy before ANY durable work happens.
    ///
    /// It is static and called by the endpoint before the container is even opened, as well as here
    /// — one implementation, two call sites. The early call is the load-bearing one: without it a
    /// blank instruction would create a durable container on its way to being rejected, so a person
    /// who pressed Run by accident would leave a Project changed.
    /// </summary>
    public static void ValidateSubmission(Guid commandId, string input)
    {
        if (commandId == Guid.Empty)
            throw new ProjectOperationException(
                ProjectOperationErrorCode.InvalidMissionInput, "A mission run requires a command id.");

        if (string.IsNullOrWhiteSpace(input))
            throw new ProjectOperationException(
                ProjectOperationErrorCode.InvalidMissionInput, "Enter an instruction to run.");

        if (input.Length > MaxInputCharacters)
            throw new ProjectOperationException(
                ProjectOperationErrorCode.InvalidMissionInput,
                $"That instruction is too long ({input.Length} characters); the limit is {MaxInputCharacters}.");
    }

    // Janus may request a tool and is therefore told what this session can execute. Naive declares
    // none: nothing is offered to the provider, so there is nothing for it to ask for.
    private ConversationCapabilityDeclaration[] CapabilitiesFor(string mission) =>
        mission == ProjectMissions.Janus ? _tools.Declarations : [];

    private ProjectRecord ReadProject() =>
        _projects.Open(_projectHome).Project
            ?? throw new ProjectOperationException(ProjectOperationErrorCode.InvalidManifest,
                $"{_projectHome} holds no Forge Project manifest.");

    // The create command ID is derived from the stable manifest project ID, so this whole method is
    // safe to repeat: a retry after Host acceptance but before the manifest write re-derives the
    // same command ID, reaches the same deterministic container, and gets its original acceptance
    // back rather than creating a second container for the same Project.
    private async Task<Guid> CreateAndRecordAsync(ProjectRecord project, CancellationToken ct)
    {
        var response = await _hostClient.CreateProjectMissionContainerAsync(
            new CreateProjectMissionContainerRequest(
                project.Manifest.ProjectId,
                ConversationDeterministicIds.ProjectMissionContainerCreate(project.Manifest.ProjectId),
                project.Manifest.Goal),
            ct);

        // Only after durable acceptance. A failed write leaves the container valid and reports
        // ManifestWriteFailed — never a new container, never a successful local write.
        _projects.SetProjectMissionContainerId(project.Home, response.ContainerId);
        return response.ContainerId;
    }

    private Task OnTailEventAsync(ConversationEvent evt, CancellationToken ct) =>
        _tools.OnTailEventAsync(_containerId!.Value, evt, ct);

    /// <summary>Maps an expected Conversation-service rejection to this Project's typed error
    /// vocabulary. An unexpected status is left to fail the transport normally rather than being
    /// laundered into a domain code.</summary>
    public static ProjectOperationErrorCode? ToErrorCode(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.BadRequest => ProjectOperationErrorCode.InvalidMissionInput,
        HttpStatusCode.NotFound => ProjectOperationErrorCode.MissionRunNotFound,
        // 409 covers both a genuine conflict and "one run at a time". They are not distinguished
        // here because the status alone cannot tell them apart; the endpoint reads the Host's own
        // reason text to separate them, and defaults to the conflict rather than inventing a
        // busy state that might not be true.
        HttpStatusCode.Conflict => ProjectOperationErrorCode.MissionRunConflict,
        _ => null,
    };

    public ValueTask DisposeAsync() => _tail.DisposeAsync();
}

/// <summary>A run submitted before the Project's Mission container was opened for this session. A
/// dedicated type rather than a bare <see cref="InvalidOperationException"/>, for the same reason
/// its Mission Control counterpart is: the endpoint maps THIS to a typed outcome, and mapping the
/// general exception would also launder unrelated faults into a domain error.</summary>
internal sealed class ProjectMissionNotOpenedException()
    : Exception("This Project's missions have not been opened for this session.");
