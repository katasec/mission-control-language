using System.Reflection;

namespace ForgeMission.Tests.Cli;

public sealed class BuiltinMissionsTests
{
    [Fact]
    public void Summarize_is_registered_as_baked_in_builtin()
    {
        var cli = LoadCliAssembly();
        var builtins = cli.GetType("ForgeMission.Cli.BuiltinMissions", throwOnError: true)!;
        var builtin = cli.GetType("ForgeMission.Cli.BuiltinMission", throwOnError: true)!;
        var all = (IEnumerable<object>)builtins.GetField("All", BindingFlags.Static | BindingFlags.Public)!.GetValue(null)!;

        var summarize = Assert.Single(all, b => (string)builtin.GetProperty("Label")!.GetValue(b)! == "Summarize");

        Assert.Null(builtin.GetProperty("OciRef")!.GetValue(summarize));
        Assert.Equal("summarize", builtin.GetProperty("LocalDir")!.GetValue(summarize));
    }

    private static Assembly LoadCliAssembly()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "ForgeMission.Cli",
                "bin",
                "Debug",
                "net10.0",
                "forge.dll");
            if (File.Exists(candidate))
                return Assembly.LoadFrom(candidate);

            dir = dir.Parent;
        }

        throw new FileNotFoundException("Could not locate built forge.dll for CLI reflection tests.");
    }
}
