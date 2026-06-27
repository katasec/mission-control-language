namespace ForgeMission.Core.Experts;

public class AggregateExpertLoadException(IReadOnlyList<ExpertLoadException> errors)
    : Exception($"{errors.Count} expert file error(s)")
{
    public IReadOnlyList<ExpertLoadException> Errors { get; } = errors;
}
