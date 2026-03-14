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
    string CandidateOriginReason = "");
