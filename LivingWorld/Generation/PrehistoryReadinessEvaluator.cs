using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Generation;

public enum DemographicViabilityState
{
    Critical,
    Fragile,
    Viable,
    Strong
}

public enum PopulationTrendState
{
    Declining,
    Flat,
    Improving
}

public sealed record CandidateReadinessEvaluation(
    int PolityId,
    string PolityName,
    bool IsViable,
    bool SupportsNormalEntry,
    bool CurrentSupportPasses,
    SupportStabilityState SupportStability,
    DemographicViabilityState DemographicViability,
    PopulationTrendState PopulationTrend,
    MovementCoherenceState MovementCoherence,
    RootednessState Rootedness,
    ContinuityState Continuity,
    bool SettlementDurabilityPasses,
    bool PoliticalDurabilityPasses,
    bool HasImmediateShock,
    bool HasHardCurrentMonthVeto,
    IReadOnlyList<string> HardVetoReasons,
    IReadOnlyList<string> BlockingReasons,
    IReadOnlyList<string> WarningReasons,
    string Summary)
{
    public string PrimaryBlockingReason
        => HardVetoReasons.FirstOrDefault()
            ?? BlockingReasons.FirstOrDefault()
            ?? "candidate_not_ready";
}

public sealed class PrehistoryReadinessEvaluation
{
    public required WorldReadinessReport Report { get; init; }
    public required IReadOnlyDictionary<int, CandidateReadinessEvaluation> CandidateEvaluations { get; init; }
}

public sealed class PrehistoryReadinessEvaluator
{
    private readonly WorldGenerationSettings _settings;

    public PrehistoryReadinessEvaluator(WorldGenerationSettings settings)
    {
        _settings = settings;
    }

    public PrehistoryReadinessEvaluation Evaluate(
        World world,
        PrehistoryObserverSnapshot observerSnapshot,
        IReadOnlyDictionary<int, CandidateReadinessEvaluation> candidateEvaluations,
        IReadOnlyList<PlayerEntryCandidateSummary> surfacedCandidates)
    {
        WorldAgeGateReport ageGate = BuildAgeGate(world);
        CandidatePoolReadinessSummary candidatePool = BuildCandidatePoolSummary(surfacedCandidates);

        WorldReadinessCategoryReport biological = EvaluateBiological(world);
        WorldReadinessCategoryReport social = EvaluateSocialEmergence(world, ageGate);
        WorldReadinessCategoryReport worldStructure = EvaluateWorldStructure(world, observerSnapshot, candidateEvaluations, ageGate);
        WorldReadinessCategoryReport candidate = EvaluateCandidateReadiness(candidateEvaluations, surfacedCandidates, ageGate);
        WorldReadinessCategoryReport variety = EvaluateVariety(surfacedCandidates, ageGate);
        WorldReadinessCategoryReport agency = EvaluateAgency(world, observerSnapshot, candidateEvaluations, surfacedCandidates, ageGate);

        IReadOnlyList<WorldReadinessCategoryReport> categories =
        [
            biological,
            social,
            worldStructure,
            candidate,
            variety,
            agency
        ];

        List<string> blockers = categories.SelectMany(report => report.Blockers).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        List<string> warnings = categories.SelectMany(report => report.Warnings).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        bool isThinWorld = candidatePool.IsThinWorld || surfacedCandidates.Count < Math.Max(2, _settings.MinimumViablePlayerEntryCandidates);
        bool isWeakWorld = isThinWorld || categories.Any(report => report.Status != ReadinessAssessmentStatus.Pass);

        if (isThinWorld)
        {
            warnings.Add("thin_world_candidate_pool");
        }

        if (isWeakWorld && !warnings.Contains("weak_world_readiness", StringComparer.OrdinalIgnoreCase))
        {
            warnings.Add("weak_world_readiness");
        }

        PrehistoryCheckpointOutcomeKind resolution = ResolveFinalOutcome(ageGate, biological, social, worldStructure, candidate, variety, agency, candidatePool);
        WorldReadinessSummaryData summaryData = BuildSummaryData(resolution, candidatePool, categories, ageGate, isWeakWorld, isThinWorld);

        return new PrehistoryReadinessEvaluation
        {
            Report = new WorldReadinessReport(
                ageGate,
                resolution,
                categories,
                candidatePool,
                blockers,
                warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                isWeakWorld,
                isThinWorld,
                summaryData),
            CandidateEvaluations = candidateEvaluations
        };
    }

    public IReadOnlyDictionary<int, CandidateReadinessEvaluation> EvaluateCandidateReadiness(World world, PrehistoryObserverSnapshot observerSnapshot)
    {
        Dictionary<int, CandidateReadinessEvaluation> evaluations = new();
        foreach (PeopleHistoryWindowSnapshot historyWindow in observerSnapshot.PeopleHistoryWindows)
        {
            Polity? polity = world.Polities.FirstOrDefault(candidate => candidate.Id == historyWindow.Header.PeopleId && candidate.Population > 0);
            if (polity is null)
            {
                continue;
            }

            IReadOnlyList<PeopleMonthlySnapshot> rawHistory = world.PrehistoryObserver.GetPeopleHistory(polity.Id);
            evaluations[polity.Id] = PrehistoryReadinessEvidenceEvaluator.EvaluateCandidate(
                polity,
                historyWindow,
                rawHistory,
                _settings.CandidateMinimumPopulation,
                _settings.CandidateMinimumPolityAgeYears);
        }

        return evaluations;
    }

