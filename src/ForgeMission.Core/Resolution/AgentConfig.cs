using System.Diagnostics.CodeAnalysis;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ForgeMission.Core.Resolution;

public class AgentConfig
{
    public string Mission { get; set; } = "mission.mcl";
    public int    Port    { get; set; } = 8080;
    public string Id      { get; set; } = "forge-agent";
    // Which API shape to serve: "openai" (/v1/chat/completions) or "anthropic" (/v1/messages).
    public string Wire    { get; set; } = "openai";
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
