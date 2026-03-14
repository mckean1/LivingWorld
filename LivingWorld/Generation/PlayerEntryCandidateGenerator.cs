using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public sealed class PlayerEntryCandidateGenerator
{
    private readonly WorldGenerationSettings _settings;

    public PlayerEntryCandidateGenerator(WorldGenerationSettings settings)
    {
        _settings = settings;
    }

    public IReadOnlyList<PlayerEntryCandidateSummary> Generate(World world, bool allowEmergencyFallback, out Dictionary<int, string> rejectionReasons)
    {
        rejectionReasons = new Dictionary<int, string>();
        List<PlayerEntryCandidateSummary> candidates = [];

        foreach (Polity polity in world.Polities.Where(candidate => candidate.Population > 0))
        {
            if (!TryBuildCandidate(world, polity, allowEmergencyFallback, out PlayerEntryCandidateSummary? summary, out string rejectionReason))
            {
                rejectionReasons[polity.Id] = rejectionReason;
                continue;
            }

            candidates.Add(summary!);
        }

        int targetCount = world.StartupAgeConfiguration.CandidateCountTarget;
        return ApplyDiversityTrim(candidates, targetCount);
    }

    private bool TryBuildCandidate(
        World world,
        Polity polity,
        bool allowEmergencyFallback,
        out PlayerEntryCandidateSummary? summary,
        out string rejectionReason)
    {
        summary = null;
        rejectionReason = string.Empty;

        FocalCandidateProfile? profile = world.FocalCandidateProfiles.FirstOrDefault(candidate => candidate.PolityId == polity.Id);
        Species? species = world.Species.FirstOrDefault(candidate => candidate.Id == polity.SpeciesId);
        if (profile is null || species is null || species.SentienceCapability == Life.SentienceCapabilityState.None)
        {
            rejectionReason = "missing_viable_profile_or_lineage";
            return false;
        }

        double viabilityScore = ScoreCandidate(profile, polity);
        double minimumViability = allowEmergencyFallback
            ? _settings.EmergencyCandidateMinimumViabilityScore
            : _settings.CandidateMinimumViabilityScore;
        int minimumPopulation = allowEmergencyFallback
            ? Math.Max(40, _settings.CandidateMinimumPopulation - 35)
            : _settings.CandidateMinimumPopulation;
        int minimumAge = allowEmergencyFallback
            ? Math.Max(1, _settings.CandidateMinimumPolityAgeYears - 2)
            : _settings.CandidateMinimumPolityAgeYears;
        double minimumSettlementThreshold = allowEmergencyFallback
            ? Math.Max(0.20, _settings.CandidateMinimumSettlementViability - 0.18)
            : _settings.CandidateMinimumSettlementViability;
        double maximumCollapseSeverity = allowEmergencyFallback
            ? Math.Min(0.97, _settings.CandidateMaximumCollapseSeverity + 0.09)
            : _settings.CandidateMaximumCollapseSeverity;
        double collapseSeverity = Math.Max(polity.FragmentationPressure, polity.MigrationPressure);
        double minimumSettlementViability = polity.Settlements.Count == 0
            ? 0.0
            : polity.Settlements.Average(settlement => Math.Clamp((settlement.FoodBalance + 1.2) / 2.4, 0.0, 1.0));

        if (polity.Population < minimumPopulation)
        {
            rejectionReason = "population_below_threshold";
            return false;
        }

        if (polity.YearsSinceFounded < minimumAge)
        {
            rejectionReason = "polity_too_young";
            return false;
        }

        if (minimumSettlementViability < minimumSettlementThreshold)
        {
            rejectionReason = "settlements_too_weak";
            return false;
        }

        if (collapseSeverity > maximumCollapseSeverity)
        {
            rejectionReason = "collapse_pressure_too_high";
            return false;
        }

        if (viabilityScore < minimumViability)
        {
            rejectionReason = "viability_below_threshold";
            return false;
        }

        Region homeRegion = world.Regions[polity.RegionId];
        string discoverySummary = polity.Discoveries.Count == 0
            ? "Early survival knowledge"
            : string.Join(", ", polity.Discoveries.Take(2).Select(discovery => discovery.Summary));
        string learnedSummary = polity.Advancements.Count == 0
            ? "None"
            : string.Join(", ", polity.Advancements.OrderBy(advancement => advancement).Take(2).Select(advancement => AdvancementCatalog.Get(advancement).Name));
        string historicalNote = profile.RecentHistoricalNote;
        summary = new PlayerEntryCandidateSummary(
            polity.Id,
            polity.Name,
            species.Id,
            species.Name,
            polity.LineageId,
            homeRegion.Id,
            homeRegion.Name,
            polity.YearsSinceFounded,
            world.Time.Year,
            polity.SettlementCount,
            profile.PopulationBand,
            ResolveSubsistenceStyle(world, polity),
            ResolveCurrentCondition(profile, polity),
            discoverySummary,
            learnedSummary,
            historicalNote,
            polity.CurrentPressureSummary ?? profile.PressureSummary,
            viabilityScore,
            profile.StabilityBand,
            allowEmergencyFallback && viabilityScore < _settings.CandidateMinimumViabilityScore);
        return true;
    }

    private IReadOnlyList<PlayerEntryCandidateSummary> ApplyDiversityTrim(List<PlayerEntryCandidateSummary> candidates, int targetCount)
    {
        List<PlayerEntryCandidateSummary> ordered = candidates
            .OrderByDescending(candidate => candidate.RankScore)
            .ThenByDescending(candidate => candidate.PolityAge)
            .ThenBy(candidate => candidate.PolityId)
            .ToList();
        if (ordered.Count <= targetCount)
        {
            return ordered;
        }

        List<PlayerEntryCandidateSummary> selected = [];
        HashSet<string> seenSpecies = new(StringComparer.OrdinalIgnoreCase);
        HashSet<int> seenRegions = [];
        HashSet<string> seenSubsistence = new(StringComparer.OrdinalIgnoreCase);

        while (ordered.Count > 0 && selected.Count < targetCount)
        {
            PlayerEntryCandidateSummary next = ordered
                .OrderByDescending(candidate =>
                    candidate.RankScore
                    + (seenSpecies.Contains(candidate.SpeciesName) ? 0.0 : _settings.CandidateDiversitySpeciesBonus)
                    + (seenRegions.Contains(candidate.HomeRegionId) ? 0.0 : _settings.CandidateDiversityRegionBonus)
                    + (seenSubsistence.Contains(candidate.SubsistenceStyle) ? 0.0 : _settings.CandidateDiversitySubsistenceBonus))
                .ThenByDescending(candidate => candidate.PolityAge)
                .ThenBy(candidate => candidate.PolityId)
                .First();
            selected.Add(next);
            seenSpecies.Add(next.SpeciesName);
            seenRegions.Add(next.HomeRegionId);
            seenSubsistence.Add(next.SubsistenceStyle);
            ordered.Remove(next);
        }

        return selected;
    }

    private static string ResolveSubsistenceStyle(World world, Polity polity)
    {
        EmergingSociety? founder = polity.FounderSocietyId.HasValue
            ? world.Societies.FirstOrDefault(candidate => candidate.Id == polity.FounderSocietyId.Value)
            : null;
        if (founder is not null)
        {
            return founder.SubsistenceMode switch
            {
                SubsistenceMode.HuntingFocused => "Hunting-focused",
                SubsistenceMode.ForagingFocused => "Foraging-focused",
                SubsistenceMode.MixedHunterForager => "Mixed hunter-forager",
                SubsistenceMode.ProtoFarming => "Proto-farming",
                SubsistenceMode.FarmingEmergent => "Farming-emergent",
                _ => "Mixed subsistence"
            };
        }

        return polity.HasAdvancement(AdvancementId.Agriculture)
            ? "Proto-farming"
            : polity.HasSettlements
                ? "Mixed subsistence"
                : "Mixed hunter-forager";
    }

    private static string ResolveCurrentCondition(FocalCandidateProfile profile, Polity polity)
        => (profile.StabilityBand, polity.CurrentPressureSummary ?? profile.PressureSummary) switch
        {
            (StabilityBand.Strong, _) => "Growing",
            (StabilityBand.Stable, var pressure) when pressure.Contains("anchoring", StringComparison.OrdinalIgnoreCase) => "Stable",
            (_, var pressure) when pressure.Contains("migration", StringComparison.OrdinalIgnoreCase) => "Migratory",
            (StabilityBand.Strained, _) => "Pressured",
            (StabilityBand.Fragile, _) => "Vulnerable",
            _ => "Recovering"
        };

    private static double ScoreCandidate(FocalCandidateProfile profile, Polity polity)
    {
        double stabilityScore = profile.StabilityBand switch
        {
            StabilityBand.Strong => 1.0,
            StabilityBand.Stable => 0.82,
            StabilityBand.Strained => 0.58,
            _ => 0.34
        };
        double pressurePenalty = Math.Clamp(Math.Max(polity.FragmentationPressure, polity.MigrationPressure), 0.0, 1.0) * 0.35;
        double settlementDepth = Math.Min(1.0, profile.SettlementCount / 4.0);
        double historyDepth = Math.Min(1.0, profile.PolityAge / 14.0);
        double knowledgeDepth = Math.Min(1.0, polity.Discoveries.Count / 6.0);
        double populationDepth = Math.Min(1.0, polity.Population / 260.0);
        return Math.Clamp((stabilityScore * 0.28) + (settlementDepth * 0.20) + (historyDepth * 0.18) + (knowledgeDepth * 0.16) + (populationDepth * 0.18) - pressurePenalty, 0.0, 1.5);
    }
}
