using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;
using LivingWorld.Advancement;
using Xunit;

namespace LivingWorld.Tests;

public sealed class PrehistoryCandidateSelectionEvaluatorTests
{
    [Fact]
    public void Evaluate_SurfacesViableCandidateWithStructuredPr4Fields()
    {
        World world = CreateWorld();
        AddCandidate(world, 1, "Stone Basin", 0, 180, 8, settlementCount: 2, advancements: 2, discoveries: 3);
        PrehistoryCandidateSelectionEvaluator evaluator = CreateEvaluator();

        PrehistoryCandidateSelectionResult result = evaluator.Evaluate(
            world,
            BuildObserver(world, History(1, isAnchored: true, settlementCount: 2, stableSettlementCount: 2), NeighborContext(1, relevant: 2, exchange: 1)),
            CandidateEvaluations(CreateCandidateEvaluation(1, isViable: true, supportsNormalEntry: true)));

        PlayerEntryCandidateSummary candidate = Assert.Single(result.Candidates);
        Assert.NotNull(candidate.Viability);
        Assert.True(candidate.Viability!.IsViable);
        Assert.NotNull(candidate.ScoreBreakdown);
        Assert.NotEmpty(candidate.QualificationReason);
        Assert.NotEmpty(candidate.EvidenceSentence);
        Assert.NotEmpty(candidate.SafeStrengths);
    }

    [Fact]
    public void Evaluate_RejectsCurrentSupportFloorFailure()
    {
        World world = CreateWorld();
        AddCandidate(world, 1, "Stone Basin", 0, 140, 6, settlementCount: 2);
        PrehistoryCandidateSelectionEvaluator evaluator = CreateEvaluator();

        PrehistoryCandidateSelectionResult result = evaluator.Evaluate(
            world,
            BuildObserver(world, History(1, supportAdequacy: 0.62, foodSatisfaction: 0.70, isAnchored: true, settlementCount: 2, stableSettlementCount: 1)),
            CandidateEvaluations(CreateCandidateEvaluation(1, isViable: false, supportsNormalEntry: false, currentSupportPasses: false, blockingReasons: ["current_support_must_pass"])));

        Assert.Empty(result.Candidates);
        Assert.Equal("hard_gate:current_support", result.RejectionReasons[1]);
    }

    [Fact]
    public void Evaluate_RejectsContinuityFloorFailure()
    {
        World world = CreateWorld();
        AddCandidate(world, 1, "Stone Basin", 0, 140, 2, settlementCount: 2);
        PrehistoryCandidateSelectionEvaluator evaluator = CreateEvaluator();

        PrehistoryCandidateSelectionResult result = evaluator.Evaluate(
            world,
            BuildObserver(world, History(1, continuousIdentityMonthsObserved: 4, isAnchored: true, settlementCount: 2, stableSettlementCount: 2)),
            CandidateEvaluations(CreateCandidateEvaluation(1, isViable: false, supportsNormalEntry: false, continuity: ContinuityState.Fragile, blockingReasons: ["continuity_below_established"])));

        Assert.Empty(result.Candidates);
        Assert.Equal("hard_gate:continuity_floor", result.RejectionReasons[1]);
    }

    [Fact]
    public void Evaluate_RejectsMovementAndRootingFloorFailure()
    {
        World world = CreateWorld();
        AddCandidate(world, 1, "Stone Basin", 0, 140, 6, settlementCount: 2);
        PrehistoryCandidateSelectionEvaluator evaluator = CreateEvaluator();

        PrehistoryCandidateSelectionResult result = evaluator.Evaluate(
            world,
            BuildObserver(world, History(1, isAnchored: false, homeClusterShare: 0.40, connectedFootprintShare: 0.58, routeCoverageShare: 0.45, scatterShare: 0.42, settlementCount: 2, stableSettlementCount: 2)),
            CandidateEvaluations(CreateCandidateEvaluation(1, isViable: false, supportsNormalEntry: false, movement: MovementCoherenceState.Mixed, rootedness: RootednessState.SoftAnchored, blockingReasons: ["movement_or_rooting_below_floor"])));

        Assert.Empty(result.Candidates);
        Assert.Equal("hard_gate:movement_or_rooting_floor", result.RejectionReasons[1]);
    }