    private WorldAgeGateReport BuildAgeGate(World world)
    {
        StartupWorldAgeConfiguration configuration = world.StartupAgeConfiguration;
        PrehistoryAgeGateStatus status = world.Time.Year >= configuration.MaxPrehistoryYears
            ? PrehistoryAgeGateStatus.MaximumAgeReached
            : world.Time.Year >= configuration.TargetPrehistoryYears
                ? PrehistoryAgeGateStatus.TargetAgeReached
                : world.Time.Year >= configuration.MinPrehistoryYears
                    ? PrehistoryAgeGateStatus.MinimumAgeReached
                    : PrehistoryAgeGateStatus.BeforeMinimumAge;

        return new WorldAgeGateReport(
            world.Time.Year,
            configuration.MinPrehistoryYears,
            configuration.TargetPrehistoryYears,
            configuration.MaxPrehistoryYears,
            status);
    }

    private CandidatePoolReadinessSummary BuildCandidatePoolSummary(IReadOnlyList<PlayerEntryCandidateSummary> surfacedCandidates)
    {
        int fallbackCount = surfacedCandidates.Count(candidate => candidate.IsFallbackCandidate);
        int organicCount = surfacedCandidates.Count - fallbackCount;
        int distinctSpecies = surfacedCandidates.Select(candidate => candidate.SpeciesId).Distinct().Count();
        int distinctLineages = surfacedCandidates.Select(candidate => candidate.LineageId).Distinct().Count();
        int distinctRegions = surfacedCandidates.Select(candidate => candidate.HomeRegionId).Distinct().Count();
        int distinctSubsistenceStyles = surfacedCandidates
            .Select(candidate => candidate.SubsistenceStyle)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        bool thin = surfacedCandidates.Count < _settings.MinimumViablePlayerEntryCandidates
            || distinctRegions < 2
            || distinctLineages < 2;

        return new CandidatePoolReadinessSummary(
            surfacedCandidates.Count,
            surfacedCandidates.Count,
            surfacedCandidates.Count(candidate => !candidate.IsFallbackCandidate || !candidate.IsEmergencyAdmitted),
            organicCount,
            fallbackCount,
            distinctSpecies,
            distinctLineages,
            distinctRegions,
            distinctSubsistenceStyles,
            thin,
            surfacedCandidates.Count == 0
                ? "No viable starts surfaced."
                : $"{surfacedCandidates.Count} viable starts across {Math.Max(1, distinctRegions)} regions.");
    }

    private WorldReadinessCategoryReport EvaluateBiological(World world)
    {
        List<string> blockers = [];
        List<string> warnings = [];

        bool catastrophic = world.PhaseAReadinessReport.ProducerCoverage < 0.70
            || world.PhaseAReadinessReport.ConsumerCoverage < 0.35
            || world.PhaseBReadinessReport.SentienceCapableLineageCount == 0
            || world.PhaseBReadinessReport.MatureLineageCount == 0;
        bool pass = world.PhaseAReadinessReport.IsReady && world.PhaseBReadinessReport.IsReady;

        if (catastrophic)
        {
            blockers.Add("biological_foundation_below_truth_floor");
        }
        else if (!pass)
        {
            warnings.Add("biological_history_still_maturing");
        }

        return CreateCategory(
            WorldReadinessCategoryKind.BiologicalReadiness,
            ReadinessCategoryStrictness.Medium,
            blockers,
            warnings,
            pass ? "Biological history is deep enough to support truthful entry." : "Biological history is still maturing.");
    }

    private WorldReadinessCategoryReport EvaluateSocialEmergence(World world, WorldAgeGateReport ageGate)
    {
        List<string> blockers = [];
        List<string> warnings = [];

        if (world.PhaseCReadinessReport.OrganicPersistentSocietyCount == 0)
        {
            blockers.Add("no_organic_persistent_societies");
        }

        if (world.PhaseCReadinessReport.OrganicSettlementCount < _settings.MinimumPhaseCViableSettlementCount)
        {
            if (ageGate.TargetAgeReached)
            {
                warnings.Add("settlement_depth_still_thin");
            }
            else
            {
                blockers.Add("settlement_depth_not_ready");
            }
        }

        if (world.PhaseCReadinessReport.OrganicPolityCount == 0)
        {
            blockers.Add("no_organic_polities");
        }
        else if (!world.PhaseCReadinessReport.IsReady)
        {
            warnings.Add("social_emergence_not_fully_mature");
        }

        return CreateCategory(
            WorldReadinessCategoryKind.SocialEmergenceReadiness,
            ReadinessCategoryStrictness.Strict,
            blockers,
            warnings,
            blockers.Count == 0
                ? "Social emergence has produced real durable starts."
                : "Social emergence has not yet produced enough durable starts.");
    }

