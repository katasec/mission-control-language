using System.CommandLine;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Manifest;
using ForgeMission.Core.Resolution;
using ForgeMission.Parser;
using ForgeMission.Core.Runtime;
using static ForgeMission.Core.Runtime.MissionStatus;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using Katasec.OciClient;
using ForgeMission.Serve;
using Microsoft.AspNetCore.Builder;
using Spectre.Console;
using ForgeMission.Cli;
using ForgeMission.Cli.Docker;
using System.Diagnostics;
using System.Text.Json;
using MclProgram = ForgeMission.Parser.Program;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using McpTextContent = ModelContextProtocol.Protocol.TextContentBlock;

var rootCommand = new RootCommand("forge — Mission Control Language runtime");
rootCommand.Add(BuildInitCommand());
rootCommand.Add(BuildRunCommand());
rootCommand.Add(BuildValidateCommand());
rootCommand.Add(BuildListCommand());
rootCommand.Add(BuildExpertCommand());
rootCommand.Add(BuildRegistryCommand());
rootCommand.Add(BuildLoginCommand());
rootCommand.Add(BuildWhoamiCommand());
rootCommand.Add(BuildLogoutCommand());
rootCommand.Add(BuildExecCommand());
rootCommand.Add(BuildPublishCommand());
rootCommand.Add(BuildCleanCommand());
rootCommand.Add(BuildServeCommand());
rootCommand.Add(BuildClaudeCommand());
rootCommand.Add(BuildConnectCommand());
rootCommand.Add(BuildAgentCommand());
rootCommand.Add(BuildWebuiCommand());
rootCommand.Add(BuildProviderCommand());
rootCommand.Add(BuildMcpCommand());

// No @file response-file expansion — @handles (forge claude @websearch) are arguments.
return await rootCommand.Parse(args, new ParserConfiguration { ResponseFileTokenReplacer = null }).InvokeAsync();

// ---------------------------------------------------------------------------
// fms init

