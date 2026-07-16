using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ForgeMission.Core.Resolution;

public class AgentConfig
{
    public string Mission { get; set; } = "mission.mcl";
    public int    Port    { get; set; } = 8080;
    public string Id      { get; set; } = "forge-agent";
    // Which wire(s) to serve (42.4): "both" (default) maps /v1/messages AND the OpenAI routes
    // on one app; "anthropic" or "openai" limits to that door.
    public string Wire    { get; set; } = "both";
}

public static class AgentConfigLoader
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(AgentConfig))]
    [UnconditionalSuppressMessage("AOT", "IL3050", Justification = "Type preserved via DynamicDependency")]
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public static AgentConfig Load(string path)
    {
        var yaml = File.ReadAllText(path);
        return Yaml.Deserialize<AgentConfig>(yaml);
    }
}