    private WorldReadinessCategoryReport EvaluateWorldStructure(
        World world,
        PrehistoryObserverSnapshot observerSnapshot,
        IReadOnlyDictionary<int, CandidateReadinessEvaluation> candidateEvaluations,
        WorldAgeGateReport ageGate)
    {
        List<string> blockers = [];
        List<string> warnings = [];

        int durableSettlementCount = candidateEvaluations.Values.Count(evaluation => evaluation.SettlementDurabilityPasses);
        int durablePoliticalCount = candidateEvaluations.Values.Count(evaluation => evaluation.PoliticalDurabilityPasses);
        int connectedNeighborCount = observerSnapshot.NeighborContexts.Count(snapshot => snapshot.NeighborhoodSummary.RelevantNeighborCount > 0);

        if (durableSettlementCount == 0)
        {
            blockers.Add("no_durable_settlement_persistence");
        }

        if (durablePoliticalCount == 0)
        {
            if (ageGate.TargetAgeReached)
            {
                warnings.Add("political_durability_still_thin");
            }
            else
            {
                blockers.Add("political_durability_not_ready");
            }
        }

        if (connectedNeighborCount == 0)
        {
            warnings.Add("world_structure_has_minimal_entanglement");
        }

        return CreateCategory(
            WorldReadinessCategoryKind.WorldStructureReadiness,
            ReadinessCategoryStrictness.Strict,
            blockers,
            warnings,
            blockers.Count == 0
                ? "World structure is durable enough for truthful focal selection."
                : "World structure is still too thin for normal entry.");
    }

    private WorldReadinessCategoryReport EvaluateCandidateReadiness(
        IReadOnlyDictionary<int, CandidateReadinessEvaluation> candidateEvaluations,
        IReadOnlyList<PlayerEntryCandidateSummary> surfacedCandidates,
        WorldAgeGateReport ageGate)
    {
        List<string> blockers = [];
        List<string> warnings = [];

        int viableCount = surfacedCandidates.Count;
        int normalReadyCount = surfacedCandidates.Count(candidate =>
            candidateEvaluations.TryGetValue(candidate.PolityId, out CandidateReadinessEvaluation? evaluation)
            && evaluation.SupportsNormalEntry);

        if (viableCount == 0)
        {
            blockers.Add("no_viable_candidates");
        }
        else if (normalReadyCount == 0)
        {
            if (ageGate.MaximumAgeReached)
            {
                warnings.Add("candidate_pool_viable_but_not_normal_ready");
            }
            else
            {
                blockers.Add("candidate_pool_not_yet_ready");
            }
        }
        else if (viableCount < _settings.MinimumViablePlayerEntryCandidates)
        {
            warnings.Add("candidate_pool_viable_but_thin");
        }

        if (candidateEvaluations.Values.Any(evaluation => evaluation.HasHardCurrentMonthVeto))
        {
            warnings.Add("some_candidates_failed_current_month_truth_floors");
        }

        return CreateCategory(
            WorldReadinessCategoryKind.CandidateReadiness,
            ReadinessCategoryStrictness.Strict,
            blockers,
            warnings,
            blockers.Count == 0
                ? "At least one truthful candidate pool exists."
                : "Candidate readiness is blocked by current truth floors.");
    }

    private WorldReadinessCategoryReport EvaluateVariety(IReadOnlyList<PlayerEntryCandidateSummary> surfacedCandidates, WorldAgeGateReport ageGate)
    {
        List<string> blockers = [];
        List<string> warnings = [];

        int distinctRegions = surfacedCandidates.Select(candidate => candidate.HomeRegionId).Distinct().Count();
        int distinctLineages = surfacedCandidates.Select(candidate => candidate.LineageId).Distinct().Count();
        int distinctStyles = surfacedCandidates.Select(candidate => candidate.SubsistenceStyle).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        bool varietyThin = surfacedCandidates.Count <= 1 || distinctRegions <= 1 || distinctLineages <= 1 || distinctStyles <= 1;

        if (varietyThin)
        {
            if (ageGate.TargetAgeReached)
            {
                warnings.Add("candidate_variety_is_thin");
            }
            else
            {
                blockers.Add("candidate_variety_not_yet_acceptable");
            }
        }

        return CreateCategory(
            WorldReadinessCategoryKind.VarietyReadiness,
            ReadinessCategoryStrictness.Soft,
            blockers,
            warnings,
            blockers.Count == 0
                ? "Candidate variety is acceptable for startup."
                : "Candidate variety is still too narrow.");
    }

    private WorldReadinessCategoryReport EvaluateAgency(
        World world,
        PrehistoryObserverSnapshot observerSnapshot,
        IReadOnlyDictionary<int, CandidateReadinessEvaluation> candidateEvaluations,
        IReadOnlyList<PlayerEntryCandidateSummary> surfacedCandidates,
        WorldAgeGateReport ageGate)
    {
        List<string> blockers = [];
        List<string> warnings = [];

        int agencyReadyCount = surfacedCandidates.Count(candidate =>
        {
            if (!candidateEvaluations.TryGetValue(candidate.PolityId, out CandidateReadinessEvaluation? evaluation))
            {
                return false;
            }

            NeighborContextSnapshot? neighborContext = observerSnapshot.NeighborContexts.FirstOrDefault(snapshot => snapshot.PeopleId == candidate.PolityId);
            Polity? polity = world.Polities.FirstOrDefault(entry => entry.Id == candidate.PolityId);
            bool hasExternalContext = neighborContext is not null && neighborContext.NeighborhoodSummary.RelevantNeighborCount > 0;
            bool hasInternalAgency = polity is not null
                && (polity.Advancements.Count > 0
                    || polity.Discoveries.Count >= 3
                    || polity.SettlementCount >= 2
                    || polity.TradePartnersThisMonth.Count > 0);
            return evaluation.SupportsNormalEntry && (hasExternalContext || hasInternalAgency);
        });

        if (agencyReadyCount == 0)
        {
            if (ageGate.TargetAgeReached)
            {
                warnings.Add("agency_is_present_but_still_thin");
            }
            else
            {
                blockers.Add("agency_not_yet_acceptable");
            }
        }

        return CreateCategory(
            WorldReadinessCategoryKind.AgencyReadiness,
            ReadinessCategoryStrictness.Soft,
            blockers,
            warnings,
            blockers.Count == 0
                ? "The surfaced starts show enough agency to begin play."
                : "The surfaced starts still lack enough agency for a normal stop.");
    }