static Command BuildInitCommand()
{
    var missionArg  = new Argument<FileInfo?>("mission") { Description = "Path to the .mcl mission file (default: mission.mcl)", Arity = ArgumentArity.ZeroOrOne };
    var refreshOpt  = new Option<bool>("--refresh") { Description = "Re-pull OCI experts even if already present in ~/.forge/experts" };

    var cmd = new Command("init", "Resolve expert sources and generate mcl.lock");
    cmd.Add(missionArg);
    cmd.Add(refreshOpt);

    cmd.SetAction(async result =>
    {
        var mission    = ResolveMission(result.GetValue(missionArg));
        var missionDir = mission.DirectoryName!;
        var refresh    = result.GetValue(refreshOpt);

        var source = await TryReadFile(mission.FullName);
        if (source is null) return;

        var ast = TryParse(source, mission.FullName);
        if (ast is null) return;

        ForgeManifest? manifest = null;
        try { manifest = ForgeTomlReader.TryRead(mission.FullName); }
        catch (ForgeTomlException ex) { Die(ex.Message); return; }

        Console.WriteLine("Resolving experts...\n");

        // --- Local experts: discover from ./experts
        var localCatalog    = new Dictionary<string, ResolvedExpert>(StringComparer.Ordinal);
        var localExpertsDir = Path.Combine(missionDir, SourceResolver.DefaultExpertsDir);
        if (Directory.Exists(localExpertsDir))
        {
            try { localCatalog = new SourceResolver().Resolve(missionDir); }
            catch (MclException ex) { Die(ex.Message); return; }
        }

        // Build lock file from local experts
        var lockFile = LockFileIO.Build(localCatalog, missionDir);

        foreach (var (name, entry) in lockFile.Experts.OrderBy(k => k.Key))
            Console.WriteLine($"  ✓ {name,-30} local    {entry.Path}");

        // --- OCI experts: pull from registry and add to lock file
        if (manifest?.Experts.Count > 0)
        {
            foreach (var (name, ociRef) in manifest.Experts.OrderBy(k => k.Key))
            {
                try
                {
                    var (cachePath, status) = await OciExpertPuller.PullAsync(ociRef, refresh);
                    var lockPath2           = OciExpertPuller.ToLockPath(cachePath);
                    var hash                = LockFileIO.ComputeHash(cachePath);
                    lockFile.Experts[name]  = new LockFileExpert { Source = "oci", Path = lockPath2, Hash = hash };
                    Console.WriteLine($"  ✓ {name,-30} {status,-8} {ociRef}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"  ✗ {name,-30} failed   {ociRef}");
                    Console.Error.WriteLine($"    {ex.Message}");
                    Console.Error.WriteLine("    Run 'forge registry login <registry> --token <PAT>' if this is an auth error.");
                    Die($"MCL011 OCI pull failed for '{name}'.");
                    return;
                }
            }
        }

        var lockPath = Path.Combine(missionDir, "mcl.lock");
        LockFileIO.Write(lockPath, lockFile);

        var total = lockFile.Experts.Count;
        Console.WriteLine($"\nmcl.lock written ({total} expert{(total == 1 ? "" : "s")}). Run 'forge run' to execute the mission.");
    });

    return cmd;
}

// ---------------------------------------------------------------------------
// fms run

static Command BuildRunCommand()
{
    var missionArg  = new Argument<FileInfo?>("mission") { Description = "Path to the .mcl mission file (default: mission.mcl)", Arity = ArgumentArity.ZeroOrOne };
    var stepsOpt    = new Option<bool>("--steps")   { Description = "Stream each expert's output to stderr as the pipeline runs" };
    var verboseOpt  = new Option<bool>("--verbose") { Description = "Print expert resolution source for each step before running" };
    var varOpt      = new Option<string[]>("--var")
    {
        Description = "Set a context variable as key=value (repeatable, overrides let bindings)",
        AllowMultipleArgumentsPerToken = false
    };
    varOpt.Arity = ArgumentArity.ZeroOrMore;

    var cmd = new Command("run", "Run a mission");
    cmd.Add(missionArg);
    cmd.Add(stepsOpt);
    cmd.Add(verboseOpt);
    cmd.Add(varOpt);

    cmd.SetAction(async result =>
    {
        var mission    = ResolveMission(result.GetValue(missionArg));
        var showSteps  = result.GetValue(stepsOpt);
        var verbose    = result.GetValue(verboseOpt);
        var vars       = result.GetValue(varOpt) ?? [];
        var missionDir = mission.DirectoryName!;

        var lockPath = Path.Combine(missionDir, "mcl.lock");
        if (!File.Exists(lockPath))
        {
            Die("MCL007 Mission not initialised — run 'forge init' first.");
            return;
        }

        var parsedVars = ParseVars(vars);
        if (parsedVars is null) return;

        var source = await TryReadFile(mission.FullName);
        if (source is null) return;

        var ast = TryParse(source, mission.FullName);
        if (ast is null) return;

        ForgeManifest? manifest = null;
        try { manifest = ForgeTomlReader.TryRead(mission.FullName); }
        catch (ForgeTomlException ex) { Die(ex.Message); return; }

        LockFile lockFile;
        try { lockFile = LockFileIO.Read(lockPath); }
        catch (Exception ex) { Die($"Cannot read mcl.lock: {ex.Message}"); return; }

        Dictionary<string, ExpertDefinition> expertDefs;
        try { expertDefs = ExpertResolver.ResolveAll(lockFile, missionDir, verbose ? Console.Error : null, Console.Error); }
        catch (AggregateExpertLoadException ex) { foreach (var e in ex.Errors) ReportExpertDiagnostic(e); Environment.Exit(1); return; }
        catch (ExpertLoadException ex)           { ReportExpertDiagnostic(ex); Environment.Exit(1); return; }

        if (!TryValidate(ast, expertDefs, contractErrorsAreFatal: true)) return;

        Dictionary<string, object> seedContext;
        try { seedContext = ContextBuilder.Seed(ast, parsedVars); }
        catch (InvalidOperationException ex) { Die(ex.Message); return; }

        // Build runner per profile from forge.toml; fall back to let-binding config for "default".
        var runners = BuildRunners(manifest, seedContext);
        if (runners is null) return;

        var firstMission = ast.Declarations.OfType<MissionDeclaration>().FirstOrDefault();
        if (firstMission is null) { Die("No mission declaration found in mission file."); return; }

        var options = new PipelineRunOptions(
            firstMission.Name,
            parsedVars,
            showSteps ? Console.Error : null);

        Console.Error.WriteLine($"Running mission '{firstMission.Name}'...");

        MissionResult missionResult;
        try
        {
            missionResult = await new PipelineRunner(runners, manifest?.Execution, ProviderClientBuilder.BuildWebSearch()).RunAsync(ast, expertDefs, options);
        }
        catch (InvalidOperationException ex)
        {
            Die(ex.Message);
            return;
        }

        if (missionResult.Status == MissionStatus.Fail)
        {
            Console.Error.WriteLine($"{BoldRed("error")}{Bold($": mission failed — {missionResult.FailReason}")}");
            Environment.Exit(1);
            return;
        }

        var outputDecl = ast.Outputs.FirstOrDefault(o => o.MissionName == firstMission.Name);
        if (outputDecl?.FilePath is { } filePath)
        {
            await File.WriteAllTextAsync(filePath, missionResult.Text);
            Console.Error.WriteLine($"Output written to {filePath}");
        }
        else
        {
            Console.WriteLine(missionResult.Text);
        }
    });

    return cmd;
}

// ---------------------------------------------------------------------------
// fms validate

static Command BuildValidateCommand()
{
    var missionArg = new Argument<FileInfo?>("mission") { Description = "Path to the .mcl mission file (default: mission.mcl)", Arity = ArgumentArity.ZeroOrOne };

    var cmd = new Command("validate", "Validate a mission file and its expert references");
    cmd.Add(missionArg);

    cmd.SetAction(async result =>
    {
        var mission    = ResolveMission(result.GetValue(missionArg));
        var missionDir = mission.DirectoryName!;
        var lockPath   = Path.Combine(missionDir, "mcl.lock");

        var source = await TryReadFile(mission.FullName);
        if (source is null) return;

        var ast = TryParse(source, mission.FullName);
        if (ast is null) return;

        // Validate forge.toml if present
        try { ForgeTomlReader.TryRead(mission.FullName); }
        catch (ForgeTomlException ex) { Die(ex.Message); return; }

        // Warn if lock file is absent or stale
        if (!File.Exists(lockPath))
        {
            Console.Error.WriteLine($"{BoldYellow("warning")}{Bold(": MCL006 mcl.lock not found — run 'forge init' to generate it")}");
        }
        if (!File.Exists(lockPath))
        {
            var expertDefs = TryLoadExperts(Path.Combine(missionDir, SourceResolver.DefaultExpertsDir));
            if (expertDefs is null) return;
            if (TryValidate(ast, expertDefs))
                Console.WriteLine("OK — mission is valid.");
        }
        else
        {
            var lockFile   = LockFileIO.Read(lockPath);
            Dictionary<string, ExpertDefinition> expertDefs;
            try { expertDefs = ExpertResolver.ResolveAll(lockFile, missionDir, warnings: Console.Error); }
            catch (ExpertLoadException ex) { ReportExpertDiagnostic(ex); return; }
            if (TryValidate(ast, expertDefs))
                Console.WriteLine("OK — mission is valid.");
        }
    });

    return cmd;
}

// ---------------------------------------------------------------------------
// fms list

static Command BuildListCommand()
{
    var listCommand    = new Command("list", "List available resources");
    var expertsOpt     = new Option<DirectoryInfo?>("--experts") { Description = "Experts directory (default: ./experts)" };
    var listExpertsCmd = new Command("experts", "List experts in the experts directory");
    listExpertsCmd.Add(expertsOpt);

    listExpertsCmd.SetAction(async result =>
    {
        var experts    = result.GetValue(expertsOpt);
        var expertsDir = experts?.FullName ?? "experts";

        var expertDefs = TryLoadExperts(expertsDir);
        if (expertDefs is null) return;

        if (expertDefs.Count == 0) { Console.WriteLine($"No experts found in {expertsDir}"); return; }

        Console.WriteLine($"Experts in {expertsDir}:");
        foreach (var (name, def) in expertDefs.OrderBy(k => k.Key))
            Console.WriteLine($"  {name,-30} {def.Input} -> {def.Output}");

        await Task.CompletedTask;
    });

    listCommand.Add(listExpertsCmd);
    return listCommand;
}

// ---------------------------------------------------------------------------
// fms expert

static Command BuildExpertCommand()
{
    var expertCommand = new Command("expert", "Manage experts");

    var nameArg      = new Argument<string>("name") { Description = "Expert name (PascalCase)" };
    var expertsOpt   = new Option<DirectoryInfo?>("--experts") { Description = "Experts directory (default: ./experts)" };
    var initExpertCmd = new Command("init", "Scaffold a new expert directory");
    initExpertCmd.Add(nameArg);
    initExpertCmd.Add(expertsOpt);

    initExpertCmd.SetAction(async result =>
    {
        var name       = result.GetValue(nameArg)!;
        var experts    = result.GetValue(expertsOpt);
        var expertsDir = experts?.FullName ?? "experts";

        if (!char.IsUpper(name[0]))
        {
            Die($"Expert name must be PascalCase, got '{name}'.");
            return;
        }

        var expertDir = Path.Combine(expertsDir, name);
        var expertMd  = Path.Combine(expertDir, "expert.md");

        if (File.Exists(expertMd))
        {
            Die($"Expert '{name}' already exists at {expertMd}");
            return;
        }

        Directory.CreateDirectory(expertDir);
        await File.WriteAllTextAsync(expertMd, ExpertTemplate(name));

        Console.WriteLine($"Created {expertMd}");
    });

    expertCommand.Add(initExpertCmd);
    return expertCommand;
}

// ---------------------------------------------------------------------------
// forge registry login  (42.5: OCI-registry credentials move under `forge registry`;
// the top-level `forge login` becomes platform sign-in)

static Command BuildRegistryCommand()
{
    var cmd = new Command("registry", "OCI registry operations");
    cmd.Add(BuildRegistryLoginCommand());
    return cmd;
}

static Command BuildRegistryLoginCommand()
{
    var registryArg = new Argument<string>("registry") { Description = "Registry host (e.g. ghcr.io)" };
    var tokenOpt    = new Option<string>("--token") { Description = "Credential token (e.g. GitHub PAT)" };

    var cmd = new Command("login", "Save registry credentials to ~/.forge/credentials.json");
    cmd.Add(registryArg);
    cmd.Add(tokenOpt);

    cmd.SetAction(result => SaveRegistryCredential(result.GetValue(registryArg)!, result.GetValue(tokenOpt)!));
    return cmd;
}

// forge login = platform sign-in (loopback + PKCE → platform key).
// OCI-registry credentials live under `forge registry login`.
static Command BuildLoginCommand()
{
    var cmd = new Command("login", "Sign in to Forge (browser) and store a platform key");
    cmd.SetAction(async _ => await PlatformLogin.RunAsync());
    return cmd;
}

static Command BuildWhoamiCommand()
{
    var cmd = new Command("whoami", "Show the signed-in Forge user and credit balance");
    cmd.SetAction(async _ => await PlatformLogin.WhoAmIAsync());
    return cmd;
}

static Command BuildLogoutCommand()
{
    var cmd = new Command("logout", "Sign out of Forge (remove the stored platform key)");
    cmd.SetAction(_ => PlatformLogin.Logout());
    return cmd;
}

// forge exec — the one-shot hosted-mission command (42.6 task 8).
static Command BuildExecCommand()
{
    var targetArg = new Argument<string>("target") { Description = "Mission handle, e.g. @websearch" };
    var promptArg = new Argument<string>("prompt") { Description = "The mission's free-text input" };

    var cmd = new Command("exec", "Run a hosted mission once and print the answer");
    cmd.Add(targetArg);
    cmd.Add(promptArg);

    cmd.SetAction(async result =>
        await ForgeExec.RunAsync(result.GetValue(targetArg)!, result.GetValue(promptArg)!));
    return cmd;
}

static void SaveRegistryCredential(string registry, string token)
{
    CredentialStore.SaveToken(registry, token);
    Console.WriteLine($"Credentials saved for {registry}");
}

// ---------------------------------------------------------------------------
// forge publish — package a mission directory and push it to an OCI registry (39.4)

static Command BuildPublishCommand()
{
    var refArg  = new Argument<string>("reference") { Description = "OCI reference, e.g. ghcr.io/katasec/forge-mission-guard:0.1.0" };
    var dirArg  = new Argument<DirectoryInfo?>("dir") { Description = "Mission directory (default: current dir)", Arity = ArgumentArity.ZeroOrOne };
    var descOpt = new Option<string?>("--description") { Description = "Human description (annotation)" };

    var cmd = new Command("publish", "Package a mission directory into a self-contained OCI artifact and push it");
    cmd.Add(refArg);
    cmd.Add(dirArg);
    cmd.Add(descOpt);

    cmd.SetAction(async result =>
    {
        var reference = result.GetValue(refArg)!;
        var dir       = result.GetValue(dirArg) ?? new DirectoryInfo(Directory.GetCurrentDirectory());
        var desc      = result.GetValue(descOpt);

        if (!File.Exists(Path.Combine(dir.FullName, "mission.mcl")))
        { Die($"No mission.mcl in {dir.FullName} — point at a mission directory."); return; }

        // registry/name:tag  (registry = up to first '/', tag = after the LAST ':').
        var slash = reference.IndexOf('/');
        if (slash < 0) { Die($"Invalid reference '{reference}': expected registry/name:tag"); return; }
        var registry = reference[..slash];
        var rest     = reference[(slash + 1)..];
        var colon    = rest.LastIndexOf(':');
        if (colon < 0) { Die($"Invalid reference '{reference}': expected a :tag"); return; }
        var name = rest[..colon];
        var tag  = rest[(colon + 1)..];

        var bundle = MissionBundle.Pack(dir.FullName);
        var token  = CredentialStore.GetToken(registry);
        if (string.IsNullOrWhiteSpace(token))
            Console.Error.WriteLine($"Warning: no credential for {registry} (set FORGE_REGISTRY_TOKEN or 'forge registry login').");

        var annotations = desc is null
            ? null
            : new Dictionary<string, string> { ["org.opencontainers.image.description"] = desc };

        using var client = new OciClient(credential: token);
        var digest = await client.PushMissionAsync(registry, name, tag, bundle, annotations);

        Console.WriteLine($"Published {registry}/{name}:{tag} ({bundle.Length} bytes)");
        Console.WriteLine($"Digest:  {digest}");
        Console.WriteLine($"Pinned:  {registry}/{name}@{digest}");
    });

    return cmd;
}

// ---------------------------------------------------------------------------
// forge serve

static Command BuildServeCommand()
{
    var agentArg = new Argument<FileInfo?>("agent") { Description = "Path to agent.yaml (default: agent.yaml)", Arity = ArgumentArity.ZeroOrOne };

    var cmd = new Command("serve", "Serve a mission as an OpenAI-compatible endpoint");
    cmd.Add(agentArg);

    cmd.SetAction(async result =>
    {
        var agentFile = result.GetValue(agentArg)
            ?? new FileInfo(Path.GetFullPath("agent.yaml"));

        if (!agentFile.Exists) { Die($"Agent config not found: {agentFile.FullName}\nCreate an agent.yaml next to your mission.mcl."); return; }

        AgentConfig config;
        try { config = AgentConfigLoader.Load(agentFile.FullName); }
        catch (Exception ex) { Die($"Cannot read agent.yaml: {ex.Message}"); return; }

        var built = await TryBuildMissionServerAsync(config, agentFile.DirectoryName!);
        if (built is not var (app, missionPath, anthropicDoor, openAiDoor)) return;

        Console.Error.WriteLine($"forge serve — agent '{config.Id}' listening on http://0.0.0.0:{config.Port}");
        Console.Error.WriteLine($"  mission  : {missionPath}");
        Console.Error.WriteLine($"  wire     : {(anthropicDoor && openAiDoor ? "anthropic + openai" : anthropicDoor ? "anthropic" : "openai")}");
        Console.Error.WriteLine($"  endpoints:");
        if (anthropicDoor)
            Console.Error.WriteLine($"    POST /v1/messages          (messages API, streaming + non-streaming)");
        if (openAiDoor)
        {
            Console.Error.WriteLine($"    POST /v1/chat/completions  (chat, streaming)");
            Console.Error.WriteLine($"    POST /v1/responses         (responses API, streaming)");
            Console.Error.WriteLine($"    GET  /v1/models");
        }
        Console.Error.WriteLine($"    GET  /health");

        try
        {
            await app.RunAsync();
        }
        catch (IOException ex) when (ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
                                  || ex.InnerException is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.AddressAlreadyInUse })
        {
            Die($"Port {config.Port} is already in use. Stop the existing process or change the port in agent.yaml.");
        }
    });

    return cmd;
}

