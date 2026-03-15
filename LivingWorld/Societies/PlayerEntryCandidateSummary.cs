using LivingWorld.Core;

namespace LivingWorld.Societies;

public sealed record PlayerEntryCandidateSummary(
    int PolityId,
    string PolityName,
    int SpeciesId,
    string SpeciesName,
    int LineageId,
    int HomeRegionId,
    string HomeRegionName,
    int PolityAge,
    int WorldAgeAtEntry,
    int SettlementCount,
    string PopulationBand,
    string SubsistenceStyle,
    string CurrentCondition,
    string SettlementProfile,
    string RegionalProfile,
    string LineageProfile,
    string DiscoverySummary,
    string LearnedSummary,
    string RecentHistoricalNote,
    string DefiningPressureOrOpportunity,
    double RankScore,
    StabilityBand StabilityBand,
    bool IsFallbackCandidate,
    bool IsEmergencyAdmitted = false,
    string CandidateOriginReason = "",
    CandidateViabilityResult? Viability = null,
    CandidateMaturityBand MaturityBand = CandidateMaturityBand.Anchored,
    string StabilityMode = "",
    string ArchetypeSummary = "",
    string QualificationReason = "",
    string EvidenceSentence = "",
    IReadOnlyList<string>? Strengths = null,
    IReadOnlyList<string>? Warnings = null,
    IReadOnlyList<string>? Risks = null,
    CandidateScoreBreakdown? ScoreBreakdown = null,
    IReadOnlyList<string>? DiversityTags = null,
    string DuplicateSuppressionKey = "")
{
    public IReadOnlyList<string> SafeStrengths => Strengths ?? Array.Empty<string>();
    public IReadOnlyList<string> SafeWarnings => Warnings ?? Array.Empty<string>();
    public IReadOnlyList<string> SafeRisks => Risks ?? Array.Empty<string>();
    public IReadOnlyList<string> SafeDiversityTags => DiversityTags ?? Array.Empty<string>();
}
