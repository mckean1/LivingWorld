namespace LivingWorld.Societies;

public sealed record FocalCandidateProfile(
    int PolityId,
    int LineageId,
    int SpeciesId,
    int PolityAge,
    int SettlementCount,
    string PopulationBand,
    int HomeRegionId,
    string PressureSummary,
    string KnowledgeSummary,
    string RecentHistoricalNote,
    StabilityBand StabilityBand,
    bool IsViable);
