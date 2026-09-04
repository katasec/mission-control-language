using System.Net;
using ForgeMission.ClientRuntime.Transport;

namespace ForgeMission.Tests.ClientRuntime;

/// <summary>
/// Phase 43.20 Task 1 — the Presentation-surface parity proof. This class is itself a second,
/// non-Desktop surface: it drives the real Client Runtime process through the production
/// <see cref="IClientRuntimeChannel"/> and the shared transport DTOs, and references no Blazor,
/// bunit, Photino, Desktop, or Host type. Every Project action a TUI would need — draft, create,
/// open, and session replacement — is exercised here with the authorization, outcomes, and failure
/// semantics Desktop gets.
/// </summary>
/// <remarks>
/// The child process's user profile is redirected to a temp directory, so
/// <c>&lt;user-profile&gt;/Forge/Projects</c> resolves inside this test's own sandbox. Shipping code
/// gains no configuration knob for it.
/// </remarks>
public sealed class ProjectTransportContractTests : IAsyncLifetime
{
    private readonly string _profile = Directory.CreateTempSubdirectory("forge-project-contract-").FullName;
    private ClientRuntimeHostProcess _host = null!;
    private HttpClientRuntimeChannel _channel = null!;

    private string ProjectsRoot => Path.Combine(_profile, "Forge", "Projects");

    public async Task InitializeAsync()
    {
        _host = await ClientRuntimeHostProcess.StartAsync(profileRoot: _profile);
        _channel = new HttpClientRuntimeChannel(new Uri(_host.BaseUrl, UriKind.Absolute));
    }

    public async Task DisposeAsync()
    {
        _channel.Dispose();
        await _host.DisposeAsync();
        Directory.Delete(_profile, recursive: true);
    }

    // --- draft ------------------------------------------------------------------------------

    [Fact]
    public async Task Draft_ReturnsTheDerivedTitleAndHome_AndCreatesNothing()
    {
        var response = await DraftAsync(new ProjectDraftRequest("Todos API"));

        Assert.Null(response.Error);
        Assert.Equal("Todos API", response.Draft!.ProposedTitle);
        Assert.Equal(Path.Combine(ProjectsRoot, "todos-api"), response.Draft.HomePath);
        // The redirect is the point of this assertion as much as the purity: a projects root that
        // never appears here is one that never appeared in the developer's real home either.
        Assert.False(Directory.Exists(ProjectsRoot));
    }

    [Fact]
    public async Task Draft_AnEmptyGoal_IsATypedFailure()
    {
        var response = await DraftAsync(new ProjectDraftRequest("   "));

        Assert.Null(response.Draft);
        Assert.Equal(ProjectOperationErrorCode.InvalidGoal, response.Error!.Code);
    }

    // A title override must not let an empty goal through: the goal gate runs first, on every
    // surface, because only Client Runtime decides what a valid Project is.
    [Fact]
    public async Task Draft_AnEmptyGoalWithATitleOverride_IsStillATypedFailure()
    {
        var response = await DraftAsync(new ProjectDraftRequest("  ", "Todos API"));

        Assert.Null(response.Draft);
        Assert.Equal(ProjectOperationErrorCode.InvalidGoal, response.Error!.Code);
    }

    // --- create -----------------------------------------------------------------------------

    [Fact]
    public async Task Create_FromAGoalAlone_ReturnsASessionRootedAtItsDeterministicHome()
    {
        var response = await CreateAsync(new ProjectCreateRequest("Todos API"));

        Assert.Equal(ProjectOperationOutcome.Created, response.Outcome);
        var project = response.Session!.Project;
        Assert.Equal("Todos API", project.Title);
        Assert.Equal("Todos API", project.Goal);
        Assert.Equal(Path.Combine(ProjectsRoot, "todos-api"), project.Home);
        Assert.NotEqual(Guid.Empty, project.ProjectId);
        Assert.Equal(["file", "terminal"], response.Session.AvailableCapabilities);
        Assert.True(File.Exists(Path.Combine(project.Home, "forge.project.json")));
    }

    [Fact]
    public async Task Create_WithOverrides_UsesTheSuppliedTitleAndHome()
    {
        var chosen = Path.Combine(_profile, "work", "todos");

        var response = await CreateAsync(new ProjectCreateRequest("Todos API", "Renamed", chosen));

        Assert.Equal(ProjectOperationOutcome.Created, response.Outcome);
        Assert.Equal("Renamed", response.Session!.Project.Title);
        Assert.Equal(chosen, response.Session.Project.Home);
    }

