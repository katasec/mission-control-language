using ForgeMission.Application.Transport;

namespace ForgeMission.Presentation.Components;

/// <summary>What the case editor is holding right now. It is presentation state: a case is only a
/// case once Application has accepted it, so nothing here is durable and nothing is a verdict.
/// A null <see cref="EvaluationCaseId"/> means this is a new case rather than an edit.</summary>
public sealed record EvaluationCaseDraft(
    Guid? EvaluationCaseId,
    string Input,
    EvaluationOutcomeView ExpectedOutcome,
    string RequiredFragments);
