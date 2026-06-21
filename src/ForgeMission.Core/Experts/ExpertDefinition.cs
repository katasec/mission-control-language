namespace ForgeMission.Core.Experts;

public record ExpertDefinition(string Name, string Input, string Output, string SystemPrompt, string Role = "")
{
    public bool IsJudge => Role.Equals("judge", StringComparison.OrdinalIgnoreCase);
}