// ---------------------------------------------------------------------------
// forge claude — one command: serve the mission (Anthropic wire) + launch Claude Code
// wired to it + tear down on exit (Phase 42.2). In-process by default for speed;
// --container runs the same image the cloud runs for exact parity.

static Command BuildClaudeCommand()
{
    var targetArg    = new Argument<string?>("target") { Description = "Mission: a .mcl path, an @handle (built-in), or empty (agent.yaml / lone .mcl in cwd)", Arity = ArgumentArity.ZeroOrOne };
    var containerOpt = new Option<bool>("--container")   { Description = "Run the mission as a Docker container (cloud parity) instead of in-process" };
    var portOpt      = new Option<int?>("--port")        { Description = "Pin the port instead of picking an ephemeral one" };
    var printEnvOpt  = new Option<bool>("--print-env")   { Description = "Print the export lines and keep serving (wire other tools by hand); Ctrl-C stops" };
    var promptOpt    = new Option<string?>("-p", "--prompt") { Description = "One-shot prompt passed straight through to claude" };

    var cmd = new Command("claude", "Launch Claude Code talking to a forge mission");
    cmd.Add(targetArg);
    cmd.Add(containerOpt);
    cmd.Add(portOpt);
    cmd.Add(printEnvOpt);
    cmd.Add(promptOpt);
    cmd.TreatUnmatchedTokensAsErrors = false;   // anything after the options is forwarded to claude

    cmd.SetAction(async result =>
    {
        var printEnv = result.GetValue(printEnvOpt);
        if (!printEnv && !IsOnPath("claude"))
        {
            Die("claude CLI not found on PATH.\nInstall it first: npm install -g @anthropic-ai/claude-code  (https://claude.com/claude-code)");
            return;
        }

        var resolved = await ResolveClaudeTargetAsync(result.GetValue(targetArg));
        if (resolved is not var (missionPath, missionName)) return;

        if (!await EnsureInitializedAsync(missionPath)) return;

        var port = result.GetValue(portOpt) ?? FindFreePort();
        var config = new AgentConfig
        {
            Mission = missionPath,
            Port    = result.GetValue(containerOpt) ? 8080 : port,   // container listens on 8080 inside
            Id      = missionName,
            Wire    = "anthropic",
        };

        Func<Task> teardown;
        string mode;
        if (result.GetValue(containerOpt))
        {
            var started = await StartMissionContainerAsync(config, missionPath, hostPort: port);
            if (started is null) return;
            teardown = started;
            mode     = "container";
        }
        else
        {
            var built = await TryBuildMissionServerAsync(config, Path.GetDirectoryName(missionPath)!);
            if (built is not var (app, _, _, _)) return;
            await app.StartAsync();
            teardown = async () => await app.StopAsync();
            mode     = "in-process";
        }

        var baseUrl = $"http://127.0.0.1:{port}";
        try
        {
            if (!await WaitUntilHealthyAsync(baseUrl, timeoutMs: 60_000))
            {
                Die($"Mission endpoint did not become healthy at {baseUrl}.");
                return;
            }

            Console.Error.WriteLine($"✓ mission   {missionName,-15} {missionPath}");
            Console.Error.WriteLine($"✓ endpoint  /v1/messages    {baseUrl}   ({mode})");
            Console.Error.WriteLine($"✓ wired     ANTHROPIC_BASE_URL → forge");

            if (printEnv)
            {
                Console.WriteLine($"export ANTHROPIC_BASE_URL={baseUrl}");
                Console.WriteLine("export ANTHROPIC_API_KEY=forge-local");
                Console.Error.WriteLine("↳ serving until Ctrl-C…");
                await WaitForCtrlCAsync();
                return;
            }

            Console.Error.WriteLine("↳ launching claude…");
            Environment.ExitCode = await RunWiredClaudeAsync(
                baseUrl, result.GetValue(promptOpt), result.UnmatchedTokens);
        }
        finally
        {
            await teardown();
        }
    });

    return cmd;
}

// ---------------------------------------------------------------------------
// forge connect vscode — wire the Claude Code EXTENSION (VS Code / VSCodium) to a
// mission. The extension is a GUI process that never sees shell env, so the sanctioned
// gesture is the claudeCode.environmentVariables key in the workspace settings — which
// BOTH editors read, so one write covers VS Code and VSCodium alike. A pinned port
// (default 8787) because the extension cannot chase ephemeral ones.

static Command BuildConnectCommand()
{
    var connectCmd = new Command("connect", "Wire an editor's Claude Code extension to a forge mission");
    connectCmd.Add(BuildConnectVsCodeCommand());
    return connectCmd;
}

