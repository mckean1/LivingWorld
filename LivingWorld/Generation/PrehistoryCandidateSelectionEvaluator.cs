using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public sealed class PrehistoryCandidateSelectionEvaluator
{
    private readonly WorldGenerationSettings _settings;

    public PrehistoryCandidateSelectionEvaluator(WorldGenerationSettings settings)
    {
        _settings = settings;
    }

    public PrehistoryCandidateSelectionResult Evaluate(
        World world,
        PrehistoryObserverSnapshot observerSnapshot,
        IReadOnlyDictionary<int, CandidateReadinessEvaluation> candidateEvaluations)
    {
        List<CandidateAssessment> assessments = [];
        Dictionary<int, string> rejectionReasons = new();

        foreach (Polity polity in world.Polities.Where(candidate => candidate.Population > 0).OrderBy(candidate => candidate.Id))
        {
            CandidateAssessment assessment = BuildAssessment(world, polity, observerSnapshot, candidateEvaluations);
            assessments.Add(assessment);
            if (assessment.Summary.Viability?.IsViable != true)
            {
                rejectionReasons[polity.Id] = assessment.Summary.Viability?.PrimaryFailureReason ?? "missing_candidate_readiness_evidence";
            }
        }

        List<CandidateAssessment> viableCandidates = assessments
            .Where(assessment => assessment.Summary.Viability?.IsViable == true)
            .ToList();
        IReadOnlyList<PrehistoryCandidateDiagnostics> diagnostics = assessments
            .Select(assessment => BuildDiagnostics(world, observerSnapshot, candidateEvaluations, assessment))
            .ToArray();
        CandidatePoolCompositionResult composition = ComposeCandidatePool(world, viableCandidates);

        foreach ((int polityId, string reason) in composition.SuppressionReasons)
        {
            rejectionReasons[polityId] = reason;
        }

        return new PrehistoryCandidateSelectionResult(
            composition.SelectedCandidates.Select(candidate => candidate.Summary).ToArray(),
            viableCandidates.Select(candidate => candidate.Summary).ToArray(),
            rejectionReasons,
            diagnostics,
            SummarizeDiagnostics(diagnostics, rejectionReasons));
    }

    private CandidateAssessment BuildAssessment(
        World world,
        Polity polity,
        PrehistoryObserverSnapshot observerSnapshot,
        IReadOnlyDictionary<int, CandidateReadinessEvaluation> candidateEvaluations)
    {
        if (!candidateEvaluations.TryGetValue(polity.Id, out CandidateReadinessEvaluation? readiness))
        {
            return BuildMissingEvidenceAssessment(world, polity);
        }

        PeopleHistoryWindowSnapshot? history = observerSnapshot.PeopleHistoryWindows.FirstOrDefault(snapshot => snapshot.Header.PeopleId == polity.Id);
        Species? species = world.Species.FirstOrDefault(candidate => candidate.Id == polity.SpeciesId);
        if (history is null || species is null)
        {
            return BuildMissingEvidenceAssessment(world, polity);
        }

        NeighborContextSnapshot? neighborContext = observerSnapshot.NeighborContexts.FirstOrDefault(snapshot => snapshot.PeopleId == polity.Id);
        IReadOnlyList<RegionEvaluationSnapshot> regionEvaluations = observerSnapshot.RegionEvaluations
            .Where(snapshot => snapshot.PeopleId == polity.Id)
            .ToArray();
        SocietalPersistenceTruth socialTruth = SocietalPersistenceTruthEvaluator.Evaluate(world, polity);
        CandidateViabilityResult viability = EvaluateViability(readiness);
        CandidateMaturityBand maturityBand = MapMaturityBand(polity, history, readiness);
        StabilityBand stabilityBand = MapStabilityBand(readiness);
        string subsistenceStyle = ResolveSubsistenceStyle(polity, species);
        string homeBucket = ResolveHomeBucket(world.Regions[polity.RegionId]);
        string stabilityMode = ResolveStabilityMode(polity, history, readiness);
        CandidateScoreBreakdown? scoreBreakdown = viability.IsViable
            ? ScoreCandidate(polity, history, readiness, neighborContext, regionEvaluations)
            : null;
        string archetypeBucket = ResolveArchetypeBucket(maturityBand, subsistenceStyle);
        string pressureShape = ResolvePressureShape(readiness, polity, neighborContext);
        string qualificationReason = ResolveQualificationReason(viability, scoreBreakdown, maturityBand);
        string evidenceSentence = BuildEvidenceSentence(polity, subsistenceStyle, homeBucket, maturityBand, readiness, socialTruth, neighborContext);
        IReadOnlyList<string> strengths = BuildStrengths(scoreBreakdown, history, neighborContext);
        IReadOnlyList<string> warnings = BuildWarnings(viability, history);
        IReadOnlyList<string> risks = BuildRisks(readiness, history, neighborContext);
        List<string> diversityTags =
        [
            $"maturity:{NormalizeBucketKey(maturityBand.ToDisplayLabel())}",
            $"archetype:{archetypeBucket}",
            $"home:{homeBucket}",
            $"stability:{NormalizeBucketKey(stabilityMode)}"
        ];

        PlayerEntryCandidateSummary summary = new(
            polity.Id,
            polity.Name,
            species.Id,
            species.Name,
            polity.LineageId,
            polity.RegionId,
            world.Regions[polity.RegionId].Name,
            polity.YearsSinceFounded,
            world.Time.Year,
            polity.SettlementCount,
            ResolvePopulationBand(polity.Population),
            subsistenceStyle,
            ResolveCurrentCondition(readiness, history, stabilityMode),
            ResolveSettlementProfile(polity, maturityBand),
            ResolveRegionalProfile(world.Regions[polity.RegionId], regionEvaluations),
            ResolveLineageProfile(world, species, polity.LineageId),
            ResolveDiscoverySummary(polity),
            ResolveLearnedSummary(polity),
            ResolveHistoricalNote(world, polity),
            ResolvePressureOrOpportunity(polity, history, neighborContext),
            scoreBreakdown?.Total ?? 0.0,
            stabilityBand,
            polity.IsFallbackCreated,
            false,
            BuildCandidateOriginReason(polity, socialTruth),
            viability,
            maturityBand,
            stabilityMode,
            $"{maturityBand.ToDisplayLabel()} {subsistenceStyle.ToLowerInvariant()} in {homeBucket.Replace('_', ' ')}",
            qualificationReason,
            evidenceSentence,
            strengths,
            warnings,
            risks,
            scoreBreakdown,
            diversityTags,
            $"{archetypeBucket}|{homeBucket}|{NormalizeBucketKey(stabilityMode)}|{polity.LineageId}");

        return new CandidateAssessment(
            summary,
            new CandidateDiversityProfile(
                maturityBand,
                archetypeBucket,
                homeBucket,
                NormalizeBucketKey(stabilityMode),
                polity.LineageId,
                polity.RegionId,
                pressureShape));
    }

    private static CandidateViabilityResult EvaluateViability(CandidateReadinessEvaluation readiness)
    {
        List<CandidateViabilityGate> gates =
        [
            new("hard_veto_floor", !readiness.HasHardCurrentMonthVeto, "Current-month catastrophic blockers", readiness.HasHardCurrentMonthVeto
                ? string.Join(", ", readiness.HardVetoReasons)
                : "No hard current-month veto."),
            new("current_support", readiness.CurrentSupportPasses, "Current support must pass", readiness.CurrentSupportPasses
                ? $"Support is {readiness.SupportStability}."
                : "Current support floor failed."),
            new("continuity_floor", readiness.Continuity is ContinuityState.Established or ContinuityState.Deep, "Continuity must be Established or deeper", $"Continuity is {readiness.Continuity}."),
            new("movement_or_rooting_floor",
                IsMovementFloorMet(readiness.MovementCoherence) || IsRootedFloorMet(readiness.Rootedness),
                "Movement must be Coherent or rootedness must meet the rooting floor",
                $"Movement is {readiness.MovementCoherence}; rootedness is {readiness.Rootedness}.")
        ];

        List<string> blockingReasons = [];
        if (readiness.HasHardCurrentMonthVeto)
        {
            blockingReasons.AddRange(readiness.HardVetoReasons.Select(reason => $"hard_veto:{reason}"));
        }

        blockingReasons.AddRange(gates.Where(gate => !gate.Passed).Select(gate => $"hard_gate:{gate.Key}"));
        blockingReasons.AddRange(readiness.BlockingReasons.Where(reason => !blockingReasons.Contains(reason, StringComparer.OrdinalIgnoreCase)));
        string primaryFailureReason = blockingReasons.FirstOrDefault() ?? readiness.PrimaryBlockingReason;

        return new CandidateViabilityResult(
            blockingReasons.Count == 0,
            blockingReasons.Count == 0 && readiness.SupportsNormalEntry,
            gates,
            blockingReasons,
            readiness.WarningReasons.ToArray(),
            primaryFailureReason,
            blockingReasons.Count == 0
                ? readiness.SupportsNormalEntry
                    ? "Meets the hard truth floor and normal-entry durability gates."
                    : "Meets the hard truth floor but remains thin for a normal stop."
                : $"Blocked by {primaryFailureReason}.");
    }

    private CandidatePoolCompositionResult ComposeCandidatePool(World world, IReadOnlyList<CandidateAssessment> viableCandidates)
    {
        Dictionary<int, string> suppressionReasons = new();
        if (viableCandidates.Count == 0)
        {
            return new CandidatePoolCompositionResult(Array.Empty<CandidateAssessment>(), suppressionReasons);
        }

        List<CandidateAssessment> ordered = viableCandidates
            .OrderByDescending(candidate => candidate.Summary.ScoreBreakdown?.Total ?? candidate.Summary.RankScore)
            .ThenByDescending(candidate => candidate.Summary.PolityAge)
            .ThenBy(candidate => candidate.Summary.PolityId)
            .ToList();
        int targetCount = Math.Min(world.StartupAgeConfiguration.CandidateCountTarget, ordered.Count);
        List<CandidateAssessment> selected = [];

        SeedStrongest(selected, ordered, targetCount, suppressionReasons);
        Diversify(selected, ordered, targetCount, suppressionReasons);
        FillRemaining(selected, ordered, targetCount, suppressionReasons);

        foreach (CandidateAssessment candidate in ordered)
        {
            suppressionReasons.TryAdd(candidate.Summary.PolityId, "pool_fill:lower_ranked_viable_candidate");
        }

        return new CandidatePoolCompositionResult(selected, suppressionReasons);
    }

    private void SeedStrongest(
        List<CandidateAssessment> selected,
        List<CandidateAssessment> ordered,
        int targetCount,
        Dictionary<int, string> suppressionReasons)
    {
        int seedCount = Math.Min(Math.Min(_settings.CandidatePoolSeedCount, targetCount), ordered.Count);
        while (selected.Count < seedCount && ordered.Count > 0)
        {
            CandidateAssessment seeded = ordered[0];
            selected.Add(seeded);
            ordered.RemoveAt(0);
            SuppressNearDuplicates(selected, ordered, suppressionReasons);
        }
    }

    private void Diversify(
        List<CandidateAssessment> selected,
        List<CandidateAssessment> remaining,
        int targetCount,
        Dictionary<int, string> suppressionReasons)
    {
        while (selected.Count < targetCount && remaining.Count > 0)
        {
            SuppressNearDuplicates(selected, remaining, suppressionReasons);
            CandidateAssessment? next = remaining
                .Where(candidate => AddsNewCoverage(selected, candidate))
                .Where(candidate => !IsNearDuplicate(selected, candidate, out _))
                .OrderByDescending(candidate => (candidate.Summary.ScoreBreakdown?.Total ?? candidate.Summary.RankScore) + ScoreCoverageNovelty(selected, candidate))
                .ThenByDescending(candidate => candidate.Summary.PolityAge)
                .ThenBy(candidate => candidate.Summary.PolityId)
                .FirstOrDefault();
            if (next is null)
            {
                break;
            }

            selected.Add(next);
            remaining.Remove(next);
            SuppressNearDuplicates(selected, remaining, suppressionReasons);
        }
    }

    private void FillRemaining(
        List<CandidateAssessment> selected,
        List<CandidateAssessment> remaining,
        int targetCount,
        Dictionary<int, string> suppressionReasons)
    {
        while (selected.Count < targetCount && remaining.Count > 0)
        {
            SuppressNearDuplicates(selected, remaining, suppressionReasons);
            CandidateAssessment? next = remaining
                .Where(candidate => RespectsSoftCaps(selected, candidate))
                .OrderByDescending(candidate => candidate.Summary.ScoreBreakdown?.Total ?? candidate.Summary.RankScore)
                .ThenByDescending(candidate => candidate.Summary.PolityAge)
                .ThenBy(candidate => candidate.Summary.PolityId)
                .FirstOrDefault()
                ?? remaining
                    .OrderByDescending(candidate => candidate.Summary.ScoreBreakdown?.Total ?? candidate.Summary.RankScore)
                    .ThenByDescending(candidate => candidate.Summary.PolityAge)
                    .ThenBy(candidate => candidate.Summary.PolityId)
                    .FirstOrDefault();
            if (next is null)
            {
                break;
            }

            selected.Add(next);
            remaining.Remove(next);
        }

        SuppressNearDuplicates(selected, remaining, suppressionReasons);

        foreach (CandidateAssessment candidate in remaining)
        {
            suppressionReasons.TryAdd(candidate.Summary.PolityId, "pool_fill:soft_diversity_trim");
        }
    }

    private void SuppressNearDuplicates(
        IReadOnlyList<CandidateAssessment> selected,
        List<CandidateAssessment> remaining,
        Dictionary<int, string> suppressionReasons)
    {
        if (selected.Count == 0 || remaining.Count == 0)
        {
            return;
        }

        foreach (CandidateAssessment candidate in remaining.ToList())
        {
            if (!IsNearDuplicate(selected, candidate, out int duplicateOf))
            {
                continue;
            }

            suppressionReasons[candidate.Summary.PolityId] = $"suppressed_near_duplicate_of:{duplicateOf}";
            remaining.Remove(candidate);
        }
    }

    private bool AddsNewCoverage(IReadOnlyList<CandidateAssessment> selected, CandidateAssessment candidate)
        => selected.Count == 0
            || !selected.Any(existing => existing.DiversityProfile.MaturityBand == candidate.DiversityProfile.MaturityBand)
            || !selected.Any(existing => string.Equals(existing.DiversityProfile.ArchetypeBucket, candidate.DiversityProfile.ArchetypeBucket, StringComparison.Ordinal))
            || !selected.Any(existing => string.Equals(existing.DiversityProfile.HomeBucket, candidate.DiversityProfile.HomeBucket, StringComparison.Ordinal))
            || !selected.Any(existing => string.Equals(existing.DiversityProfile.StabilityBucket, candidate.DiversityProfile.StabilityBucket, StringComparison.Ordinal));

    private static double ScoreCoverageNovelty(IReadOnlyList<CandidateAssessment> selected, CandidateAssessment candidate)
    {
        if (selected.Count == 0)
        {
            return 0.0;
        }

        double bonus = 0.0;
        if (!selected.Any(existing => existing.DiversityProfile.MaturityBand == candidate.DiversityProfile.MaturityBand)) bonus += 0.18;
        if (!selected.Any(existing => string.Equals(existing.DiversityProfile.ArchetypeBucket, candidate.DiversityProfile.ArchetypeBucket, StringComparison.Ordinal))) bonus += 0.14;
        if (!selected.Any(existing => string.Equals(existing.DiversityProfile.HomeBucket, candidate.DiversityProfile.HomeBucket, StringComparison.Ordinal))) bonus += 0.12;
        if (!selected.Any(existing => string.Equals(existing.DiversityProfile.StabilityBucket, candidate.DiversityProfile.StabilityBucket, StringComparison.Ordinal))) bonus += 0.10;
        return bonus;
    }

    private bool RespectsSoftCaps(IReadOnlyList<CandidateAssessment> selected, CandidateAssessment candidate)
    {
        int maturityCount = selected.Count(existing => existing.DiversityProfile.MaturityBand == candidate.DiversityProfile.MaturityBand);
        int archetypeCount = selected.Count(existing => string.Equals(existing.DiversityProfile.ArchetypeBucket, candidate.DiversityProfile.ArchetypeBucket, StringComparison.Ordinal));
        int homeCount = selected.Count(existing => string.Equals(existing.DiversityProfile.HomeBucket, candidate.DiversityProfile.HomeBucket, StringComparison.Ordinal));
        int stabilityCount = selected.Count(existing => string.Equals(existing.DiversityProfile.StabilityBucket, candidate.DiversityProfile.StabilityBucket, StringComparison.Ordinal));

        return maturityCount < _settings.CandidatePoolSoftCapPerMaturityBand
            && archetypeCount < _settings.CandidatePoolSoftCapPerArchetypeBucket
            && homeCount < _settings.CandidatePoolSoftCapPerHomeBucket
            && stabilityCount < _settings.CandidatePoolSoftCapPerStabilityMode;
    }

    private bool IsNearDuplicate(IReadOnlyList<CandidateAssessment> selected, CandidateAssessment candidate, out int duplicateOfPolityId)
    {
        foreach (CandidateAssessment existing in selected)
        {
            double similarity = ScoreSimilarity(existing.DiversityProfile, candidate.DiversityProfile);
            if (similarity < _settings.CandidateNearDuplicateSimilarityThreshold)
            {
                continue;
            }

            duplicateOfPolityId = existing.Summary.PolityId;
            return true;
        }

        duplicateOfPolityId = 0;
        return false;
    }

    private static double ScoreSimilarity(CandidateDiversityProfile left, CandidateDiversityProfile right)
    {
        double similarity = 0.0;
        if (left.LineageId == right.LineageId) similarity += 0.20;
        if (left.HomeRegionId == right.HomeRegionId) similarity += 0.18;
        if (left.MaturityBand == right.MaturityBand) similarity += 0.16;
        if (string.Equals(left.ArchetypeBucket, right.ArchetypeBucket, StringComparison.Ordinal)) similarity += 0.16;
        if (string.Equals(left.HomeBucket, right.HomeBucket, StringComparison.Ordinal)) similarity += 0.12;
        if (string.Equals(left.StabilityBucket, right.StabilityBucket, StringComparison.Ordinal)) similarity += 0.10;
        if (string.Equals(left.PressureShape, right.PressureShape, StringComparison.Ordinal)) similarity += 0.08;
        return similarity;
    }

    private static CandidateMaturityBand MapMaturityBand(Polity polity, PeopleHistoryWindowSnapshot history, CandidateReadinessEvaluation readiness)
    {
        if (history.CurrentPeopleState.SettlementCount == 0
            || (IsMovementFloorMet(readiness.MovementCoherence) && !IsRootedFloorMet(readiness.Rootedness)))
        {
            return CandidateMaturityBand.Mobile;
        }

        if (readiness.PoliticalDurabilityPasses
            && history.PoliticalHistoryRollup.OrganizedMonthsLast12Months >= 8
            && history.PoliticalHistoryRollup.MultiSettlementMonthsLast12Months >= 6
            && polity.Stage >= PolityStage.Tribe)
        {
            return CandidateMaturityBand.EmergentPolity;
        }

        if (history.PoliticalHistoryRollup.AgricultureMonthsLast12Months >= 4
            || history.PoliticalHistoryRollup.MultiSettlementMonthsLast12Months >= 4
            || history.SettlementHistoryRollup.EstablishedSettlementMonthsLast12Months >= 8
            || polity.SettlementCount >= 3)
        {
            return CandidateMaturityBand.Settling;
        }

        return CandidateMaturityBand.Anchored;
    }

    private static StabilityBand MapStabilityBand(CandidateReadinessEvaluation readiness)
        => readiness.SupportStability switch
        {
            SupportStabilityState.Stable when readiness.Continuity is ContinuityState.Established or ContinuityState.Deep => StabilityBand.Strong,
            SupportStabilityState.Stable => StabilityBand.Stable,
            SupportStabilityState.Recovering => StabilityBand.Stable,
            SupportStabilityState.Volatile => StabilityBand.Strained,
            _ => StabilityBand.Fragile
        };

    private static string ResolveStabilityMode(Polity polity, PeopleHistoryWindowSnapshot history, CandidateReadinessEvaluation readiness)
    {
        if (readiness.SupportStability == SupportStabilityState.Recovering) return "recovering center";
        if (readiness.SupportStability == SupportStabilityState.Volatile || history.CurrentPeopleState.HasCurrentSupportCrash) return "volatile support";
        if (IsRootedFloorMet(readiness.Rootedness) && IsMovementFloorMet(readiness.MovementCoherence)) return "rooted coherence";
        if (IsMovementFloorMet(readiness.MovementCoherence)) return "mobile coherence";
        if (polity.MigrationPressure >= 0.50 || polity.FragmentationPressure >= 0.50) return "frontier strain";
        return "holding center";
    }

    private CandidateScoreBreakdown ScoreCandidate(
        Polity polity,
        PeopleHistoryWindowSnapshot history,
        CandidateReadinessEvaluation readiness,
        NeighborContextSnapshot? neighborContext,
        IReadOnlyList<RegionEvaluationSnapshot> regionEvaluations)
    {
        double survivalStrength = Clamp01((ScoreSupportState(readiness.SupportStability) * 0.45) + (ScoreDemography(readiness.DemographicViability) * 0.35) + (readiness.CurrentSupportPasses ? 0.20 : 0.0) - (Math.Max(polity.MigrationPressure, polity.FragmentationPressure) * 0.12));
        double continuityDepth = Clamp01((ScoreContinuity(readiness.Continuity) * 0.55) + Math.Min(0.25, polity.YearsSinceFounded / 16.0) + Math.Min(0.20, history.SocialContinuityHistoryRollup.ObservedContinuousIdentityMonths / 24.0));
        double spatialIdentity = Clamp01((ScoreRootedness(readiness.Rootedness) * 0.40) + (ScoreMovement(readiness.MovementCoherence) * 0.28) + (history.CurrentPeopleState.HomeClusterShare * 0.18) + ((regionEvaluations.Count == 0 ? 0.0 : regionEvaluations.Max(region => region.Relative.SupportContributionShare)) * 0.14));
        double agency = Clamp01((readiness.SettlementDurabilityPasses ? 0.28 : 0.08) + (readiness.PoliticalDurabilityPasses ? 0.22 : 0.08) + Math.Min(0.16, polity.SettlementCount / 6.0) + Math.Min(0.16, polity.Discoveries.Count / 6.0) + Math.Min(0.18, polity.Advancements.Count / 5.0));
        double externalEntanglement = Clamp01(((neighborContext?.NeighborhoodSummary.RelevantNeighborCount ?? 0) * 0.12) + ((neighborContext?.NeighborhoodSummary.ExchangeContextNeighborCount ?? 0) * 0.14) + ((neighborContext?.NeighborhoodSummary.PressureNeighborCount ?? 0) * 0.12) + Math.Min(0.14, (neighborContext?.NeighborAggregateMetrics.StrongerNeighborCount ?? 0) * 0.07));
        double strategicOpportunity = Clamp01((history.CurrentPeopleState.HasExpansionOpportunity ? 0.32 : 0.0) + Math.Min(0.24, regionEvaluations.Count(region => region.Relative.RelationshipType == PeopleRegionRelationshipType.AdjacentCandidate && region.Relative.SupportAdequacy >= 0.70) * 0.12) + Math.Min(0.20, (neighborContext?.NeighborhoodSummary.ExchangeContextNeighborCount ?? 0) * 0.10) + Math.Min(0.14, regionEvaluations.Count(region => region.Global.Fertility >= 0.60 && region.Global.WaterAvailability >= 0.55) * 0.07) + ((neighborContext?.NeighborhoodSummary.PressureNeighborCount ?? 0) > 0 ? 0.08 : 0.0));
        double fragilityPenalty = Clamp01((readiness.HasImmediateShock ? 0.34 : 0.0) + (readiness.SupportStability == SupportStabilityState.Volatile ? 0.18 : 0.0) + (readiness.SupportStability == SupportStabilityState.Recovering ? 0.10 : 0.0) + (readiness.PopulationTrend == PopulationTrendState.Declining ? 0.16 : 0.0) + Math.Max(polity.MigrationPressure, polity.FragmentationPressure) * 0.18);
        double total = Clamp01((survivalStrength * _settings.CandidateScoreWeightSurvivalStrength) + (continuityDepth * _settings.CandidateScoreWeightContinuityDepth) + (spatialIdentity * _settings.CandidateScoreWeightSpatialIdentity) + (agency * _settings.CandidateScoreWeightAgencyAndInternalOrganization) + (externalEntanglement * _settings.CandidateScoreWeightExternalEntanglement) + (strategicOpportunity * _settings.CandidateScoreWeightStrategicOpportunity) - (fragilityPenalty * _settings.CandidateScoreWeightFragilityPenalty));
        CandidateScoreTier tier = total switch
        {
            >= 0.78 => CandidateScoreTier.Exceptional,
            >= 0.62 => CandidateScoreTier.Strong,
            >= 0.44 => CandidateScoreTier.Promising,
            _ => CandidateScoreTier.Modest
        };

        return new CandidateScoreBreakdown(
            survivalStrength,
            continuityDepth,
            spatialIdentity,
            agency,
            externalEntanglement,
            strategicOpportunity,
            fragilityPenalty,
            total,
            tier,
            ResolveScoreExplanation(survivalStrength, continuityDepth, spatialIdentity, agency, externalEntanglement, strategicOpportunity, fragilityPenalty));
    }

    private static string ResolveQualificationReason(CandidateViabilityResult viability, CandidateScoreBreakdown? scoreBreakdown, CandidateMaturityBand maturityBand)
    {
        string maturityLabel = maturityBand.ToLowerDisplayLabel();
        if (!viability.IsViable) return viability.PrimaryFailureReason;
        if (!viability.SupportsNormalEntry) return $"Truthful but thin {maturityLabel} start.";

        return scoreBreakdown?.Tier switch
        {
            CandidateScoreTier.Exceptional => $"High-cohesion {maturityLabel} start with strong playability.",
            CandidateScoreTier.Strong => $"Solid {maturityLabel} start with clear internal shape.",
            _ => $"Viable {maturityLabel} start with usable early agency."
        };
    }

    private static string BuildEvidenceSentence(
        Polity polity,
        string subsistenceStyle,
        string homeBucket,
        CandidateMaturityBand maturityBand,
        CandidateReadinessEvaluation readiness,
        SocietalPersistenceTruth socialTruth,
        NeighborContextSnapshot? neighborContext)
    {
        string maturityLabel = maturityBand.ToLowerDisplayLabel();
        string support = readiness.SupportStability switch
        {
            SupportStabilityState.Stable => "stable support",
            SupportStabilityState.Recovering => "recovering support",
            SupportStabilityState.Volatile => "volatile support",
            _ => "collapsed support"
        };
        string provenance = socialTruth.CandidateSocialBackingType switch
        {
            CandidateSocialBackingType.ActiveSocietyBacked => "active-society backing",
            CandidateSocialBackingType.HistoricalLineageOnly => "lineage-carried backing",
            _ => "polity-shell continuity"
        };
        string external = neighborContext is null || neighborContext.NeighborhoodSummary.RelevantNeighborCount == 0
            ? "light external entanglement"
            : $"{neighborContext.NeighborhoodSummary.RelevantNeighborCount} nearby counterpart{(neighborContext.NeighborhoodSummary.RelevantNeighborCount == 1 ? string.Empty : "s")}";
        return $"{polity.Name} is a {maturityLabel} {subsistenceStyle.ToLowerInvariant()} start on {homeBucket.Replace('_', ' ')}, with {support}, {readiness.Continuity.ToString().ToLowerInvariant()} continuity, {provenance}, and {external}.";
    }

    private static IReadOnlyList<string> BuildStrengths(CandidateScoreBreakdown? scoreBreakdown, PeopleHistoryWindowSnapshot history, NeighborContextSnapshot? neighborContext)
    {
        List<string> strengths = [];
        if (scoreBreakdown is not null)
        {
            if (scoreBreakdown.SurvivalStrength >= 0.70) strengths.Add("Reliable current support");
            if (scoreBreakdown.ContinuityDepth >= 0.68) strengths.Add("Established continuity");
            if (scoreBreakdown.SpatialIdentity >= 0.68) strengths.Add("Clear home-ground identity");
            if (scoreBreakdown.AgencyAndInternalOrganization >= 0.64) strengths.Add("Usable internal organization");
            if (scoreBreakdown.ExternalEntanglement >= 0.55) strengths.Add("Meaningful neighboring context");
            if (scoreBreakdown.StrategicOpportunity >= 0.55) strengths.Add("Visible room to maneuver");
        }

        if (history.CurrentPeopleState.HasTradeContact) strengths.Add("Current exchange context");
        if ((neighborContext?.NeighborhoodSummary.ExchangeContextNeighborCount ?? 0) > 0) strengths.Add("Nearby exchange opportunities");
        return strengths.Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
    }

    private static IReadOnlyList<string> BuildWarnings(CandidateViabilityResult viability, PeopleHistoryWindowSnapshot history)
    {
        List<string> warnings = viability.WarningReasons.Select(reason => reason.Replace('_', ' ')).ToList();
        if (!viability.SupportsNormalEntry && viability.IsViable) warnings.Add("Thin durability for a normal stop");
        if (history.CurrentPeopleState.HasCurrentDisplacement) warnings.Add("Recent displacement still matters");
        return warnings.Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
    }

    private static IReadOnlyList<string> BuildRisks(CandidateReadinessEvaluation readiness, PeopleHistoryWindowSnapshot history, NeighborContextSnapshot? neighborContext)
    {
        List<string> risks = [];
        if (readiness.HasImmediateShock) risks.Add("Recent shock may still cascade");
        if (readiness.PopulationTrend == PopulationTrendState.Declining) risks.Add("Population is trending down");
        if (readiness.SupportStability == SupportStabilityState.Volatile) risks.Add("Support remains volatile");
        if ((neighborContext?.NeighborhoodSummary.PressureNeighborCount ?? 0) > 0) risks.Add("Nearby pressure can harden quickly");
        if (history.CurrentPeopleState.PressureNeighborCount > 0) risks.Add("Local frontier pressure is active");
        return risks.Distinct(StringComparer.OrdinalIgnoreCase).Take(3).ToArray();
    }

    private static string ResolvePressureShape(CandidateReadinessEvaluation readiness, Polity polity, NeighborContextSnapshot? neighborContext)
    {
        if (readiness.SupportStability == SupportStabilityState.Recovering) return "recovery";
        if (readiness.SupportStability == SupportStabilityState.Volatile) return "volatility";
        if ((neighborContext?.NeighborhoodSummary.PressureNeighborCount ?? 0) > 0) return "external_pressure";
        return polity.MigrationPressure >= polity.FragmentationPressure ? "migration" : "holding";
    }

    private static string ResolveArchetypeBucket(CandidateMaturityBand maturityBand, string subsistenceStyle)
        => NormalizeBucketKey($"{maturityBand}_{subsistenceStyle}");

    private static string ResolveHomeBucket(Region region)
        => region.Biome switch
        {
            RegionBiome.RiverValley => "river_valley",
            RegionBiome.Coast => "coastal_margin",
            RegionBiome.Wetlands => "wetland_basin",
            RegionBiome.Plains => "open_plains",
            RegionBiome.Forest => "deep_forest",
            RegionBiome.Highlands => "highland_shelf",
            RegionBiome.Mountains => "mountain_edge",
            RegionBiome.Drylands => "dry_frontier",
            _ => "mixed_country"
        };

    private static string ResolveCurrentCondition(CandidateReadinessEvaluation readiness, PeopleHistoryWindowSnapshot history, string stabilityMode)
    {
        if (history.CurrentPeopleState.StarvingSettlementCount > 0) return "Food crisis";
        if (history.CurrentPeopleState.HasCurrentDisplacement) return "Displaced";
        if (readiness.SupportStability == SupportStabilityState.Recovering) return "Recovering";
        return stabilityMode switch
        {
            "rooted coherence" => "Anchored",
            "mobile coherence" => "Mobile",
            "frontier strain" => "Frontier strain",
            "volatile support" => "Volatile",
            _ => "Holding"
        };
    }

    private static string ResolveSettlementProfile(Polity polity, CandidateMaturityBand maturityBand)
        => maturityBand switch
        {
            CandidateMaturityBand.Mobile => polity.SettlementCount == 0 ? "seasonal route" : "mobile hearth cluster",
            CandidateMaturityBand.Anchored => polity.SettlementCount <= 1 ? "single anchored hearth" : "anchored hearth network",
            CandidateMaturityBand.Settling => polity.SettlementCount <= 2 ? "young settlement web" : "growing settlement web",
            CandidateMaturityBand.EmergentPolity => polity.SettlementCount <= 2 ? "organized hearth pair" : "organized settlement web",
            _ => "mixed settlement web"
        };

    private static string ResolveRegionalProfile(Region homeRegion, IReadOnlyList<RegionEvaluationSnapshot> regionEvaluations)
    {
        RegionEvaluationSnapshot? core = regionEvaluations.FirstOrDefault(region => region.Relative.IsCurrentCenterRegion) ?? regionEvaluations.FirstOrDefault();
        string support = core is null
            ? "mixed support"
            : core.Relative.SupportAdequacy >= 0.90
                ? "high local support"
                : core.Relative.SupportAdequacy >= 0.70
                    ? "usable local support"
                    : "thin local support";
        return $"{homeRegion.Biome.ToString().ToLowerInvariant().Replace('_', ' ')} with {support}";
    }

    private static string ResolveLineageProfile(World world, Species species, int lineageId)
    {
        EvolutionaryLineage? lineage = world.GetLineage(lineageId);
        if (lineage is null) return $"{species.EcologyNiche} branch";

        string depth = lineage.AncestryDepth >= 2
            ? "deep descendant branch"
            : lineage.ParentLineageId.HasValue
                ? "younger descendant branch"
                : "root branch";
        string adaptation = string.IsNullOrWhiteSpace(lineage.HabitatAdaptationSummary) ? "mixed adaptation" : lineage.HabitatAdaptationSummary;
        return $"{depth}; {adaptation}";
    }

    private static string ResolveDiscoverySummary(Polity polity)
        => polity.Discoveries.Count == 0 ? "Shared survival lore" : string.Join(", ", polity.Discoveries.Take(2).Select(discovery => discovery.Summary));

    private static string ResolveLearnedSummary(Polity polity)
        => polity.Advancements.Count == 0 ? "None" : string.Join(", ", polity.Advancements.OrderBy(advancement => advancement).Take(2).Select(advancement => AdvancementCatalog.Get(advancement).Name));

    private static string ResolveHistoricalNote(World world, Polity polity)
    {
        return world.CivilizationalHistory
            .Where(evt => evt.PolityId == polity.Id)
            .OrderByDescending(evt => evt.Year)
            .ThenByDescending(evt => evt.Month)
            .Select(evt => evt.Type switch
            {
                CivilizationalHistoryEventType.SettlementFounded => "recently founded a settlement",
                CivilizationalHistoryEventType.MigrationWave => "recently shifted into new ground",
                CivilizationalHistoryEventType.Fragmentation => "descended from an older split",
                CivilizationalHistoryEventType.PolityFormation => "recently consolidated into a polity",
                _ => evt.Summary
            })
            .FirstOrDefault()
            ?? "holds an older local history";
    }

    private static string ResolvePressureOrOpportunity(Polity polity, PeopleHistoryWindowSnapshot history, NeighborContextSnapshot? neighborContext)
    {
        if (history.CurrentPeopleState.HasExpansionOpportunity) return "room to push outward from a real center";
        if ((neighborContext?.NeighborhoodSummary.ExchangeContextNeighborCount ?? 0) > 0) return "exchange edges are already visible";
        if ((neighborContext?.NeighborhoodSummary.PressureNeighborCount ?? 0) > 0) return "neighbor pressure is already shaping choices";
        return string.IsNullOrWhiteSpace(polity.CurrentPressureSummary) ? "holding local continuity" : polity.CurrentPressureSummary;
    }

    private static string ResolveSubsistenceStyle(Polity polity, Species species)
        => PolityProfileResolver.ResolveSubsistenceMode(polity, species) switch
        {
            SubsistenceMode.HuntingFocused => "Hunting-focused",
            SubsistenceMode.ForagingFocused => "Foraging-focused",
            SubsistenceMode.MixedHunterForager => "Mixed hunter-forager",
            SubsistenceMode.ProtoFarming => "Proto-farming",
            SubsistenceMode.FarmingEmergent => "Farming-emergent",
            _ => "Mixed subsistence"
        };

    private static string ResolvePopulationBand(int population)
        => population switch
        {
            < 90 => "Small",
            < 180 => "Modest",
            < 320 => "Established",
            _ => "Large"
        };

    private static CandidateAssessment BuildMissingEvidenceAssessment(World world, Polity polity)
    {
        PlayerEntryCandidateSummary summary = new(
            polity.Id,
            polity.Name,
            polity.SpeciesId,
            world.Species.FirstOrDefault(species => species.Id == polity.SpeciesId)?.Name ?? $"Species {polity.SpeciesId}",
            polity.LineageId,
            polity.RegionId,
            world.Regions[polity.RegionId].Name,
            polity.YearsSinceFounded,
            world.Time.Year,
            polity.SettlementCount,
            ResolvePopulationBand(polity.Population),
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            "Unknown",
            0.0,
            StabilityBand.Fragile,
            polity.IsFallbackCreated,
            false,
            "missing_candidate_readiness_evidence",
            new CandidateViabilityResult(
                false,
                false,
                [new CandidateViabilityGate("observer_evidence", false, "Observer evidence is required", "No observer history window was available.")],
                ["missing_candidate_readiness_evidence"],
                Array.Empty<string>(),
                "missing_candidate_readiness_evidence",
                "Blocked: missing observer evidence."));

        return new CandidateAssessment(summary, new CandidateDiversityProfile(CandidateMaturityBand.Anchored, "unknown", "unknown", "unknown", polity.LineageId, polity.RegionId, "unknown"));
    }

    private static string BuildCandidateOriginReason(Polity polity, SocietalPersistenceTruth socialTruth)
        => polity.IsFallbackCreated
            ? $"fallback polity; {socialTruth.CandidateBackingSummary}"
            : socialTruth.CandidateBackingSummary;

    private static PrehistoryCandidateDiagnostics BuildDiagnostics(
        World world,
        PrehistoryObserverSnapshot observerSnapshot,
        IReadOnlyDictionary<int, CandidateReadinessEvaluation> candidateEvaluations,
        CandidateAssessment assessment)
    {
        PlayerEntryCandidateSummary summary = assessment.Summary;
        Polity polity = world.Polities.First(candidate => candidate.Id == summary.PolityId);
        Species species = world.Species.First(candidate => candidate.Id == polity.SpeciesId);
        PeopleHistoryWindowSnapshot? history = observerSnapshot.PeopleHistoryWindows.FirstOrDefault(snapshot => snapshot.Header.PeopleId == polity.Id);
        CandidateReadinessEvaluation? readiness = candidateEvaluations.TryGetValue(polity.Id, out CandidateReadinessEvaluation? evaluation)
            ? evaluation
            : null;
        EmergingSociety? founderSociety = polity.FounderSocietyId.HasValue
            ? world.Societies.FirstOrDefault(candidate => candidate.Id == polity.FounderSocietyId.Value)
            : null;
        SocietalPersistenceTruth socialTruth = SocietalPersistenceTruthEvaluator.Evaluate(world, polity);

        List<string> failedTruthFloors = [];
        if (summary.Viability is not null)
        {
            failedTruthFloors.AddRange(summary.Viability.Gates.Where(gate => !gate.Passed).Select(gate => gate.Key));
        }

        return new PrehistoryCandidateDiagnostics(
            polity.Id,
            polity.Name,
            species.Id,
            species.Name,
            polity.LineageId,
            polity.RegionId,
            world.Regions[polity.RegionId].Name,
            socialTruth.FounderSocietyId,
            socialTruth.SourceIdentityPath,
            socialTruth.SocietyPersistenceState,
            socialTruth.CandidateSocialBackingType,
            summary.MaturityBand,
            readiness?.SupportStability ?? SupportStabilityState.Collapsed,
            readiness?.DemographicViability ?? DemographicViabilityState.Critical,
            readiness?.PopulationTrend ?? PopulationTrendState.Declining,
            readiness?.MovementCoherence ?? MovementCoherenceState.Scattered,
            readiness?.Rootedness ?? RootednessState.Displaced,
            readiness?.Continuity ?? ContinuityState.Broken,
            polity.YearsSinceFounded,
            socialTruth.ActiveSocietyAgeYears,
            history?.SocialContinuityHistoryRollup.ObservedContinuousIdentityMonths ?? 0,
            history?.SocialContinuityHistoryRollup.MonthsSinceIdentityBreak ?? 0,
            history?.SocialContinuityHistoryRollup.IdentityBreakCountLast12Months ?? 0,
            history?.SocialContinuityHistoryRollup.IdentityBreakCountLast24Months ?? 0,
            history?.SettlementHistoryRollup.SettlementPresentMonthsLast12Months ?? 0,
            history?.SettlementHistoryRollup.EstablishedSettlementMonthsLast12Months ?? 0,
            history?.RootednessHistoryRollup.AnchoredMonthsLast12Months ?? 0,
            history?.RootednessHistoryRollup.StrongAnchoredMonthsLast12Months ?? 0,
            history?.CurrentPeopleState.HomeClusterShare ?? 0.0,
            history?.RootednessHistoryRollup.AverageHomeClusterShareLast12Months ?? 0.0,
            history?.CurrentPeopleState.ConnectedFootprintShare ?? 0.0,
            history?.CurrentPeopleState.RouteCoverageShare ?? 0.0,
            history?.CurrentPeopleState.ScatterShare ?? 1.0,
            readiness?.SettlementDurabilityPasses ?? false,
            readiness?.PoliticalDurabilityPasses ?? false,
            readiness?.HasHardCurrentMonthVeto ?? false,
            readiness?.HardVetoReasons ?? Array.Empty<string>(),
            readiness?.BlockingReasons ?? Array.Empty<string>(),
            readiness?.WarningReasons ?? Array.Empty<string>(),
            failedTruthFloors,
            summary.Viability?.IsViable == true,
            summary.Viability?.SupportsNormalEntry == true)
        {
            SourcePeopleId = polity.Id,
            SourcePolityId = polity.Id,
            SourceSocietyId = socialTruth.FounderSocietyId,
            HistoricalSocietyLineageAgeYears = socialTruth.HistoricalSocietyLineageAgeYears,
            HasActiveSocietySubstrate = socialTruth.HasActiveSocietySubstrate,
            HasHistoricalSocietyLineage = socialTruth.HasHistoricalSocietyLineage,
            PolityBackedByActiveSociety = socialTruth.PolityBackedByActiveSociety,
            CandidateBackedByHistoricalLineageOnly = socialTruth.CandidateBackedByHistoricalLineageOnly,
            PolityOutlivingSocietySubstrate = socialTruth.PolityOutlivingSocietySubstrate,
            PolityShellWithoutSocietySubstrate = socialTruth.PolityShellWithoutSocietySubstrate,
            CandidateBackingSummary = socialTruth.CandidateBackingSummary,
            CurrentFootprintRegionCount = readiness?.CurrentTruthSnapshot?.FootprintRegionCount ?? history?.SpatialHistoryRollup.CurrentOccupiedRegionCount ?? 0,
            CurrentHomeClusterRegionId = readiness?.CurrentTruthSnapshot?.HomeClusterRegionId ?? polity.RegionId,
            CurrentSupportAdequacy = readiness?.CurrentTruthSnapshot?.SupportAdequacy ?? history?.SupportHistoryRollup.CurrentSupportAdequacy ?? 0.0,
            CurrentFootprintSupportRatio = readiness?.CurrentTruthSnapshot?.FootprintSupportRatio ?? 0.0,
            CurrentMonthSupportPasses = readiness?.CurrentTruthSnapshot?.SupportPasses ?? false,
            CurrentMonthCoherent = readiness?.CurrentTruthSnapshot?.IsCoherentMonth ?? false,
            CurrentMonthStrongCoherent = readiness?.CurrentTruthSnapshot?.IsStrongCoherentMonth ?? false,
            CurrentMonthScattered = readiness?.CurrentTruthSnapshot?.IsScatteredMonth ?? false,
            CurrentMonthRooted = readiness?.CurrentTruthSnapshot?.IsRootedMonth ?? false,
            CurrentMonthDeeplyRooted = readiness?.CurrentTruthSnapshot?.IsDeeplyRootedMonth ?? false,
            CurrentMonthCatastrophicScatterVeto = readiness?.CurrentTruthSnapshot?.CatastrophicScatterVeto ?? false,
            SupportRuleResult = readiness?.SupportRuleResult ?? string.Empty,
            MovementRuleResult = readiness?.MovementRuleResult ?? string.Empty,
            RootednessRuleResult = readiness?.RootednessRuleResult ?? string.Empty,
            ContinuityRuleResult = readiness?.ContinuityRuleResult ?? string.Empty,
            Rollup6Months = readiness?.Rolling6Months,
            Rollup12Months = readiness?.Rolling12Months,
            Rollup24Months = readiness?.Rolling24Months,
            BlockerTraces = readiness?.BlockerTraces ?? Array.Empty<CandidateRuleTrace>(),
            FailedDueToCurrentRawState = readiness?.FailedDueToCurrentRawState ?? false,
            FailedDueToRollingHistory = readiness?.FailedDueToRollingHistory ?? false,
            FailedDueToMixedTruthSources = readiness?.FailedDueToMixedTruthSources ?? false
        };
    }

    private static PrehistoryCandidateDiagnosticsSummary SummarizeDiagnostics(
        IReadOnlyList<PrehistoryCandidateDiagnostics> diagnostics,
        IReadOnlyDictionary<int, string> rejectionReasons)
    {
        Dictionary<string, int> rejectionCounts = rejectionReasons.Values
            .GroupBy(reason => reason, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> domainCounts = new(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, int> sourceCounts = diagnostics
            .GroupBy(diagnostic => diagnostic.SourceIdentityPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        foreach (PrehistoryCandidateDiagnostics diagnostic in diagnostics)
        {
            if (diagnostic.IsViable)
            {
                continue;
            }

            Increment(domainCounts, ResolveFailureDomain(diagnostic));
        }

        return new PrehistoryCandidateDiagnosticsSummary(rejectionCounts, domainCounts, sourceCounts)
        {
            PassedTruthFloorButRejectedLaterCount = diagnostics.Count(diagnostic => diagnostic.TruthFloorPassed && !diagnostic.IsViable),
            FailedDueToCurrentMonthCount = diagnostics.Count(diagnostic => diagnostic.FailedDueToCurrentRawState && !diagnostic.FailedDueToRollingHistory),
            FailedDueToRollingHistoryCount = diagnostics.Count(diagnostic => diagnostic.FailedDueToRollingHistory && !diagnostic.FailedDueToCurrentRawState),
            FailedDueToMixedTruthSourcesCount = diagnostics.Count(diagnostic => diagnostic.FailedDueToMixedTruthSources)
        };
    }

    private static string ResolveFailureDomain(PrehistoryCandidateDiagnostics diagnostic)
    {
        if (diagnostic.HasHardCurrentMonthVeto || diagnostic.HardVetoReasons.Count > 0) return "continuity";
        if (!diagnostic.SettlementDurabilityPasses) return "settlement_persistence";
        if (!diagnostic.PoliticalDurabilityPasses) return "society_persistence";
        if (diagnostic.BlockingReasons.Any(reason => reason.Contains("support", StringComparison.OrdinalIgnoreCase))) return "support";
        if (diagnostic.BlockingReasons.Any(reason => reason.Contains("continuity", StringComparison.OrdinalIgnoreCase))) return "continuity";
        if (diagnostic.BlockingReasons.Any(reason => reason.Contains("root", StringComparison.OrdinalIgnoreCase) || reason.Contains("movement", StringComparison.OrdinalIgnoreCase))) return "rootedness";
        if (diagnostic.FailedTruthFloors.Any(reason => reason.Contains("continuity", StringComparison.OrdinalIgnoreCase))) return "continuity";
        if (diagnostic.FailedTruthFloors.Any(reason => reason.Contains("movement", StringComparison.OrdinalIgnoreCase) || reason.Contains("root", StringComparison.OrdinalIgnoreCase))) return "rootedness";
        return "other";
    }

    private static void Increment(IDictionary<string, int> counts, string key)
        => counts[key] = counts.TryGetValue(key, out int current) ? current + 1 : 1;

    private static double ScoreSupportState(SupportStabilityState state) => state switch
    {
        SupportStabilityState.Stable => 0.95,
        SupportStabilityState.Recovering => 0.80,
        SupportStabilityState.Volatile => 0.52,
        _ => 0.12
    };

    private static double ScoreDemography(DemographicViabilityState state) => state switch
    {
        DemographicViabilityState.Strong => 0.94,
        DemographicViabilityState.Viable => 0.74,
        DemographicViabilityState.Fragile => 0.44,
        _ => 0.16
    };

    private static double ScoreContinuity(ContinuityState state) => state switch
    {
        ContinuityState.Deep => 0.96,
        ContinuityState.Established => 0.82,
        ContinuityState.Fragile => 0.48,
        ContinuityState.New => 0.28,
        _ => 0.08
    };

    private static double ScoreRootedness(RootednessState state) => state switch
    {
        RootednessState.DeeplyRooted => 0.96,
        RootednessState.Rooted => 0.80,
        RootednessState.SoftAnchored => 0.48,
        _ => 0.12
    };

    private static double ScoreMovement(MovementCoherenceState state) => state switch
    {
        MovementCoherenceState.Strong => 0.96,
        MovementCoherenceState.Coherent => 0.82,
        MovementCoherenceState.Mixed => 0.52,
        _ => 0.16
    };

    private static string ResolveScoreExplanation(double survival, double continuity, double spatial, double agency, double external, double strategic, double fragility)
    {
        Dictionary<string, double> dimensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ["survival strength"] = survival,
            ["continuity depth"] = continuity,
            ["spatial identity"] = spatial,
            ["internal organization"] = agency,
            ["external entanglement"] = external,
            ["strategic opportunity"] = strategic
        };
        string strongest = dimensions.OrderByDescending(entry => entry.Value).First().Key;
        string weakest = dimensions.OrderBy(entry => entry.Value).First().Key;
        return fragility >= 0.28
            ? $"Runs on {strongest}, but fragility still drags on {weakest}."
            : $"Runs on {strongest}, with no major drag beyond {weakest}.";
    }

    private static string NormalizeBucketKey(string value)
        => value.Trim().ToLowerInvariant().Replace(' ', '_').Replace('-', '_');

    private static double Clamp01(double value)
        => Math.Clamp(value, 0.0, 1.0);

    private static bool IsMovementFloorMet(MovementCoherenceState state)
        => state is MovementCoherenceState.Strong or MovementCoherenceState.Coherent;

    private static bool IsRootedFloorMet(RootednessState state)
        => state is RootednessState.DeeplyRooted or RootednessState.Rooted;
}

public sealed record PrehistoryCandidateSelectionResult(
    IReadOnlyList<PlayerEntryCandidateSummary> Candidates,
    IReadOnlyList<PlayerEntryCandidateSummary> AllViableCandidates,
    IReadOnlyDictionary<int, string> RejectionReasons,
    IReadOnlyList<PrehistoryCandidateDiagnostics> Diagnostics,
    PrehistoryCandidateDiagnosticsSummary DiagnosticsSummary);

internal sealed record CandidateAssessment(
    PlayerEntryCandidateSummary Summary,
    CandidateDiversityProfile DiversityProfile);

internal sealed record CandidateDiversityProfile(
    CandidateMaturityBand MaturityBand,
    string ArchetypeBucket,
    string HomeBucket,
    string StabilityBucket,
    int LineageId,
    int HomeRegionId,
    string PressureShape);

internal sealed record CandidatePoolCompositionResult(
    IReadOnlyList<CandidateAssessment> SelectedCandidates,
    IReadOnlyDictionary<int, string> SuppressionReasons);