    private static WorldReadinessCategoryReport CreateCategory(
        WorldReadinessCategoryKind kind,
        ReadinessCategoryStrictness strictness,
        IReadOnlyList<string> blockers,
        IReadOnlyList<string> warnings,
        string summary)
    {
        ReadinessAssessmentStatus status = blockers.Count > 0
            ? ReadinessAssessmentStatus.Blocker
            : warnings.Count > 0
                ? ReadinessAssessmentStatus.Warning
                : ReadinessAssessmentStatus.Pass;
        return new WorldReadinessCategoryReport(kind, status, strictness, summary, blockers, warnings);
    }

    private static PrehistoryCheckpointOutcomeKind ResolveFinalOutcome(
        WorldAgeGateReport ageGate,
        WorldReadinessCategoryReport biological,
        WorldReadinessCategoryReport social,
        WorldReadinessCategoryReport worldStructure,
        WorldReadinessCategoryReport candidate,
        WorldReadinessCategoryReport variety,
        WorldReadinessCategoryReport agency,
        CandidatePoolReadinessSummary candidatePool)
    {
        if (!ageGate.MinimumAgeReached)
        {
            return PrehistoryCheckpointOutcomeKind.ContinuePrehistory;
        }

        bool strictPass = candidate.IsPass && social.IsPass && worldStructure.IsPass;
        bool mediumPass = biological.Status != ReadinessAssessmentStatus.Blocker;
        bool softAcceptable = variety.Status != ReadinessAssessmentStatus.Blocker && agency.Status != ReadinessAssessmentStatus.Blocker;
        bool normalStop = strictPass && mediumPass && softAcceptable;

        if (normalStop)
        {
            return PrehistoryCheckpointOutcomeKind.EnterFocalSelection;
        }

        if (!ageGate.MaximumAgeReached)
        {
            return PrehistoryCheckpointOutcomeKind.ContinuePrehistory;
        }

        return candidatePool.ViableCandidateCount == 0
            ? PrehistoryCheckpointOutcomeKind.GenerationFailure
            : PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection;
    }

    private static WorldReadinessSummaryData BuildSummaryData(
        PrehistoryCheckpointOutcomeKind resolution,
        CandidatePoolReadinessSummary candidatePool,
        IReadOnlyList<WorldReadinessCategoryReport> categories,
        WorldAgeGateReport ageGate,
        bool isWeakWorld,
        bool isThinWorld)
    {
        int passing = categories.Count(category => category.Status == ReadinessAssessmentStatus.Pass);
        int warnings = categories.Count(category => category.Status == ReadinessAssessmentStatus.Warning);
        int blockers = categories.Count(category => category.Status == ReadinessAssessmentStatus.Blocker);

        string headline = resolution switch
        {
            PrehistoryCheckpointOutcomeKind.EnterFocalSelection => "World ready for focal selection.",
            PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection => "Maximum age reached; forcing entry with the best real starts.",
            PrehistoryCheckpointOutcomeKind.GenerationFailure => "Maximum age reached without any viable starts.",
            _ when !ageGate.MinimumAgeReached => "Minimum prehistory age not reached yet.",
            _ => "World not ready yet; prehistory continues."
        };
        string candidateHeadline = candidatePool.ViableCandidateCount switch
        {
            0 => "0 viable starts",
            1 => "1 viable start",
            _ => $"{candidatePool.ViableCandidateCount} viable starts"
        };
        string worldCondition = resolution switch
        {
            PrehistoryCheckpointOutcomeKind.GenerationFailure => "No truthful entry exists at max age.",
            PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection when isThinWorld => "Late thin world; forcing only the best real starts.",
            PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection when isWeakWorld => "Late weak world; forcing viable starts without inventing strength.",
            PrehistoryCheckpointOutcomeKind.EnterFocalSelection => "Readiness gates passed for a truthful normal stop.",
            _ when isWeakWorld => "Readiness remains weak or incomplete.",
            _ => "Readiness remains incomplete."
        };

        return new WorldReadinessSummaryData(headline, candidateHeadline, worldCondition, passing, warnings, blockers);
    }
}

