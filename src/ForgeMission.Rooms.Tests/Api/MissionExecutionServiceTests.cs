using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ForgeMission.Api;
using ForgeMission.Billing;
using ForgeMission.Rooms;
using ForgeMission.Rooms.Data;
using ForgeMission.Runner.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ForgeMission.Rooms.Tests;

/// <summary>
/// <see cref="MissionExecutionService"/> — API A's core operation (42.6 task 5a) — against real
/// billing (Postgres, via <see cref="PostgresFixture"/>) and a stubbed runner HTTP client. Exercises
/// the service directly rather than through a hosted ForgeAPI TestServer: the HTTP adapter
/// (<see cref="MissionEndpoints"/>) is a thin route → principal → this class call, already covered in
/// spirit by <c>PlatformKeyAuthFilterTests</c>' filter-in-isolation approach.
/// </summary>
public sealed class MissionExecutionServiceTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private BillingService Billing => fixture.Services.GetRequiredService<BillingService>();
    private IWriteStore Writes => fixture.Services.GetRequiredService<IWriteStore>();

    private async Task<Member> NewMemberAsync() =>
        await Writes.AddMemberAsync(new Member
        {
            Kind = MemberKind.Human,
            DisplayName = "Execute Tester",
            Issuer = "dev",
            Subject = $"sub-{Guid.NewGuid():N}",
        });

    private static IMissionCatalog Catalog(params string[] availableMissionRefs) =>
        new StaticMissionCatalog(availableMissionRefs
            .Select(r => new MissionInfo(r, Description: r))
            .ToList());

    private static IMissionCatalog Catalog(params MissionInfo[] availableMissions) =>
        new StaticMissionCatalog(availableMissions);

    private static IHttpClientFactory RunnerFactory(HttpMessageHandler handler) =>
        new StubHttpClientFactory(new HttpClient(handler) { BaseAddress = new Uri("http://runner.test") });

    private MissionExecutionService NewService(
        IMissionCatalog catalog,
        HttpMessageHandler handler,
        IArtifactStore? artifacts = null) =>
        new(catalog, new InMemoryRunStore(), artifacts ?? new StubArtifactStore(), RunnerFactory(handler), Billing,
            NullLogger<MissionExecutionService>.Instance);

    [Fact]
    public async Task Execute_resolves_runs_and_debits_the_actual_cost()
    {
        var member = await NewMemberAsync();
        await Billing.GrantStartingCreditAsync(member.Id);
        var startingBalance = await Billing.GetBalanceMicroUsdAsync(member.Id);

        var runnerResult = new RunResponse(
            AgentText: "The answer, grounded.",
            Verified: true,
            StepCount: 2,
            RetryCount: 0,
            Trace: [new RunTraceStep("Answerer", "pass", "The answer, grounded.", null, 1)],
            Usage: new RunUsage(InputTokens: 100, OutputTokens: 50, ComputeSeconds: 1.0, Model: "gpt-4o-mini"));

        var handler = new StubRunnerHandler(
        [
            new RunStreamEvent("progress", Progress: new RunProgress("SearchRouter", "llm")),
            new RunStreamEvent("result", Result: runnerResult),
        ]);

        var svc = NewService(Catalog("WebSearch"), handler);
        var msg = new ExecuteMission
        {
            Mission = "websearch",
            Input = "what shipped this week?",
            ClientToken = $"tok-{Guid.NewGuid():N}",
        };

        var response = await svc.ExecuteAsync(msg, new PlatformKeyContext(member.Id, startingBalance), CancellationToken.None);

        Assert.Null(response.ResponseStatus.ErrorCode);
        Assert.Equal("websearch", response.Mission); // echoes the RESOLVED handle
        Assert.Equal("The answer, grounded.", response.Answer);
        Assert.True(response.Verified);
        Assert.True(response.Usage.CostMicroUsd > 0);
        Assert.Equal(startingBalance - response.Usage.CostMicroUsd, response.BalanceMicroUsd);
        Assert.Equal(startingBalance - response.Usage.CostMicroUsd, await Billing.GetBalanceMicroUsdAsync(member.Id));
        Assert.NotEmpty(response.RunId);
    }

    [Fact]
    public async Task Execute_returns_MissionNotFound_for_an_unresolvable_handle()
    {
        var member = await NewMemberAsync();
        await Billing.GrantStartingCreditAsync(member.Id);

        // Catalog with nothing advertised — "websearch" won't resolve.
        var svc = NewService(Catalog(Array.Empty<string>()), new StubRunnerHandler([]));
        var msg = new ExecuteMission { Mission = "websearch", Input = "hi", ClientToken = $"tok-{Guid.NewGuid():N}" };

        var response = await svc.ExecuteAsync(msg, new PlatformKeyContext(member.Id, 5_000_000), CancellationToken.None);

        Assert.Equal(ErrorCode.MissionNotFound, response.ResponseStatus.ErrorCode);
    }

    [Fact]
    public async Task Execute_returns_InsufficientCredit_at_zero_balance_and_never_calls_the_runner()
    {
        var member = await NewMemberAsync(); // no starting credit granted — balance is 0

        var handler = new StubRunnerHandler([]) { ThrowIfCalled = true };
        var svc = NewService(Catalog("WebSearch"), handler);
        var msg = new ExecuteMission { Mission = "websearch", Input = "hi", ClientToken = $"tok-{Guid.NewGuid():N}" };

        var response = await svc.ExecuteAsync(msg, new PlatformKeyContext(member.Id, 0), CancellationToken.None);

        Assert.Equal(ErrorCode.InsufficientCredit, response.ResponseStatus.ErrorCode);
    }

    [Fact]
    public async Task Retried_ClientToken_does_not_double_debit_across_two_Execute_calls()
    {
        var member = await NewMemberAsync();
        await Billing.GrantStartingCreditAsync(member.Id);
        var startingBalance = await Billing.GetBalanceMicroUsdAsync(member.Id);

        var runnerResult = new RunResponse(
            AgentText: "ok", Verified: true, StepCount: 1, RetryCount: 0, Trace: [],
            Usage: new RunUsage(InputTokens: 10, OutputTokens: 10, ComputeSeconds: 0.5, Model: "gpt-4o-mini"));

        var token = $"tok-{Guid.NewGuid():N}";
        var msg = new ExecuteMission { Mission = "websearch", Input = "hi", ClientToken = token };
        var principal = new PlatformKeyContext(member.Id, startingBalance);

        var first = await NewService(Catalog("WebSearch"),
            new StubRunnerHandler([new RunStreamEvent("result", Result: runnerResult)]))
            .ExecuteAsync(msg, principal, CancellationToken.None);

        var second = await NewService(Catalog("WebSearch"),
            new StubRunnerHandler([new RunStreamEvent("result", Result: runnerResult)]))
            .ExecuteAsync(msg, principal, CancellationToken.None);

        Assert.Equal(first.Usage.CostMicroUsd, second.Usage.CostMicroUsd);
        Assert.Equal(startingBalance - first.Usage.CostMicroUsd, await Billing.GetBalanceMicroUsdAsync(member.Id));
    }

    [Fact]
    public async Task Execute_copies_runner_output_artifacts_into_api_artifact_store()
    {
        var member = await NewMemberAsync();
        await Billing.GrantStartingCreditAsync(member.Id);
        var startingBalance = await Billing.GetBalanceMicroUsdAsync(member.Id);

        var runnerArtifact = new RunArtifact(
            Id: "runner-art-1",
            Name: "proof.txt",
            ContentType: "text/plain",
            Size: "artifact proof"u8.ToArray().Length,
            Sha256: "",
            Role: "output");
        var runnerResult = new RunResponse(
            AgentText: "Created proof.txt",
            Verified: true,
            StepCount: 1,
            RetryCount: 0,
            Trace: [],
            Usage: new RunUsage(InputTokens: 0, OutputTokens: 0, ComputeSeconds: 0.1, Model: null),
            OutputArtifacts: [runnerArtifact]);

        var store = new StubArtifactStore();
        var svc = NewService(
            Catalog("WebSearch"),
            new StubRunnerHandler([new RunStreamEvent("result", Result: runnerResult)])
            {
                ArtifactBytes = "artifact proof"u8.ToArray(),
            },
            store);
        var msg = new ExecuteMission { Mission = "websearch", Input = "make proof", ClientToken = $"tok-{Guid.NewGuid():N}" };

        var response = await svc.ExecuteAsync(msg, new PlatformKeyContext(member.Id, startingBalance), CancellationToken.None);

        var artifact = Assert.Single(response.Artifacts);
        Assert.Equal("proof.txt", artifact.Name);
        Assert.Equal("text/plain", artifact.ContentType);
        Assert.Equal("output", artifact.Role);
        Assert.Equal("artifact proof", store.ReadText(artifact.Id, member.Id));
    }

    [Fact]
    public async Task Execute_shapes_input_artifact_upload_failure_as_RunFailed_without_debit()
    {
        var member = await NewMemberAsync();
        await Billing.GrantStartingCreditAsync(member.Id);
        var startingBalance = await Billing.GetBalanceMicroUsdAsync(member.Id);

        var store = new StubArtifactStore();
        var input = store.AddInput(member.Id, "scan.jpg", "image/jpeg", "image bytes"u8.ToArray());
        var svc = NewService(
            Catalog("Ocr"),
            new StubRunnerHandler([]) { ArtifactUploadStatus = HttpStatusCode.InternalServerError },
            store);
        var msg = new ExecuteMission
        {
            Mission = "ocr",
            InputArtifactIds = [input.Id],
            ClientToken = $"tok-{Guid.NewGuid():N}",
        };

        var response = await svc.ExecuteAsync(msg, new PlatformKeyContext(member.Id, startingBalance), CancellationToken.None);

        Assert.Equal(ErrorCode.RunFailed, response.ResponseStatus.ErrorCode);
        Assert.Equal(startingBalance, await Billing.GetBalanceMicroUsdAsync(member.Id));
    }

    [Fact]
    public async Task Execute_shapes_output_artifact_download_failure_as_RunFailed_after_debit()
    {
        var member = await NewMemberAsync();
        await Billing.GrantStartingCreditAsync(member.Id);
        var startingBalance = await Billing.GetBalanceMicroUsdAsync(member.Id);

        var runnerArtifact = new RunArtifact(
            Id: "runner-art-1",
            Name: "proof.txt",
            ContentType: "text/plain",
            Size: 12,
            Sha256: "",
            Role: "output");
        var runnerResult = new RunResponse(
            AgentText: "Created proof.txt",
            Verified: true,
            StepCount: 1,
            RetryCount: 0,
            Trace: [],
            Usage: new RunUsage(InputTokens: 10, OutputTokens: 10, ComputeSeconds: 0.5, Model: "gpt-4o-mini"),
            OutputArtifacts: [runnerArtifact]);

        var svc = NewService(
            Catalog("WebSearch"),
            new StubRunnerHandler([new RunStreamEvent("result", Result: runnerResult)])
            {
                ArtifactDownloadStatus = HttpStatusCode.InternalServerError,
            });
        var msg = new ExecuteMission { Mission = "websearch", Input = "make proof", ClientToken = $"tok-{Guid.NewGuid():N}" };

        var response = await svc.ExecuteAsync(msg, new PlatformKeyContext(member.Id, startingBalance), CancellationToken.None);

        Assert.Equal(ErrorCode.RunFailed, response.ResponseStatus.ErrorCode);
        Assert.True(response.Usage.CostMicroUsd > 0);
        Assert.Equal(10, response.Usage.InputTokens);
        Assert.Equal(10, response.Usage.OutputTokens);
        Assert.Equal("gpt-4o-mini", response.Usage.Model);
        Assert.Equal(startingBalance - response.Usage.CostMicroUsd, response.BalanceMicroUsd);
        Assert.Equal(response.BalanceMicroUsd, await Billing.GetBalanceMicroUsdAsync(member.Id));
    }

    [Fact]
    public async Task Execute_rejects_input_artifact_that_violates_mission_capabilities_before_runner_call()
    {
        var member = await NewMemberAsync();
        await Billing.GrantStartingCreditAsync(member.Id);
        var startingBalance = await Billing.GetBalanceMicroUsdAsync(member.Id);

        var store = new StubArtifactStore();
        var input = store.AddInput(member.Id, "note.txt", "text/plain", "not an image"u8.ToArray());
        var catalog = Catalog(new MissionInfo(
            "Ocr",
            Description: "OCR",
            ArtifactCapabilities: new MissionArtifactCapabilities(
            [
                new MissionArtifactInputCapability(
                    "source",
                    ["image/jpeg", "image/png"],
                    MaxSizeMb: 100),
            ])));
        var svc = NewService(catalog, new StubRunnerHandler([]) { ThrowIfCalled = true }, store);
        var msg = new ExecuteMission
        {
            Mission = "ocr",
            InputArtifactIds = [input.Id],
            ClientToken = $"tok-{Guid.NewGuid():N}",
        };

        var response = await svc.ExecuteAsync(msg, new PlatformKeyContext(member.Id, startingBalance), CancellationToken.None);

        Assert.Equal(ErrorCode.InvalidInput, response.ResponseStatus.ErrorCode);
        Assert.Equal(startingBalance, await Billing.GetBalanceMicroUsdAsync(member.Id));
    }

    // --- harness ------------------------------------------------------------------------------

    private sealed class StubHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubArtifactStore : IArtifactStore
    {
        private readonly Dictionary<string, (Guid Owner, MissionArtifact Artifact, byte[] Bytes)> _store = [];

        public async Task<ArtifactSaveResult> SaveAsync(
            ArtifactWriteRequest request,
            Stream content,
            PlatformKeyContext owner,
            CancellationToken ct)
        {
            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, ct);
            var id = $"art-{Guid.NewGuid():N}";
            var artifact = new MissionArtifact
            {
                Id = id,
                Name = request.Name,
                ContentType = request.ContentType,
                Size = ms.Length,
                Sha256 = request.Sha256,
                Role = request.Role,
            };
            _store[id] = (owner.MemberId, artifact, ms.ToArray());
            return new ArtifactSaveResult(artifact, Sha256Matched: true);
        }

        public Task<ArtifactRead?> OpenAsync(string artifactId, PlatformKeyContext owner, CancellationToken ct) =>
            Task.FromResult(_store.TryGetValue(artifactId, out var entry) && entry.Owner == owner.MemberId
                ? new ArtifactRead(entry.Artifact, new MemoryStream(entry.Bytes))
                : null);

        public Task DeleteAsync(string artifactId, PlatformKeyContext owner, CancellationToken ct)
        {
            if (_store.TryGetValue(artifactId, out var entry) && entry.Owner == owner.MemberId)
                _store.Remove(artifactId);
            return Task.CompletedTask;
        }

        public MissionArtifact AddInput(Guid owner, string name, string contentType, byte[] bytes)
        {
            var id = $"art-{Guid.NewGuid():N}";
            var artifact = new MissionArtifact
            {
                Id = id,
                Name = name,
                ContentType = contentType,
                Size = bytes.Length,
                Sha256 = "",
                Role = ArtifactRole.Input,
            };
            _store[id] = (owner, artifact, bytes);
            return artifact;
        }

        public string ReadText(string artifactId, Guid owner)
        {
            var entry = _store[artifactId];
            Assert.Equal(owner, entry.Owner);
            return Encoding.UTF8.GetString(entry.Bytes);
        }
    }

    private sealed class StubRunnerHandler(IReadOnlyList<RunStreamEvent> events) : HttpMessageHandler
    {
        public bool ThrowIfCalled { get; init; }
        public byte[]? ArtifactBytes { get; init; }
        public HttpStatusCode ArtifactUploadStatus { get; init; } = HttpStatusCode.OK;
        public HttpStatusCode ArtifactDownloadStatus { get; init; } = HttpStatusCode.OK;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (ThrowIfCalled)
                throw new InvalidOperationException("The runner must not be called when the credit check fails.");

            if (request.RequestUri?.AbsolutePath == "/artifacts/upload")
            {
                if (ArtifactUploadStatus != HttpStatusCode.OK)
                    return Task.FromResult(new HttpResponseMessage(ArtifactUploadStatus));

                var artifact = new RunArtifact(
                    Id: "runner-input-1",
                    Name: request.Headers.GetValues("X-Forge-Artifact-Name").First(),
                    ContentType: request.Content?.Headers.ContentType?.MediaType ?? "application/octet-stream",
                    Size: request.Content?.Headers.ContentLength ?? 0,
                    Sha256: "",
                    Role: "input");
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(artifact, RunContractsContext.Default.RunArtifact),
                });
            }

            if (request.RequestUri?.AbsolutePath.StartsWith("/artifacts/", StringComparison.Ordinal) == true)
            {
                if (request.Method == HttpMethod.Delete)
                    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));

                var artifactResponse = new HttpResponseMessage(ArtifactDownloadStatus)
                {
                    Content = new ByteArrayContent(ArtifactBytes ?? []),
                };
                artifactResponse.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("text/plain");
                return Task.FromResult(artifactResponse);
            }

            var sb = new StringBuilder();
            foreach (var evt in events)
                sb.AppendLine(JsonSerializer.Serialize(evt, RunContractsContext.Default.RunStreamEvent));

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(sb.ToString(), Encoding.UTF8, "application/x-ndjson"),
            };
            return Task.FromResult(response);
        }
    }
}
