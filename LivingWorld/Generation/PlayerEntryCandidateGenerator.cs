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

    public IReadOnlyList<PlayerEntryCandidateSummary> Generate(
        World world,
        IReadOnlyDictionary<int, CandidateReadinessEvaluation> candidateEvaluations,
        out Dictionary<int, string> rejectionReasons)
    {
        rejectionReasons = new Dictionary<int, string>();
        List<PlayerEntryCandidateSummary> candidates = [];

        foreach (Polity polity in world.Polities.Where(candidate => candidate.Population > 0))
        {
            if (!candidateEvaluations.TryGetValue(polity.Id, out CandidateReadinessEvaluation? readiness))
            {
                rejectionReasons[polity.Id] = "missing_candidate_readiness_evidence";
                continue;
            }

            if (!readiness.IsViable)
            {
                rejectionReasons[polity.Id] = readiness.PrimaryBlockingReason;
                continue;
            }

            if (!TryBuildCandidateFromReadiness(world, polity, readiness, out PlayerEntryCandidateSummary? summary, out string rejectionReason))
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
        if (profile is null || species is null || species.SentienceCapability == SentienceCapabilityState.None)
        {
            rejectionReason = "missing_viable_profile_or_lineage";
            return false;
        }

        CandidateAdmissionEvaluation standardAdmission = EvaluateAdmission(profile, polity, allowEmergencyFallback: false);
        CandidateAdmissionEvaluation finalAdmission = allowEmergencyFallback
            ? EvaluateAdmission(profile, polity, allowEmergencyFallback: true)
            : standardAdmission;
        if (!finalAdmission.Passes)
        {
            rejectionReason = finalAdmission.RejectionReason;
            return false;
        }

        bool emergencyAdmissionUsed = allowEmergencyFallback && !standardAdmission.Passes;
        bool fallbackDerived = polity.IsFallbackCreated || emergencyAdmissionUsed;

        Region homeRegion = world.Regions[polity.RegionId];
        EvolutionaryLineage? lineage = world.GetLineage(polity.LineageId);
        string subsistenceStyle = ResolveSubsistenceStyle(polity, species);
        string currentCondition = ResolveCurrentCondition(profile, polity);
        string settlementProfile = ResolveSettlementProfile(polity);
        string regionalProfile = ResolveRegionalProfile(homeRegion);
        string lineageProfile = ResolveLineageProfile(species, lineage);
        string discoverySummary = polity.Discoveries.Count == 0
            ? "Shared survival lore"
            : string.Join(", ", polity.Discoveries.Take(2).Select(discovery => SanitizePlayerFacingText(discovery.Summary, "Shared survival lore")));
        string learnedSummary = polity.Advancements.Count == 0
            ? "None"
            : string.Join(", ", polity.Advancements.OrderBy(advancement => advancement).Take(2).Select(advancement => AdvancementCatalog.Get(advancement).Name));
        string historicalNote = ResolveHistoricalNote(world, polity, profile);
        string originReason = polity.IsFallbackCreated
            ? emergencyAdmissionUsed
                ? $"fallback_polity+emergency:{standardAdmission.RejectionReason}"
                : "fallback_polity"
            : emergencyAdmissionUsed
                ? $"emergency_admission:{standardAdmission.RejectionReason}"
                : string.Empty;

        summary = BuildCandidateSummary(
            world,
            polity,
            profile,
            species,
            finalAdmission.ViabilityScore,
            profile.StabilityBand,
            fallbackDerived,
            emergencyAdmissionUsed,
            originReason);
        return true;
    }

    private bool TryBuildCandidateFromReadiness(
        World world,
        Polity polity,
        CandidateReadinessEvaluation readiness,
        out PlayerEntryCandidateSummary? summary,
        out string rejectionReason)
    {
        summary = null;
        rejectionReason = string.Empty;

        FocalCandidateProfile? profile = world.FocalCandidateProfiles.FirstOrDefault(candidate => candidate.PolityId == polity.Id);
        Species? species = world.Species.FirstOrDefault(candidate => candidate.Id == polity.SpeciesId);
        if (profile is null || species is null || species.SentienceCapability == SentienceCapabilityState.None)
        {
            rejectionReason = "missing_viable_profile_or_lineage";
            return false;
        }

        summary = BuildCandidateSummary(
            world,
            polity,
            profile,
            species,
            readiness.SupportsNormalEntry ? 1.0 : 0.82,
            readiness.SupportStability switch
            {
                SupportStabilityState.Stable => StabilityBand.Strong,
                SupportStabilityState.Recovering => StabilityBand.Stable,
                SupportStabilityState.Volatile => StabilityBand.Strained,
                _ => StabilityBand.Fragile
            },
            polity.IsFallbackCreated,
            false,
            polity.IsFallbackCreated ? "fallback_polity" : string.Empty);
        return true;
    }

    private PlayerEntryCandidateSummary BuildCandidateSummary(
        World world,
        Polity polity,
        FocalCandidateProfile profile,
        Species species,
        double rankScore,
        StabilityBand stabilityBand,
        bool fallbackDerived,
        bool emergencyAdmissionUsed,
        string originReason)
    {
        Region homeRegion = world.Regions[polity.RegionId];
        EvolutionaryLineage? lineage = world.GetLineage(polity.LineageId);
        string subsistenceStyle = ResolveSubsistenceStyle(polity, species);
        string currentCondition = ResolveCurrentCondition(profile, polity);
        string settlementProfile = ResolveSettlementProfile(polity);
        string regionalProfile = ResolveRegionalProfile(homeRegion);
        string lineageProfile = ResolveLineageProfile(species, lineage);
        string discoverySummary = polity.Discoveries.Count == 0
            ? "Shared survival lore"
            : string.Join(", ", polity.Discoveries.Take(2).Select(discovery => SanitizePlayerFacingText(discovery.Summary, "Shared survival lore")));
        string learnedSummary = polity.Advancements.Count == 0
            ? "None"
            : string.Join(", ", polity.Advancements.OrderBy(advancement => advancement).Take(2).Select(advancement => AdvancementCatalog.Get(advancement).Name));
        string historicalNote = ResolveHistoricalNote(world, polity, profile);

        return new PlayerEntryCandidateSummary(
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
            subsistenceStyle,
            currentCondition,
            settlementProfile,
            regionalProfile,
            lineageProfile,
            discoverySummary,
            learnedSummary,
            historicalNote,
            ResolvePressureLine(polity.CurrentPressureSummary ?? profile.PressureSummary),
            rankScore,
            stabilityBand,
            fallbackDerived,
            emergencyAdmissionUsed,
            originReason);
    }

    private CandidateAdmissionEvaluation EvaluateAdmission(FocalCandidateProfile profile, Polity polity, bool allowEmergencyFallback)
    {
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

        List<string> failures = [];
        if (polity.Population < minimumPopulation)
        {
            failures.Add("population_below_threshold");
        }

        if (polity.YearsSinceFounded < minimumAge)
        {
            failures.Add("polity_too_young");
        }

        if (minimumSettlementViability < minimumSettlementThreshold)
        {
            failures.Add("settlements_too_weak");
        }

        if (collapseSeverity > maximumCollapseSeverity)
        {
            failures.Add("collapse_pressure_too_high");
        }

        if (viabilityScore < minimumViability)
        {
            failures.Add("viability_below_threshold");
        }

        return new CandidateAdmissionEvaluation(
            failures.Count == 0,
            failures.Count == 0 ? string.Empty : failures[0],
            failures,
            viabilityScore);
    }

    private IReadOnlyList<PlayerEntryCandidateSummary> ApplyDiversityTrim(List<PlayerEntryCandidateSummary> candidates, int targetCount)
    {
        List<PlayerEntryCandidateSummary> ordered = candidates
            .OrderByDescending(candidate => candidate.RankScore)
            .ThenByDescending(candidate => candidate.PolityAge)
            .ThenBy(candidate => candidate.PolityId)
            .ToList();

        List<PlayerEntryCandidateSummary> selected = [];
        int selectionTarget = Math.Min(targetCount, ordered.Count);
        while (ordered.Count > 0 && selected.Count < selectionTarget)
        {
            PlayerEntryCandidateSummary next = ordered
                .OrderByDescending(candidate =>
                    candidate.RankScore + ScoreDiversityNovelty(selected, candidate))
                .ThenByDescending(candidate => candidate.PolityAge)
                .ThenBy(candidate => candidate.PolityId)
                .First();
            selected.Add(next);
            ordered.Remove(next);
        }

        return selected;
    }

    private static string ResolveSubsistenceStyle(Polity polity, Species species)
    {
        return PolityProfileResolver.ResolveSubsistenceMode(polity, species) switch
        {
            SubsistenceMode.HuntingFocused => "Hunting-focused",
            SubsistenceMode.ForagingFocused => "Foraging-focused",
            SubsistenceMode.MixedHunterForager => "Mixed hunter-forager",
            SubsistenceMode.ProtoFarming => "Proto-farming",
            SubsistenceMode.FarmingEmergent => "Farming-emergent",
            _ => "Mixed subsistence"
        };
    }

    private static string ResolveCurrentCondition(FocalCandidateProfile profile, Polity polity)
    {
        int surplusSettlements = polity.Settlements.Count(settlement => settlement.FoodState == FoodState.Surplus);
        int deficitSettlements = polity.Settlements.Count(settlement => settlement.FoodState == FoodState.Deficit);
        int starvingSettlements = polity.Settlements.Count(settlement => settlement.FoodState == FoodState.Starving);
        double averageSettlementAge = polity.SettlementCount == 0
            ? 0.0
            : polity.Settlements.Average(settlement => settlement.YearsEstablished);
        string pressure = polity.CurrentPressureSummary ?? profile.PressureSummary;

        if (starvingSettlements > 0)
        {
            return "Food crisis";
        }

        if (deficitSettlements > 0)
        {
            return polity.MigrationPressure >= 0.44
                ? "Migration under strain"
                : "Lean-season pressure";
        }

        if (surplusSettlements == polity.SettlementCount && polity.SettlementCount >= 3 && averageSettlementAge < 2.0)
        {
            return polity.FragmentationPressure >= 0.62
                ? "Frontier expansion"
                : "Anchored growth";
        }

        if (polity.FragmentationPressure >= 0.72)
        {
            return "Fragmenting";
        }

        if (polity.MigrationPressure >= 0.56 || pressure.Contains("migration", StringComparison.OrdinalIgnoreCase))
        {
            return "Migratory";
        }

        return profile.StabilityBand switch
        {
            StabilityBand.Strong => "Growing",
            StabilityBand.Stable when pressure.Contains("anchoring", StringComparison.OrdinalIgnoreCase) => "Anchored",
            StabilityBand.Stable when pressure.Contains("frontier", StringComparison.OrdinalIgnoreCase) => "Frontier consolidation",
            StabilityBand.Stable => "Holding",
            StabilityBand.Strained => "Pressured",
            StabilityBand.Fragile => "Vulnerable",
            _ => "Recovering"
        };
    }

    private double ScoreDiversityNovelty(IReadOnlyList<PlayerEntryCandidateSummary> selected, PlayerEntryCandidateSummary candidate)
    {
        if (selected.Count == 0)
        {
            return 0.0;
        }

        return selected
            .Select(existing => ScorePairwiseDiversity(existing, candidate))
            .DefaultIfEmpty(0.0)
            .Min();
    }

    private double ScorePairwiseDiversity(PlayerEntryCandidateSummary left, PlayerEntryCandidateSummary right)
    {
        double score = 0.0;
        if (!string.Equals(left.SpeciesName, right.SpeciesName, StringComparison.OrdinalIgnoreCase))
        {
            score += _settings.CandidateDiversitySpeciesBonus;
        }

        if (left.LineageId != right.LineageId)
        {
            score += _settings.CandidateDiversityLineageBonus;
        }

        if (left.HomeRegionId != right.HomeRegionId)
        {
            score += _settings.CandidateDiversityRegionBonus;
        }

        if (!string.Equals(ExtractBiomeKey(left.RegionalProfile), ExtractBiomeKey(right.RegionalProfile), StringComparison.OrdinalIgnoreCase))
        {
            score += _settings.CandidateDiversityBiomeBonus;
        }

        if (!string.Equals(left.SubsistenceStyle, right.SubsistenceStyle, StringComparison.OrdinalIgnoreCase))
        {
            score += _settings.CandidateDiversitySubsistenceBonus;
        }

        if (!string.Equals(left.SettlementProfile, right.SettlementProfile, StringComparison.OrdinalIgnoreCase))
        {
            score += _settings.CandidateDiversitySettlementProfileBonus;
        }

        if (!string.Equals(left.CurrentCondition, right.CurrentCondition, StringComparison.OrdinalIgnoreCase))
        {
            score += _settings.CandidateDiversityConditionBonus;
        }

        return score;
    }

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

    private static string ResolveSettlementProfile(Polity polity)
    {
        int settlementCount = polity.SettlementCount;
        double averageSettlementAge = settlementCount == 0
            ? 0.0
            : polity.Settlements.Average(settlement => settlement.YearsEstablished);
        bool agrarian = polity.CultivatedLand >= Math.Max(0.8, settlementCount * 0.20)
            || polity.ManagedFoodSupplyEstablished
            || polity.Settlements.Sum(settlement => settlement.CultivatedCrops.Count) > 0;
        bool stressed = polity.Settlements.Any(settlement => settlement.FoodState is FoodState.Deficit or FoodState.Starving)
            || polity.FragmentationPressure >= 0.62;
        bool frontier = averageSettlementAge < 2.0;

        return settlementCount switch
        {
            <= 1 => frontier ? "single frontier hearth" : "single hearth",
            2 => frontier
                ? "paired frontier hearths"
                : agrarian
                    ? "paired agrarian hearths"
                    : "paired hearths",
            <= 4 => frontier
                ? stressed
                    ? "strained frontier web"
                    : "young frontier web"
                : agrarian
                    ? "growing agrarian web"
                    : "settled cluster",
            _ => frontier
                ? stressed
                    ? "stretched frontier spread"
                    : "new settlement spread"
                : agrarian
                    ? stressed
                        ? "stretched agrarian web"
                        : "rooted agrarian web"
                    : stressed
                        ? "stretched settlement web"
                        : "broad settlement web"
        };
    }

    private static string ResolveRegionalProfile(Region homeRegion)
    {
        string biome = homeRegion.Biome switch
        {
            RegionBiome.RiverValley => "river valley",
            RegionBiome.Wetlands => "wetland basin",
            RegionBiome.Coast => "coastal margin",
            RegionBiome.Plains => "open plains",
            RegionBiome.Forest => "deep forest",
            RegionBiome.Highlands => "highland shelf",
            RegionBiome.Mountains => "mountain edge",
            RegionBiome.Drylands => "dry frontier",
            _ => "mixed country"
        };
        string support = homeRegion.Fertility >= 0.70 && homeRegion.WaterAvailability >= 0.65
            ? "rich water and fertile ground"
            : homeRegion.Fertility >= 0.58
                ? "reliable mixed ground"
                : homeRegion.WaterAvailability < 0.36
                    ? "thin water and lean ground"
                    : "uneven ground";
        return $"{biome}, {support}";
    }

    private static string ResolveLineageProfile(Species species, EvolutionaryLineage? lineage)
    {
        if (lineage is null)
        {
            return $"{species.EcologyNiche} branch";
        }

        string depth = lineage.AncestryDepth >= 2
            ? "deep descendant branch"
            : lineage.ParentLineageId is not null
                ? "younger descendant branch"
                : "root branch";
        string adaptation = string.IsNullOrWhiteSpace(lineage.HabitatAdaptationSummary)
            ? "mixed adaptation"
            : SanitizePlayerFacingText(lineage.HabitatAdaptationSummary, "mixed adaptation");
        return $"{depth}; {adaptation}";
    }

    private static string ResolveHistoricalNote(World world, Polity polity, FocalCandidateProfile profile)
    {
        string note = world.CivilizationalHistory
            .Where(evt => evt.PolityId == polity.Id)
            .OrderByDescending(evt => evt.Year)
            .ThenByDescending(evt => evt.Month)
            .Select(evt => evt.Type switch
            {
                CivilizationalHistoryEventType.SettlementFounded => "recently founded a settlement",
                CivilizationalHistoryEventType.MigrationWave => "recently shifted into new ground",
                CivilizationalHistoryEventType.Fragmentation => "descended from an older split",
                CivilizationalHistoryEventType.PolityFormation => "recently consolidated into a polity",
                _ => SanitizePlayerFacingText(evt.Summary, "holds an older local history")
            })
            .FirstOrDefault()
            ?? profile.RecentHistoricalNote;
        return SanitizePlayerFacingText(note, "holds an older local history");
    }

    private static string ResolvePressureLine(string pressureSummary)
    {
        string normalized = pressureSummary.Trim();
        if (normalized.Contains("bootstrap", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("fallback", StringComparison.OrdinalIgnoreCase))
        {
            return "holding together through recent strain";
        }

        return normalized switch
        {
            "anchoring on rich ground" => "holding fertile ground",
            "frontier strain" => "pushing into lean frontier",
            "shared survival" => "bound by shared survival",
            "ecological hardship" => "weathering food and climate strain",
            "group fragmentation" => "managing internal fracture",
            "society fragmentation" => "holding together after an internal split",
            "forming" => "gathering into a stronger center",
            "activation" => "newly consolidated",
            _ => SanitizePlayerFacingText(normalized, "managing local strain")
        };
    }

    private static string ExtractBiomeKey(string regionalProfile)
    {
        int separatorIndex = regionalProfile.IndexOf(',', StringComparison.Ordinal);
        return separatorIndex < 0
            ? regionalProfile
            : regionalProfile[..separatorIndex];
    }

    private static string SanitizePlayerFacingText(string text, string fallback)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        string sanitized = text
            .Replace("bootstrap social safeguard", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("fallback polity", "younger polity", StringComparison.OrdinalIgnoreCase)
            .Replace("bootstrap", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("fallback", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("safeguard", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim(' ', '.', ',', ';', ':', '-');

        return string.IsNullOrWhiteSpace(sanitized) ? fallback : sanitized;
    }

    private sealed record CandidateAdmissionEvaluation(
        bool Passes,
        string RejectionReason,
        IReadOnlyList<string> FailureReasons,
        double ViabilityScore);
}