public static class PrehistoryReadinessEvidenceEvaluator
{
    public static EvaluatorHealthSummary EvaluateHealth(
        PeopleMonthlySnapshot current,
        IReadOnlyList<PeopleMonthlySnapshot> last3,
        IReadOnlyList<PeopleMonthlySnapshot> last6,
        IReadOnlyList<PeopleMonthlySnapshot> last12,
        IReadOnlyList<PeopleMonthlySnapshot> last24,
        DemographyHistoryRollup demography,
        SupportHistoryRollup support,
        SpatialHistoryRollup spatial,
        RootednessHistoryRollup rootedness,
        SocialContinuityHistoryRollup continuity)
    {
        int supportedMonthsLast6 = last6.Count(IsSupportedMonth);
        int supportedMonthsLast12 = last12.Count(IsSupportedMonth);
        int severeUnsupportedMonthsLast6 = last6.Count(IsSevereUnsupportedMonth);
        int severeUnsupportedMonthsLast12 = last12.Count(IsSevereUnsupportedMonth);
        DemographicViabilityState demographicViability = ClassifyDemographicViability(current, demography, 90);
        PopulationTrendState populationTrend = ClassifyPopulationTrend(last6, last12, demography);
        bool currentSupported = IsSupportedMonth(current);
        bool stable = currentSupported
            && supportedMonthsLast6 >= 5
            && supportedMonthsLast12 >= 9
            && severeUnsupportedMonthsLast6 <= 1
            && severeUnsupportedMonthsLast12 <= 2
            && demographicViability >= DemographicViabilityState.Viable
            && CountDeclineStreak(last12) < 4
            && !last3.Any(HasMajorShock);
        bool recovering = !stable
            && last12.Any(HasMajorShock)
            && currentSupported
            && !IsSevereUnsupportedMonth(current)
            && IsSupportTrendImproving(last6, support)
            && populationTrend == PopulationTrendState.Improving
            && HasSpatialRestabilization(last6);
        SupportStabilityState supportState = IsSevereUnsupportedMonth(current) || demographicViability == DemographicViabilityState.Critical
            ? SupportStabilityState.Collapsed
            : stable
                ? SupportStabilityState.Stable
                : recovering
                    ? SupportStabilityState.Recovering
                    : SupportStabilityState.Volatile;

        double footprintSupportRatio = current.OccupiedRegionIds.Count == 0
            ? current.SupportAdequacy
            : current.SupportAdequacy / current.OccupiedRegionIds.Count;
        int coherentMonthsLast6 = last6.Count(IsCoherentMonth);
        int coherentMonthsLast12 = last12.Count(IsCoherentMonth);
        int strongMonthsLast12 = last12.Count(IsStrongCoherentMonth);
        int scatteredMonthsLast6 = last6.Count(IsScatteredMonth);
        int scatteredMonthsLast12 = last12.Count(IsScatteredMonth);
        MovementCoherenceState movementState = IsStrongCoherentMonth(current) && strongMonthsLast12 >= 6 && scatteredMonthsLast12 <= 1
            ? MovementCoherenceState.Strong
            : IsCoherentMonth(current) && coherentMonthsLast6 >= 4 && scatteredMonthsLast6 <= 1
                ? MovementCoherenceState.Coherent
                : IsScatteredMonth(current) || scatteredMonthsLast6 >= 3
                    ? MovementCoherenceState.Scattered
                    : MovementCoherenceState.Mixed;

        int rootedMonthsLast12 = last12.Count(IsRootedMonth);
        int deeplyRootedMonthsLast12 = last12.Count(IsDeeplyRootedMonth);
        bool recoveringFromRecentDisplacement = !current.DisplacementThisMonth
            && IsRootedMonth(current)
            && last6.Any(snapshot => snapshot.DisplacementThisMonth)
            && last3.All(snapshot => !snapshot.DisplacementThisMonth);
        RootednessState rootednessState = current.DisplacementThisMonth
            || (last6.Count(snapshot => snapshot.DisplacementThisMonth) >= 2 && rootedMonthsLast12 < 4)
            ? RootednessState.Displaced
            : IsDeeplyRootedMonth(current) && deeplyRootedMonthsLast12 >= 6 && rootedness.DisplacementMonthsLast12Months == 0
                ? RootednessState.DeeplyRooted
                : IsRootedMonth(current) && rootedMonthsLast12 >= 6
                    ? RootednessState.Rooted
                    : RootednessState.SoftAnchored;

        ContinuityState continuityState = current.ActiveIdentityBreakNow
            ? ContinuityState.Broken
            : continuity.ObservedContinuousIdentityMonths < 6
                ? ContinuityState.New
                : continuity.ObservedContinuousIdentityMonths >= 24
                    && continuity.IdentityBreakCountLast24Months == 0
                    && continuity.MonthsSinceIdentityBreak >= 24
                    ? ContinuityState.Deep
                    : continuity.ObservedContinuousIdentityMonths >= 12
                        && continuity.IdentityBreakCountLast12Months == 0
                        && continuity.MonthsSinceIdentityBreak >= 12
                        ? ContinuityState.Established
                        : ContinuityState.Fragile;

        return new EvaluatorHealthSummary(
            new DemographicHealthSummary(
                demography.CurrentPopulation,
                demography.AveragePopulationLast6Months,
                demography.AveragePopulationLast12Months,
                demography.DeclineMonthsLast12Months,
                demography.MinimumPopulationLast12Months,
                last12.Count(snapshot => snapshot.StarvingSettlementCount > 0)),
            new SupportStabilityHealth(
                supportState,
                current.SupportAdequacy,
                support.AverageSupportAdequacyLast6Months,
                support.AverageSupportAdequacyLast12Months,
                support.AverageFoodSatisfactionLast12Months,
                support.ShortageMonthsLast6Months,
                support.ShortageMonthsLast12Months,
                current.SupportCrashThisMonth,
                support.SupportCrashMonthsLast6Months,
                support.SupportCrashMonthsLast12Months,
                recovering),
            new MovementCoherenceHealth(
                movementState,
                current.ConnectedFootprintShare,
                current.RouteCoverageShare,
                current.ScatterShare,
                footprintSupportRatio,
                spatial.AverageRouteCoverageShareLast6Months,
                spatial.AverageRouteCoverageShareLast12Months,
                coherentMonthsLast6,
                coherentMonthsLast12,
                scatteredMonthsLast6,
                scatteredMonthsLast12),
            new RootednessHealth(
                rootednessState,
                rootedMonthsLast12,
                deeplyRootedMonthsLast12,
                rootedness.AverageHomeClusterShareLast12Months,
                rootedness.EstablishedSettlementMonthsLast12Months,
                current.DisplacementThisMonth,
                rootedness.DisplacementMonthsLast6Months,
                rootedness.DisplacementMonthsLast12Months,
                recoveringFromRecentDisplacement),
            new ContinuityHealth(
                continuityState,
                continuity.ObservedContinuousIdentityMonths,
                continuity.MonthsSinceIdentityBreak,
                continuity.IdentityBreakCountLast6Months,
                continuity.IdentityBreakCountLast12Months,
                continuity.IdentityBreakCountLast24Months,
                continuity.ActiveIdentityBreakNow));
    }