    [Fact]
    public void Evaluate_MapsRepresentativeMaturityBands()
    {
        World world = CreateWorld();
        AddCandidate(world, 1, "Drifters", 1, 120, 5, settlementCount: 0);
        AddCandidate(world, 2, "Marsh Polity", 2, 220, 10, settlementCount: 3, advancements: 2, discoveries: 4);
        world.Polities.Single(polity => polity.Id == 2).Stage = PolityStage.Tribe;
        PrehistoryCandidateSelectionEvaluator evaluator = CreateEvaluator();

        PrehistoryCandidateSelectionResult result = evaluator.Evaluate(
            world,
            BuildObserver(
                world,
                History(1, isAnchored: false, settlementCount: 0, occupiedRegionIds: [1, 2]),
                History(2, isAnchored: true, settlementCount: 3, stableSettlementCount: 3, oldestSettlementAgeMonths: 24)),
            CandidateEvaluations(
                CreateCandidateEvaluation(1, isViable: true, supportsNormalEntry: true, movement: MovementCoherenceState.Coherent, rootedness: RootednessState.SoftAnchored),
                CreateCandidateEvaluation(2, isViable: true, supportsNormalEntry: true, politicalDurabilityPasses: true)));

        Assert.Equal(CandidateMaturityBand.Mobile, result.Candidates.Single(candidate => candidate.PolityId == 1).MaturityBand);
        Assert.Equal(CandidateMaturityBand.EmergentPolity, result.Candidates.Single(candidate => candidate.PolityId == 2).MaturityBand);
    }

    [Fact]
    public void Evaluate_ComposesPoolWithDiversificationAndNearDuplicateSuppression()
    {
        World world = CreateWorld(candidateTarget: 3);
        AddCandidate(world, 1, "Stone Basin", 0, 220, 10, settlementCount: 2, advancements: 2, discoveries: 4);
        AddCandidate(world, 2, "Stone Basin Kin", 0, 205, 9, settlementCount: 2, advancements: 2, discoveries: 4);
        AddCandidate(world, 3, "North Shelf", 1, 190, 8, settlementCount: 3, advancements: 1, discoveries: 3);
        AddCandidate(world, 4, "Salt Coast", 2, 200, 11, settlementCount: 3, advancements: 2, discoveries: 4);
        PrehistoryCandidateSelectionEvaluator evaluator = CreateEvaluator();

        PrehistoryCandidateSelectionResult result = evaluator.Evaluate(
            world,
            BuildObserver(
                world,
                History(1, isAnchored: true, settlementCount: 2, stableSettlementCount: 2, oldestSettlementAgeMonths: 18),
                History(2, isAnchored: true, settlementCount: 2, stableSettlementCount: 2, oldestSettlementAgeMonths: 18),
                History(3, isAnchored: true, settlementCount: 3, stableSettlementCount: 3, oldestSettlementAgeMonths: 18),
                History(4, isAnchored: true, settlementCount: 3, stableSettlementCount: 3, oldestSettlementAgeMonths: 20)),
            CandidateEvaluations(
                CreateCandidateEvaluation(1, isViable: true, supportsNormalEntry: true),
                CreateCandidateEvaluation(2, isViable: true, supportsNormalEntry: true),
                CreateCandidateEvaluation(3, isViable: true, supportsNormalEntry: true),
                CreateCandidateEvaluation(4, isViable: true, supportsNormalEntry: true, politicalDurabilityPasses: true)));

        Assert.Equal(3, result.Candidates.Count);
        Assert.Contains(result.Candidates, candidate => candidate.PolityId == 1);
        Assert.Contains(result.Candidates, candidate => candidate.PolityId == 3);
        Assert.Contains(result.Candidates, candidate => candidate.PolityId == 4);
        Assert.DoesNotContain(result.Candidates, candidate => candidate.PolityId == 2);
        Assert.StartsWith("suppressed_near_duplicate_of:1", result.RejectionReasons[2]);
    }

    [Fact]
    public void Evaluate_HandlesThinAndZeroWorldsHonestly()
    {
        World thinWorld = CreateWorld(candidateTarget: 3);
        AddCandidate(thinWorld, 1, "Thin Start", 0, 140, 7, settlementCount: 2);
        PrehistoryCandidateSelectionEvaluator evaluator = CreateEvaluator();

        PrehistoryCandidateSelectionResult thinResult = evaluator.Evaluate(
            thinWorld,
            BuildObserver(thinWorld, History(1, isAnchored: true, settlementCount: 2, stableSettlementCount: 2)),
            CandidateEvaluations(CreateCandidateEvaluation(1, isViable: true, supportsNormalEntry: true)));

        Assert.Single(thinResult.Candidates);

        World emptyWorld = CreateWorld();
        AddCandidate(emptyWorld, 1, "No Start", 0, 140, 2, settlementCount: 1);
        PrehistoryCandidateSelectionResult emptyResult = evaluator.Evaluate(
            emptyWorld,
            BuildObserver(emptyWorld, History(1, isAnchored: false, settlementCount: 1, stableSettlementCount: 1)),
            CandidateEvaluations(CreateCandidateEvaluation(1, isViable: false, supportsNormalEntry: false, hardVetoReasons: ["severe_unsupported_current_month"], hasHardVeto: true)));

        Assert.Empty(emptyResult.Candidates);
        Assert.Equal("hard_veto:severe_unsupported_current_month", emptyResult.RejectionReasons[1]);
    }

