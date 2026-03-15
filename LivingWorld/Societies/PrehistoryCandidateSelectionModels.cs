namespace LivingWorld.Societies;

public enum CandidateMaturityBand
{
    Mobile,
    Anchored,
    Settling,
    EmergentPolity
}

public enum CandidateScoreTier
{
    Modest,
    Promising,
    Strong,
    Exceptional
}

public sealed record CandidateScoreBreakdown(
    double SurvivalStrength,
    double ContinuityDepth,
    double SpatialIdentity,
    double AgencyAndInternalOrganization,
    double ExternalEntanglement,
    double StrategicOpportunity,
    double FragilityPenalty,
    double Total,
    CandidateScoreTier Tier,
    string Explanation);

public sealed record CandidateViabilityGate(
    string Key,
    bool Passed,
    string Summary,
    string Evidence);

public sealed record CandidateViabilityResult(
    bool IsViable,
    bool SupportsNormalEntry,
    IReadOnlyList<CandidateViabilityGate> Gates,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> WarningReasons,
    string PrimaryFailureReason,
    string Summary);

public static class CandidatePresentationText
{
    public static string ToDisplayLabel(this CandidateMaturityBand maturityBand)
        => maturityBand switch
        {
            CandidateMaturityBand.Mobile => "Mobile",
            CandidateMaturityBand.Anchored => "Anchored",
            CandidateMaturityBand.Settling => "Settling",
            CandidateMaturityBand.EmergentPolity => "Emergent polity",
            _ => "Anchored"
        };

    public static string ToLowerDisplayLabel(this CandidateMaturityBand maturityBand)
        => maturityBand.ToDisplayLabel().ToLowerInvariant();
}