    public static CandidateReadinessEvaluation EvaluateCandidate(
        Polity polity,
        PeopleHistoryWindowSnapshot historyWindow,
        IReadOnlyList<PeopleMonthlySnapshot> rawHistory,
        int minimumPopulation,
        int minimumPolityAgeYears)
    {
        IReadOnlyList<PeopleMonthlySnapshot> last3 = SelectRecent(rawHistory, 3);
        IReadOnlyList<PeopleMonthlySnapshot> last6 = SelectRecent(rawHistory, 6);
        IReadOnlyList<PeopleMonthlySnapshot> last12 = SelectRecent(rawHistory, 12);
        IReadOnlyList<PeopleMonthlySnapshot> last24 = SelectRecent(rawHistory, 24);
        PeopleMonthlySnapshot current = rawHistory.Count > 0
            ? rawHistory[^1]
            : CreateSyntheticCurrent(historyWindow);

        EvaluatorHealthSummary health = EvaluateHealth(
            current,
            last3,
            last6,
            last12,
            last24,
            historyWindow.DemographyHistoryRollup,
            historyWindow.SupportHistoryRollup,
            historyWindow.SpatialHistoryRollup,
            historyWindow.RootednessHistoryRollup,
            historyWindow.SocialContinuityHistoryRollup);

        DemographicViabilityState demographicViability = ClassifyDemographicViability(current, historyWindow.DemographyHistoryRollup, minimumPopulation);
        PopulationTrendState populationTrend = ClassifyPopulationTrend(last6, last12, historyWindow.DemographyHistoryRollup);
        bool currentSupportPasses = IsSupportedMonth(current);
        bool sparseHistory = rawHistory.Count < 6;
        bool settlementDurabilityPasses = historyWindow.SettlementHistoryRollup.SettlementPresentMonthsLast12Months >= 6
            && historyWindow.SettlementHistoryRollup.EstablishedSettlementMonthsLast12Months >= 4;
        bool politicalDurabilityPasses = polity.YearsSinceFounded >= minimumPolityAgeYears
            && historyWindow.PoliticalHistoryRollup.OrganizedMonthsLast12Months >= 6;

        List<string> hardVetoes = [];
        if (IsSevereUnsupportedMonth(current))
        {
            hardVetoes.Add("severe_unsupported_current_month");
        }

        if (current.ActiveIdentityBreakNow)
        {
            hardVetoes.Add("active_identity_break");
        }

        if (current.ScatterShare >= 0.60 || current.ConnectedFootprintShare < 0.45 || current.RouteCoverageShare < 0.30)
        {
            hardVetoes.Add("catastrophically_scattered_current_footprint");
        }

        if (current.Population < minimumPopulation)
        {
            hardVetoes.Add("population_below_minimum_demographic_viability");
        }

        if (current.DisplacementThisMonth && last3.Any(snapshot => snapshot.DisplacementThisMonth) && !HasSpatialRestabilization(last6))
        {
            hardVetoes.Add("catastrophic_unresolved_displacement");
        }

        List<string> blockers = [];
        List<string> warnings = [];
        if (!currentSupportPasses)
        {
            blockers.Add("current_support_must_pass");
        }

        bool continuityPasses = health.Continuity.State >= ContinuityState.Established
            || (sparseHistory && polity.YearsSinceFounded >= minimumPolityAgeYears && !current.ActiveIdentityBreakNow);
        if (!continuityPasses)
        {
            blockers.Add("continuity_below_established");
        }

        bool movementOrRootingPasses = health.MovementCoherence.State >= MovementCoherenceState.Coherent
            || health.Rootedness.State >= RootednessState.Rooted;
        if (!movementOrRootingPasses)
        {
            blockers.Add("movement_or_rooting_below_floor");
        }

        if (!settlementDurabilityPasses)
        {
            warnings.Add("settlement_durability_thin");
        }

        if (!politicalDurabilityPasses)
        {
            warnings.Add("political_durability_thin");
        }

        if (health.Support.State == SupportStabilityState.Recovering)
        {
            warnings.Add("candidate_recovering_from_recent_shock");
        }
        else if (health.Support.State == SupportStabilityState.Volatile)
        {
            warnings.Add("candidate_support_is_volatile");
        }

        bool viable = hardVetoes.Count == 0 && blockers.Count == 0;
        bool supportStableEnoughForNormalEntry = sparseHistory
            ? currentSupportPasses
            : health.Support.State is SupportStabilityState.Stable or SupportStabilityState.Recovering;
        bool supportsNormalEntry = viable
            && settlementDurabilityPasses
            && politicalDurabilityPasses
            && supportStableEnoughForNormalEntry;

        string summary = viable
            ? supportsNormalEntry
                ? "Meets hard viability and normal-entry durability gates."
                : "Meets hard viability truth but remains thin for a normal stop."
            : $"Blocked: {(hardVetoes.FirstOrDefault() ?? blockers.FirstOrDefault() ?? "candidate_not_ready")}.";

        return new CandidateReadinessEvaluation(
            polity.Id,
            polity.Name,
            viable,
            supportsNormalEntry,
            currentSupportPasses,
            health.Support.State,
            demographicViability,
            populationTrend,
            health.MovementCoherence.State,
            health.Rootedness.State,
            health.Continuity.State,
            settlementDurabilityPasses,
            politicalDurabilityPasses,
            last3.Any(HasMajorShock),
            hardVetoes.Count > 0,
            hardVetoes,
            blockers,
            warnings,
            summary);
    }