static Command BuildConnectVsCodeCommand()
{
    var targetArg = new Argument<string?>("target") { Description = "Mission: a .mcl path, an @handle (built-in), or empty (agent.yaml / lone .mcl in cwd)", Arity = ArgumentArity.ZeroOrOne };
    var portOpt   = new Option<int>("--port") { Description = "Port to serve on and write into the settings (default 8787)", DefaultValueFactory = _ => 8787 };

    var cmd = new Command("vscode", "Write .vscode/settings.json and serve the mission (also covers VSCodium)");
    cmd.Add(targetArg);
    cmd.Add(portOpt);

    cmd.SetAction(async result =>
    {
        var resolved = await ResolveClaudeTargetAsync(result.GetValue(targetArg));
        if (resolved is not var (missionPath, missionName)) return;

        if (!await EnsureInitializedAsync(missionPath)) return;

        var port    = result.GetValue(portOpt);
        var baseUrl = $"http://127.0.0.1:{port}";
        if (!TryWriteVsCodeSettings(Directory.GetCurrentDirectory(), baseUrl)) return;

        var config = new AgentConfig { Mission = missionPath, Port = port, Id = missionName, Wire = "anthropic" };
        var built  = await TryBuildMissionServerAsync(config, Path.GetDirectoryName(missionPath)!);
        if (built is not var (app, _, _, _)) return;

        Console.Error.WriteLine($"✓ mission   {missionName,-15} {missionPath}");
        Console.Error.WriteLine($"✓ endpoint  /v1/messages    {baseUrl}");
        Console.Error.WriteLine($"✓ settings  .vscode/settings.json → claudeCode.environmentVariables");
        Console.Error.WriteLine($"↳ open VS Code / VSCodium in this folder — the Claude Code extension now talks to the mission.");
        Console.Error.WriteLine($"  Serving until Ctrl-C (settings persist; rerun this command to reconnect).");

        try { await app.RunAsync(); }
        catch (IOException ex) when (ex.Message.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
                                  || ex.InnerException is System.Net.Sockets.SocketException { SocketErrorCode: System.Net.Sockets.SocketError.AddressAlreadyInUse })
        {
            Die($"Port {port} is already in use. Pass a different --port (and rerun so the settings match).");
        }
    });

    return cmd;
}

// Merge claudeCode.environmentVariables into the workspace settings. settings.json is
// JSONC (comments allowed) and a DOM rewrite would destroy comments — so: create freely
// when absent; merge when comment-free; otherwise print the snippet instead of clobbering.
static bool TryWriteVsCodeSettings(string workspaceDir, string baseUrl)
{
    var settingsDir  = Path.Combine(workspaceDir, ".vscode");
    var settingsPath = Path.Combine(settingsDir, "settings.json");

    var envNode = new System.Text.Json.Nodes.JsonObject
    {
        ["ANTHROPIC_BASE_URL"] = baseUrl,
        ["ANTHROPIC_API_KEY"]  = "forge-local",
    };

    System.Text.Json.Nodes.JsonObject root;
    if (!File.Exists(settingsPath))
    {
        root = [];
    }
    else
    {
        var raw = File.ReadAllText(settingsPath);
        if (HasJsoncComments(raw))
        {
            Console.Error.WriteLine($"{settingsPath} contains comments, which a rewrite would lose.");
            Console.Error.WriteLine("Add this key yourself, then rerun with the same --port:\n");
            Console.Error.WriteLine($$"""
                "claudeCode.environmentVariables": {
                  "ANTHROPIC_BASE_URL": "{{baseUrl}}",
                  "ANTHROPIC_API_KEY": "forge-local"
                }
                """);
            return false;
        }
        try
        {
            root = System.Text.Json.Nodes.JsonNode.Parse(raw,
                documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true })
                as System.Text.Json.Nodes.JsonObject ?? [];
        }
        catch (JsonException ex) { Die($"Cannot parse {settingsPath}: {ex.Message}"); return false; }
    }

    root["claudeCode.environmentVariables"] = envNode;

    Directory.CreateDirectory(settingsDir);
    using var stream = File.Create(settingsPath);
    using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
    root.WriteTo(writer);
    return true;
}

// Cheap comment detection outside string literals — good enough to protect user JSONC.
static bool HasJsoncComments(string raw)
{
    var inString = false;
    for (var i = 0; i < raw.Length - 1; i++)
    {
        var c = raw[i];
        if (inString)
        {
            if (c == '\\') i++;
            else if (c == '"') inString = false;
        }
        else if (c == '"') inString = true;
        else if (c == '/' && (raw[i + 1] == '/' || raw[i + 1] == '*')) return true;
    }
    return false;
}

// target → (absolute mission path, mission name). @handle pulls the built-in from the
// OCI catalog by pinned digest; a path uses that file; empty means agent.yaml or the
// lone .mcl in the current directory.
static async Task<(string MissionPath, string MissionName)?> ResolveClaudeTargetAsync(string? target)
{
    if (target is { } t && t.StartsWith('@'))
    {
        var handle  = t[1..];
        var builtin = BuiltinMissions.All.FirstOrDefault(
            b => b.Label.Equals(handle, StringComparison.OrdinalIgnoreCase));
        if (builtin is null)
        {
            Die($"Unknown built-in mission '{t}'. Available: {string.Join(", ", BuiltinMissions.All.Select(b => "@" + b.Label.ToLowerInvariant()))}");
            return null;
        }
        Console.Error.WriteLine($"↳ pulling {t} from the mission catalog…");
        var (dir, status) = await OciMissionPuller.PullAsync(builtin.OciRef, refresh: false);
        Console.Error.WriteLine($"✓ {t} {status}");
        return (Path.Combine(dir, "mission.mcl"), builtin.Label.ToLowerInvariant());
    }

    if (target is { } path)
    {
        var full = Path.GetFullPath(path);
        if (!File.Exists(full)) { Die($"Mission file not found: {full}"); return null; }
        return (full, Path.GetFileNameWithoutExtension(full) is "mission" or ""
            ? new DirectoryInfo(Path.GetDirectoryName(full)!).Name
            : Path.GetFileNameWithoutExtension(full));
    }

    var agentYaml = Path.GetFullPath("agent.yaml");
    if (File.Exists(agentYaml))
    {
        try
        {
            var config = AgentConfigLoader.Load(agentYaml);
            return (Path.GetFullPath(Path.Combine(Path.GetDirectoryName(agentYaml)!, config.Mission)), config.Id);
        }
        catch (Exception ex) { Die($"Cannot read agent.yaml: {ex.Message}"); return null; }
    }

    var candidates = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.mcl");
    if (candidates.Length == 1)
        return (candidates[0], new DirectoryInfo(Directory.GetCurrentDirectory()).Name);

    Die(candidates.Length == 0
        ? "No mission found. Pass a .mcl path, an @handle, or run from a directory with a mission."
        : "Multiple .mcl files here — pass the one you mean: forge claude ./<mission>.mcl");
    return null;
}

// Missing mcl.lock ⇒ run `forge init` silently (local experts resolve without network).
static async Task<bool> EnsureInitializedAsync(string missionPath)
{
    var lockPath = Path.Combine(Path.GetDirectoryName(missionPath)!, "mcl.lock");
    if (File.Exists(lockPath)) return true;

    Console.Error.WriteLine("↳ initialising mission (forge init)…");
    var exit = await BuildInitCommand().Parse([missionPath]).InvokeAsync();
    return exit == 0 && File.Exists(lockPath);
}

// --container: the EXACT image the cloud runs (the converged /v1 runner, 42.4) as an EPHEMERAL
// auto-named container — started and torn down within this command (unlike the persistent
// `forge agent start`). MissionFile env selects the runner's local-mission mode (serve exactly
// the mounted mission, no built-ins); no agent.yaml is synthesized.
static async Task<Func<Task>?> StartMissionContainerAsync(AgentConfig config, string missionPath, int hostPort)
{
    const string runnerImage = "ghcr.io/katasec/forge-runner:latest";

    var docker = await DockerPrereqChecker.CheckDockerAsync();
    if (!DockerPrereqChecker.RunAndPrint([docker])) return null;

    if (!await DockerCli.IsImagePresentAsync(runnerImage))
    {
        AnsiConsole.MarkupLine($"[yellow]Pulling {runnerImage}...[/]");
        await DockerCli.PullImageAsync(runnerImage);
    }
    await DockerCli.EnsureNetworkAsync("forge-net");

    var missionDir         = Path.GetDirectoryName(missionPath)!;
    var workspaceRoot      = FindGitRoot(missionDir) ?? missionDir;
    var missionInWorkspace = "/workspace/" + Path.GetRelativePath(workspaceRoot, missionPath).Replace('\\', '/');

    var containerName = $"forge-claude-{Guid.NewGuid():N}"[..20];
    await DockerCli.RunContainerAsync(
        name:          containerName,
        image:         runnerImage,
        cmd:           [],   // the image's entrypoint IS the runner; config rides in env
        // Forward the full provider-key table (docs/design/deploy.md) — the mounted mission's
        // forge.toml names whichever env(...) keys it wants, exactly as the in-process path
        // sees them.
        env:           [.. BuildEnvArray("MCL_API_KEY", "MCL_MODEL", "MCL_PROVIDER", "MCL_ENDPOINT",
                                         "OPENAI_API_KEY", "CLAUDE_API_KEY", "XAI_API_KEY",
                                         "GROK_API_KEY", "GOOGLE_SEARCH_API_KEY"),
                        $"MissionFile={missionInWorkspace}"],
        binds:         [$"{workspaceRoot}:/workspace"],
        hostPort:      hostPort,
        containerPort: config.Port,
        network:       "forge-net");

    return async () => await DockerCli.StopAndRemoveAsync(containerName);
}

