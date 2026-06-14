using ForgeMission.Core.Experts;
using ForgeMission.Core.Runtime;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace ForgeMission.Core.Adapters;

/// <summary>
/// IExpertRunner implementation backed by Microsoft Agent Framework.
/// This is the only file in the codebase that touches MAF.
/// </summary>
public class MafExpertRunner(IChatClient chatClient) : IExpertRunner
{
    public async Task<string> RunAsync(ExpertDefinition expert, string context, CancellationToken ct = default)
    {
        var agent = new ChatClientAgent(chatClient, expert.SystemPrompt, expert.Name);
        var session = await agent.CreateSessionAsync(ct);
        var response = await agent.RunAsync(context, session, new ChatClientAgentRunOptions(), ct);
        return response.Text;
    }
}