    public static bool IsSupportedMonth(PeopleMonthlySnapshot snapshot)
        => snapshot.SupportAdequacy >= 0.85
            && snapshot.FoodSatisfaction >= 0.85
            && snapshot.StarvingSettlementCount == 0
            && !snapshot.SupportCrashThisMonth;

    public static bool IsSevereUnsupportedMonth(PeopleMonthlySnapshot snapshot)
        => snapshot.SupportAdequacy < 0.65
            || snapshot.FoodSatisfaction < 0.60
            || snapshot.StarvingSettlementCount > 0;

    public static bool IsCoherentMonth(PeopleMonthlySnapshot snapshot)
        => snapshot.RouteCoverageShare >= 0.60
            && snapshot.ConnectedFootprintShare >= 0.75
            && snapshot.ScatterShare <= 0.25
            && (snapshot.OccupiedRegionIds.Count == 0 || (snapshot.SupportAdequacy / snapshot.OccupiedRegionIds.Count) >= 0.28);

    public static bool IsStrongCoherentMonth(PeopleMonthlySnapshot snapshot)
        => snapshot.RouteCoverageShare >= 0.80
            && snapshot.ConnectedFootprintShare >= 0.90
            && snapshot.ScatterShare <= 0.12
            && (snapshot.OccupiedRegionIds.Count == 0 || (snapshot.SupportAdequacy / snapshot.OccupiedRegionIds.Count) >= 0.45);

    public static bool IsScatteredMonth(PeopleMonthlySnapshot snapshot)
        => snapshot.ScatterShare >= 0.45
            || snapshot.ConnectedFootprintShare <= 0.55
            || snapshot.RouteCoverageShare < 0.40;

    public static bool IsRootedMonth(PeopleMonthlySnapshot snapshot)
        => (snapshot.IsAnchoredThisMonth || (snapshot.HomeClusterShare >= 0.65 && snapshot.OldestSettlementAgeMonths >= 6))
            && !snapshot.DisplacementThisMonth;

    public static bool IsDeeplyRootedMonth(PeopleMonthlySnapshot snapshot)
        => (snapshot.IsStrongAnchoredThisMonth || (snapshot.HomeClusterShare >= 0.80 && snapshot.OldestSettlementAgeMonths >= 12))
            && !snapshot.DisplacementThisMonth;

    public static bool HasMajorShock(PeopleMonthlySnapshot snapshot)
        => snapshot.SupportCrashThisMonth
            || snapshot.DisplacementThisMonth
            || snapshot.SettlementLossThisMonth
            || snapshot.CollapseMarkerThisMonth
            || snapshot.IdentityBreakThisMonth;

    private static IReadOnlyList<PeopleMonthlySnapshot> SelectRecent(IReadOnlyList<PeopleMonthlySnapshot> rawHistory, int months)
    {
        if (rawHistory.Count == 0)
        {
            return Array.Empty<PeopleMonthlySnapshot>();
        }

        int currentIndex = rawHistory[^1].AbsoluteMonthIndex;
        return rawHistory
            .Where(snapshot => currentIndex - snapshot.AbsoluteMonthIndex < months)
            .ToArray();
    }