static async Task<bool> WaitUntilHealthyAsync(string baseUrl, int timeoutMs)
{
    using var http = new HttpClient();
    var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
    while (DateTime.UtcNow < deadline)
    {
        try
        {
            var response = await http.SendAsync(new HttpRequestMessage(HttpMethod.Head, baseUrl + "/"));
            if (response.IsSuccessStatusCode) return true;
        }
        catch (HttpRequestException) { /* not up yet */ }
        await Task.Delay(250);
    }
    return false;
}

// Launch claude with the redirect env, stdio inherited (fully interactive), and
// forward -p plus any unparsed tokens. Returns claude's exit code.
static async Task<int> RunWiredClaudeAsync(string baseUrl, string? prompt, IReadOnlyList<string> passthrough)
{
    var psi = new ProcessStartInfo("claude") { UseShellExecute = false };
    if (prompt is not null) { psi.ArgumentList.Add("-p"); psi.ArgumentList.Add(prompt); }
    foreach (var token in passthrough) psi.ArgumentList.Add(token);

    psi.Environment["ANTHROPIC_BASE_URL"] = baseUrl;
    psi.Environment["ANTHROPIC_API_KEY"]  = "forge-local";   // forge ignores the value

    // Ctrl-C goes to the whole foreground group: claude handles it and exits; forge
    // must survive the signal so teardown still runs.
    Console.CancelKeyPress += Survive;
    try
    {
        using var proc = Process.Start(psi)!;
        await proc.WaitForExitAsync();
        return proc.ExitCode;
    }
    finally
    {
        Console.CancelKeyPress -= Survive;
    }

    static void Survive(object? _, ConsoleCancelEventArgs e) => e.Cancel = true;
}

static async Task WaitForCtrlCAsync()
{
    var stopped = new TaskCompletionSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; stopped.TrySetResult(); };
    await stopped.Task;
}

static int FindFreePort()
{
    var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
    listener.Start();
    var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    listener.Stop();
    return port;
}

static bool IsOnPath(string binary) =>
    (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
        .Split(Path.PathSeparator)
        .Any(dir => File.Exists(Path.Combine(dir, binary))
                 || File.Exists(Path.Combine(dir, binary + ".exe")));

// Loads the mission behind an AgentConfig and builds the wire server, ready to start.
// Shared by `forge serve` (blocking RunAsync) and `forge claude` (background StartAsync).
// Returns null after reporting the error (Die) — callers just return.
static async Task<(WebApplication App, string MissionPath, bool AnthropicDoor, bool OpenAiDoor)?> TryBuildMissionServerAsync(
    AgentConfig config, string agentDir)
{
    var missionPath = Path.GetFullPath(Path.Combine(agentDir, config.Mission));
    var lockPath    = Path.Combine(Path.GetDirectoryName(missionPath)!, "mcl.lock");

    if (!File.Exists(missionPath)) { Die($"Mission file not found: {missionPath}"); return null; }
    if (!File.Exists(lockPath))    { Die("MCL007 Mission not initialised — run 'forge init' first."); return null; }

    var source = await TryReadFile(missionPath);
    if (source is null) return null;

    var ast = TryParse(source, missionPath);
    if (ast is null) return null;

    LockFile lockFile;
    try { lockFile = LockFileIO.Read(lockPath); }
    catch (Exception ex) { Die($"Cannot read mcl.lock: {ex.Message}"); return null; }

    Dictionary<string, ExpertDefinition> expertDefs;
    try { expertDefs = ExpertLoader.LoadFromLockFile(lockFile, Path.GetDirectoryName(missionPath)!); }
    catch (AggregateExpertLoadException ex) { foreach (var e in ex.Errors) ReportExpertDiagnostic(e); Environment.Exit(1); return null; }
    catch (ExpertLoadException ex)           { ReportExpertDiagnostic(ex); Environment.Exit(1); return null; }

    if (!TryValidate(ast, expertDefs)) return null;

    Dictionary<string, object> seedContext;
    try { seedContext = ContextBuilder.Seed(ast, new Dictionary<string, string>()); }
    catch (InvalidOperationException ex) { Die(ex.Message); return null; }

    ForgeManifest? manifest = null;
    try { manifest = ForgeTomlReader.TryRead(missionPath); }
    catch (ForgeTomlException ex) { Die(ex.Message); return null; }

    var runners = BuildRunners(manifest, seedContext);
    if (runners is null) return null;

    var defaultRunner = runners.TryGetValue("default", out var dr)
        ? dr
        : runners.Values.First();

    // Wire doors (42.4): ONE app serves every enabled wire — `wire:` in agent.yaml is now a
    // disable switch ("anthropic" / "openai" limits to that door; default = both). Each door
    // gets its own MissionChatClient over the same mission core: the Anthropic wire hands the
    // mission the FULL conversation (context["conversation"]/["system"], goal = last text block
    // of the last user message); the OpenAI wire keeps the legacy last-turn behaviour so
    // existing users don't regress (42.1 decision, unchanged).
    var wire = string.IsNullOrWhiteSpace(config.Wire) ? "both" : config.Wire.Trim().ToLowerInvariant();
    if (wire is not ("both" or "anthropic" or "openai"))
    {
        Die($"Unknown wire '{config.Wire}' in agent.yaml — use \"both\", \"anthropic\", or \"openai\".");
        return null;
    }

    // kind:search backend (41.2) — same seam as `forge run` and the cloud runner; null when
    // XAI_API_KEY is unset, so missions without kind:search are unaffected.
    var webSearch = ProviderClientBuilder.BuildWebSearch();

    var anthropicDoor = wire is "both" or "anthropic"
        ? new MissionChatClient(ast, expertDefs, defaultRunner, fullConversation: true, webSearch: webSearch)
        : null;
    var openAiDoor = wire is "both" or "openai"
        ? new MissionChatClient(ast, expertDefs, defaultRunner, fullConversation: false, webSearch: webSearch)
        : null;

    // Aux passthrough (42.3 §0): client housekeeping (title-gen, state-check) is answered by a
    // plain provider model; the mission never runs — and never bills — for those.
    IChatClient? auxClient = null;
    if (anthropicDoor is not null && ResolveDefaultProfile(manifest, seedContext) is { } auxProfile)
    {
        try { auxClient = ProviderClientBuilder.BuildChatClient(auxProfile); }
        catch { /* aux degrades to the server's canned replies */ }
    }

    var app = ForgeServe.BuildApp(config.Id, config.Port, anthropicDoor, openAiDoor, auxClient);

    return (app, missionPath, anthropicDoor is not null, openAiDoor is not null);
}

// ---------------------------------------------------------------------------
// forge clean

static Command BuildCleanCommand()
{
    var registryOpt = new Option<string?>("--registry") { Description = "Limit to a specific registry host (e.g. ghcr.io)" };

    var cmd = new Command("clean", "Remove cached OCI experts from ~/.forge/experts");
    cmd.Add(registryOpt);

    cmd.SetAction(result =>
    {
        var registry = result.GetValue(registryOpt);
        var target   = registry is not null
            ? Path.Combine(ForgeCache.ExpertsRoot, registry)
            : ForgeCache.ExpertsRoot;

        if (!Directory.Exists(target))
        {
            Console.WriteLine($"Nothing to clean ({target} does not exist).");
            return;
        }

        var dirs = Directory.GetDirectories(target, "*", SearchOption.AllDirectories);
        var files = Directory.GetFiles(target, "*", SearchOption.AllDirectories);
        Directory.Delete(target, recursive: true);

        Console.WriteLine($"Removed {files.Length} cached expert file(s) from {target}");
        return;
    });

    return cmd;
}

// ---------------------------------------------------------------------------
// Helpers

static async Task<string?> TryReadFile(string path)
{
    try { return await File.ReadAllTextAsync(path); }
    catch (Exception ex) { Die($"Cannot read file '{path}': {ex.Message}"); return null; }
}

static MclProgram? TryParse(string source, string filePath)
{
    var result = MclParser.TryParse(source);
    if (!result.Success)
    {
        foreach (var d in result.Diagnostics)
            ReportDiagnostic(source, filePath, d);
        Environment.Exit(1);
        return null;
    }
    return result.Ast!;
}

static void ReportDiagnostic(string source, string filePath, Diagnostic d)
{
    var lines      = source.Split('\n');
    var sourceLine = d.Line >= 1 && d.Line <= lines.Length
        ? lines[d.Line - 1].TrimEnd('\r')
        : string.Empty;

    var lineNumStr = d.Line.ToString();
    var gutter     = new string(' ', lineNumStr.Length);
    var col        = Math.Max(0, d.Column);
    var caretLen   = d.EndColumn > d.Column ? d.EndColumn - d.Column : 1;
    var carets     = new string('^', caretLen);
    var spaces     = new string(' ', Math.Min(col, sourceLine.Length));

    var e = Console.Error;
    e.WriteLine($"{BoldRed("error")}{Bold($": {d.Message}")}");
    e.WriteLine($"  {BoldBlue("-->")} {filePath}:{d.Line}:{col + 1}");
    e.WriteLine($"{BoldBlue($"{gutter}   |")}");
    e.WriteLine($"{BoldBlue($"{lineNumStr}   |")} {sourceLine}");
    e.WriteLine($"{BoldBlue($"{gutter}   |")} {spaces}{BoldRed(carets)}");
    e.WriteLine($"{BoldBlue($"{gutter}   |")}");
}

static void ReportExpertDiagnostic(ExpertLoadException ex)
{
    // No file path — fall back to plain Die-style output
    if (ex.FilePath is null || ex.Line == 0) { Die(ex.Message); return; }

    var source     = File.Exists(ex.FilePath) ? File.ReadAllText(ex.FilePath) : string.Empty;
    var lines      = source.Split('\n');
    var sourceLine = ex.Line >= 1 && ex.Line <= lines.Length
        ? lines[ex.Line - 1].TrimEnd('\r')
        : string.Empty;

    var lineNumStr = ex.Line.ToString();
    var gutter     = new string(' ', lineNumStr.Length);
    var col        = Math.Max(0, ex.Column);
    var caretLen   = ex.EndColumn > ex.Column ? ex.EndColumn - ex.Column : 1;
    var carets     = new string('^', caretLen);
    var spaces     = new string(' ', Math.Min(col, sourceLine.Length));

    var e = Console.Error;
    e.WriteLine($"{BoldRed("error")}{Bold($": {ex.Message}")}");
    e.WriteLine($"  {BoldBlue("-->")} {ex.FilePath}:{ex.Line}:{col + 1}");
    e.WriteLine($"{BoldBlue($"{gutter}   |")}");
    e.WriteLine($"{BoldBlue($"{lineNumStr}   |")} {sourceLine}");
    e.WriteLine($"{BoldBlue($"{gutter}   |")} {spaces}{BoldRed(carets)}");
    e.WriteLine($"{BoldBlue($"{gutter}   |")}");
}

// ANSI helpers — bold+color when stderr is a terminal and NO_COLOR is unset
static bool UseAnsi() =>
    !Console.IsErrorRedirected &&
    Environment.GetEnvironmentVariable("NO_COLOR") is null;

static string Bold(string s)       => UseAnsi() ? $"\x1b[1m{s}\x1b[0m"    : s;
static string BoldRed(string s)    => UseAnsi() ? $"\x1b[1;31m{s}\x1b[0m" : s;
static string BoldYellow(string s) => UseAnsi() ? $"\x1b[1;33m{s}\x1b[0m" : s;
static string BoldBlue(string s)   => UseAnsi() ? $"\x1b[1;34m{s}\x1b[0m" : s;

static Dictionary<string, ExpertDefinition>? TryLoadExperts(string expertsDir)
{
    if (!Directory.Exists(expertsDir)) { Die($"Experts directory not found: {expertsDir}"); return null; }
    try { return new ExpertLoader(expertsDir).LoadAll(); }
    catch (AggregateExpertLoadException ex) { foreach (var e in ex.Errors) ReportExpertDiagnostic(e); Environment.Exit(1); return null; }
    catch (ExpertLoadException ex) { ReportExpertDiagnostic(ex); Environment.Exit(1); return null; }
}

static bool TryValidate(MclProgram ast, Dictionary<string, ExpertDefinition> experts,
    bool contractErrorsAreFatal = false)
{
    try
    {
        ExpertLoader.Validate(ast, experts, Console.Error, contractErrorsAreFatal);
        return true;
    }
    catch (AggregateExpertLoadException ex) { foreach (var e in ex.Errors) ReportExpertDiagnostic(e); return false; }
    catch (ExpertLoadException ex)          { ReportExpertDiagnostic(ex); return false; }
}

// Builds the full runner dictionary from forge.toml profiles.
// Falls back to let-binding context for the "default" runner if no forge.toml.
static IReadOnlyDictionary<string, IExpertRunner>? BuildRunners(
    ForgeManifest? manifest,
    Dictionary<string, object> seedContext)
{
    var runners = new Dictionary<string, IExpertRunner>(StringComparer.Ordinal);

    // Build a runner per declared profile.
    if (manifest?.Providers is { Count: > 0 } profiles)
    {
        foreach (var (name, profile) in profiles)
        {
            try { runners[name] = ProviderClientBuilder.Build(profile); }
            catch (Exception ex) { Die($"Cannot initialise provider profile '{name}': {ex.Message}"); return null; }
        }
    }

    // If no "default" profile came from forge.toml, fall back to let-binding context.
    if (!runners.ContainsKey("default"))
    {
        var defaultProfile = manifest?.Providers.GetValueOrDefault("default");
        var apiKey   = GetContextString(seedContext, "apiKey")   ?? defaultProfile?.ApiKey;
        var model    = GetContextString(seedContext, "model")    ?? defaultProfile?.Model;
        var provider = GetContextString(seedContext, "provider") ?? defaultProfile?.Provider ?? "openai";
        var endpoint = GetContextString(seedContext, "endpoint") ?? defaultProfile?.Endpoint ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            Die("No API key found. Add [providers.default] to forge.toml with apiKey = env(\"MCL_API_KEY\").");
            return null;
        }
        if (string.IsNullOrWhiteSpace(model))
        {
            Die("No model found. Add [providers.default] to forge.toml with model = env(\"MCL_MODEL\", \"gpt-4o-mini\").");
            return null;
        }

        try
        {
            runners["default"] = ProviderClientBuilder.Build(new ProviderProfile
            {
                Provider = provider,
                Model    = model,
                ApiKey   = apiKey,
                Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint
            });
        }
        catch (Exception ex) { Die($"Cannot initialise default provider: {ex.Message}"); return null; }
    }

    return runners;
}

