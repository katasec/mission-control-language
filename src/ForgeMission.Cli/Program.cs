using System.CommandLine;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Parser;
using ForgeMission.Core.Resolution;
using ForgeMission.Core.Runtime;
using static ForgeMission.Core.Runtime.MissionStatus;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;
using Katasec.OciClient;
using Katasec.OaiServer;
using MclProgram = ForgeMission.Core.Parser.Program;

var rootCommand = new RootCommand("forge — Mission Control Language runtime");
rootCommand.Add(BuildInitCommand());
rootCommand.Add(BuildRunCommand());
rootCommand.Add(BuildValidateCommand());
rootCommand.Add(BuildListCommand());
rootCommand.Add(BuildExpertCommand());
rootCommand.Add(BuildLoginCommand());
rootCommand.Add(BuildCleanCommand());
rootCommand.Add(BuildServeCommand());

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

        Console.WriteLine("Resolving experts...\n");

        // --- OCI experts: pull to ~/.forge/experts/<registry>/<name>/<version>/expert.md
        var ociDecls = ast.Declarations.OfType<ExpertDeclaration>()
            .Where(e => e.Source is not null)
            .ToList();

        var lockFile = new LockFile();

        foreach (var decl in ociDecls)
        {
            var src      = decl.Source!;
            var slash    = src.Registry.IndexOf('/');
            var registry = slash >= 0 ? src.Registry[..slash] : src.Registry;
            var ociName  = slash >= 0 ? src.Registry[(slash + 1)..] : src.Registry;
            var cachePath = ForgeCache.ExpertMdPath(registry, ociName, src.Version);

            if (File.Exists(cachePath) && !refresh)
            {
                Console.WriteLine($"  ✓ {decl.Name}  (cached)");
            }
            else
            {
                Console.Write($"  ↓ {decl.Name}  ({src.Registry}:{src.Version}) ... ");
                try
                {
                    var token = CredentialStore.GetToken(registry);
                    using var client = new OciClient(token);
                    var content = await client.PullExpertAsync(registry, ociName, src.Version);
                    Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                    await File.WriteAllTextAsync(cachePath, content);
                    Console.WriteLine("done");
                }
                catch (OciAuthException ex)
                {
                    Console.WriteLine();
                    Die($"MCL011 Authentication failed pulling {decl.Name}: {ex.Message}\n\nRun: forge login {registry} --token <token>");
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Die($"MCL011 Failed to pull {decl.Name} from {src.Registry}:{src.Version}: {ex.Message}");
                    return;
                }
            }

            // Store path as ~/.forge/... so the lock file is portable across machines
            var home         = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var portablePath = "~" + cachePath[home.Length..].Replace(Path.DirectorySeparatorChar, '/');
            lockFile.Experts[decl.Name] = new LockFileExpert
            {
                Source = $"{src.Registry}:{src.Version}",
                Path   = portablePath
            };
        }

        // --- Local experts: discover from ./experts
        var localCatalog = new Dictionary<string, ResolvedExpert>(StringComparer.Ordinal);
        var localExpertsDir = Path.Combine(missionDir, SourceResolver.DefaultExpertsDir);
        if (Directory.Exists(localExpertsDir))
        {
            try { localCatalog = new SourceResolver().Resolve(missionDir); }
            catch (MclException ex) { Die(ex.Message); return; }
        }

        foreach (var (name, expert) in localCatalog.OrderBy(k => k.Key))
        {
            if (lockFile.Experts.ContainsKey(name)) continue; // OCI takes precedence if same name
            var relativePath = Path.GetRelativePath(missionDir, expert.ExpertMdPath);
            lockFile.Experts[name] = new LockFileExpert { Source = expert.Source, Path = relativePath };
        }

        var totalCount = lockFile.Experts.Count;
        Console.WriteLine($"\n  ✓ experts  ({totalCount} found)");
        Console.WriteLine("\nResolved:");
        foreach (var (name, entry) in lockFile.Experts.OrderBy(k => k.Key))
            Console.WriteLine($"  {name,-30} {entry.Source}");

        var lockPath = Path.Combine(missionDir, "mcl.lock");
        LockFileIO.Write(lockPath, lockFile);

        Console.WriteLine($"\nGenerated {lockPath}");
    });

    return cmd;
}

// ---------------------------------------------------------------------------
// fms run

