using ForgeMission.Core.Tools;

namespace ForgeMission.Tests.Tools;

public sealed class CapabilityDispatcherTests
{
    [Fact]
    public void DefaultPolicy_AutoApprovesExistingFileAndTerminalCapabilities()
    {
        var policy = CapabilityAuthorizationPolicy.Default;

        Assert.Equal(AuthorizationOutcome.AutoApproved, policy.RuleFor("file").Outcome);
        Assert.Equal(AuthorizationOutcome.AutoApproved, policy.RuleFor("terminal").Outcome);
        Assert.Equal(AuthorizationOutcome.AutoDenied, policy.RuleFor("unknown").Outcome);
    }

    [Fact]
    public async Task AutoDenied_RequestDoesNotInvokeProvider_AndIsAudited()
    {
        var provider = new CountingFileProvider();
        var capabilities = new CapabilityRegistry([provider]);
        var auditLog = new InMemoryCapabilityAuditLog();
        var dispatcher = new CapabilityDispatcher(capabilities,
            new FixedOutcomeAuthorizer(AuthorizationOutcome.AutoDenied), auditLog);

        var result = await dispatcher.DispatchAsync("file", new ReadFileCapabilityRequest("notes.txt", 0, null), default);

        Assert.True(result.IsError);
        Assert.Equal(0, provider.InvocationCount);
        var audit = Assert.Single(auditLog.Records);
        Assert.Equal("file", audit.CapabilityName);
        Assert.Equal(AuthorizationOutcome.AutoDenied, audit.Outcome);
        Assert.False(audit.ProviderInvoked);
        Assert.False(audit.Succeeded);
    }

    [Fact]
    public async Task DefaultPolicy_RequestInvokesProvider_AndIsAudited()
    {
        var provider = new CountingTerminalProvider();
        var capabilities = new CapabilityRegistry([provider]);
        var auditLog = new InMemoryCapabilityAuditLog();
        var dispatcher = new CapabilityDispatcher(capabilities,
            new PolicyCapabilityAuthorizer(CapabilityAuthorizationPolicy.Default), auditLog);

        var result = await dispatcher.DispatchAsync("terminal", new ExecuteTerminalCapabilityRequest("echo hello"), default);

        Assert.False(result.IsError);
        Assert.Equal(1, provider.InvocationCount);
        var audit = Assert.Single(auditLog.Records);
        Assert.Equal(AuthorizationOutcome.AutoApproved, audit.Outcome);
        Assert.True(audit.ProviderInvoked);
        Assert.True(audit.Succeeded);
        Assert.NotEqual(default, audit.Timestamp);
    }

    [Fact]
    public async Task Confirmation_IsEvaluatedPerRequest_AndEachDispatchIsAudited()
    {
        var provider = new CountingTerminalProvider();
        var capabilities = new CapabilityRegistry([provider]);
        var auditLog = new InMemoryCapabilityAuditLog();
        var confirmation = new CountingConfirmationHandler();
        var dispatcher = new CapabilityDispatcher(capabilities,
            new FixedOutcomeAuthorizer(AuthorizationOutcome.RequiresUserConfirmation), auditLog, confirmation);

        var first = await dispatcher.DispatchAsync("terminal", new ExecuteTerminalCapabilityRequest("echo first"), default);
        var second = await dispatcher.DispatchAsync("terminal", new ExecuteTerminalCapabilityRequest("echo second"), default);

        Assert.False(first.IsError);
        Assert.False(second.IsError);
        Assert.Equal(2, confirmation.InvocationCount);
        Assert.Equal(2, provider.InvocationCount);
        Assert.Equal(2, auditLog.Records.Count);
        Assert.All(auditLog.Records, record =>
        {
            Assert.Equal(AuthorizationOutcome.RequiresUserConfirmation, record.Outcome);
            Assert.True(record.UserConfirmed);
            Assert.True(record.ProviderInvoked);
        });
    }

    [Fact]
    public async Task Executor_ReceivesOnlyDispatcher_AndDispatchesTypedRequest()
    {
        var dispatcher = new RecordingDispatcher();
        var executor = new ReadToolExecutor();

        var result = await executor.ExecuteAsync(
            new Dictionary<string, object?> { ["file_path"] = "notes.txt" }, dispatcher);

        Assert.False(result.IsError);
        Assert.Equal("file", dispatcher.CapabilityName);
        var request = Assert.IsType<ReadFileCapabilityRequest>(dispatcher.Request);
        Assert.Equal("notes.txt", request.FilePath);
    }

    [Fact]
    public async Task Executor_DoesNotBypassDispatcher_WhenPolicyDenies()
    {
        var provider = new CountingFileProvider();
        var capabilities = new CapabilityRegistry([provider]);
        var auditLog = new InMemoryCapabilityAuditLog();
        var dispatcher = new CapabilityDispatcher(capabilities,
            new FixedOutcomeAuthorizer(AuthorizationOutcome.AutoDenied), auditLog);
        var executor = new WriteToolExecutor();

        var result = await executor.ExecuteAsync(
            new Dictionary<string, object?> { ["file_path"] = "notes.txt", ["content"] = "secret" }, dispatcher);

        Assert.True(result.IsError);
        Assert.Equal(0, provider.InvocationCount);
        Assert.Single(auditLog.Records);
    }

    private sealed class FixedOutcomeAuthorizer(AuthorizationOutcome outcome) : ICapabilityAuthorizer
    {
        public Task<AuthorizationOutcome> AuthorizeAsync(string capabilityName, object request, CancellationToken ct) =>
            Task.FromResult(outcome);
    }

    private sealed class CountingConfirmationHandler : ICapabilityConfirmationHandler
    {
        public int InvocationCount { get; private set; }

        public Task<bool> ConfirmAsync(CapabilityConfirmationRequest request, CancellationToken ct)
        {
            InvocationCount++;
            return Task.FromResult(true);
        }
    }

    private sealed class RecordingDispatcher : ICapabilityDispatcher
    {
        public string? CapabilityName { get; private set; }
        public object? Request { get; private set; }

        public Task<ToolExecutionResult> DispatchAsync(string capabilityName, object request, CancellationToken ct)
        {
            CapabilityName = capabilityName;
            Request = request;
            return Task.FromResult(new ToolExecutionResult("dispatched"));
        }
    }

    private sealed class CountingFileProvider : IFileProvider
    {
        public string CapabilityName => "file";
        public IReadOnlyList<string> Roots => [];
        public int InvocationCount { get; private set; }

        public bool TryResolvePath(string path, out string resolved, out string? error)
        {
            InvocationCount++;
            resolved = path;
            error = null;
            return true;
        }

        public Task<bool> ExistsAsync(string resolvedPath, CancellationToken ct = default)
        {
            InvocationCount++;
            return Task.FromResult(true);
        }

        public Task<string> ReadFileAsync(string resolvedPath, CancellationToken ct = default)
        {
            InvocationCount++;
            return Task.FromResult("content");
        }

        public Task WriteFileAsync(string resolvedPath, string content, CancellationToken ct = default)
        {
            InvocationCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class CountingTerminalProvider : ITerminalProvider
    {
        public string CapabilityName => "terminal";
        public int InvocationCount { get; private set; }

        public Task<ToolExecutionResult> ExecuteAsync(
            string command,
            string? workingDir = null,
            CancellationToken ct = default)
        {
            InvocationCount++;
            return Task.FromResult(new ToolExecutionResult(command));
        }
    }
}