// The default provider profile, resolved the same way BuildRunners falls back:
// forge.toml [providers.default] first, then the mission's let-binding context.
static ProviderProfile? ResolveDefaultProfile(ForgeManifest? manifest, Dictionary<string, object> seedContext)
{
    var profile  = manifest?.Providers.GetValueOrDefault("default");
    var apiKey   = GetContextString(seedContext, "apiKey")   ?? profile?.ApiKey;
    var model    = GetContextString(seedContext, "model")    ?? profile?.Model;
    var provider = GetContextString(seedContext, "provider") ?? profile?.Provider ?? "openai";
    var endpoint = GetContextString(seedContext, "endpoint") ?? profile?.Endpoint ?? string.Empty;

    if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model)) return null;

    return new ProviderProfile
    {
        Provider = provider,
        Model    = model,
        ApiKey   = apiKey,
        Endpoint = string.IsNullOrWhiteSpace(endpoint) ? null : endpoint
    };
}

static Dictionary<string, string>? ParseVars(string[] vars)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var v in vars)
    {
        var idx = v.IndexOf('=');
        if (idx <= 0) { Die($"Invalid --var value '{v}': expected key=value format."); return null; }
        result[v[..idx]] = v[(idx + 1)..];
    }
    return result;
}

static string ExpertTemplate(string name) => $"""
    ---
    name: {name}
    version: 0.1.0
    description: [One-line description of what this expert does]
    input: [Input description]
    output: [Output description]
    ---

    You are a [role description].

    Your job is to:
    1. [Step one]
    2. [Step two]
    3. [Step three]

    Produce [output description].
    """;

static string? GetContextString(Dictionary<string, object> ctx, string key)
    => ctx.TryGetValue(key, out var v) && v is string s && s.Length > 0 ? s : null;

static FileInfo ResolveMission(FileInfo? arg)
    => new FileInfo(Path.GetFullPath(arg?.FullName ?? "mission.mcl"));

static void Die(string message)
{
    Console.Error.WriteLine($"{BoldRed("error")}{Bold($": {message}")}");
    Environment.Exit(1);
}

// ---------------------------------------------------------------------------
// forge agent

static Command BuildAgentCommand()
{
    var agentCmd = new Command("agent", "Manage forge agents running in Docker");
    agentCmd.Add(BuildAgentStartCommand());
    agentCmd.Add(BuildAgentStopCommand());
    return agentCmd;
}

