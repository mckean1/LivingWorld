namespace LivingWorld.Core;

public sealed record WorldReadinessReport(
    bool IsReady,
    int WorldAgeYears,
    double BiologicalScore,
    double SocialScore,
    double CivilizationalScore,
    double CandidateScore,
    double StabilityScore,
    int ViableCandidateCount,
    IReadOnlyList<string> FailureReasons,
    IReadOnlyDictionary<string, bool> ReadinessPassesByCategory,
    int OrganicCandidateCount = 0,
    int FallbackCandidateCount = 0,
    int OrganicHealthyCandidateCount = 0)
{
    public static WorldReadinessReport Empty { get; } = new(
        false,
        0,
        0.0,
        0.0,
        0.0,
        0.0,
        0.0,
        0,
        Array.Empty<string>(),
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase));
}
