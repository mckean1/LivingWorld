using LivingWorld.Societies;

namespace LivingWorld.Core;

public enum SocietyPersistenceState
{
    NoHistoricalSociety,
    HistoricalLineageOnly,
    ActiveSocietySubstrate
}

public enum CandidateSocialBackingType
{
    PolityShell,
    HistoricalLineageOnly,
    ActiveSocietyBacked
}

public sealed record SocietalPersistenceTruth(
    int? FounderSocietyId,
    SocietyPersistenceState SocietyPersistenceState,
    CandidateSocialBackingType CandidateSocialBackingType,
    bool HasActiveSocietySubstrate,
    bool HasHistoricalSocietyLineage,
    int ActiveSocietyAgeYears,
    int HistoricalSocietyLineageAgeYears,
    string SourceIdentityPath,
    string CandidateBackingSummary)
{
    public bool PolityBackedByActiveSociety => CandidateSocialBackingType == CandidateSocialBackingType.ActiveSocietyBacked;
    public bool CandidateBackedByHistoricalLineageOnly => CandidateSocialBackingType == CandidateSocialBackingType.HistoricalLineageOnly;
    public bool PolityOutlivingSocietySubstrate => CandidateBackedByHistoricalLineageOnly;
    public bool PolityShellWithoutSocietySubstrate => CandidateSocialBackingType == CandidateSocialBackingType.PolityShell;
}

public static class SocietalPersistenceTruthEvaluator
{
    // A live society is an uncollapsed society object that still has a population base.
    // Founder-polity survival is historical carryover only; it does not keep the society substrate active.
    public static bool HasActiveSocietySubstrate(EmergingSociety? society)
        => society is not null
            && !society.IsCollapsed
            && society.Population > 0;

    public static SocietalPersistenceTruth Evaluate(World world, Polity polity)
    {
        EmergingSociety? founderSociety = polity.FounderSocietyId.HasValue
            ? world.Societies.FirstOrDefault(candidate => candidate.Id == polity.FounderSocietyId.Value)
            : null;
        bool hasActiveSocietySubstrate = HasActiveSocietySubstrate(founderSociety);
        bool hasHistoricalSocietyLineage = founderSociety is not null;
        int historicalLineageAgeYears = founderSociety is null
            ? 0
            : Math.Max(0, world.Time.Year - founderSociety.FoundingYear);
        int activeSocietyAgeYears = hasActiveSocietySubstrate
            ? historicalLineageAgeYears
            : 0;

        SocietyPersistenceState societyPersistenceState = hasActiveSocietySubstrate
            ? SocietyPersistenceState.ActiveSocietySubstrate
            : hasHistoricalSocietyLineage
                ? SocietyPersistenceState.HistoricalLineageOnly
                : SocietyPersistenceState.NoHistoricalSociety;
        CandidateSocialBackingType backingType = hasActiveSocietySubstrate
            ? CandidateSocialBackingType.ActiveSocietyBacked
            : hasHistoricalSocietyLineage
                ? CandidateSocialBackingType.HistoricalLineageOnly
                : CandidateSocialBackingType.PolityShell;
        string sourceIdentityPath = backingType switch
        {
            CandidateSocialBackingType.ActiveSocietyBacked => "active_society_backed_polity_candidate",
            CandidateSocialBackingType.HistoricalLineageOnly => "lineage_carrying_polity_candidate",
            _ => "polity_shell_candidate"
        };
        string candidateBackingSummary = backingType switch
        {
            CandidateSocialBackingType.ActiveSocietyBacked => "active society-backed polity candidate",
            CandidateSocialBackingType.HistoricalLineageOnly => "lineage-carrying polity with degraded active society",
            _ => "polity shell without active society substrate"
        };

        return new SocietalPersistenceTruth(
            founderSociety?.Id,
            societyPersistenceState,
            backingType,
            hasActiveSocietySubstrate,
            hasHistoricalSocietyLineage,
            activeSocietyAgeYears,
            historicalLineageAgeYears,
            sourceIdentityPath,
            candidateBackingSummary);
    }
}
