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
using Katasec.AnthropicServer;
using Spectre.Console;
using ForgeMission.Cli.Docker;
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
rootCommand.Add(BuildLoginCommand());
rootCommand.Add(BuildPublishCommand());
rootCommand.Add(BuildCleanCommand());
rootCommand.Add(BuildServeCommand());
rootCommand.Add(BuildAgentCommand());
rootCommand.Add(BuildWebuiCommand());
rootCommand.Add(BuildProviderCommand());
rootCommand.Add(BuildMcpCommand());

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
            Console.Error.WriteLine($"Warning: no credential for {registry} (set FORGE_REGISTRY_TOKEN or 'forge login').");

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

        var agentDir    = agentFile.DirectoryName!;
        var missionPath = Path.GetFullPath(Path.Combine(agentDir, config.Mission));
        var lockPath    = Path.Combine(Path.GetDirectoryName(missionPath)!, "mcl.lock");

        if (!File.Exists(missionPath)) { Die($"Mission file not found: {missionPath}"); return; }
        if (!File.Exists(lockPath))    { Die("MCL007 Mission not initialised — run 'forge init' first."); return; }

        var source = await TryReadFile(missionPath);
        if (source is null) return;

        var ast = TryParse(source, missionPath);
        if (ast is null) return;

        // OCI expert validation moved to forge.toml (Spoke 2).

        LockFile lockFile;
        try { lockFile = LockFileIO.Read(lockPath); }
        catch (Exception ex) { Die($"Cannot read mcl.lock: {ex.Message}"); return; }

        Dictionary<string, ExpertDefinition> expertDefs;
        try { expertDefs = ExpertLoader.LoadFromLockFile(lockFile, Path.GetDirectoryName(missionPath)!); }
        catch (AggregateExpertLoadException ex) { foreach (var e in ex.Errors) ReportExpertDiagnostic(e); Environment.Exit(1); return; }
        catch (ExpertLoadException ex)           { ReportExpertDiagnostic(ex); Environment.Exit(1); return; }

        if (!TryValidate(ast, expertDefs)) return;

        Dictionary<string, object> seedContext;
        try { seedContext = ContextBuilder.Seed(ast, new Dictionary<string, string>()); }
        catch (InvalidOperationException ex) { Die(ex.Message); return; }

        ForgeManifest? serveManifest = null;
        try { serveManifest = ForgeTomlReader.TryRead(missionPath); }
        catch (ForgeTomlException ex) { Die(ex.Message); return; }

        var serveRunners = BuildRunners(serveManifest, seedContext);
        if (serveRunners is null) return;

        var defaultRunner = serveRunners.TryGetValue("default", out var dr)
            ? dr
            : serveRunners.Values.First();

        // Wire selector (42.1): the Anthropic wire hands the mission the FULL conversation
        // (context["conversation"]/["system"], goal = last text block of the last user message);
        // the OpenAI wire keeps the legacy last-turn behaviour so existing users don't regress.
        var anthropicWire = string.Equals(config.Wire, "anthropic", StringComparison.OrdinalIgnoreCase);
        var missionClient = new MissionChatClient(ast, expertDefs, defaultRunner, fullConversation: anthropicWire);
        var app           = anthropicWire
            ? AnthropicServer.Build(missionClient, config.Id, config.Port)
            : OaiServer.Build(missionClient, config.Id, config.Port);

        Console.Error.WriteLine($"forge serve — agent '{config.Id}' listening on http://0.0.0.0:{config.Port}");
        Console.Error.WriteLine($"  mission  : {missionPath}");
        Console.Error.WriteLine($"  wire     : {(anthropicWire ? "anthropic" : "openai")}");
        if (anthropicWire)
        {
            Console.Error.WriteLine($"  endpoints: POST /v1/messages  (messages API, streaming + non-streaming)");
        }
        else
        {
            Console.Error.WriteLine($"  endpoints: POST /v1/chat/completions  (chat, streaming)");
            Console.Error.WriteLine($"             POST /v1/responses          (responses API, streaming)");
            Console.Error.WriteLine($"             GET  /v1/models");
        }

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