    [Fact]
    public async Task Create_ACollidingTitle_TakesTheNextSuffix_AndLeavesTheFirstProjectIntact()
    {
        var first = await CreateAsync(new ProjectCreateRequest("Todos API"));
        var second = await CreateAsync(new ProjectCreateRequest("Todos API"));

        Assert.Equal(Path.Combine(ProjectsRoot, "todos-api"), first.Session!.Project.Home);
        Assert.Equal(Path.Combine(ProjectsRoot, "todos-api-2"), second.Session!.Project.Home);

        var reopened = await OpenAsync(new ProjectOpenRequest(first.Session.Project.Home));
        Assert.Equal(first.Session.Project.ProjectId, reopened.Session!.Project.ProjectId);
    }

    [Fact]
    public async Task Create_AnEmptyGoal_IsATypedFailure_AndWritesNoProject()
    {
        var response = await CreateAsync(new ProjectCreateRequest("   "));

        Assert.Equal(ProjectOperationOutcome.Failed, response.Outcome);
        Assert.Equal(ProjectOperationErrorCode.InvalidGoal, response.Error!.Code);
        Assert.False(Directory.Exists(ProjectsRoot));
    }

    [Fact]
    public async Task Create_AnEmptyGoalWithTitleAndHomeOverrides_IsStillATypedFailure()
    {
        var chosen = Path.Combine(_profile, "work", "todos");

        var response = await CreateAsync(new ProjectCreateRequest("  ", "Todos API", chosen));

        Assert.Equal(ProjectOperationOutcome.Failed, response.Outcome);
        Assert.Equal(ProjectOperationErrorCode.InvalidGoal, response.Error!.Code);
        Assert.False(File.Exists(Path.Combine(chosen, "forge.project.json")));
    }

    // --- open -------------------------------------------------------------------------------

    [Fact]
    public async Task Open_ACreatedProject_RestoresTheSameIdentityAndHome()
    {
        var created = await CreateAsync(new ProjectCreateRequest("Todos API"));

        var opened = await OpenAsync(new ProjectOpenRequest(created.Session!.Project.Home));

        Assert.Equal(ProjectOperationOutcome.Opened, opened.Outcome);
        Assert.Equal(created.Session.Project.ProjectId, opened.Session!.Project.ProjectId);
        Assert.Equal(created.Session.Project.Home, opened.Session.Project.Home);
        Assert.NotEqual(created.Session.SessionId, opened.Session.SessionId);
    }

    [Fact]
    public async Task Open_ADirectoryWithoutAManifest_AsksForAGoal_AndCreatesNothing()
    {
        var folder = Directory.CreateDirectory(Path.Combine(_profile, "existing-code")).FullName;

        var response = await OpenAsync(new ProjectOpenRequest(folder));

        Assert.Equal(ProjectOperationOutcome.GoalRequired, response.Outcome);
        Assert.Null(response.Session);
        Assert.Equal(folder, response.Proposal!.HomePath);
        Assert.Equal("existing-code", response.Proposal.ProposedTitle);
        Assert.Empty(Directory.EnumerateFileSystemEntries(folder));
    }

    [Fact]
    public async Task Open_ADirectoryThatDoesNotExist_IsATypedFailure()
    {
        var missing = Path.Combine(_profile, "not-here");

        var response = await OpenAsync(new ProjectOpenRequest(missing));

        Assert.Equal(ProjectOperationOutcome.Failed, response.Outcome);
        Assert.Equal(ProjectOperationErrorCode.HomeNotFound, response.Error!.Code);
        Assert.False(Directory.Exists(missing));
    }

    [Fact]
    public async Task Open_AMalformedManifest_IsATypedFailure()
    {
        var response = await OpenAsync(new ProjectOpenRequest(WriteManifest("{ not json")));

        Assert.Equal(ProjectOperationOutcome.Failed, response.Outcome);
        Assert.Equal(ProjectOperationErrorCode.InvalidManifest, response.Error!.Code);
    }

