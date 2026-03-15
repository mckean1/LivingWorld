using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Societies;
using Xunit;

namespace LivingWorld.Tests;

public sealed class PrehistoryReadinessEvaluatorTests
{
    [Fact]
    public void Report_CannotStopBeforeMinimumPrehistoryAge()
    {
        World world = CreateWorld(age: 600);
        PrehistoryReadinessEvaluator evaluator = new(new WorldGenerationSettings());

        WorldReadinessReport report = evaluator.Evaluate(
            world,
            EmptyObserver(world),
            CandidateEvaluations(CreateCandidateEvaluation(isViable: true, supportsNormalEntry: true)),
            [CreateCandidateSummary(1)]).Report;

        Assert.Equal(PrehistoryCheckpointOutcomeKind.ContinuePrehistory, report.FinalCheckpointResolution);
        Assert.Equal(PrehistoryAgeGateStatus.BeforeMinimumAge, report.AgeGate.Status);
    }

    [Fact]
    public void Report_EntersFocalSelectionAfterMinimumAgeWhenStrictCategoriesPass()
    {
        World world = CreateWorld(age: 1020);
        PrehistoryReadinessEvaluator evaluator = new(new WorldGenerationSettings());

        WorldReadinessReport report = evaluator.Evaluate(
            world,
            ObserverWithNeighbor(world, 1),
            CandidateEvaluations(CreateCandidateEvaluation(isViable: true, supportsNormalEntry: true)),
            [CreateCandidateSummary(1), CreateCandidateSummary(2, regionId: 2, lineageId: 2, subsistence: "Proto-farming")]).Report;

        Assert.Equal(PrehistoryCheckpointOutcomeKind.EnterFocalSelection, report.FinalCheckpointResolution);
        Assert.Equal(ReadinessAssessmentStatus.Pass, report.GetCategory(WorldReadinessCategoryKind.CandidateReadiness).Status);
    }

    [Fact]
    public void Report_ContinuesPrehistoryWhenStrictCategoryBlockedBeforeMaxAge()
    {
        World world = CreateWorld(age: 960);
        world.PhaseCReadinessReport = world.PhaseCReadinessReport with { OrganicPolityCount = 0 };
        PrehistoryReadinessEvaluator evaluator = new(new WorldGenerationSettings());

        WorldReadinessReport report = evaluator.Evaluate(
            world,
            EmptyObserver(world),
            CandidateEvaluations(CreateCandidateEvaluation(isViable: true, supportsNormalEntry: true)),
            [CreateCandidateSummary(1)]).Report;

        Assert.Equal(PrehistoryCheckpointOutcomeKind.ContinuePrehistory, report.FinalCheckpointResolution);
        Assert.Equal(ReadinessAssessmentStatus.Blocker, report.GetCategory(WorldReadinessCategoryKind.SocialEmergenceReadiness).Status);
    }

    [Fact]
    public void Report_ContinuesPrehistoryWhenCandidatePoolIsNotYetViableBeforeMaxAge()
    {
        World world = CreateWorld(age: 980);
        PrehistoryReadinessEvaluator evaluator = new(new WorldGenerationSettings());

        WorldReadinessReport report = evaluator.Evaluate(
            world,
            EmptyObserver(world),
            CandidateEvaluations(CreateCandidateEvaluation(isViable: false, supportsNormalEntry: false, blocker: "current_support_must_pass")),
            Array.Empty<PlayerEntryCandidateSummary>()).Report;

        Assert.Equal(PrehistoryCheckpointOutcomeKind.ContinuePrehistory, report.FinalCheckpointResolution);
        Assert.Contains("no_viable_candidates", report.GlobalBlockingReasons);
    }

    [Fact]
    public void Report_ForcesEntryAtMaxAgeWhenViableCandidatesExistButWorldIsThin()
    {
        World world = CreateWorld(age: 1400);
        PrehistoryReadinessEvaluator evaluator = new(new WorldGenerationSettings());

        WorldReadinessReport report = evaluator.Evaluate(
            world,
            EmptyObserver(world),
            CandidateEvaluations(CreateCandidateEvaluation(isViable: true, supportsNormalEntry: true)),
            [CreateCandidateSummary(1)]).Report;

        Assert.Equal(PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection, report.FinalCheckpointResolution);
        Assert.True(report.IsThinWorld);
    }

