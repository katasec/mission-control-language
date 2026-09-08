using System.Net;
using System.Text;
using System.Text.Json;
using ForgeMission.Application;
using ForgeMission.Application.Transport;
using ForgeMission.Application.Host;
using ForgeMission.Conversations.Contracts;
using ForgeMission.Core.Tools;
using Microsoft.Extensions.Configuration;
using RuntimeStartProjectMissionRunRequest = ForgeMission.Application.Transport.StartProjectMissionRunRequest;

namespace ForgeMission.Tests.Application;

public sealed class MissionSubmissionServiceTests : IDisposable
{
    private readonly string _profile = Directory.CreateTempSubdirectory("forge-project-mission-").FullName;

    public void Dispose() => Directory.Delete(_profile, recursive: true);

    [Fact]
    public async Task Start_PreparesThenAcceptsTheExactImmutableSubmission()
    {
        var fixture = NewFixture();
        var request = new RuntimeStartProjectMissionRunRequest(fixture.Session.Id, Guid.NewGuid(), null, "Implement it.");

        var response = await fixture.Application.StartAsync(fixture.Session, request, CancellationToken.None);

        Assert.Null(response.Error);
        Assert.Equal(ProjectSubmissionState.Accepted, response.Submission!.State);
        Assert.Equal(fixture.RunId, response.Submission.RunId);
        var persisted = fixture.Projects.ReadForHome(fixture.Project.Home).Manifest.Submission!;
        Assert.Equal(ProjectSubmissionPhase.Accepted, persisted.Phase);
        Assert.Equal(request.Input, persisted.Input);
        Assert.Equal(ProjectMissionNames.Janus, persisted.Mission);
        Assert.Equal(1, fixture.Handler.StartCount);
    }

    [Fact]
    public async Task LostAcceptanceResponse_LeavesPrepared_AndRetryReconcilesWithoutASecondStart()
    {
        var fixture = NewFixture();
        fixture.Handler.ThrowAfterAccept = true;
        var commandId = Guid.NewGuid();

        var uncertain = await fixture.Application.StartAsync(fixture.Session,
            new RuntimeStartProjectMissionRunRequest(fixture.Session.Id, commandId, null, "Keep this intent."), CancellationToken.None);

        Assert.Equal(ProjectOperationErrorCode.SubmissionUncertain, uncertain.Error!.Code);
        Assert.Equal(ProjectSubmissionState.Prepared, uncertain.Submission!.State);
        Assert.Equal(1, fixture.Handler.StartCount);

        fixture.Handler.ThrowAfterAccept = false;
        var accepted = await fixture.Application.RetryAsync(fixture.Session,
            new RetryProjectMissionSubmissionRequest(fixture.Session.Id, commandId), CancellationToken.None);

        Assert.Null(accepted.Error);
        Assert.Equal(ProjectSubmissionState.Accepted, accepted.Submission!.State);
        Assert.Equal(1, fixture.Handler.StartCount);
    }

    [Fact]
    public async Task ActiveContainer_RefusesNewCommandBeforeMutatingTheJournal()
    {
        var fixture = NewFixture();
        await fixture.Projects.SetProjectMissionContainerIdAsync(fixture.Project.Home, fixture.ContainerId, CancellationToken.None);
        fixture.Handler.ActiveRunId = Guid.NewGuid();

        var response = await fixture.Application.StartAsync(fixture.Session,
            new RuntimeStartProjectMissionRunRequest(fixture.Session.Id, Guid.NewGuid(), null, "Do not submit."), CancellationToken.None);

        Assert.Equal(ProjectOperationErrorCode.RunAlreadyActive, response.Error!.Code);
        Assert.Null(fixture.Projects.ReadForHome(fixture.Project.Home).Manifest.Submission);
        Assert.Equal(0, fixture.Handler.StartCount);
    }

    [Fact]
    public async Task DefinitiveHostRejection_PersistsAndReturnsItsTypedReason()
    {
        var fixture = NewFixture();
        fixture.Handler.RejectStart = true;

        var response = await fixture.Application.StartAsync(fixture.Session,
            new RuntimeStartProjectMissionRunRequest(fixture.Session.Id, Guid.NewGuid(), null, "Cannot start."), CancellationToken.None);

        Assert.Null(response.Error);
        Assert.Equal(ProjectSubmissionState.Rejected, response.Submission!.State);
        Assert.Equal(ProjectOperationErrorCode.RunAlreadyActive, response.Submission.Rejection!.Code);
        Assert.Equal(ProjectSubmissionPhase.Rejected, fixture.Projects.ReadForHome(fixture.Project.Home).Manifest.Submission!.Phase);
    }

    [Fact]
    public async Task Generic_acknowledged_start_rejection_revokes_the_provisional_hands_attachment()
    {
        var fixture = NewFixture(genericHands: true);
        fixture.Handler.RejectStart = true;
        var manifest = fixture.Projects.ReadForHome(fixture.Project.Home).Manifest with
        {
            ApprovedMissionLaunches = [GenericLaunch()],
        };
        File.WriteAllText(Path.Combine(fixture.Project.Home, "forge.project.json"),
            JsonSerializer.Serialize(manifest, ProjectManifestJsonContext.Default.ProjectManifest));

        var response = await fixture.Application.StartAsync(fixture.Session,
            new RuntimeStartProjectMissionRunRequest(fixture.Session.Id, Guid.NewGuid(), null, "generic input", ProfileAccepted: true),
            CancellationToken.None);

        Assert.Null(response.Error);
        Assert.Equal(ProjectSubmissionState.Rejected, response.Submission!.State);
        Assert.Equal(1, fixture.Handler.AttachCount);
        Assert.Equal(1, fixture.Handler.DetachCount);
    }

