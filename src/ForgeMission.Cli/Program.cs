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
using Katasec.OaiServer;
using Spectre.Console;
using ForgeMission.Cli.Docker;
using MclProgram = ForgeMission.Parser.Program;

var rootCommand = new RootCommand("forge — Mission Control Language runtime");
rootCommand.Add(BuildInitCommand());
rootCommand.Add(BuildRunCommand());
rootCommand.Add(BuildValidateCommand());
rootCommand.Add(BuildListCommand());
rootCommand.Add(BuildExpertCommand());
rootCommand.Add(BuildLoginCommand());
rootCommand.Add(BuildCleanCommand());
rootCommand.Add(BuildServeCommand());
rootCommand.Add(BuildAgentCommand());
rootCommand.Add(BuildWebuiCommand());

return await rootCommand.Parse(args).InvokeAsync();

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

        var ast = TryParse(source);
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
                    Console.Error.WriteLine("    Run 'forge login <registry> --token <PAT>' if this is an auth error.");
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

        var ast = TryParse(source);
        if (ast is null) return;

        ForgeManifest? manifest = null;
        try { manifest = ForgeTomlReader.TryRead(mission.FullName); }
        catch (ForgeTomlException ex) { Die(ex.Message); return; }

        LockFile lockFile;
        try { lockFile = LockFileIO.Read(lockPath); }
        catch (Exception ex) { Die($"Cannot read mcl.lock: {ex.Message}"); return; }

        Dictionary<string, ExpertDefinition> expertDefs;
        try { expertDefs = ExpertResolver.ResolveAll(lockFile, missionDir, verbose ? Console.Error : null); }
        catch (ExpertLoadException ex) { Die(ex.Message); return; }

        if (!TryValidate(ast, expertDefs)) return;

        Dictionary<string, object> seedContext;
        try { seedContext = ContextBuilder.Seed(ast, parsedVars); }
        catch (InvalidOperationException ex) { Die(ex.Message); return; }

        // Resolve provider config: let bindings first, forge.toml [providers.default] as fallback
        var defaultProfile = manifest?.Providers.GetValueOrDefault("default");

        var apiKeyStr  = GetContextString(seedContext, "apiKey")  ?? defaultProfile?.ApiKey;
        var modelStr   = GetContextString(seedContext, "model")   ?? defaultProfile?.Model;
        var providerStr = GetContextString(seedContext, "provider") ?? defaultProfile?.Provider ?? "openai";
        var endpointStr = GetContextString(seedContext, "endpoint") ?? defaultProfile?.Endpoint ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKeyStr))
        {
            Die("No API key found. Set 'let apiKey = env(\"MCL_API_KEY\")' in the mission or add [providers.default] to forge.toml.");
            return;
        }
        if (string.IsNullOrWhiteSpace(modelStr))
        {
            Die("No model found. Set 'let model = env(\"MCL_MODEL\", \"gpt-4o-mini\")' in the mission or add [providers.default] to forge.toml.");
            return;
        }

        var apiKeyObj   = (object)apiKeyStr;
        var provider    = providerStr;
        var endpoint    = endpointStr;

        var runner = TryBuildRunner(provider, apiKeyStr, modelStr, endpoint);
        if (runner is null) return;

        var firstMission = ast.Declarations.OfType<MissionDeclaration>().FirstOrDefault();
        if (firstMission is null) { Die("No mission declaration found in mission file."); return; }

        var options = new PipelineRunOptions(
            firstMission.Name,
            parsedVars,
            showSteps ? Console.Error : null);

        Console.Error.WriteLine($"Running mission '{firstMission.Name}'...");

        var missionResult = await new PipelineRunner(runner).RunAsync(ast, expertDefs, options);

        if (missionResult.Status == MissionStatus.Fail)
        {
            Console.Error.WriteLine($"error: mission failed — {missionResult.FailReason}");
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

        var ast = TryParse(source);
        if (ast is null) return;

        // Validate forge.toml if present
        try { ForgeTomlReader.TryRead(mission.FullName); }
        catch (ForgeTomlException ex) { Die(ex.Message); return; }

        // Warn if lock file is absent or stale
        if (!File.Exists(lockPath))
        {
            Console.Error.WriteLine("warning: MCL006 mcl.lock not found — run 'forge init' to generate it");
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
            var expertDefs = ExpertLoader.LoadFromLockFile(lockFile, missionDir);
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
// forge login

static Command BuildLoginCommand()
{
    var registryArg = new Argument<string>("registry") { Description = "Registry host (e.g. ghcr.io)" };
    var tokenOpt    = new Option<string>("--token") { Description = "Credential token (e.g. GitHub PAT)" };

    var cmd = new Command("login", "Save registry credentials to ~/.forge/credentials.json");
    cmd.Add(registryArg);
    cmd.Add(tokenOpt);

    cmd.SetAction(async result =>
    {
        var registry = result.GetValue(registryArg)!;
        var token    = result.GetValue(tokenOpt)!;

        CredentialStore.SaveToken(registry, token);
        Console.WriteLine($"Credentials saved for {registry}");

        await Task.CompletedTask;
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

        var agentDir    = agentFile.DirectoryName!;
        var missionPath = Path.GetFullPath(Path.Combine(agentDir, config.Mission));
        var lockPath    = Path.Combine(Path.GetDirectoryName(missionPath)!, "mcl.lock");

        if (!File.Exists(missionPath)) { Die($"Mission file not found: {missionPath}"); return; }
        if (!File.Exists(lockPath))    { Die("MCL007 Mission not initialised — run 'forge init' first."); return; }

        var source = await TryReadFile(missionPath);
        if (source is null) return;

        var ast = TryParse(source);
        if (ast is null) return;

        // OCI expert validation moved to forge.toml (Spoke 2).

        LockFile lockFile;
        try { lockFile = LockFileIO.Read(lockPath); }
        catch (Exception ex) { Die($"Cannot read mcl.lock: {ex.Message}"); return; }

        Dictionary<string, ExpertDefinition> expertDefs;
        try { expertDefs = ExpertLoader.LoadFromLockFile(lockFile, Path.GetDirectoryName(missionPath)!); }
        catch (ExpertLoadException ex) { Die(ex.Message); return; }

        if (!TryValidate(ast, expertDefs)) return;

        Dictionary<string, object> seedContext;
        try { seedContext = ContextBuilder.Seed(ast, new Dictionary<string, string>()); }
        catch (InvalidOperationException ex) { Die(ex.Message); return; }

        ForgeManifest? serveManifest = null;
        try { serveManifest = ForgeTomlReader.TryRead(missionPath); }
        catch (ForgeTomlException ex) { Die(ex.Message); return; }

        var serveProfile = serveManifest?.Providers.GetValueOrDefault("default");

        var serveApiKey  = GetContextString(seedContext, "apiKey")  ?? serveProfile?.ApiKey;
        var serveModel   = GetContextString(seedContext, "model")   ?? serveProfile?.Model;
        var provider     = GetContextString(seedContext, "provider") ?? serveProfile?.Provider ?? "openai";
        var endpoint     = GetContextString(seedContext, "endpoint") ?? serveProfile?.Endpoint ?? string.Empty;

        if (string.IsNullOrWhiteSpace(serveApiKey))
        {
            Die("No API key found. Set 'let apiKey = env(\"MCL_API_KEY\")' in the mission or add [providers.default] to forge.toml.");
            return;
        }
        if (string.IsNullOrWhiteSpace(serveModel))
        {
            Die("No model found. Set 'let model = env(\"MCL_MODEL\", \"gpt-4o-mini\")' in the mission or add [providers.default] to forge.toml.");
            return;
        }

        var apiKeyObj = (object)serveApiKey;
        var modelObj  = (object)serveModel;

        var runner = TryBuildRunner(provider, serveApiKey, serveModel, endpoint);
        if (runner is null) return;

        var missionClient = new MissionChatClient(ast, expertDefs, runner);
        var app           = OaiServer.Build(missionClient, config.Id, config.Port);

        Console.Error.WriteLine($"forge serve — agent '{config.Id}' listening on http://0.0.0.0:{config.Port}");
        Console.Error.WriteLine($"  mission : {missionPath}");
        Console.Error.WriteLine($"  endpoint: POST /v1/chat/completions");

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

static MclProgram? TryParse(string source)
{
    try { return MclParser.Parse(source); }
    catch (ParseException ex) { Die(ex.Message); return null; }
}

static Dictionary<string, ExpertDefinition>? TryLoadExperts(string expertsDir)
{
    if (!Directory.Exists(expertsDir)) { Die($"Experts directory not found: {expertsDir}"); return null; }
    try { return new ExpertLoader(expertsDir).LoadAll(); }
    catch (ExpertLoadException ex) { Die(ex.Message); return null; }
}

static bool TryValidate(MclProgram ast, Dictionary<string, ExpertDefinition> experts)
{
    try { ExpertLoader.Validate(ast, experts); return true; }
    catch (ExpertLoadException ex) { Die(ex.Message); return false; }
}

static IExpertRunner? TryBuildRunner(string provider, string apiKey, string model, string endpoint)
{
    if (!provider.Equals("openai", StringComparison.OrdinalIgnoreCase))
    {
        Die($"Unknown provider '{provider}'. Supported: openai");
        return null;
    }

    var options = new OpenAIClientOptions();
    if (!string.IsNullOrWhiteSpace(endpoint))
        options.Endpoint = new Uri(endpoint);

    var chatClient = new OpenAIClient(new ApiKeyCredential(apiKey), options).GetChatClient(model).AsIChatClient();
    return new DirectExpertRunner(chatClient);
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
    Console.Error.WriteLine($"error: {message}");
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