    [Fact]
    public void Report_FailsHonestlyAtMaxAgeWhenZeroViableCandidatesExist()
    {
        World world = CreateWorld(age: 1400);
        PrehistoryReadinessEvaluator evaluator = new(new WorldGenerationSettings());

        WorldReadinessReport report = evaluator.Evaluate(
            world,
            EmptyObserver(world),
            CandidateEvaluations(CreateCandidateEvaluation(isViable: false, supportsNormalEntry: false, blocker: "continuity_below_established")),
            Array.Empty<PlayerEntryCandidateSummary>()).Report;

        Assert.Equal(PrehistoryCheckpointOutcomeKind.GenerationFailure, report.FinalCheckpointResolution);
        Assert.Contains("no_viable_candidates", report.GlobalBlockingReasons);
    }

    [Fact]
    public void CandidateReadiness_HardCurrentMonthVetoesBlockViability()
    {
        Polity polity = CreatePolity(1, population: 80, polityAge: 8);
        PeopleMonthlySnapshot current = Snapshot(0, population: 80, supportAdequacy: 0.50, foodSatisfaction: 0.50, starvingSettlementCount: 1, continuousIdentityMonthsObserved: 18, isAnchored: true, settlementCount: 2, stableSettlementCount: 2);
        PeopleHistoryWindowSnapshot snapshot = new PeopleHistoryWindowSnapshotBuilder().Build([current]);

        CandidateReadinessEvaluation evaluation = PrehistoryReadinessEvidenceEvaluator.EvaluateCandidate(polity, snapshot, [current], 90, 3);

        Assert.False(evaluation.IsViable);
        Assert.Contains("severe_unsupported_current_month", evaluation.HardVetoReasons);
        Assert.Contains("population_below_minimum_demographic_viability", evaluation.HardVetoReasons);
    }

    [Fact]
    public void HealthEvaluator_DistinguishesStableAndRecovering()
    {
        EvaluatorHealthSummary stable = BuildHealth(Enumerable.Range(0, 12)
            .Select(index => Snapshot(index, supportAdequacy: 1.0, foodSatisfaction: 1.0, continuousIdentityMonthsObserved: index + 12, isAnchored: true, settlementCount: 2, stableSettlementCount: 2))
            .ToList());
        EvaluatorHealthSummary recovering = BuildHealth([
            Snapshot(0, population: 92, supportCrash: true, displacement: true, supportAdequacy: 0.55, foodSatisfaction: 0.55, continuousIdentityMonthsObserved: 12, settlementCount: 2, stableSettlementCount: 1),
            Snapshot(1, population: 93, supportAdequacy: 0.62, foodSatisfaction: 0.64, continuousIdentityMonthsObserved: 13, settlementCount: 2, stableSettlementCount: 1),
            Snapshot(2, population: 94, supportAdequacy: 0.68, foodSatisfaction: 0.70, continuousIdentityMonthsObserved: 14, settlementCount: 2, stableSettlementCount: 1),
            Snapshot(3, population: 96, supportAdequacy: 0.70, foodSatisfaction: 0.72, continuousIdentityMonthsObserved: 15, settlementCount: 2, stableSettlementCount: 1),
            Snapshot(4, population: 98, supportAdequacy: 0.74, foodSatisfaction: 0.76, continuousIdentityMonthsObserved: 16, settlementCount: 2, stableSettlementCount: 1),
            Snapshot(5, population: 100, supportAdequacy: 0.78, foodSatisfaction: 0.80, continuousIdentityMonthsObserved: 17, settlementCount: 2, stableSettlementCount: 1),
            Snapshot(6, population: 104, supportAdequacy: 0.82, foodSatisfaction: 0.84, continuousIdentityMonthsObserved: 18, isAnchored: true, settlementCount: 2, stableSettlementCount: 1),
            Snapshot(7, population: 108, supportAdequacy: 0.88, foodSatisfaction: 0.90, continuousIdentityMonthsObserved: 19, isAnchored: true, settlementCount: 2, stableSettlementCount: 2),
            Snapshot(8, population: 112, supportAdequacy: 0.92, foodSatisfaction: 0.94, continuousIdentityMonthsObserved: 20, isAnchored: true, settlementCount: 2, stableSettlementCount: 2),
            Snapshot(9, population: 116, supportAdequacy: 0.96, foodSatisfaction: 0.96, continuousIdentityMonthsObserved: 21, isAnchored: true, settlementCount: 2, stableSettlementCount: 2),
            Snapshot(10, population: 120, supportAdequacy: 0.98, foodSatisfaction: 0.98, continuousIdentityMonthsObserved: 22, isAnchored: true, settlementCount: 2, stableSettlementCount: 2),
            Snapshot(11, population: 124, supportAdequacy: 1.0, foodSatisfaction: 1.0, continuousIdentityMonthsObserved: 23, isAnchored: true, settlementCount: 2, stableSettlementCount: 2)
        ]);

        Assert.Equal(SupportStabilityState.Stable, stable.Support.State);
        Assert.Equal(SupportStabilityState.Recovering, recovering.Support.State);
    }

