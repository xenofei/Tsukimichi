namespace Tsukimichi.Core.Model;

/// <summary>One gate on a quest, produced by the requirement evaluator. Concrete kinds carry their own data.</summary>
public abstract record Requirement(RequirementKind Kind);

/// <summary>Outcome of checking one requirement against a character.</summary>
/// <param name="Detail">One clause describing the gap or the pass, e.g. "Trusted, needs Sworn".</param>
public sealed record RequirementResult(Requirement Req, bool Met, string Detail);