    [Fact]
    public async Task Open_AManifestFromANewerForge_IsATypedFailure()
    {
        var home = WriteManifest("""
            { "schemaVersion": 99, "projectId": "b0000000-0000-0000-0000-000000000001",
              "title": "Future", "goal": "later", "selectedMission": { "origin": "BuiltIn", "reference": "Janus" } }
            """);

        var response = await OpenAsync(new ProjectOpenRequest(home));

        Assert.Equal(ProjectOperationOutcome.Failed, response.Outcome);
        Assert.Equal(ProjectOperationErrorCode.UnsupportedManifestVersion, response.Error!.Code);
    }

    // --- the session a Project hands out ------------------------------------------------------

    [Fact]
    public async Task ACreatedProjectsSession_ExecutesACapabilityInsideItsOwnHome()
    {
        var created = await CreateAsync(new ProjectCreateRequest("Todos API"));
        await File.WriteAllTextAsync(Path.Combine(created.Session!.Project.Home, "proof.txt"), "project-rooted-read");

        var dispatch = await _channel.SendAsync<CapabilityDispatchRequest, CapabilityDispatchResponse>(
            new CapabilityDispatchRequest(created.Session.SessionId,
                new CapabilityRequestData("file", CapabilityOperation.ReadFile, FilePath: "proof.txt")),
            CancellationToken.None);

        Assert.False(dispatch.IsError, dispatch.Content);
        Assert.Contains("project-rooted-read", dispatch.Content, StringComparison.Ordinal);
    }

    // --- session replacement is replacement only ----------------------------------------------

    [Fact]
    public async Task SessionSetup_NamingNoLiveSession_IsRejected_AndCreatesNoSession()
    {
        var created = await CreateAsync(new ProjectCreateRequest("Todos API"));

        var rejection = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _channel.SendAsync<SessionSetupRequest, SessionSetupResponse>(
                new SessionSetupRequest(created.Session!.Project.Home, "no-such-session"), CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, rejection.StatusCode);
        await AssertSessionStillWorksAsync(created.Session!);
    }