static Command BuildAgentStartCommand()
{
    var agentFileOpt = new Option<string?>("--agent-file") { Description = "Path to agent.yaml (default: ./agent.yaml)" };

    var cmd = new Command("start", "Start forge serve inside a Docker container");
    cmd.Add(agentFileOpt);

    cmd.SetAction(async result =>
    {
        var agentFile = result.GetValue(agentFileOpt) ?? "./agent.yaml";
        var agentFileFull = Path.GetFullPath(agentFile);

        if (!File.Exists(agentFileFull))
        {
            Die($"Agent config not found: {agentFileFull}");
            return;
        }

        AgentConfig config;
        try { config = AgentConfigLoader.Load(agentFileFull); }
        catch (Exception ex) { Die($"Cannot read agent.yaml: {ex.Message}"); return; }

        const string forgeImage = "ghcr.io/katasec/forge:latest";

        var prereqs = new[]
        {
            await DockerPrereqChecker.CheckDockerAsync(),
            DockerPrereqChecker.CheckPort(config.Port),
            DockerPrereqChecker.CheckFileExists(agentFileFull, "agent.yaml"),
        };

        if (!DockerPrereqChecker.RunAndPrint(prereqs))
        {
            Environment.Exit(1);
            return;
        }

        if (!await DockerCli.IsImagePresentAsync(forgeImage))
        {
            AnsiConsole.MarkupLine($"[yellow]Pulling {forgeImage}...[/]");
            await DockerCli.PullImageAsync(forgeImage);
        }

        await DockerCli.EnsureNetworkAsync("forge-net");

        var containerName = $"forge-agent-{config.Id}";
        if (await DockerCli.ContainerExistsAsync(containerName))
        {
            AnsiConsole.MarkupLine($"[yellow]Container {containerName} already exists. Stop it first.[/]");
            Environment.Exit(1);
            return;
        }

        // Mount the git root so relative mission paths in agent.yaml resolve correctly.
        // Fall back to the agent file's directory if not in a git repo.
        var agentDir     = Path.GetDirectoryName(agentFileFull)!;
        var workspaceRoot = FindGitRoot(agentDir) ?? agentDir;
        var agentInWorkspace = "/workspace/" + Path.GetRelativePath(workspaceRoot, agentFileFull).Replace('\\', '/');

        await DockerCli.RunContainerAsync(
            name:          containerName,
            image:         forgeImage,
            cmd:           ["serve", agentInWorkspace],
            env:           [.. BuildEnvArray("MCL_API_KEY", "MCL_MODEL", "MCL_PROVIDER", "MCL_ENDPOINT"),
                           "DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1"],
            binds:         [$"{workspaceRoot}:/workspace"],
            hostPort:      config.Port,
            containerPort: config.Port,
            network:       "forge-net");

        AnsiConsole.MarkupLine($"[green]✓[/] Agent [bold]{config.Id}[/] started");
        AnsiConsole.MarkupLine($"  Endpoint : http://localhost:{config.Port}/v1");
        AnsiConsole.MarkupLine($"  Container: {containerName}");
        AnsiConsole.MarkupLine($"  Network  : forge-net");
    });

    return cmd;
}

static Command BuildAgentStopCommand()
{
    var agentFileOpt = new Option<string?>("--agent-file") { Description = "Path to agent.yaml (default: ./agent.yaml)" };

    var cmd = new Command("stop", "Stop and remove the forge agent container");
    cmd.Add(agentFileOpt);

    cmd.SetAction(async result =>
    {
        var agentFile = result.GetValue(agentFileOpt) ?? "./agent.yaml";
        var agentFileFull = Path.GetFullPath(agentFile);

        if (!File.Exists(agentFileFull))
        {
            Die($"Agent config not found: {agentFileFull}");
            return;
        }

        AgentConfig config;
        try { config = AgentConfigLoader.Load(agentFileFull); }
        catch (Exception ex) { Die($"Cannot read agent.yaml: {ex.Message}"); return; }

        var containerName = $"forge-agent-{config.Id}";
        await DockerCli.StopAndRemoveAsync(containerName);
        AnsiConsole.MarkupLine($"[green]✓[/] Agent [bold]{config.Id}[/] stopped");
    });

    return cmd;
}

// ---------------------------------------------------------------------------
// forge webui

static Command BuildWebuiCommand()
{
    var webuiCmd = new Command("webui", "Manage Open WebUI connected to a forge agent");
    webuiCmd.Add(BuildWebuiStartCommand());
    webuiCmd.Add(BuildWebuiStopCommand());
    return webuiCmd;
}

static Command BuildWebuiStartCommand()
{
    var agentFileOpt = new Option<string?>("--agent-file") { Description = "Path to agent.yaml (default: ./agent.yaml)" };
    var portOpt = new Option<int?>("--port") { Description = "Host port for Open WebUI (default: 3000)" };

    var cmd = new Command("start", "Start Open WebUI pre-configured to connect to the forge agent");
    cmd.Add(agentFileOpt);
    cmd.Add(portOpt);

    cmd.SetAction(async result =>
    {
        var agentFile = result.GetValue(agentFileOpt) ?? "./agent.yaml";
        var webuiPort = result.GetValue(portOpt) ?? 3000;
        var agentFileFull = Path.GetFullPath(agentFile);

        if (!File.Exists(agentFileFull))
        {
            Die($"Agent config not found: {agentFileFull}");
            return;
        }

        AgentConfig config;
        try { config = AgentConfigLoader.Load(agentFileFull); }
        catch (Exception ex) { Die($"Cannot read agent.yaml: {ex.Message}"); return; }

        const string webuiImage = "ghcr.io/open-webui/open-webui:main";

        var prereqs = new[]
        {
            await DockerPrereqChecker.CheckDockerAsync(),
            DockerPrereqChecker.CheckFileExists(agentFileFull, "agent.yaml"),
        };

        if (!DockerPrereqChecker.RunAndPrint(prereqs))
        {
            Environment.Exit(1);
            return;
        }

        var agentContainerName = $"forge-agent-{config.Id}";
        var agentUrl = $"http://{agentContainerName}:{config.Port}/v1";

        if (!await DockerCli.IsContainerRunningAsync(agentContainerName))
        {
            AnsiConsole.MarkupLine($"[yellow]Agent container {agentContainerName} is not running.[/]");
            AnsiConsole.MarkupLine("[yellow]Run 'forge agent start' first.[/]");
            Environment.Exit(1);
            return;
        }

        await DockerCli.EnsureNetworkAsync("forge-net");

        if (await DockerCli.ContainerExistsAsync("open-webui"))
        {
            AnsiConsole.MarkupLine("[yellow]open-webui container already exists. Stop it first.[/]");
            Environment.Exit(1);
            return;
        }

        if (!await DockerCli.IsImagePresentAsync(webuiImage))
        {
            AnsiConsole.MarkupLine("[grey]Pulling open-webui image (first run may take a minute)...[/]");
            await DockerCli.PullImageAsync(webuiImage);
        }

        await DockerCli.RunContainerAsync(
            name:          "open-webui",
            image:         webuiImage,
            cmd:           [],
            env:           [$"OPENAI_API_BASE_URL={agentUrl}", "OPENAI_API_KEY=forge"],
            binds:         ["open-webui-data:/app/backend/data"],
            hostPort:      webuiPort,
            containerPort: 8080,
            network:       "forge-net");

        AnsiConsole.MarkupLine("[green]✓[/] Open WebUI started");
        AnsiConsole.MarkupLine($"  URL      : http://localhost:{webuiPort}");
        AnsiConsole.MarkupLine($"  Agent    : {agentUrl}");
        AnsiConsole.MarkupLine($"  Container: open-webui");
    });

    return cmd;
}

static Command BuildWebuiStopCommand()
{
    var cmd = new Command("stop", "Stop and remove the Open WebUI container");

    cmd.SetAction(async _ =>
    {
        await DockerCli.StopAndRemoveAsync("open-webui");
        AnsiConsole.MarkupLine("[green]✓[/] Open WebUI stopped");
    });

    return cmd;
}

// ---------------------------------------------------------------------------
// forge provider

static Command BuildProviderCommand()
{
    var providerCmd = new Command("provider", "Manage LLM provider profiles");
    providerCmd.Add(BuildProviderListCommand());
    providerCmd.Add(BuildProviderScaffoldCommand());
    return providerCmd;
}

static Command BuildProviderListCommand()
{
    var cmd = new Command("list", "List all known LLM providers");
    cmd.SetAction(_ =>
    {
        Console.WriteLine("Known providers:\n");
        Console.WriteLine($"  {"NAME",-12} {"REQUIRED FIELDS",-30} NOTES");
        Console.WriteLine($"  {"----",-12} {"---------------",-30} -----");
        Console.WriteLine($"  {"openai",-12} {"apiKey, model",-30} Default OpenAI endpoint");
        Console.WriteLine($"  {"anthropic",-12} {"apiKey, model",-30} Anthropic (Claude models)");
        Console.WriteLine($"  {"azure",-12} {"apiKey, model, endpoint",-30} Azure OpenAI Service");
        Console.WriteLine($"  {"ollama",-12} {"model",-30} Local Ollama (no apiKey required)");
        Console.WriteLine("\nRun 'forge provider scaffold <name>' to generate a forge.toml block.");
    });
    return cmd;
}

