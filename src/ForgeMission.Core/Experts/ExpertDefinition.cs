namespace ForgeMission.Core.Experts;

public record ExpertDefinition(
    string Name,
    string Input,
    string Output,
    string SystemPrompt,
    string Role                        = "",
    string Kind                        = "llm",
    string Endpoint                    = "",
    string Check                       = "",
    string OnFail                      = "",
    string Model                       = "",
    IReadOnlyList<string>? Inputs      = null,
    string OutputKey                   = "",
    string Threshold                   = "",
    string Command                     = "",
    IReadOnlyList<string>? Args        = null,
    string Timeout                     = "",
    string ExpertDirectory             = "",
    IReadOnlyDictionary<string, string>? OutputKeys = null,
    IReadOnlyDictionary<string, string>? InputKeys  = null)
{
    public bool IsJudge       => Role.Equals("judge",       StringComparison.OrdinalIgnoreCase);
    public bool IsHttp        => Kind.Equals("http",        StringComparison.OrdinalIgnoreCase);
    public bool IsRule        => Kind.Equals("rule",        StringComparison.OrdinalIgnoreCase);
    public bool IsOnnx        => Kind.Equals("onnx",        StringComparison.OrdinalIgnoreCase);
    public bool IsJsonExtract => Kind.Equals("json_extract",StringComparison.OrdinalIgnoreCase);
    public bool IsExec        => Kind.Equals("exec",        StringComparison.OrdinalIgnoreCase);
    public bool IsSearch      => Kind.Equals("search",      StringComparison.OrdinalIgnoreCase);
}