    private static PeopleMonthlySnapshot CreateSyntheticCurrent(PeopleHistoryWindowSnapshot historyWindow)
        => new(
            historyWindow.Header.PeopleId,
            historyWindow.Header.PeopleName,
            historyWindow.Header.SpeciesId,
            historyWindow.Header.LineageId,
            historyWindow.Header.WorldYear,
            historyWindow.Header.WorldMonth,
            0,
            historyWindow.CurrentPeopleState.Population,
            historyWindow.CurrentPeopleState.CurrentRegionId,
            historyWindow.CurrentPeopleState.CurrentRegionId,
            [historyWindow.CurrentPeopleState.CurrentRegionId],
            historyWindow.CurrentPeopleState.CurrentRegionId,
            historyWindow.CurrentPeopleState.SettlementCount,
            0,
            historyWindow.CurrentPeopleState.StarvingSettlementCount == 0 ? historyWindow.CurrentPeopleState.SettlementCount : 0,
            0,
            historyWindow.CurrentPeopleState.StarvingSettlementCount,
            historyWindow.CurrentPeopleState.HomeClusterShare,
            historyWindow.CurrentPeopleState.ConnectedFootprintShare,
            historyWindow.CurrentPeopleState.RouteCoverageShare,
            historyWindow.CurrentPeopleState.ScatterShare,
            0,
            0.0,
            1.0,
            historyWindow.CurrentPeopleState.SupportAdequacy,
            historyWindow.CurrentPeopleState.SupportAdequacy,
            historyWindow.CurrentPeopleState.FoodSatisfaction,
            0.0,
            0.0,
            0,
            0.0,
            0,
            0,
            0,
            historyWindow.CurrentPeopleState.MigrationPressure,
            historyWindow.CurrentPeopleState.FragmentationPressure,
            SettlementStatus.Nomadic,
            PolityStage.Band,
            false,
            false,
            false,
            false,
            historyWindow.CurrentPeopleState.IsAnchored,
            historyWindow.CurrentPeopleState.IsStrongAnchored,
            historyWindow.CurrentPeopleState.HasExpansionOpportunity,
            historyWindow.CurrentPeopleState.HasTradeContact,
            false,
            historyWindow.CurrentPeopleState.HasCurrentSupportCrash,
            historyWindow.CurrentPeopleState.HasCurrentDisplacement,
            historyWindow.CurrentPeopleState.HasCurrentSettlementLoss,
            historyWindow.CurrentPeopleState.HasCurrentCollapseMarker,
            historyWindow.CurrentPeopleState.ActiveIdentityBreakNow,
            historyWindow.CurrentPeopleState.ActiveIdentityBreakNow,
            historyWindow.SocialContinuityHistoryRollup.ObservedContinuousIdentityMonths,
            historyWindow.CurrentPeopleState.RelevantNeighborCount,
            0,
            0,
            historyWindow.CurrentPeopleState.PressureNeighborCount);

    private static bool IsSupportTrendImproving(IReadOnlyList<PeopleMonthlySnapshot> last6, SupportHistoryRollup support)
    {
        if (last6.Count < 2)
        {
            return support.AverageSupportAdequacyLast6Months >= support.AverageSupportAdequacyLast12Months;
        }

        return last6[^1].SupportAdequacy >= last6[0].SupportAdequacy
            && support.AverageSupportAdequacyLast6Months >= support.AverageSupportAdequacyLast12Months;
    }

    private static bool HasSpatialRestabilization(IReadOnlyList<PeopleMonthlySnapshot> last6)
    {
        if (last6.Count < 3)
        {
            return false;
        }

        IReadOnlyList<PeopleMonthlySnapshot> recentThree = last6.TakeLast(3).ToArray();
        return recentThree.All(snapshot => !snapshot.DisplacementThisMonth)
            && recentThree.Count(IsCoherentMonth) >= 2;
    }

    private static DemographicViabilityState ClassifyDemographicViability(
        PeopleMonthlySnapshot current,
        DemographyHistoryRollup demography,
        int minimumPopulation)
    {
        if (current.Population < minimumPopulation || demography.MinimumPopulationLast12Months < minimumPopulation)
        {
            return DemographicViabilityState.Critical;
        }

        if (current.Population < (minimumPopulation * 1.25))
        {
            return DemographicViabilityState.Fragile;
        }

        if (current.Population >= (minimumPopulation * 2)
            && demography.AveragePopulationLast12Months >= (minimumPopulation * 1.5)
            && demography.DeclineMonthsLast12Months <= 2)
        {
            return DemographicViabilityState.Strong;
        }

        return DemographicViabilityState.Viable;
    }

    private static PopulationTrendState ClassifyPopulationTrend(
        IReadOnlyList<PeopleMonthlySnapshot> last6,
        IReadOnlyList<PeopleMonthlySnapshot> last12,
        DemographyHistoryRollup demography)
    {
        double averageLast6 = last6.Count == 0 ? demography.AveragePopulationLast6Months : last6.Average(snapshot => snapshot.Population);
        double averageLast12 = last12.Count == 0 ? demography.AveragePopulationLast12Months : last12.Average(snapshot => snapshot.Population);
        if (averageLast6 > (averageLast12 * 1.03))
        {
            return PopulationTrendState.Improving;
        }

        if (averageLast6 < (averageLast12 * 0.97))
        {
            return PopulationTrendState.Declining;
        }

        return PopulationTrendState.Flat;
    }

    private static int CountDeclineStreak(IReadOnlyList<PeopleMonthlySnapshot> history)
    {
        if (history.Count < 2)
        {
            return 0;
        }

        int streak = 0;
        for (int index = history.Count - 1; index > 0; index--)
        {
            if (history[index].Population < history[index - 1].Population)
            {
                streak++;
                continue;
            }

            break;
        }

        return streak;
    }
}