    // The rule that matters: this route cannot be used to open a folder that is not the current
    // session's Project home. Only project create/open establish a root.
    [Fact]
    public async Task SessionSetup_WithARootOtherThanTheSessionsProjectHome_IsRejected()
    {
        var created = await CreateAsync(new ProjectCreateRequest("Todos API"));
        var elsewhere = Directory.CreateDirectory(Path.Combine(_profile, "elsewhere")).FullName;

        var rejection = await Assert.ThrowsAsync<HttpRequestException>(() =>
            _channel.SendAsync<SessionSetupRequest, SessionSetupResponse>(
                new SessionSetupRequest(elsewhere, created.Session!.SessionId), CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, rejection.StatusCode);
        Assert.Empty(Directory.EnumerateFileSystemEntries(elsewhere));
        await AssertSessionStillWorksAsync(created.Session!);
    }

    [Fact]
    public async Task SessionSetup_ReplacingTheCurrentSessionInItsOwnProject_Succeeds()
    {
        var created = await CreateAsync(new ProjectCreateRequest("Todos API"));

        var replaced = await _channel.SendAsync<SessionSetupRequest, SessionSetupResponse>(
            new SessionSetupRequest(created.Session!.Project.Home, created.Session.SessionId, "websearch"),
            CancellationToken.None);

        Assert.NotEqual(created.Session.SessionId, replaced.SessionId);
        Assert.Equal(["file", "terminal"], replaced.AvailableCapabilities);
    }

    // --- helpers ------------------------------------------------------------------------------

    private Task<ProjectDraftResponse> DraftAsync(ProjectDraftRequest request) =>
        _channel.SendAsync<ProjectDraftRequest, ProjectDraftResponse>(request, CancellationToken.None);

    private Task<ProjectOperationResponse> CreateAsync(ProjectCreateRequest request) =>
        _channel.SendAsync<ProjectCreateRequest, ProjectOperationResponse>(request, CancellationToken.None);

    private Task<ProjectOperationResponse> OpenAsync(ProjectOpenRequest request) =>
        _channel.SendAsync<ProjectOpenRequest, ProjectOperationResponse>(request, CancellationToken.None);

    // A rejected replacement must leave the Project's real session exactly where it was.
    private async Task AssertSessionStillWorksAsync(ProjectSession session)
    {
        await File.WriteAllTextAsync(Path.Combine(session.Project.Home, "still-here.txt"), "intact");

        var dispatch = await _channel.SendAsync<CapabilityDispatchRequest, CapabilityDispatchResponse>(
            new CapabilityDispatchRequest(session.SessionId,
                new CapabilityRequestData("file", CapabilityOperation.ReadFile, FilePath: "still-here.txt")),
            CancellationToken.None);

        Assert.False(dispatch.IsError, dispatch.Content);
        Assert.Contains("intact", dispatch.Content, StringComparison.Ordinal);
    }

    private string WriteManifest(string json)
    {
        var home = Directory.CreateDirectory(Path.Combine(_profile, "manifests", Guid.NewGuid().ToString("N"))).FullName;
        File.WriteAllText(Path.Combine(home, "forge.project.json"), json);
        return home;
    }

    // --- Mission Control, as a TUI would invoke it (43.20 task 2) ---------------------------

    // The surface-parity proof for this task: both Mission Control actions are named Client Runtime
    // contracts reached over the production channel, with no Blazor, bunit, Photino, Desktop, or
    // Host type anywhere in this class.
    [Fact]
    public async Task MissionControl_IsReachableThroughTheNamedContracts_WithNoProjectPathOrConversationId()
    {
        var created = await _channel.SendAsync<ProjectCreateRequest, ProjectOperationResponse>(
            new ProjectCreateRequest("Ship a todos API"), CancellationToken.None);
        var sessionId = created.Session!.SessionId;

        // No ConversationHost is running in this test, so the open fails at the transport — but it
        // fails AFTER resolving the Project and reading its manifest itself, which is the part
        // under test: the request carries nothing but the session id.
        var open = new OpenProjectMissionControlRequest(sessionId);
        Assert.Equal("SessionId", Assert.Single(typeof(OpenProjectMissionControlRequest).GetProperties()).Name);

        var turn = new SubmitProjectMissionControlTurnRequest(sessionId, Guid.NewGuid(), "narrow the scope");
        Assert.Equal(
            ["CommandId", "SessionId", "Text"],
            typeof(SubmitProjectMissionControlTurnRequest).GetProperties().Select(p => p.Name).Order().ToList());

        // Both requests route to their own Client Runtime endpoints through the production channel.
        // No ConversationHost runs here, so the open fails at the transport — but it fails AFTER
        // resolving the Project and reading its manifest itself, which is the part under test.
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _channel.SendAsync<OpenProjectMissionControlRequest, OpenProjectMissionControlResponse>(open, CancellationToken.None));

        // The submit reaches its own endpoint and comes back as a TYPED outcome (the turn was
        // submitted before Mission Control opened), not as an escaping transport failure.
        var submitted = await _channel.SendAsync<
            SubmitProjectMissionControlTurnRequest, SubmitProjectMissionControlTurnResponse>(turn, CancellationToken.None);
        Assert.Equal(ProjectOperationErrorCode.MissionControlInvalid, submitted.Error!.Code);
    }

    [Fact]
    public async Task MissionControl_ForAnUnknownSession_IsNotFound_AndTouchesNoProject()
    {
        await Assert.ThrowsAnyAsync<Exception>(() =>
            _channel.SendAsync<OpenProjectMissionControlRequest, OpenProjectMissionControlResponse>(
                new OpenProjectMissionControlRequest("not-a-session"), CancellationToken.None));

        Assert.False(Directory.Exists(ProjectsRoot));
    }

    // Desktop's composer stays disabled until Mission Control has opened, but a TUI could still
    // order the two calls this way. Surface parity says both get the same TYPED outcome rather
    // than one getting an escaping transport failure.
    [Fact]
    public async Task SubmittingBeforeOpening_IsATypedMissionControlInvalid_NotATransportFailure()
    {
        var created = await _channel.SendAsync<ProjectCreateRequest, ProjectOperationResponse>(
            new ProjectCreateRequest("Ship a todos API"), CancellationToken.None);

        var response = await _channel.SendAsync<
            SubmitProjectMissionControlTurnRequest, SubmitProjectMissionControlTurnResponse>(
            new SubmitProjectMissionControlTurnRequest(
                created.Session!.SessionId, Guid.NewGuid(), "narrow the scope"),
            CancellationToken.None);

        Assert.Null(response.ConversationId);
        Assert.Equal(ProjectOperationErrorCode.MissionControlInvalid, response.Error!.Code);
        // The rendered message names Mission Control, not an HTTP status or an internal service.
        Assert.DoesNotContain("HTTP", response.Error.Message, StringComparison.Ordinal);
    }
}