    [Fact]
    public void HealthEvaluator_RespectsContinuityRootednessAndMovementThresholdBoundaries()
    {
        EvaluatorHealthSummary continuity = BuildHealth(Enumerable.Range(0, 24)
            .Select(index => Snapshot(index, continuousIdentityMonthsObserved: index + 1, isAnchored: true, settlementCount: 2, stableSettlementCount: 2))
            .ToList());
        EvaluatorHealthSummary rooted = BuildHealth(Enumerable.Range(0, 12)
            .Select(index => Snapshot(index, homeClusterShare: 0.85, isAnchored: true, isStrongAnchored: true, oldestSettlementAgeMonths: 18, settlementCount: 2, stableSettlementCount: 2, continuousIdentityMonthsObserved: index + 12))
            .ToList());
        EvaluatorHealthSummary movement = BuildHealth(Enumerable.Range(0, 12)
            .Select(index => Snapshot(index, connectedFootprintShare: 0.92, routeCoverageShare: 0.86, scatterShare: 0.08, supportAdequacy: 1.0, occupiedRegionIds: [1, 2], settlementCount: 2, stableSettlementCount: 2, continuousIdentityMonthsObserved: index + 12))
            .ToList());

        Assert.Equal(ContinuityState.Deep, continuity.Continuity.State);
        Assert.Equal(RootednessState.DeeplyRooted, rooted.Rootedness.State);
        Assert.Equal(MovementCoherenceState.Strong, movement.MovementCoherence.State);
    }

    [Fact]
    public void Report_KeepsBlockersAndWarningsSeparate()
    {
        World world = CreateWorld(age: 1200);
        PrehistoryReadinessEvaluator evaluator = new(new WorldGenerationSettings());

        WorldReadinessReport report = evaluator.Evaluate(
            world,
            EmptyObserver(world),
            CandidateEvaluations(CreateCandidateEvaluation(isViable: true, supportsNormalEntry: true)),
            [CreateCandidateSummary(1)]).Report;

        Assert.Contains("candidate_variety_is_thin", report.GlobalWarningReasons);
        Assert.DoesNotContain("candidate_variety_is_thin", report.GlobalBlockingReasons);
    }

    private static World CreateWorld(int age)
    {
        World world = new(new WorldTime(age, 1));
        world.PhaseAReadinessReport = new PhaseAReadinessReport(10, 9, 0.9, 9, 0.9, 7, 0.7, 2, 0.2, 12, 8, 0, true, []);
        world.PhaseBReadinessReport = new PhaseBReadinessReport(true, 5, 3, 1, 3, 4, 2, 8, []);
        world.PhaseCReadinessReport = new PhaseCReadinessReport(true, 4, 4, 0, 3, 3, 0, 4, 4, 0, 4, 2, 2, 0, 2, 2, 0, 18, 0.5, []);
        return world;
    }

    private static PrehistoryObserverSnapshot EmptyObserver(World world)
        => new(world.Time.Year, world.Time.Month, Array.Empty<PeopleHistoryWindowSnapshot>(), Array.Empty<RegionEvaluationSnapshot>(), Array.Empty<NeighborContextSnapshot>(), "empty");