    private Fixture NewFixture(bool genericHands = false)
    {
        var projects = new ProjectService(Path.Combine(_profile, "Forge", "Projects"));
        var project = projects.Create("Build a reliable Project Mission.", null, null);
        var sessions = new ApplicationSessionService(CapabilityAuthorizationPolicy.Default, _ => { }, CancellationToken.None);
        var session = sessions.CreateForProject(project.Home);
        var handler = new ProjectMissionHandler(project.Manifest.ProjectId, project.Manifest.Goal);
        var factory = new HandlerFactory(handler);
        var hands = genericHands ? new MissionHandsConversationService(projects, sessions, factory,
            CapabilityAuthorizationPolicy.Default, CancellationToken.None) : null;
        var application = new MissionSubmissionService(projects, factory, sessions, hands);
        return new Fixture(projects, project, session, application, handler);
    }

    private static MissionVersionLaunch GenericLaunch()
    {
        const string source = "mission Durable(task) = {\n Researcher\n}\n";
        const string expert = "---\nname: Researcher\nkind: llm\ninput: task\noutput: answer\n---\n{{task}}";
        var resolved = new DurableResolvedExpert("Researcher", "inline", "experts/Researcher/expert.md", Hash(expert), expert);
        var input = new ForgeMission.Core.Runtime.DurableMissionPackageInput(1, "", source, "Durable", "task", [
            new ForgeMission.Core.Runtime.DurableResolvedExpertInput(resolved.Name, resolved.LockSource, resolved.LockPath, resolved.LockHash, resolved.ExpertMarkdown)]);
        var package = new DurableMissionPackage(1, ForgeMission.Core.Runtime.DurableMissionPackageValidator.ComputeHash(input), source, "Durable", "task", [resolved]);
        const string definition = "approved";
        return new MissionVersionLaunch(Guid.NewGuid(), 1, Hash(definition), definition, MissionHandsProfile.NoHands, DateTimeOffset.UtcNow, package);
    }

    private static string Hash(string value) => "sha256:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed record Fixture(ProjectService Projects, ProjectRecord Project, ApplicationSession Session,
        MissionSubmissionService Application, ProjectMissionHandler Handler)
    {
        public Guid ContainerId => Handler.ContainerId;
        public Guid RunId => Handler.RunId;
    }

    private sealed class HandlerFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false)
        {
            BaseAddress = new Uri("https://conversation-host.test/"),
        };
    }

    private sealed class ProjectMissionHandler(Guid projectId, string projectGoal) : HttpMessageHandler
    {
        public Guid ContainerId { get; } = Guid.NewGuid();
        public Guid RunId { get; } = Guid.NewGuid();
        public Guid? ActiveRunId { get; set; }
        public bool ThrowAfterAccept { get; set; }
        public bool RejectStart { get; set; }
        public int StartCount { get; private set; }
        public int AttachCount { get; private set; }
        public int DetachCount { get; private set; }
        private Guid? _commandId;
        private string? _mission;
        private string? _input;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Post && path == "/conversations/project-mission")
                return Json(new { containerId = ContainerId, acceptedSequence = 0 });
            if (request.Method == HttpMethod.Get && path == $"/conversations/{ContainerId}")
                return Json(new
                {
                    snapshot = new { conversationId = ContainerId, missionRef = (string?)null, activeRunId = ActiveRunId,
                        lastSequence = 0, status = "queued", expectedToolRequestId = (Guid?)null,
                        updatedAtUtc = DateTimeOffset.UtcNow, purpose = "projectMission", projectId },
                });
            if (request.Method == HttpMethod.Get && path.Contains("/project-commands/", StringComparison.Ordinal))
            {
                if (_commandId is null) return new HttpResponseMessage(HttpStatusCode.NotFound)
                { Content = new StringContent("{\"code\":\"notFound\",\"message\":\"missing\"}", Encoding.UTF8, "application/json") };
                return Json(new { containerId = ContainerId, runId = RunId, mission = _mission, input = _input,
                    projectGoal, acceptedSequence = 1, status = "queued" });
            }
            if (request.Method == HttpMethod.Post && path == "/mission-hands/attach")
            {
                AttachCount++;
                return Json(new MissionHandsResult(MissionHandsStatus.Attached, 1));
            }
            if (request.Method == HttpMethod.Post && path == "/mission-hands/detach")
            {
                DetachCount++;
                return Json(new MissionHandsResult(MissionHandsStatus.Completed, 1));
            }
            if (request.Method == HttpMethod.Post && path == $"/conversations/{ContainerId}/mission-runs")
            {
                await using var body = await request.Content!.ReadAsStreamAsync(ct);
                using var document = await JsonDocument.ParseAsync(body, cancellationToken: ct);
                _commandId = document.RootElement.GetProperty("commandId").GetGuid();
                _mission = document.RootElement.GetProperty("mission").GetString();
                _input = document.RootElement.GetProperty("input").GetString();
                StartCount++;
                if (ThrowAfterAccept) throw new HttpRequestException("connection reset after acceptance");
                if (RejectStart) return new HttpResponseMessage(HttpStatusCode.Conflict)
                { Content = new StringContent("{\"code\":\"runAlreadyActive\",\"message\":\"active\"}", Encoding.UTF8, "application/json") };
                return Json(new { containerId = ContainerId, runId = RunId, acceptedSequence = 1, status = "queued" }, HttpStatusCode.Accepted);
            }
            throw new InvalidOperationException($"Unexpected request: {request.Method} {path}");
        }

        private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json"),
        };
    }
}