static Command BuildRunCommand()
{
    var missionArg = new Argument<FileInfo?>("mission") { Description = "Path to the .mcl mission file (default: mission.mcl)", Arity = ArgumentArity.ZeroOrOne };
    var stepsOpt   = new Option<bool>("--steps") { Description = "Stream each expert's output to stderr as the pipeline runs" };
    var varOpt     = new Option<string[]>("--var")
    {
        Description = "Set a context variable as key=value (repeatable, overrides let bindings)",
        AllowMultipleArgumentsPerToken = false
    };
    varOpt.Arity = ArgumentArity.ZeroOrMore;

    var cmd = new Command("run", "Run a mission");
    cmd.Add(missionArg);
    cmd.Add(stepsOpt);
    cmd.Add(varOpt);

    cmd.SetAction(async result =>
    {
        var mission    = ResolveMission(result.GetValue(missionArg));
        var showSteps  = result.GetValue(stepsOpt);
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

        // Pass 2: assert OCI experts exist in ~/.forge/experts cache
        foreach (var decl in ast.Declarations.OfType<ExpertDeclaration>().Where(e => e.Source is not null))
        {
            var src      = decl.Source!;
            var slash    = src.Registry.IndexOf('/');
            var registry = slash >= 0 ? src.Registry[..slash] : src.Registry;
            var ociName  = slash >= 0 ? src.Registry[(slash + 1)..] : src.Registry;
            var cachePath = ForgeCache.ExpertMdPath(registry, ociName, src.Version);
            if (!File.Exists(cachePath))
            {
                Die($"MCL010 Expert '{decl.Name}' not resolved — run 'forge init' to pull remote experts");
                return;
            }
        }

        LockFile lockFile;
        try { lockFile = LockFileIO.Read(lockPath); }
        catch (Exception ex) { Die($"Cannot read mcl.lock: {ex.Message}"); return; }

        Dictionary<string, ExpertDefinition> expertDefs;
        try { expertDefs = ExpertLoader.LoadFromLockFile(lockFile, missionDir); }
        catch (ExpertLoadException ex) { Die(ex.Message); return; }

        if (!TryValidate(ast, expertDefs)) return;

        Dictionary<string, object> seedContext;
        try { seedContext = ContextBuilder.Seed(ast, parsedVars); }
        catch (InvalidOperationException ex) { Die(ex.Message); return; }

        if (!seedContext.TryGetValue("apiKey", out var apiKeyObj) || string.IsNullOrWhiteSpace(apiKeyObj?.ToString()))
        {
            Die("Mission must declare an API key: let apiKey = env(\"MCL_API_KEY\")");
            return;
        }
        if (!seedContext.TryGetValue("model", out var modelObj) || string.IsNullOrWhiteSpace(modelObj?.ToString()))
        {
            Die("Mission must declare a model: let model = env(\"MCL_MODEL\", \"gpt-4o-mini\")");
            return;
        }

        var provider = seedContext.TryGetValue("provider", out var providerObj)
            ? providerObj.ToString()!
            : "openai";
        var endpoint = seedContext.TryGetValue("endpoint", out var endpointObj)
            ? endpointObj.ToString()!
            : string.Empty;

        var runner = TryBuildRunner(provider, apiKeyObj.ToString()!, modelObj.ToString()!, endpoint);
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

        // Pass 2: assert OCI experts are cached
        foreach (var decl in ast.Declarations.OfType<ExpertDeclaration>().Where(e => e.Source is not null))
        {
            var src      = decl.Source!;
            var slash    = src.Registry.IndexOf('/');
            var registry = slash >= 0 ? src.Registry[..slash] : src.Registry;
            var ociName  = slash >= 0 ? src.Registry[(slash + 1)..] : src.Registry;
            if (!File.Exists(ForgeCache.ExpertMdPath(registry, ociName, src.Version)))
            {
                Die($"MCL010 Expert '{decl.Name}' not resolved — run 'forge init' first.");
                return;
            }
        }

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

        if (!seedContext.TryGetValue("apiKey", out var apiKeyObj) || string.IsNullOrWhiteSpace(apiKeyObj?.ToString()))
        {
            Die("Mission must declare an API key: let apiKey = env(\"MCL_API_KEY\")");
            return;
        }
        if (!seedContext.TryGetValue("model", out var modelObj) || string.IsNullOrWhiteSpace(modelObj?.ToString()))
        {
            Die("Mission must declare a model: let model = env(\"MCL_MODEL\", \"gpt-4o-mini\")");
            return;
        }

        var provider = seedContext.TryGetValue("provider", out var pObj) ? pObj.ToString()! : "openai";
        var endpoint = seedContext.TryGetValue("endpoint", out var eObj) ? eObj.ToString()! : string.Empty;

        var runner = TryBuildRunner(provider, apiKeyObj.ToString()!, modelObj.ToString()!, endpoint);
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

static FileInfo ResolveMission(FileInfo? arg)
    => new FileInfo(Path.GetFullPath(arg?.FullName ?? "mission.mcl"));

static void Die(string message)
{
    Console.Error.WriteLine($"error: {message}");
    Environment.Exit(1);
}