    private static PrehistoryObserverSnapshot ObserverWithNeighbor(World world, int peopleId)
        => new(
            world.Time.Year,
            world.Time.Month,
            Array.Empty<PeopleHistoryWindowSnapshot>(),
            Array.Empty<RegionEvaluationSnapshot>(),
            [new NeighborContextSnapshot(peopleId, world.Time.Year, world.Time.Month, new NeighborhoodSummary(1, 1, 1, 0, 1, 0), Array.Empty<NeighborRelationshipSnapshot>(), new NeighborAggregateMetrics(100, 0, 0, 1.0, 0.2))],
            "neighbor");

    private static IReadOnlyDictionary<int, CandidateReadinessEvaluation> CandidateEvaluations(params CandidateReadinessEvaluation[] evaluations)
        => evaluations.ToDictionary(evaluation => evaluation.PolityId);

    private static CandidateReadinessEvaluation CreateCandidateEvaluation(bool isViable, bool supportsNormalEntry, string? blocker = null)
        => new(1, "Candidate 1", isViable, supportsNormalEntry, isViable, SupportStabilityState.Stable, DemographicViabilityState.Viable, PopulationTrendState.Flat, MovementCoherenceState.Coherent, RootednessState.Rooted, ContinuityState.Established, true, true, false, false, Array.Empty<string>(), blocker is null ? Array.Empty<string>() : [blocker], Array.Empty<string>(), "summary");

    private static PlayerEntryCandidateSummary CreateCandidateSummary(int polityId, int regionId = 1, int lineageId = 1, string subsistence = "Mixed")
        => new(polityId, $"Polity {polityId}", polityId, $"Species {polityId}", lineageId, regionId, $"Region {regionId}", 12, 1000, 2, "Medium", subsistence, "Stable", "Rooted", "Temperate", "Deep", "Discovery", "Learned", "History", "Pressure", 0.9, StabilityBand.Stable, false);

    private static Polity CreatePolity(int id, int population, int polityAge)
        => new(id, $"Polity {id}", 1, 1, population, lineageId: 1) { YearsSinceFounded = polityAge };

    private static EvaluatorHealthSummary BuildHealth(IReadOnlyList<PeopleMonthlySnapshot> history)
    {
        PeopleHistoryWindowSnapshot snapshot = new PeopleHistoryWindowSnapshotBuilder().Build(history);
        return snapshot.EvaluatorHealthSummary;
    }

    private static PeopleMonthlySnapshot Snapshot(
        int absoluteMonthIndex,
        int population = 120,
        double supportAdequacy = 1.0,
        double foodSatisfaction = 1.0,
        int starvingSettlementCount = 0,
        bool supportCrash = false,
        bool displacement = false,
        bool isAnchored = true,
        bool isStrongAnchored = false,
        double homeClusterShare = 0.75,
        double connectedFootprintShare = 0.85,
        double routeCoverageShare = 0.75,
        double scatterShare = 0.15,
        int settlementCount = 2,
        int stableSettlementCount = 2,
        int oldestSettlementAgeMonths = 12,
        int continuousIdentityMonthsObserved = 12,
        IReadOnlyList<int>? occupiedRegionIds = null)
        => new(
            1,
            "People",
            1,
            1,
            absoluteMonthIndex / 12,
            (absoluteMonthIndex % 12) + 1,
            absoluteMonthIndex,
            population,
            1,
            1,
            occupiedRegionIds ?? [1, 2],
            1,
            settlementCount,
            0,
            stableSettlementCount,
            0,
            starvingSettlementCount,
            homeClusterShare,
            connectedFootprintShare,
            routeCoverageShare,
            scatterShare,
            1,
            10,
            8,
            9,
            supportAdequacy,
            foodSatisfaction,
            0,
            0.1,
            oldestSettlementAgeMonths,
            oldestSettlementAgeMonths,
            2,
            1,
            0,
            0.2,
            0.2,
            SettlementStatus.SemiSettled,
            PolityStage.Tribe,
            false,
            false,
            false,
            false,
            isAnchored,
            isStrongAnchored,
            false,
            false,
            false,
            supportCrash,
            displacement,
            false,
            false,
            false,
            false,
            continuousIdentityMonthsObserved,
            1,
            1,
            1,
            0);
}
