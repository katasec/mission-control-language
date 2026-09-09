using System.Net;
using ForgeMission.Application.Transport;
using ForgeMission.Tests.ApplicationHost;

namespace ForgeMission.Tests.Application;

/// <summary>
/// Phase 43.20 Task 1 — the Presentation-surface parity proof. This class is itself a second,
/// non-Desktop surface: it drives the real Application Host through the production
/// <see cref="IApplicationChannel"/> and the shared transport DTOs, and references no Blazor,
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
    private ApplicationHostProcess _host = null!;
    private HttpApplicationChannel _channel = null!;

    private string ProjectsRoot => Path.Combine(_profile, "Forge", "Projects");

    public async Task InitializeAsync()
    {
        _host = await ApplicationHostProcess.StartAsync(profileRoot: _profile);
        _channel = new HttpApplicationChannel(new Uri(_host.BaseUrl, UriKind.Absolute));
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
    // surface, because only Application decides what a valid Project is.
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

    // --- the Missions landing's three actions, over the real transport -------------------------
    // This class is the parity proof: a TUI reaching the same routes with the same DTOs gets the
    // same authorization, outcomes, and failures, with no Blazor or Desktop type in sight.

    [Fact]
    public async Task MissionsLanding_AnswersAFreshProjectWithNothingPinnedAndNothingApproved()
    {
        var created = await CreateAsync(new ProjectCreateRequest("Todos API"));
        var session = created.Session!.SessionId;

        var options = await _channel.SendAsync<ListApprovedMissionVersionsRequest, ListApprovedMissionVersionsResponse>(
            new ListApprovedMissionVersionsRequest(session), CancellationToken.None);

        // A Project read, so it answers on its own: empty is a real answer, and it is a different
        // answer from unavailable.
        Assert.Null(options.Error);
        Assert.Empty(options.Options!);

        // The conversation directory is Host-owned, and this harness runs no Conversation Host.
        // Either honest outcome is allowed — a typed availability failure, or a failed request —
        // but a fabricated list is not, so a caller can never mistake "cannot reach it" for
        // "this Project has none".
        try
        {
            var conversations = await _channel.SendAsync<ListMissionConversationsRequest, ListMissionConversationsResponse>(
                new ListMissionConversationsRequest(session), CancellationToken.None);
            Assert.NotNull(conversations.Error);
            Assert.Null(conversations.Conversations);
        }
        catch (HttpRequestException)
        {
            // Also honest: nothing was invented.
        }
    }

    [Fact]
    public async Task MissionsLanding_RefusesToCreateAgainstAMissionThisProjectDoesNotApprove()
    {
        var created = await CreateAsync(new ProjectCreateRequest("Todos API"));

        var response = await _channel.SendAsync<CreateMissionConversationRequest, CreateMissionConversationResponse>(
            new CreateMissionConversationRequest(created.Session!.SessionId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.Null(response.Created);
        Assert.NotNull(response.Error);
    }

    [Fact]
    public async Task MissionsLanding_RefusesAForeignSessionOnEveryAction()
    {
        var foreign = Guid.NewGuid().ToString("N");

        foreach (var send in new Func<Task>[]
                 {
                     () => _channel.SendAsync<ListMissionConversationsRequest, ListMissionConversationsResponse>(
                         new ListMissionConversationsRequest(foreign), CancellationToken.None),
                     () => _channel.SendAsync<ListApprovedMissionVersionsRequest, ListApprovedMissionVersionsResponse>(
                         new ListApprovedMissionVersionsRequest(foreign), CancellationToken.None),
                     () => _channel.SendAsync<CreateMissionConversationRequest, CreateMissionConversationResponse>(
                         new CreateMissionConversationRequest(foreign, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None),
                 })
        {
            var rejection = await Assert.ThrowsAsync<HttpRequestException>(send);
            Assert.Equal(HttpStatusCode.NotFound, rejection.StatusCode);
        }
    }

    [Fact]
    public void MissionsLanding_RequestsCannotNameAccess_OnlyAMissionAndWhatWasDisplayed()
    {
        Assert.Equal(["SessionId", "MissionId", "CommandId", "ExpectedMissionVersionId"],
            typeof(CreateMissionConversationRequest).GetProperties().Select(property => property.Name));

        foreach (var type in new[]
                 {
                     typeof(CreateMissionConversationRequest), typeof(ListMissionConversationsRequest),
                     typeof(ListApprovedMissionVersionsRequest),
                 })
        {
            var names = type.GetProperties().Select(property => property.Name).ToArray();
            foreach (var forbidden in new[] { "Profile", "Package", "Definition", "Capabilities", "Tool", "Path", "Home", "AttachmentId", "Launch" })
                Assert.DoesNotContain(names, name => name.Contains(forbidden, StringComparison.OrdinalIgnoreCase));
        }

        // What comes back is identity and the profile to display — never the executable package.
        var approval = typeof(MissionAccessApproval).GetProperties().Select(property => property.Name).ToArray();
        Assert.Equal(["MissionVersionId", "VersionNumber", "DefinitionHash", "Profile"], approval);
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


}
