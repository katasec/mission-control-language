using System.CommandLine;
using ForgeMission.Core.Adapters;
using ForgeMission.Core.Experts;
using ForgeMission.Core.Parser;
using ForgeMission.Core.Resolution;
using ForgeMission.Core.Runtime;
using static ForgeMission.Core.Runtime.MissionStatus;
using Microsoft.Extensions.AI;
using OpenAI;
using MclProgram = ForgeMission.Core.Parser.Program;

var rootCommand = new RootCommand("mcl — Mission Control Language runtime");
rootCommand.Add(BuildInitCommand());
rootCommand.Add(BuildRunCommand());
rootCommand.Add(BuildValidateCommand());
rootCommand.Add(BuildListCommand());
rootCommand.Add(BuildExpertCommand());

return await rootCommand.Parse(args).InvokeAsync();

// ---------------------------------------------------------------------------
// fms init

static Command BuildInitCommand()
{
    var missionArg = new Argument<FileInfo?>("mission") { Description = "Path to the .mcl mission file (default: mission.mcl)", Arity = ArgumentArity.ZeroOrOne };

    var cmd = new Command("init", "Resolve expert sources and generate mcl.lock");
    cmd.Add(missionArg);

    cmd.SetAction(async result =>
    {
        var mission = ResolveMission(result.GetValue(missionArg));
        var missionDir = mission.DirectoryName!;

        var source = await TryReadFile(mission.FullName);
        if (source is null) return;

        var ast = TryParse(source);
        if (ast is null) return;

        if (ast.Uses.Count == 0)
        {
            Console.WriteLine("No 'use' declarations found — adding default: use \"./experts\"");
            // Treat as if use "./experts" was declared
            ast = ast with { Uses = [new UseDeclaration("./experts")] };
        }

        Console.WriteLine("Resolving sources...\n");

        Dictionary<string, ResolvedExpert> catalog;
        try
        {
            catalog = new SourceResolver().Resolve(ast.Uses, missionDir);
        }
        catch (MclException ex)
        {
            Die(ex.Message);
            return;
        }

        foreach (var use in ast.Uses)
            Console.WriteLine($"  ✓ {use.Source}  ({catalog.Values.Count(e => e.Source == use.Source)} experts)");

        Console.WriteLine("\nResolved:");
        foreach (var (name, expert) in catalog.OrderBy(k => k.Key))
            Console.WriteLine($"  {name,-30} {expert.Source}");

        var lockFile = LockFileIO.Build(ast.Uses.Select(u => u.Source).ToList(), catalog, missionDir);
        var lockPath = Path.Combine(missionDir, "mcl.lock");
        LockFileIO.Write(lockPath, lockFile);

        Console.WriteLine($"\nGenerated {lockPath}");

        await Task.CompletedTask;
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
            Die("MCL007 Mission not initialised — run 'mcl init' first.");
            return;
        }

        var parsedVars = ParseVars(vars);
        if (parsedVars is null) return;

        var source = await TryReadFile(mission.FullName);
        if (source is null) return;

        var ast = TryParse(source);
        if (ast is null) return;

        LockFile lockFile;
        try { lockFile = LockFileIO.Read(lockPath); }
        catch (Exception ex) { Die($"Cannot read mcl.lock: {ex.Message}"); return; }

        Dictionary<string, ExpertDefinition> expertDefs;
        try { expertDefs = ExpertLoader.LoadFromLockFile(lockFile, missionDir); }
        catch (ExpertLoadException ex) { Die(ex.Message); return; }

        if (!TryValidate(ast, expertDefs)) return;

        var runner = TryBuildRunner();
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
            Console.Error.WriteLine("warning: MCL006 mcl.lock not found — run 'mcl init' to generate it");
        }
        else
        {
            // Check sources in lock file still match use declarations
            var lockFile = LockFileIO.Read(lockPath);
            var currentSources = ast.Uses.Select(u => u.Source).ToHashSet(StringComparer.Ordinal);
            var lockedSources  = lockFile.Sources.ToHashSet(StringComparer.Ordinal);
            if (!currentSources.SetEquals(lockedSources))
                Console.Error.WriteLine("warning: MCL006 mcl.lock is stale — run 'mcl init' to update it");
        }

        if (!File.Exists(lockPath))
        {
            // Fall back to directory scan when no lock file
            var expertsDir = Path.Combine(missionDir, "experts");
            var expertDefs = TryLoadExperts(expertsDir);
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

static IExpertRunner? TryBuildRunner()
{
    var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    if (string.IsNullOrWhiteSpace(apiKey)) { Die("OPENAI_API_KEY environment variable is not set."); return null; }
    var chatClient = new OpenAIClient(apiKey).GetChatClient("gpt-4o-mini").AsIChatClient();
    return new MafExpertRunner(chatClient);
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