    private static PrehistoryCandidateSelectionEvaluator CreateEvaluator()
        => new(new WorldGenerationSettings());

    private static World CreateWorld(int candidateTarget = 4)
    {
        World world = new(new WorldTime(1200, 1));
        world.StartupAgeConfiguration = StartupWorldAgeConfiguration.ForPreset(StartupWorldAgePreset.StandardWorld) with
        {
            CandidateCountTarget = candidateTarget
        };
        world.Regions.Add(new Region(0, "Stone Basin") { Biome = RegionBiome.RiverValley, Fertility = 0.82, WaterAvailability = 0.76 });
        world.Regions.Add(new Region(1, "North Shelf") { Biome = RegionBiome.Plains, Fertility = 0.61, WaterAvailability = 0.55 });
        world.Regions.Add(new Region(2, "Salt Coast") { Biome = RegionBiome.Coast, Fertility = 0.58, WaterAvailability = 0.73 });
        world.Species.Add(new Species(1, "Stonefolk", 0.5, 0.4) { LineageId = 10, EcologyNiche = "adaptive omnivore" });
        world.Species.Add(new Species(2, "Shelfkin", 0.5, 0.4) { LineageId = 11, EcologyNiche = "frontier forager" });
        world.Species.Add(new Species(3, "Coastborn", 0.5, 0.4) { LineageId = 12, EcologyNiche = "coastal gatherer" });
        world.EvolutionaryLineages.Add(new EvolutionaryLineage(10, 1, "adaptive omnivore", TrophicRole.Omnivore));
        world.EvolutionaryLineages.Add(new EvolutionaryLineage(11, 2, "frontier forager", TrophicRole.Omnivore));
        world.EvolutionaryLineages.Add(new EvolutionaryLineage(12, 3, "coastal gatherer", TrophicRole.Omnivore));
        return world;
    }

    private static void AddCandidate(World world, int polityId, string name, int regionId, int population, int age, int settlementCount, int advancements = 0, int discoveries = 0)
    {
        int speciesId = regionId + 1;
        int lineageId = speciesId + 9;
        AdvancementId[] availableAdvancements =
        [
            AdvancementId.SeasonalPlanning,
            AdvancementId.FoodStorage,
            AdvancementId.Agriculture
        ];
        Polity polity = new(polityId, name, speciesId, regionId, population, lineageId: lineageId)
        {
            YearsSinceFounded = age,
            CurrentPressureSummary = "holding center"
        };
        polity.EstablishFirstSettlement(regionId, $"{name} Hearth");
        for (int index = 1; index < settlementCount; index++)
        {
            polity.AddSettlement(regionId, $"{name} Hearth {index + 1}");
        }

        for (int index = 0; index < advancements; index++)
        {
            polity.LearnAdvancement(availableAdvancements[index]);
        }

        for (int index = 0; index < discoveries; index++)
        {
            polity.AddDiscovery(new CulturalDiscovery($"disc-{polityId}-{index}", $"Discovery {index}", CulturalDiscoveryCategory.Geography, RegionId: regionId));
        }

        world.Polities.Add(polity);
        world.AddCivilizationalHistoryEvent(CivilizationalHistoryEventType.PolityFormation, lineageId, regionId, $"{name} formed", polityId: polityId);
    }

    private static PrehistoryObserverSnapshot BuildObserver(World world, params PeopleHistoryWindowSnapshot[] histories)
        => new(
            world.Time.Year,
            world.Time.Month,
            histories,
            Array.Empty<RegionEvaluationSnapshot>(),
            Array.Empty<NeighborContextSnapshot>(),
            "observer");