static Command BuildProviderScaffoldCommand()
{
    var nameArg  = new Argument<string>("name") { Description = "Provider name (openai, anthropic, azure, ollama)" };
    var writeOpt = new Option<bool>("--write") { Description = "Append the block to forge.toml instead of printing it" };

    var cmd = new Command("scaffold", "Generate a ready-to-paste forge.toml provider block");
    cmd.Add(nameArg);
    cmd.Add(writeOpt);

    cmd.SetAction(async result =>
    {
        var name  = result.GetValue(nameArg)!.ToLowerInvariant();
        var write = result.GetValue(writeOpt);

        var block = name switch
        {
            "openai" => """
                [providers.default]
                provider = "openai"
                model    = "gpt-4o-mini"         # or: gpt-4o, gpt-4-turbo
                apiKey   = env("MCL_API_KEY")     # set MCL_API_KEY before running
                # endpoint = "..."               # optional — omit for default OpenAI endpoint
                """,
            "anthropic" => """
                [providers.default]
                provider = "anthropic"
                model    = "claude-sonnet-4-6"   # or: claude-opus-4-8, claude-haiku-4-5-20251001
                apiKey   = env("ANTHROPIC_API_KEY")
                """,
            "azure" => """
                [providers.default]
                provider = "azure"
                model    = "gpt-4o"
                apiKey   = env("AZURE_OPENAI_API_KEY")
                endpoint = "https://<your-resource>.openai.azure.com/openai/deployments/<deployment>/chat/completions?api-version=2024-02-01"
                """,
            "ollama" => """
                [providers.default]
                provider = "ollama"
                model    = "llama3"              # any model pulled with 'ollama pull <name>'
                endpoint = "http://localhost:11434/v1"  # omit to use this default
                # no apiKey required for local Ollama
                """,
            _ => null
        };

        if (block is null)
        {
            Die($"Unknown provider '{name}'. Run 'forge provider list' to see known providers.");
            return;
        }

        if (write)
        {
            await File.AppendAllTextAsync("forge.toml", $"\n{block}\n");
            Console.WriteLine("Appended to forge.toml.");
        }
        else
        {
            Console.WriteLine(block);
        }
    });

    return cmd;
}

// ---------------------------------------------------------------------------
// forge mcp

static Command BuildMcpCommand()
{
    var missionArg = new Argument<FileInfo?>("mission")
    {
        Description = "Path to the .mcl mission file (default: mission.mcl)",
        Arity = ArgumentArity.ZeroOrOne
    };

    var cmd = new Command("mcp", "Expose a mission as a stdio MCP server (for Claude Desktop and MCP-aware tools)");
    cmd.Add(missionArg);

    cmd.SetAction(async result =>
    {
        var mission    = ResolveMission(result.GetValue(missionArg));
        var missionDir = mission.DirectoryName!;
        var lockPath   = Path.Combine(missionDir, "mcl.lock");

        if (!File.Exists(lockPath)) { Die("MCL007 Mission not initialised — run 'forge init' first."); return; }

        var source = await TryReadFile(mission.FullName);
        if (source is null) return;

        var ast = TryParse(source, mission.FullName);
        if (ast is null) return;

        ForgeManifest? manifest = null;
        try { manifest = ForgeTomlReader.TryRead(mission.FullName); }
        catch (ForgeTomlException ex) { Die(ex.Message); return; }

        LockFile lockFile;
        try { lockFile = LockFileIO.Read(lockPath); }
        catch (Exception ex) { Die($"Cannot read mcl.lock: {ex.Message}"); return; }

        Dictionary<string, ExpertDefinition> expertDefs;
        try { expertDefs = ExpertResolver.ResolveAll(lockFile, missionDir, verbose: null, warnings: Console.Error); }
        catch (AggregateExpertLoadException ex) { foreach (var e in ex.Errors) ReportExpertDiagnostic(e); Environment.Exit(1); return; }
        catch (ExpertLoadException ex)           { ReportExpertDiagnostic(ex); Environment.Exit(1); return; }

        if (!TryValidate(ast, expertDefs, contractErrorsAreFatal: true)) return;

        var firstMission = ast.Declarations.OfType<MissionDeclaration>().FirstOrDefault();
        if (firstMission is null) { Die("No mission declaration found in mission file."); return; }

        // Build the JSON schema for this mission's parameters
        var toolName   = firstMission.Name;
        var properties = new Dictionary<string, JsonSchemaProperty>(StringComparer.Ordinal);
        foreach (var param in firstMission.Params)
            properties[param] = new JsonSchemaProperty("string", $"Value for {param}");

        var inputSchema = new JsonSchema(properties, firstMission.Params);

        Console.Error.WriteLine($"forge mcp — serving mission '{toolName}' over stdio");
        Console.Error.WriteLine($"  mission: {mission.FullName}");

        var services = new ServiceCollection();
        services.AddMcpServer(options => { options.ServerInfo = new Implementation { Name = "forge", Version = "1.0" }; })
            .WithStdioServerTransport()
            .WithListToolsHandler((_, _) => ValueTask.FromResult(new ListToolsResult
            {
                Tools =
                [
                    new Tool
                    {
                        Name        = toolName,
                        Description = $"Run the '{toolName}' MCL mission",
                        InputSchema = inputSchema.ToJsonElement()
                    }
                ]
            }))
            .WithCallToolHandler(async (request, ct) =>
            {
                if (request.Params?.Name != toolName)
                    return new CallToolResult { Content = [new McpTextContent { Text = $"Unknown tool: {request.Params?.Name}" }], IsError = true };

                // Extract call-time arguments as string overrides
                var callVars = new Dictionary<string, string>(StringComparer.Ordinal);
                if (request.Params.Arguments is { } callArgs)
                {
                    foreach (var kv in callArgs)
                    {
                        var strVal = kv.Value.ValueKind == System.Text.Json.JsonValueKind.String
                            ? kv.Value.GetString() ?? string.Empty
                            : kv.Value.ToString();
                        callVars[kv.Key] = strVal;
                    }
                }

                // Build runners lazily at call time so startup never needs the API key
                Dictionary<string, object> callSeedContext;
                try { callSeedContext = ContextBuilder.Seed(ast, callVars); }
                catch (InvalidOperationException ex) { return new CallToolResult { Content = [new McpTextContent { Text = ex.Message }], IsError = true }; }

                var callRunners = BuildRunners(manifest, callSeedContext);
                if (callRunners is null)
                    return new CallToolResult { Content = [new McpTextContent { Text = "Cannot initialise provider — check API key env vars in claude_desktop_config.json" }], IsError = true };

                var runOptions = new PipelineRunOptions(toolName, callVars);

                MissionResult missionResult;
                try { missionResult = await new PipelineRunner(callRunners, manifest?.Execution, ProviderClientBuilder.BuildWebSearch()).RunAsync(ast, expertDefs, runOptions); }
                catch (Exception ex) { return new CallToolResult { Content = [new McpTextContent { Text = ex.Message }], IsError = true }; }

                if (missionResult.Status == MissionStatus.Fail)
                    return new CallToolResult { Content = [new McpTextContent { Text = $"Mission failed: {missionResult.FailReason}" }], IsError = true };

                return new CallToolResult { Content = [new McpTextContent { Text = missionResult.Text }] };
            });

        var sp = services.BuildServiceProvider();
        var server = sp.GetRequiredService<McpServer>();
        await server.RunAsync(CancellationToken.None);
    });

    return cmd;
}

// ---------------------------------------------------------------------------
// Docker helpers

static string? FindGitRoot(string startDir)
{
    var dir = startDir;
    while (dir is not null)
    {
        if (Directory.Exists(Path.Combine(dir, ".git"))) return dir;
        dir = Path.GetDirectoryName(dir);
    }
    return null;
}

static string[] BuildEnvArray(params string[] vars) =>
    vars
        .Select(v => (Name: v, Value: Environment.GetEnvironmentVariable(v)))
        .Where(x => x.Value is not null)
        .Select(x => $"{x.Name}={x.Value}")
        .ToArray();

// ---------------------------------------------------------------------------
// MCP helpers — must be after all top-level statements

// Minimal JSON schema helpers for MCP tool registration (AOT-safe, no STJ reflection)
file sealed class JsonSchemaProperty(string type, string description)
{
    public string Type        { get; } = type;
    public string Description { get; } = description;
}

file sealed class JsonSchema(Dictionary<string, JsonSchemaProperty> properties, IReadOnlyList<string> required)
{
    public System.Text.Json.JsonElement ToJsonElement()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"type\":\"object\",\"properties\":{");
        var first = true;
        foreach (var (name, prop) in properties)
        {
            if (!first) sb.Append(',');
            sb.Append($"\"{EscapeJson(name)}\":{{\"type\":\"{EscapeJson(prop.Type)}\",\"description\":\"{EscapeJson(prop.Description)}\"}}");
            first = false;
        }
        sb.Append("},\"required\":[");
        for (var i = 0; i < required.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($"\"{EscapeJson(required[i])}\"");
        }
        sb.Append("]}");
        return System.Text.Json.JsonDocument.Parse(sb.ToString()).RootElement;
    }

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}