    private static PrehistoryObserverSnapshot BuildObserver(World world, IEnumerable<PeopleHistoryWindowSnapshot> histories, IEnumerable<NeighborContextSnapshot> neighborContexts)
        => new(world.Time.Year, world.Time.Month, histories.ToArray(), Array.Empty<RegionEvaluationSnapshot>(), neighborContexts.ToArray(), "observer");

    private static PrehistoryObserverSnapshot BuildObserver(World world, PeopleHistoryWindowSnapshot history, NeighborContextSnapshot neighborContext)
        => BuildObserver(world, [history], [neighborContext]);

    private static PrehistoryObserverSnapshot BuildObserver(World world, params object[] parts)
    {
        List<PeopleHistoryWindowSnapshot> histories = [];
        List<NeighborContextSnapshot> neighbors = [];
        foreach (object part in parts)
        {
            switch (part)
            {
                case PeopleHistoryWindowSnapshot history:
                    histories.Add(history);
                    break;
                case NeighborContextSnapshot neighbor:
                    neighbors.Add(neighbor);
                    break;
            }
        }

        return BuildObserver(world, histories, neighbors);
    }

    private static NeighborContextSnapshot NeighborContext(int peopleId, int relevant, int exchange)
        => new(
            peopleId,
            1200,
            1,
            new NeighborhoodSummary(relevant, relevant, relevant, Math.Max(0, relevant - 1), exchange, 0),
            Array.Empty<NeighborRelationshipSnapshot>(),
            new NeighborAggregateMetrics(200, 1, 2, 1.0, 0.4));

    private static IReadOnlyDictionary<int, CandidateReadinessEvaluation> CandidateEvaluations(params CandidateReadinessEvaluation[] evaluations)
        => evaluations.ToDictionary(evaluation => evaluation.PolityId);

    private static CandidateReadinessEvaluation CreateCandidateEvaluation(
        int polityId,
        bool isViable,
        bool supportsNormalEntry,
        bool currentSupportPasses = true,
        SupportStabilityState supportStability = SupportStabilityState.Stable,
        DemographicViabilityState demographicViability = DemographicViabilityState.Viable,
        PopulationTrendState populationTrend = PopulationTrendState.Flat,
        MovementCoherenceState movement = MovementCoherenceState.Coherent,
        RootednessState rootedness = RootednessState.Rooted,
        ContinuityState continuity = ContinuityState.Established,
        bool settlementDurabilityPasses = true,
        bool politicalDurabilityPasses = true,
        bool hasImmediateShock = false,
        bool hasHardVeto = false,
        IReadOnlyList<string>? hardVetoReasons = null,
        IReadOnlyList<string>? blockingReasons = null,
        IReadOnlyList<string>? warningReasons = null)
        => new(
            polityId,
            $"Polity {polityId}",
            isViable,
            supportsNormalEntry,
            currentSupportPasses,
            supportStability,
            demographicViability,
            populationTrend,
            movement,
            rootedness,
            continuity,
            settlementDurabilityPasses,
            politicalDurabilityPasses,
            hasImmediateShock,
            hasHardVeto,
            hardVetoReasons ?? Array.Empty<string>(),
            blockingReasons ?? Array.Empty<string>(),
            warningReasons ?? Array.Empty<string>(),
            "summary");

    private static PeopleHistoryWindowSnapshot History(
        int peopleId,
        int population = 140,
        double supportAdequacy = 1.0,
        double foodSatisfaction = 1.0,
        bool isAnchored = true,
        bool isStrongAnchored = false,
        double homeClusterShare = 0.80,
        double connectedFootprintShare = 0.88,
        double routeCoverageShare = 0.82,
        double scatterShare = 0.12,
        int settlementCount = 2,
        int stableSettlementCount = 2,
        int oldestSettlementAgeMonths = 12,
        int continuousIdentityMonthsObserved = 18,
        IReadOnlyList<int>? occupiedRegionIds = null)
    {
        IReadOnlyList<PeopleMonthlySnapshot> history = Enumerable.Range(0, 12)
            .Select(index => new PeopleMonthlySnapshot(
                peopleId,
                $"People {peopleId}",
                Math.Min(3, peopleId),
                Math.Min(12, peopleId + 9),
                100 + (index / 12),
                (index % 12) + 1,
                index,
                population,
                0,
                0,
                occupiedRegionIds ?? [0],
                0,
                settlementCount,
                0,
                stableSettlementCount,
                0,
                0,
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
                true,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                false,
                continuousIdentityMonthsObserved + index,
                1,
                1,
                1,
                0))
            .ToArray();

        return new PeopleHistoryWindowSnapshotBuilder().Build(history);
    }
}
