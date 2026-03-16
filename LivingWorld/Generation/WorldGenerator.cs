using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using LivingWorld.Systems;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LivingWorld.Generation;

public sealed class WorldGenerator
{
    private readonly Random _random;
    private readonly int _seed;
    private readonly Queue<string> _regionNames;
    private readonly List<PrimitiveLineageTemplate> _primitiveTemplates;
    private readonly WorldGenerationSettings _settings;
    private readonly PrehistoryRuntimeOrchestrator _prehistoryRuntimeOrchestrator = new();
    private readonly StartupProgressRenderer? _progressRenderer;
    private readonly PrehistoryCheckpointCoordinator _checkpointCoordinator;

    public WorldGenerator(int seed, WorldGenerationSettings? settings = null, StartupProgressRenderer? progressRenderer = null)
    {
        _seed = seed;
        _random = new Random(seed);
        _settings = settings ?? new WorldGenerationSettings();
        _regionNames = new Queue<string>(BuildShuffledNames(WorldGenerationCatalog.CreateRegionNames()));
        _primitiveTemplates = WorldGenerationCatalog.CreatePrimitiveLineageTemplates();
        _progressRenderer = progressRenderer;
        _checkpointCoordinator = new(
            _prehistoryRuntimeOrchestrator,
            new PrehistoryCheckpointEvaluationAdapter(_settings));
    }

    public World Generate()
    {
        ValidateSettings();

        World? lastWorld = null;
        List<string> priorRegenerationReasons = [];
        List<GenerationAttemptDiagnosticsSummary> attemptHistory = [];
        for (int attempt = 0; attempt < _settings.MaxStartupRegenerationAttempts; attempt++)
        {
            World attemptWorld = attempt == 0
                ? GenerateSingleAttempt(attempt, priorRegenerationReasons)
                : new WorldGenerator(DeriveAttemptSeed(attempt), _settings, _progressRenderer)
                    .GenerateSingleAttempt(attempt, priorRegenerationReasons);
            GenerationAttemptDiagnosticsSummary attemptSummary = WorldGenerationDiagnosticsEvaluator.BuildAttemptSummary(attemptWorld);
            attemptHistory.Add(attemptSummary);
            bool completed = attemptWorld.PrehistoryRuntime.CurrentPhase == PrehistoryRuntimePhase.FocalSelection;
            bool willRegenerate = !completed && attempt + 1 < _settings.MaxStartupRegenerationAttempts;
            WorldGenerationDiagnosticsEvaluator.ApplyAttemptHistory(attemptWorld, attemptHistory, postmortem: null);
            EmitAttemptDiagnostics(attemptWorld, attemptSummary, willRegenerate);

            if (completed)
            {
                WorldGenerationDiagnosticsEvaluator.ApplyAttemptHistory(attemptWorld, attemptHistory, postmortem: null);
                return attemptWorld;
            }

            lastWorld = attemptWorld;
            priorRegenerationReasons = WorldGenerationDiagnosticsEvaluator.BuildRegenerationReasons(attemptSummary)
                .Select(reason => reason.Code)
                .ToList();
        }

        if (lastWorld is not null)
        {
            GenerationFailurePostmortem? postmortem = lastWorld.PrehistoryRuntime.CurrentPhase == PrehistoryRuntimePhase.GenerationFailure
                ? WorldGenerationDiagnosticsEvaluator.BuildFailurePostmortem(attemptHistory)
                : null;
            WorldGenerationDiagnosticsEvaluator.ApplyAttemptHistory(lastWorld, attemptHistory, postmortem);
            if (postmortem is not null)
            {
                lastWorld.Prehistory.LegacyCompatibility.ReplaceStartupDiagnostics(WorldGenerationDiagnosticsFormatter.BuildFinalFailureLines(postmortem));
                EmitFinalFailureDiagnostics(lastWorld, postmortem);
            }

            return lastWorld;
        }

        throw new InvalidOperationException("Player entry failed before world generation could complete.");
    }

    private World GenerateSingleAttempt(int attempt, IReadOnlyList<string>? regenerationReasons = null)
    {
        World world = new(new WorldTime(), WorldSimulationPhase.Bootstrap)
        {
            StartupStage = WorldStartupStage.PrimitiveEcologyFoundation,
            StartupGenerationAttempt = attempt
        };
        _prehistoryRuntimeOrchestrator.Initialize(world, StartupWorldAgeConfiguration.ForPreset(_settings.StartupWorldAgePreset));
        _prehistoryRuntimeOrchestrator.Describe(
            world,
            "Generating world frame",
            "Preparing continent and climate",
            "Laying out the continent, biomes, and primitive ecological starting points.");
        ReportProgress(world);

        GenerateRegions(world);
        _prehistoryRuntimeOrchestrator.Describe(
            world,
            "Generating world frame",
            "Laying out regions",
            "Defining the world map and regional environmental profiles.");
        ReportProgress(world);
        ConnectRegions(world);
        _prehistoryRuntimeOrchestrator.Describe(
            world,
            "Generating world frame",
            "Connecting land and river corridors",
            "Linking regions so ecological spread and migration can follow real geography.");
        ReportProgress(world);
        GeneratePrimitiveLineages(world);
        _prehistoryRuntimeOrchestrator.Describe(
            world,
            "Seeding primitive life",
            "Creating foundational lineages",
            "Seeding the earliest primitive lineages across the new world.");
        ReportProgress(world);
        AssignInitialPrimitiveRanges(world);
        _prehistoryRuntimeOrchestrator.Describe(
            world,
            "Seeding primitive life",
            "Establishing initial ranges",
            "Placing primitive life into viable habitats before long ecological stabilization.");
        ReportProgress(world);
        StabilizePrimitiveEcology(world);
        InitializeEvolutionaryLineages(world);
        AdvanceEvolutionaryHistory(world);
        AdvanceCivilizationalEmergence(world);
        AdvancePlayerEntryEvaluation(world, regenerationReasons);

        return world;
    }

    private void GenerateRegions(World world)
    {
        RegionBiome[,] biomeGrid = WorldGenerationCatalog.CreateBiomeGrid();

        for (int row = 0; row < _settings.ContinentHeight; row++)
        {
            for (int column = 0; column < _settings.ContinentWidth; column++)
            {
                int regionId = (row * _settings.ContinentWidth) + column;
                RegionBiome biome = biomeGrid[row, column];
                Region region = new(regionId, NextRegionName(regionId))
                {
                    Biome = biome
                };

                ApplyRegionProfile(region, biome);
                region.EcologyProfile = RegionEcologyProfileBuilder.Build(region);
                world.Regions.Add(region);
            }
        }
    }

    private void ConnectRegions(World world)
    {
        for (int row = 0; row < _settings.ContinentHeight; row++)
        {
            for (int column = 0; column < _settings.ContinentWidth; column++)
            {
                Region region = world.Regions[(row * _settings.ContinentWidth) + column];

                if (column + 1 < _settings.ContinentWidth)
                {
                    AddConnection(region, world.Regions[(row * _settings.ContinentWidth) + column + 1]);
                }

                if (row + 1 < _settings.ContinentHeight)
                {
                    AddConnection(region, world.Regions[((row + 1) * _settings.ContinentWidth) + column]);
                }

                if (column % 2 == 0 && column + 1 < _settings.ContinentWidth && row + 1 < _settings.ContinentHeight)
                {
                    AddConnection(region, world.Regions[((row + 1) * _settings.ContinentWidth) + column + 1]);
                }
            }
        }

        int riverColumn = Math.Clamp(_settings.ContinentWidth / 2, 0, _settings.ContinentWidth - 1);
        for (int row = 0; row < _settings.ContinentHeight - 1; row++)
        {
            AddConnection(
                world.Regions[(row * _settings.ContinentWidth) + riverColumn],
                world.Regions[((row + 1) * _settings.ContinentWidth) + riverColumn]);
        }
    }

    private void GeneratePrimitiveLineages(World world)
    {
        foreach ((PrimitiveLineageTemplate template, int index) in _primitiveTemplates
                     .Take(_settings.InitialSpeciesCount)
                     .Select((template, index) => (template, index)))
        {
            Species lineage = new(index, template.Name, intelligence: 0.02, cooperation: template.TrophicRole == TrophicRole.Predator ? 0.12 : 0.06)
            {
                IsPrimitiveLineage = true,
                PrimitiveTemplateId = template.Id,
                EcologyNiche = template.EcologyNiche,
                TrophicRole = template.TrophicRole,
                TemperaturePreference = template.TemperaturePreference,
                TemperatureTolerance = template.TemperatureTolerance,
                MoisturePreference = template.MoisturePreference,
                MoistureTolerance = template.MoistureTolerance,
                FertilityPreference = template.FertilityPreference,
                WaterPreference = template.WaterPreference,
                PlantBiomassAffinity = template.PlantBiomassAffinity,
                AnimalBiomassAffinity = template.AnimalBiomassAffinity,
                BaseCarryingCapacityFactor = template.BaseCarryingCapacityFactor,
                BaseReproductionRate = template.BaseReproductionRate,
                BaseDeclineRate = template.BaseDeclineRate,
                SpringReproductionModifier = template.TrophicRole == TrophicRole.Producer ? 1.28 : 1.14,
                SummerReproductionModifier = 1.04,
                AutumnReproductionModifier = 0.90,
                WinterReproductionModifier = template.TrophicRole == TrophicRole.Producer ? 0.70 : 0.62,
                MigrationCapability = template.MigrationCapability,
                ExpansionPressure = template.ExpansionPressure,
                Resilience = template.Resilience,
                StartingSpreadWeight = template.StartingSpreadWeight,
                MutationPotential = template.MutationPotential,
                SentiencePotential = template.SentiencePotential,
                MeatYield = template.MeatYield,
                HuntingDifficulty = template.HuntingDifficulty,
                HuntingDanger = template.HuntingDanger,
                DomesticationAffinity = 0.0,
                CultivationAffinity = 0.0,
                OriginCause = "phase_a_primitive_seed"
            };

            foreach (RegionBiome biome in template.PreferredBiomes)
            {
                lineage.PreferredBiomes.Add(biome);
            }

            world.Species.Add(lineage);
        }

        Dictionary<string, Species> speciesByTemplateId = world.Species
            .Where(species => species.PrimitiveTemplateId is not null)
            .ToDictionary(species => species.PrimitiveTemplateId!, StringComparer.Ordinal);
        foreach (PrimitiveLineageTemplate template in _primitiveTemplates.Take(_settings.InitialSpeciesCount))
        {
            Species lineage = speciesByTemplateId[template.Id];
            foreach (string dietTemplateId in template.DietTemplateIds)
            {
                if (speciesByTemplateId.TryGetValue(dietTemplateId, out Species? prey))
                {
                    lineage.DietSpeciesIds.Add(prey.Id);
                }
            }
        }
    }

    private void AssignInitialPrimitiveRanges(World world)
    {
        foreach (Species lineage in world.Species)
        {
            List<(Region Region, double Score)> viableRegions = world.Regions
                .Select(region => (Region: region, Score: ScoreRegionForLineage(lineage, region)))
                .Where(entry => entry.Score >= ResolveRangeThreshold(lineage))
                .OrderByDescending(entry => entry.Score + SeedNoise(lineage.Id, entry.Region.Id))
                .ThenBy(entry => entry.Region.Id)
                .ToList();

            if (viableRegions.Count == 0)
            {
                Region fallbackRegion = world.Regions.OrderByDescending(region => ScoreRegionForLineage(lineage, region)).First();
                lineage.InitialRangeRegionIds.Add(fallbackRegion.Id);
                continue;
            }

            int targetRangeSize = ResolveTargetRangeSize(lineage, viableRegions.Count, world.Regions.Count);
            Region seedRegion = viableRegions[Math.Min(viableRegions.Count - 1, _random.Next(Math.Min(3, viableRegions.Count)))].Region;
            foreach (int regionId in BuildClusteredRange(world, viableRegions, seedRegion.Id, targetRangeSize))
            {
                lineage.InitialRangeRegionIds.Add(regionId);
            }
        }

        EnsureBroadProducerCoverage(world);
        EnsureConsumerPresence(world);
        ConstrainPredatorsToSupportedRegions(world);
    }

    private void StabilizePrimitiveEcology(World world)
    {
        _prehistoryRuntimeOrchestrator.BeginAdvancingPhase(world, PrehistoryRuntimePhase.WorldSeeding);
        _prehistoryRuntimeOrchestrator.SetDetailView(world, PrehistoryRuntimeDetailView.EcologyFoundation);
        _prehistoryRuntimeOrchestrator.Describe(
            world,
            "Growing primitive ecologies",
            "Balancing the first food webs",
            "Expanding primitive ecosystems across fertile regions and balancing early food webs.");
        ReportProgress(world);
        FoodSystem foodSystem = new();
        EcosystemSystem ecosystemSystem = new();
        ecosystemSystem.InitializeRegionalPopulations(world);

        for (int month = 1; month <= _settings.PhaseAMaximumBootstrapMonths; month++)
        {
            foodSystem.UpdateRegionEcology(world);
            if (month % 3 == 0)
            {
                ecosystemSystem.UpdateSeason(world);
                ecosystemSystem.ResolveSeasonalCleanup(world);
                world.PhaseAReadinessReport = PhaseAReadinessEvaluator.Evaluate(world, _settings);
                if (month % 12 == 0 || world.PhaseAReadinessReport.IsReady)
                {
                    _prehistoryRuntimeOrchestrator.Describe(
                        world,
                        "Testing the living foundation",
                        "Checking the seeded biosphere",
                        "Checking whether primitive ecosystems have spread broadly enough to support deeper history.");
                    ReportProgress(world);
                }
                if (month >= _settings.PhaseAMinimumBootstrapMonths && world.PhaseAReadinessReport.IsReady)
                {
                    break;
                }
            }

            world.Time.AdvanceOneMonth();
        }

        _prehistoryRuntimeOrchestrator.RefreshAge(world);
        _prehistoryRuntimeOrchestrator.Describe(
            world,
            "World seeding established",
            "Living foundation ready",
            "Primitive ecosystems stabilized enough to support evolutionary history.",
            "World seeding complete: ecosystems stabilized.");
        ReportProgress(world);
    }

    private static void InitializeEvolutionaryLineages(World world)
    {
        world.EvolutionaryLineages.Clear();
        foreach (Species species in world.Species)
        {
            species.LineageId = species.Id;
            species.SentienceCapability = SentienceCapabilityState.None;

            EvolutionaryLineage lineage = new(species.LineageId, species.Id, species.EcologyNiche, species.TrophicRole)
            {
                ParentLineageId = null,
                RootAncestorLineageId = species.LineageId,
                OriginRegionId = species.OriginRegionId ?? species.InitialRangeRegionIds.FirstOrDefault(),
                OriginYear = world.Time.Year,
                Stage = LineageStage.Primitive,
                TraitProfileSummary = BuildTraitSummary(species, null),
                HabitatAdaptationSummary = "ancestral fit",
                AdaptationPressureSummary = "mixed"
            };

            world.EvolutionaryLineages.Add(lineage);

            foreach (Region region in world.Regions)
            {
                RegionSpeciesPopulation? population = region.GetSpeciesPopulation(species.Id);
                if (population is null)
                {
                    continue;
                }

                population.FounderLineageId = species.LineageId;
                population.LastContactYear = world.Time.Year;
                population.AdaptationPressureSummary = "mixed";
            }
        }
    }

    private void AdvanceEvolutionaryHistory(World world)
    {
        _prehistoryRuntimeOrchestrator.BeginAdvancingPhase(world, PrehistoryRuntimePhase.BiologicalDivergence);
        _prehistoryRuntimeOrchestrator.SetDetailView(world, PrehistoryRuntimeDetailView.EvolutionaryExpansion);
        world.StartupStage = WorldStartupStage.EvolutionaryExpansion;
        _prehistoryRuntimeOrchestrator.Describe(
            world,
            "Letting lineages branch, adapt, die out, and recolonize",
            "Diverging regional lineages",
            "Diverging isolated lineages into new branches and adaptation paths.");
        ReportProgress(world);
        MutationSystem mutationSystem = new(seed: _seed + 177);
        FoodSystem foodSystem = new();
        EcosystemSystem ecosystemSystem = new();

        for (int year = 1; year <= _settings.PhaseBMaximumBootstrapYears; year++)
        {
            for (int month = 0; month < 12; month++)
            {
                foodSystem.UpdateRegionEcology(world);
                if ((world.Time.Month % 3) == 0)
                {
                    ecosystemSystem.UpdateSeason(world);
                    mutationSystem.UpdateSeason(world);
                    ecosystemSystem.ResolveSeasonalCleanup(world);
                }

                world.Time.AdvanceOneMonth();
            }

            RefreshEvolutionaryLineageSnapshots(world);
            world.PhaseBReadinessReport = PhaseBReadinessEvaluator.Evaluate(world, _settings);
            world.PhaseBDiagnostics = PhaseBDiagnosticsEvaluator.Evaluate(world, _settings);
            _prehistoryRuntimeOrchestrator.RefreshAge(world);
            if (year == 1 || year % 25 == 0 || world.PhaseBReadinessReport.IsReady)
            {
                _prehistoryRuntimeOrchestrator.Describe(
                    world,
                    world.PhaseBReadinessReport.IsReady ? "Testing mature biological history" : "Letting lineages keep branching",
                    world.PhaseBReadinessReport.IsReady ? "Checking for sentience-capable biological depth" : "Deepening biological branching",
                    world.PhaseBReadinessReport.IsReady
                        ? "Testing whether the world's biological history has matured enough for sentience-capable branches."
                        : "Letting isolated populations branch, adapt, go extinct, and recolonize over deep time.");
                ReportProgress(world);
            }
            if (year >= _settings.PhaseBMinimumBootstrapYears && world.PhaseBReadinessReport.IsReady)
            {
                break;
            }
        }

        RefreshEvolutionaryLineageSnapshots(world);
        world.PhaseBReadinessReport = PhaseBReadinessEvaluator.Evaluate(world, _settings);
        world.PhaseBDiagnostics = PhaseBDiagnosticsEvaluator.Evaluate(world, _settings);
        _prehistoryRuntimeOrchestrator.RefreshAge(world);
        _prehistoryRuntimeOrchestrator.Describe(
            world,
            "Biological history established",
            "Divergence pass complete",
            "Evolutionary history matured into a deeper biological world.",
            "Biological divergence complete: evolutionary history matured.");
        ReportProgress(world);
    }

    private void AdvanceCivilizationalEmergence(World world)
    {
        SocialEmergenceSystem socialEmergenceSystem = new(_seed + 313, _settings);
        _prehistoryRuntimeOrchestrator.BeginAdvancingPhase(world, PrehistoryRuntimePhase.SocialEmergence);
        _prehistoryRuntimeOrchestrator.SetDetailView(world, PrehistoryRuntimeDetailView.SocietalEmergence);
        world.StartupStage = WorldStartupStage.SentienceActivation;
        _prehistoryRuntimeOrchestrator.Describe(
            world,
            "Persistent peoples, settlements, and early polities forming",
            "Activating sentient branches",
            "Identifying sentience-capable branches and grounding the first social trajectories.");
        ReportProgress(world);
        EnsureSentienceCapableSeedBranches(world);
        world.PhaseBReadinessReport = PhaseBReadinessEvaluator.Evaluate(world, _settings);
        world.PhaseBDiagnostics = PhaseBDiagnosticsEvaluator.Evaluate(world, _settings);

        for (int year = 1; year <= _settings.PhaseCMaximumBootstrapYears; year++)
        {
            socialEmergenceSystem.UpdateYear(world);
            foreach (Societies.Polity polity in world.Polities.Where(candidate => candidate.Population > 0))
            {
                polity.YearsSinceFounded++;
                polity.YearsInCurrentRegion++;
            }

            _prehistoryRuntimeOrchestrator.RefreshAge(world);
            if (year == 1 || year % 10 == 0 || world.PhaseCReadinessReport.IsReady)
            {
                _prehistoryRuntimeOrchestrator.Describe(
                    world,
                    "Persistent peoples, settlements, and early polities forming",
                    "Growing groups, settlements, and polities",
                    "Growing early societies, settlements, and the first plausible polity starts.");
                ReportProgress(world);
            }
            if (year >= _settings.PhaseCMinimumBootstrapYears && world.PhaseCReadinessReport.IsReady)
            {
                break;
            }

            world.Time.Reset(world.Time.Year + 1, world.Time.Month);
        }

        if (_settings.AllowPhaseCFallbackCivilizationalSeeding
            && world.Polities.All(polity => polity.Population <= 0))
        {
            SeedFallbackCivilizationalActor(world);
        }

        socialEmergenceSystem.UpdateYear(world);
        world.PhaseCReadinessReport = PhaseCReadinessEvaluator.Evaluate(world, _settings);
        _prehistoryRuntimeOrchestrator.RefreshAge(world);
        _prehistoryRuntimeOrchestrator.Describe(
            world,
            "Social foundations established",
            "Persistent societies ready",
            "Early societies matured into plausible player-entry material.",
            "Social emergence complete: viable societies formed.");
        ReportProgress(world);
    }

    private void AdvancePlayerEntryEvaluation(World world, IReadOnlyList<string>? regenerationReasons)
    {
        _prehistoryRuntimeOrchestrator.BeginAdvancingPhase(world, PrehistoryRuntimePhase.SocialEmergence);
        _prehistoryRuntimeOrchestrator.SetDetailView(world, PrehistoryRuntimeDetailView.CandidateEvaluation);
        world.StartupStage = WorldStartupStage.PlayerEntryEvaluation;
        _prehistoryRuntimeOrchestrator.Describe(
            world,
            "Evaluating viable starts during late social emergence",
            "Building focal candidates",
            "Evaluating whether the world is mature enough to surface healthy starting candidates.");
        ReportProgress(world);

        SocialEmergenceSystem socialEmergenceSystem = new(_seed + 727, _settings);

        PrehistoryCheckpointOutcome initialOutcome = _checkpointCoordinator.Evaluate(
            world,
            phaseLabel: "Evaluating viable starts",
            subphaseLabel: "Building focal candidates",
            activitySummary: "Evaluating whether the world is mature enough to surface healthy starting candidates.",
            completionSummary: "world_readiness_passed",
            allowEmergencyFallback: false,
            regenerationReasons: regenerationReasons);

        if (HandlePostCheckpointOutcome(world, initialOutcome))
        {
            return;
        }

        while (world.Time.Year < world.StartupAgeConfiguration.MaxPrehistoryYears)
        {
            _prehistoryRuntimeOrchestrator.RefreshAge(world);
            bool readinessWindowOpen = world.PrehistoryRuntime.AreReadinessChecksActive;
            bool shouldEvaluate = readinessWindowOpen
                && ((world.Time.Year - world.StartupAgeConfiguration.MinPrehistoryYears) % Math.Max(1, _settings.ReadinessEvaluationIntervalYears) == 0);
            if (shouldEvaluate)
            {
                PrehistoryCheckpointOutcome checkpointOutcome = _checkpointCoordinator.Evaluate(
                    world,
                    phaseLabel: "Reviewing whether the world can stop truthfully",
                    subphaseLabel: "Checking stop conditions",
                    activitySummary: "Evaluating whether the world is ready for player entry or needs more historical time.",
                    completionSummary: "world_readiness_passed",
                    allowEmergencyFallback: false,
                    regenerationReasons: regenerationReasons);

                if (HandlePostCheckpointOutcome(world, checkpointOutcome))
                {
                    return;
                }
            }

            if (world.Time.Year >= world.StartupAgeConfiguration.TargetPrehistoryYears
                && world.PlayerEntryCandidates.Count >= world.StartupAgeConfiguration.CandidateCountTarget
                && world.PhaseCReadinessReport.IsReady)
            {
                PrehistoryCheckpointOutcome targetOutcome = _checkpointCoordinator.Evaluate(
                    world,
                    phaseLabel: "Reviewing target-age handoff readiness",
                    subphaseLabel: "Testing target-age handoff",
                    activitySummary: "Checking whether the world can stop at target age with a healthy candidate pool.",
                    completionSummary: "target_age_readiness_passed",
                    allowEmergencyFallback: false,
                    regenerationReasons: regenerationReasons);

                if (HandlePostCheckpointOutcome(world, targetOutcome))
                {
                    return;
                }
            }

            socialEmergenceSystem.UpdateYear(world);
            foreach (Polity polity in world.Polities.Where(candidate => candidate.Population > 0))
            {
                polity.YearsSinceFounded++;
                polity.YearsInCurrentRegion++;
            }

            world.Time.Reset(world.Time.Year + 1, world.Time.Month);
            if ((world.Time.Year - world.StartupAgeConfiguration.MinPrehistoryYears) % Math.Max(1, _settings.ReadinessEvaluationIntervalYears) == 0)
            {
                _prehistoryRuntimeOrchestrator.Describe(
                    world,
                    "Letting late social emergence continue",
                    "Advancing late prehistory",
                    "Letting late prehistory continue so additional settlements, polities, and candidates can mature.");
                ReportProgress(world);
            }
        }

        PrehistoryCheckpointOutcome finalOutcome = _checkpointCoordinator.Evaluate(
            world,
            phaseLabel: "Reviewing final-start readiness",
            subphaseLabel: "Final candidate pass",
            activitySummary: "Running the final candidate pass at max age and checking whether the world still needs rescue paths.",
            completionSummary: "max_prehistory_age_reached",
            allowEmergencyFallback: true,
            regenerationReasons: regenerationReasons);
        HandlePostCheckpointOutcome(world, finalOutcome);
    }

    private void ReportProgress(World world)
        => _progressRenderer?.Render(world);

    private int DeriveAttemptSeed(int attempt)
        => MixSeed(_seed, attempt, 6151);

    private bool HandlePostCheckpointOutcome(World world, PrehistoryCheckpointOutcome outcome)
    {
        if (outcome.Kind == PrehistoryCheckpointOutcomeKind.EnterFocalSelection
            || outcome.Kind == PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection)
        {
            string activity = outcome.Kind == PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection
                ? "Preparing the final fallback starts for selection."
                : "Preparing the final candidate starts for selection.";
            _prehistoryRuntimeOrchestrator.Describe(
                world,
                "Reviewing surfaced candidate starts",
                "Building focal starts",
                activity,
                transitionSummary: outcome.Summary);
            ReportProgress(world);
            return true;
        }

        if (outcome.Kind == PrehistoryCheckpointOutcomeKind.GenerationFailure)
        {
            _prehistoryRuntimeOrchestrator.RecordGenerationFailure(world, outcome.Summary, outcome.Details);
            return true;
        }

        return false;
    }

    private void EmitAttemptDiagnostics(World world, GenerationAttemptDiagnosticsSummary summary, bool willRegenerate)
    {
        IReadOnlyList<string> lines = WorldGenerationDiagnosticsFormatter.BuildAttemptSummaryLines(summary, willRegenerate);
        world.Prehistory.LegacyCompatibility.ReplaceStartupDiagnostics(lines);
        world.PrehistoryRuntime.TransitionSummary = WorldGenerationDiagnosticsFormatter.BuildAttemptTransitionSummary(summary, willRegenerate);
        if (_progressRenderer is not null)
        {
            ReportProgress(world);
        }

        if (_progressRenderer is not null && !Console.IsOutputRedirected)
        {
            return;
        }

        foreach (string line in lines)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine();
    }

    private void EmitFinalFailureDiagnostics(World world, GenerationFailurePostmortem postmortem)
    {
        if (_progressRenderer is not null)
        {
            ReportProgress(world);
        }

        if (_progressRenderer is not null && !Console.IsOutputRedirected)
        {
            return;
        }

        foreach (string line in WorldGenerationDiagnosticsFormatter.BuildFinalFailureLines(postmortem))
        {
            Console.WriteLine(line);
        }

        Console.WriteLine();
    }

    private void EnsureSentienceCapableSeedBranches(World world)
    {
        int targetCapableLineageCount = Math.Max(
            _settings.MinimumPhaseBSentienceCapableLineageCount,
            _settings.BootstrapSentienceCapableLineageTarget);
        int capableLineageCount = world.EvolutionaryLineages.Count(lineage =>
            !lineage.IsExtinct
            && lineage.SentienceCapability == SentienceCapabilityState.Capable);
        HashSet<int> capableRoots = world.Species
            .Where(species => species.SentienceCapability == SentienceCapabilityState.Capable)
            .Select(species => species.RootAncestorSpeciesId)
            .ToHashSet();
        HashSet<int> adaptedBiomes = world.EvolutionaryLineages
            .Where(lineage => lineage.SentienceCapability == SentienceCapabilityState.Capable)
            .SelectMany(lineage => lineage.AdaptedBiomeIds)
            .ToHashSet();
        if (capableLineageCount >= targetCapableLineageCount)
        {
            return;
        }

        foreach (Species species in world.Species
                     .Where(candidate => !candidate.IsGloballyExtinct && candidate.SentienceCapability != SentienceCapabilityState.Capable)
                     .OrderBy(candidate => capableRoots.Contains(candidate.RootAncestorSpeciesId) ? 1 : 0)
                     .ThenByDescending(candidate =>
                     {
                         EvolutionaryLineage? lineage = world.GetLineageForSpecies(candidate.Id);
                         bool introducesBiomeNovelty = lineage is not null && lineage.AdaptedBiomeIds.Any(biomeId => !adaptedBiomes.Contains(biomeId));
                         double lineageDepth = lineage?.AncestryDepth ?? 0;
                         double adaptationBreadth = lineage?.AdaptedBiomeIds.Count ?? 0;
                         return (introducesBiomeNovelty ? 0.30 : 0.0)
                                + (candidate.SentiencePotential + candidate.Intelligence + candidate.Cooperation)
                                + Math.Min(0.24, lineageDepth * 0.06)
                                + Math.Min(0.18, adaptationBreadth * 0.04);
                     })
                     .ThenByDescending(candidate => world.Regions.Sum(region => region.GetSpeciesPopulation(candidate.Id)?.PopulationCount ?? 0))
                     .ThenBy(candidate => candidate.Id))
        {
            int totalPopulation = world.Regions.Sum(region => region.GetSpeciesPopulation(species.Id)?.PopulationCount ?? 0);
            if (totalPopulation < 60)
            {
                continue;
            }

            species.SentienceCapability = SentienceCapabilityState.Capable;
            if (world.GetLineageForSpecies(species.Id) is EvolutionaryLineage lineage)
            {
                lineage.SentienceCapability = SentienceCapabilityState.Capable;
                lineage.Stage = LineageStage.SentienceCapable;
                foreach (int biomeId in lineage.AdaptedBiomeIds)
                {
                    adaptedBiomes.Add(biomeId);
                }
            }

            world.AddEvolutionaryHistoryEvent(
                EvolutionaryHistoryEventType.SentienceCapabilityMilestone,
                species.LineageId,
                species.ParentSpeciesId,
                species.Id,
                species.OriginRegionId,
                $"{species.Name} remained the strongest sentience-capable branch after Phase B bootstrap",
                "bootstrap_sentience_handoff",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["populationSupport"] = totalPopulation.ToString(),
                    ["sentiencePotential"] = species.SentiencePotential.ToString("F2"),
                    ["rootNovelty"] = (!capableRoots.Contains(species.RootAncestorSpeciesId)).ToString()
                });

            capableRoots.Add(species.RootAncestorSpeciesId);
            capableLineageCount++;
            if (capableLineageCount >= targetCapableLineageCount)
            {
                break;
            }
        }
    }

    private static void SeedFallbackCivilizationalActor(World world)
    {
        Species? species = world.Species
            .Where(candidate => !candidate.IsGloballyExtinct)
            .OrderByDescending(candidate => candidate.SentienceCapability)
            .ThenByDescending(candidate => candidate.SentiencePotential + candidate.Intelligence + candidate.Cooperation)
            .ThenByDescending(candidate => world.Regions.Sum(region => region.GetSpeciesPopulation(candidate.Id)?.PopulationCount ?? 0))
            .FirstOrDefault();
        if (species is null)
        {
            return;
        }

        Region region = world.Regions
            .OrderByDescending(candidate => (candidate.GetSpeciesPopulation(species.Id)?.PopulationCount ?? 0) + (candidate.Fertility * 100))
            .ThenBy(candidate => candidate.Id)
            .First();
        species.SentienceCapability = SentienceCapabilityState.Capable;
        species.IsSapient = true;
        if (world.GetLineageForSpecies(species.Id) is EvolutionaryLineage lineage)
        {
            lineage.SentienceCapability = SentienceCapabilityState.Capable;
            lineage.Stage = LineageStage.SentienceCapable;
        }

        SentientPopulationGroup group = new(world.SentientGroups.Count == 0 ? 1 : world.SentientGroups.Max(candidate => candidate.Id) + 1)
        {
            SourceLineageId = species.LineageId,
            CurrentRegionId = region.Id,
            FounderRegionId = region.Id,
            ActivationYear = world.Time.Year,
            PopulationCount = Math.Max(36, (region.GetSpeciesPopulation(species.Id)?.PopulationCount ?? 72) / 2),
            MobilityMode = Societies.MobilityMode.SemiSedentary,
            Cohesion = 0.54,
            SocialComplexity = 0.46,
            SurvivalKnowledge = 0.48,
            SettlementIntent = 0.58,
            Stress = 0.20,
            SedentismPressure = 0.66,
            ContinuityYears = 6,
            IdentityStrength = 0.52,
            MigrationPattern = "anchored basin circuit",
            FoundingMemorySeed = $"{region.Name} continuity",
            ThreatMemorySeed = "bootstrap social safeguard",
            PressureSummary = "anchoring on rich ground",
            IsFallbackCreated = true,
            FoodSecurity = 0.70,
            StorageSupport = 0.40,
            LocalCarryingSupport = 0.72,
            MigrationPressure = 0.18,
            FragmentationPressure = 0.12
        };
        group.SharedKnowledge["water"] = new Societies.CulturalDiscovery("water", $"{region.Name} holds reliable water", Societies.CulturalDiscoveryCategory.Geography, RegionId: region.Id);
        group.SharedKnowledge["fertile"] = new Societies.CulturalDiscovery("fertile", $"{region.Name} is fertile ground", Societies.CulturalDiscoveryCategory.Environment, RegionId: region.Id);
        group.SharedKnowledge["prey"] = new Societies.CulturalDiscovery("prey", "Nearby fauna can be hunted", Societies.CulturalDiscoveryCategory.SpeciesUse, RegionId: region.Id);
        world.SentientGroups.Add(group);

        EmergingSociety society = new(world.Societies.Count == 0 ? 1 : world.Societies.Max(candidate => candidate.Id) + 1)
        {
            LineageId = species.LineageId,
            SpeciesId = species.Id,
            OriginRegionId = region.Id,
            FoundingYear = Math.Max(0, world.Time.Year - 5),
            Population = Math.Max(80, group.PopulationCount + 40),
            MobilityMode = Societies.MobilityMode.SemiSedentary,
            SubsistenceMode = region.Fertility >= 0.68 ? Societies.SubsistenceMode.ProtoFarming : Societies.SubsistenceMode.MixedHunterForager,
            Cohesion = 0.58,
            IdentityStrength = 0.56,
            SocialComplexity = 0.60,
            SurvivalKnowledge = 0.62,
            SedentismPressure = 0.72,
            PressureSummary = "bootstrap social safeguard",
            ContinuityYears = 8,
            PredecessorGroupId = group.Id,
            FoundingMemorySeed = group.FoundingMemorySeed,
            ThreatMemorySeed = group.ThreatMemorySeed,
            IsFallbackCreated = true,
            FoodSecurity = 0.74,
            StorageSupport = 0.44,
            SettlementSupport = 0.70,
            LocalCarryingSupport = 0.76,
            MigrationPressure = 0.16,
            FragmentationPressure = 0.12
        };
        society.RegionIds.Add(region.Id);
        society.IdentityMarkers.Add(region.Biome.ToString());
        foreach ((string key, Societies.CulturalDiscovery discovery) in group.SharedKnowledge)
        {
            society.CulturalKnowledge[key] = discovery;
        }
        world.Societies.Add(society);

        SocialSettlement socialSettlement = new(world.SocialSettlements.Count == 0 ? 1 : world.SocialSettlements.Max(candidate => candidate.Id) + 1)
        {
            FounderSocietyId = society.Id,
            FounderLineageId = society.LineageId,
            RegionId = region.Id,
            FoundingYear = Math.Max(0, world.Time.Year - 3),
            Population = 52,
            FoodBaseProfile = region.Fertility >= 0.68 ? "fertile mixed subsistence" : "anchored foraging",
            StorageLevel = 0.42,
            SettlementViability = 0.72,
            CurrentPressureSummary = "bootstrap social safeguard",
            IsFallbackCreated = true,
            FoodSecurity = 0.72,
            LocalCarryingSupport = 0.74,
            Stress = 0.16
        };
        world.SocialSettlements.Add(socialSettlement);
        society.SettlementIds.Add(socialSettlement.Id);

        int polityId = world.Polities.Count == 0 ? 1 : world.Polities.Max(candidate => candidate.Id) + 1;
        Societies.Polity polity = new(polityId, $"{region.Name} {species.Name.Split(' ')[0]} Polity", species.Id, region.Id, society.Population, lineageId: society.LineageId)
        {
            FounderSocietyId = society.Id,
            Stage = Societies.PolityStage.Tribe,
            SettlementStatus = Societies.SettlementStatus.SemiSettled,
            YearsSinceFounded = 4,
            CurrentPressureSummary = "bootstrap social safeguard",
            IdentitySeed = region.Biome.ToString(),
            IsFallbackCreated = true
        };
        polity.EstablishFirstSettlement(region.Id, $"{region.Name} Hearth");
        polity.AddDiscovery(new Societies.CulturalDiscovery("water", $"{region.Name} holds reliable water", Societies.CulturalDiscoveryCategory.Geography, RegionId: region.Id));
        polity.AddDiscovery(new Societies.CulturalDiscovery("fertile", $"{region.Name} is fertile ground", Societies.CulturalDiscoveryCategory.Environment, RegionId: region.Id));
        polity.AddDiscovery(new Societies.CulturalDiscovery("prey", "Nearby fauna can be hunted", Societies.CulturalDiscoveryCategory.SpeciesUse, RegionId: region.Id));
        polity.LearnAdvancement(Advancement.AdvancementId.Fire);
        polity.LearnAdvancement(Advancement.AdvancementId.StoneTools);
        polity.LearnAdvancement(Advancement.AdvancementId.SeasonalPlanning);
        polity.LearnAdvancement(Advancement.AdvancementId.FoodStorage);
        if (region.Fertility >= 0.68)
        {
            polity.LearnAdvancement(Advancement.AdvancementId.Agriculture);
        }
        world.Polities.Add(polity);

        world.AddCivilizationalHistoryEvent(Societies.CivilizationalHistoryEventType.SentientActivation, species.LineageId, region.Id, $"{species.Name} activated a fallback sentient group", "bootstrap_social_safeguard", groupId: group.Id);
        world.AddCivilizationalHistoryEvent(Societies.CivilizationalHistoryEventType.SocietyFormation, species.LineageId, region.Id, $"Society {society.Id} stabilized in {region.Name}", "bootstrap_social_safeguard", societyId: society.Id);
        world.AddCivilizationalHistoryEvent(Societies.CivilizationalHistoryEventType.SettlementFounded, species.LineageId, region.Id, $"A settlement took root in {region.Name}", "bootstrap_social_safeguard", societyId: society.Id, settlementId: socialSettlement.Id);
        world.AddCivilizationalHistoryEvent(Societies.CivilizationalHistoryEventType.PolityFormation, species.LineageId, region.Id, $"{polity.Name} emerged as a fallback polity", "bootstrap_social_safeguard", societyId: society.Id, polityId: polity.Id);
    }

    private static void RefreshEvolutionaryLineageSnapshots(World world)
    {
        foreach (EvolutionaryLineage lineage in world.EvolutionaryLineages)
        {
            Species? species = world.Species.FirstOrDefault(candidate => candidate.LineageId == lineage.Id);
            if (species is null)
            {
                continue;
            }

            List<RegionSpeciesPopulation> activePopulations = world.Regions
                .Select(region => region.GetSpeciesPopulation(species.Id))
                .Where(population => population is not null && population.PopulationCount > 0)
                .Cast<RegionSpeciesPopulation>()
                .ToList();
            lineage.CurrentPopulationRegions = activePopulations.Count;
            lineage.CurrentPopulationCount = activePopulations.Sum(population => population.PopulationCount);
            lineage.Stage = ResolveLineageStage(species, activePopulations);
            lineage.IsExtinct = species.IsGloballyExtinct;
            lineage.ExtinctionYear = species.ExtinctionYear;
            lineage.ExtinctionMonth = species.ExtinctionMonth;
            lineage.TraitProfileSummary = BuildTraitSummary(species, activePopulations.FirstOrDefault());
            lineage.HabitatAdaptationSummary = BuildHabitatSummary(world, species, activePopulations);
            lineage.AdaptationPressureSummary = activePopulations
                .OrderByDescending(population => population.DivergencePressure)
                .Select(population => population.AdaptationPressureSummary)
                .FirstOrDefault(summary => !string.IsNullOrWhiteSpace(summary))
                ?? "mixed";
            lineage.SentienceCapability = species.SentienceCapability;
        }
    }

    private static LineageStage ResolveLineageStage(Species species, IReadOnlyCollection<RegionSpeciesPopulation> activePopulations)
    {
        if (species.SentienceCapability == SentienceCapabilityState.Capable)
        {
            return LineageStage.SentienceCapable;
        }

        if (activePopulations.Any(population => population.DivergenceScore >= 1.4) || species.ParentSpeciesId is not null)
        {
            return species.ParentSpeciesId is not null
                ? LineageStage.EstablishedSpecies
                : LineageStage.Diverging;
        }

        return LineageStage.Primitive;
    }

    private static string BuildTraitSummary(Species species, RegionSpeciesPopulation? population)
    {
        double intelligence = population is null ? species.Intelligence : PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Intelligence);
        double sociality = population is null ? species.Cooperation : PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Sociality);
        double endurance = population is null ? PopulationTraitResolver.GetBaselineTrait(species, SpeciesTrait.Endurance) : PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Endurance);
        double flexibility = population is null ? PopulationTraitResolver.GetBaselineTrait(species, SpeciesTrait.DietFlexibility) : PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.DietFlexibility);
        return $"Int {intelligence:F2}, Soc {sociality:F2}, End {endurance:F2}, Flex {flexibility:F2}";
    }

    private static string BuildHabitatSummary(World world, Species species, IReadOnlyCollection<RegionSpeciesPopulation> activePopulations)
    {
        RegionBiome? dominantBiome = activePopulations
            .GroupBy(population => world.Regions[population.RegionId].Biome)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .Cast<RegionBiome?>()
            .FirstOrDefault();
        if (dominantBiome is null)
        {
            return "ancestral fit";
        }

        double climateTolerance = activePopulations.Count == 0
            ? species.TemperatureTolerance
            : activePopulations.Average(population => PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.ClimateTolerance));
        return $"{dominantBiome} specialist, climate tolerance {climateTolerance:F2}";
    }

    private void EnsureBroadProducerCoverage(World world)
    {
        HashSet<int> producerCoveredRegions = world.Species
            .Where(species => species.TrophicRole == TrophicRole.Producer)
            .SelectMany(species => species.InitialRangeRegionIds)
            .ToHashSet();

        foreach (Region region in world.Regions
                     .Where(region => region.EffectiveEcologyProfile.HabitabilityScore >= 0.44)
                     .OrderByDescending(region => region.EffectiveEcologyProfile.BasePrimaryProductivity)
                     .ThenBy(region => region.Id))
        {
            if (producerCoveredRegions.Contains(region.Id))
            {
                continue;
            }

            Species? producer = world.Species
                .Where(species => species.TrophicRole == TrophicRole.Producer)
                .OrderByDescending(species => ScoreRegionForLineage(species, region))
                .ThenByDescending(species => species.StartingSpreadWeight)
                .FirstOrDefault();
            if (producer is null || ScoreRegionForLineage(producer, region) < 0.48)
            {
                continue;
            }

            producer.InitialRangeRegionIds.Add(region.Id);
            producerCoveredRegions.Add(region.Id);
        }
    }

    private void EnsureConsumerPresence(World world)
    {
        HashSet<int> consumerCoveredRegions = world.Species
            .Where(species => species.TrophicRole is TrophicRole.Herbivore or TrophicRole.Omnivore)
            .SelectMany(species => species.InitialRangeRegionIds)
            .ToHashSet();

        foreach (Region region in world.Regions
                     .Where(region => region.EffectiveEcologyProfile.BasePrimaryProductivity >= 0.46)
                     .OrderByDescending(region => region.EffectiveEcologyProfile.HabitabilityScore)
                     .ThenBy(region => region.Id))
        {
            if (consumerCoveredRegions.Contains(region.Id))
            {
                continue;
            }

            Species? consumer = world.Species
                .Where(species => species.TrophicRole is TrophicRole.Herbivore or TrophicRole.Omnivore)
                .OrderByDescending(species => ScoreRegionForLineage(species, region))
                .ThenByDescending(species => species.StartingSpreadWeight)
                .FirstOrDefault();
            if (consumer is null || ScoreRegionForLineage(consumer, region) < 0.54)
            {
                continue;
            }

            List<int>? path = BuildRangeExpansionPath(world, consumer, region.Id);
            if (path is null)
            {
                consumer.InitialRangeRegionIds.Add(region.Id);
            }
            else
            {
                foreach (int regionId in path)
                {
                    consumer.InitialRangeRegionIds.Add(regionId);
                }
            }

            consumerCoveredRegions.Add(region.Id);
        }
    }

    private void ConstrainPredatorsToSupportedRegions(World world)
    {
        HashSet<int> preySupportedRegions = world.Species
            .Where(species => species.TrophicRole is TrophicRole.Herbivore or TrophicRole.Omnivore)
            .SelectMany(species => species.InitialRangeRegionIds)
            .ToHashSet();

        foreach (Species predator in world.Species.Where(species => species.TrophicRole is TrophicRole.Predator or TrophicRole.Apex))
        {
            List<int> supportedRegions = predator.InitialRangeRegionIds
                .Where(preySupportedRegions.Contains)
                .Distinct()
                .OrderBy(regionId => regionId)
                .ToList();

            if (supportedRegions.Count == 0)
            {
                Region? fallbackRegion = world.Regions
                    .Where(region => preySupportedRegions.Contains(region.Id))
                    .OrderByDescending(region => ScoreRegionForLineage(predator, region))
                    .FirstOrDefault();
                if (fallbackRegion is not null)
                {
                    supportedRegions.Add(fallbackRegion.Id);
                }
            }

            predator.InitialRangeRegionIds.Clear();
            foreach (int regionId in supportedRegions.Take(predator.TrophicRole == TrophicRole.Predator ? 5 : 3))
            {
                predator.InitialRangeRegionIds.Add(regionId);
            }
        }
    }

    private IEnumerable<int> BuildClusteredRange(
        World world,
        IReadOnlyList<(Region Region, double Score)> viableRegions,
        int seedRegionId,
        int targetRangeSize)
    {
        HashSet<int> selected = [seedRegionId];
        Queue<int> frontier = new();
        frontier.Enqueue(seedRegionId);

        while (frontier.Count > 0 && selected.Count < targetRangeSize)
        {
            int sourceId = frontier.Dequeue();
            Region sourceRegion = world.Regions[sourceId];

            foreach (int neighborId in sourceRegion.ConnectedRegionIds
                         .OrderByDescending(id => viableRegions.FirstOrDefault(entry => entry.Region.Id == id).Score)
                         .ThenBy(id => id))
            {
                if (selected.Count >= targetRangeSize)
                {
                    break;
                }

                if (!viableRegions.Any(entry => entry.Region.Id == neighborId) || !selected.Add(neighborId))
                {
                    continue;
                }

                frontier.Enqueue(neighborId);
            }
        }

        foreach ((Region region, _) in viableRegions)
        {
            if (selected.Count >= targetRangeSize)
            {
                break;
            }

            selected.Add(region.Id);
        }

        return selected;
    }

    private List<int>? BuildRangeExpansionPath(World world, Species lineage, int targetRegionId)
    {
        if (lineage.InitialRangeRegionIds.Contains(targetRegionId))
        {
            return [];
        }

        HashSet<int> occupied = lineage.InitialRangeRegionIds.ToHashSet();
        Queue<int> frontier = new();
        Dictionary<int, int?> parents = new();

        foreach (int seedRegionId in occupied)
        {
            frontier.Enqueue(seedRegionId);
            parents[seedRegionId] = null;
        }

        while (frontier.Count > 0)
        {
            int currentRegionId = frontier.Dequeue();
            if (currentRegionId == targetRegionId)
            {
                break;
            }

            foreach (int neighborId in world.Regions[currentRegionId].ConnectedRegionIds
                         .OrderByDescending(id => ScoreRegionForLineage(lineage, world.Regions[id]))
                         .ThenBy(id => id))
            {
                if (parents.ContainsKey(neighborId))
                {
                    continue;
                }

                if (ScoreRegionForLineage(lineage, world.Regions[neighborId]) < ResolveRangeThreshold(lineage))
                {
                    continue;
                }

                parents[neighborId] = currentRegionId;
                frontier.Enqueue(neighborId);
            }
        }

        if (!parents.ContainsKey(targetRegionId))
        {
            return null;
        }

        List<int> path = [];
        int? cursor = targetRegionId;
        while (cursor is not null)
        {
            int regionId = cursor.Value;
            if (!occupied.Contains(regionId))
            {
                path.Add(regionId);
            }

            cursor = parents[regionId];
        }

        path.Reverse();
        return path;
    }

    private void ApplyRegionProfile(Region region, RegionBiome biome)
    {
        (double fertilityMin, double fertilityMax, double waterMin, double waterMax, int plantMin, int plantMax, int animalMin, int animalMax) = biome switch
        {
            RegionBiome.Coast => (0.46, 0.72, 0.78, 0.98, 760, 1120, 220, 360),
            RegionBiome.RiverValley => (0.74, 0.95, 0.78, 0.96, 980, 1320, 280, 420),
            RegionBiome.Plains => (0.58, 0.82, 0.42, 0.66, 760, 1080, 190, 320),
            RegionBiome.Forest => (0.54, 0.80, 0.60, 0.84, 900, 1260, 180, 300),
            RegionBiome.Highlands => (0.34, 0.60, 0.34, 0.58, 520, 860, 120, 220),
            RegionBiome.Mountains => (0.18, 0.42, 0.20, 0.42, 340, 620, 80, 180),
            RegionBiome.Wetlands => (0.60, 0.84, 0.76, 0.96, 880, 1220, 200, 320),
            RegionBiome.Drylands => (0.18, 0.40, 0.12, 0.28, 260, 520, 90, 180),
            _ => (0.50, 0.70, 0.40, 0.60, 680, 1000, 160, 280)
        };

        region.Fertility = Roll(fertilityMin, fertilityMax);
        region.WaterAvailability = Roll(waterMin, waterMax);
        region.MaxPlantBiomass = _random.Next(plantMin, plantMax + 1);
        region.MaxAnimalBiomass = _random.Next(animalMin, animalMax + 1);
        region.PlantBiomass = region.MaxPlantBiomass * Roll(0.52, 0.72);
        region.AnimalBiomass = region.MaxAnimalBiomass * Roll(0.24, 0.44);
        AssignMaterialAbundance(region, biome);
    }

    private void AssignMaterialAbundance(Region region, RegionBiome biome)
    {
        double RollMaterial(double min, double max, int salt)
        {
            Random localRandom = new(MixSeed(_seed, region.Id, (int)biome, salt));
            return min + (localRandom.NextDouble() * (max - min));
        }

        (region.WoodAbundance, region.StoneAbundance, region.ClayAbundance, region.FiberAbundance, region.SaltAbundance, region.CopperOreAbundance, region.IronOreAbundance) = biome switch
        {
            RegionBiome.Coast => (RollMaterial(0.40, 0.68, 11), RollMaterial(0.34, 0.56, 12), RollMaterial(0.42, 0.68, 13), RollMaterial(0.38, 0.58, 14), RollMaterial(0.58, 0.92, 15), RollMaterial(0.10, 0.28, 16), RollMaterial(0.06, 0.18, 17)),
            RegionBiome.RiverValley => (RollMaterial(0.54, 0.80, 21), RollMaterial(0.26, 0.46, 22), RollMaterial(0.60, 0.88, 23), RollMaterial(0.58, 0.82, 24), RollMaterial(0.16, 0.34, 25), RollMaterial(0.08, 0.22, 26), RollMaterial(0.06, 0.16, 27)),
            RegionBiome.Plains => (RollMaterial(0.42, 0.64, 31), RollMaterial(0.28, 0.50, 32), RollMaterial(0.36, 0.58, 33), RollMaterial(0.44, 0.66, 34), RollMaterial(0.12, 0.26, 35), RollMaterial(0.10, 0.24, 36), RollMaterial(0.06, 0.18, 37)),
            RegionBiome.Forest => (RollMaterial(0.72, 0.96, 41), RollMaterial(0.24, 0.42, 42), RollMaterial(0.20, 0.40, 43), RollMaterial(0.52, 0.76, 44), RollMaterial(0.06, 0.18, 45), RollMaterial(0.08, 0.18, 46), RollMaterial(0.06, 0.16, 47)),
            RegionBiome.Highlands => (RollMaterial(0.30, 0.54, 51), RollMaterial(0.56, 0.82, 52), RollMaterial(0.30, 0.48, 53), RollMaterial(0.20, 0.38, 54), RollMaterial(0.10, 0.24, 55), RollMaterial(0.30, 0.54, 56), RollMaterial(0.22, 0.44, 57)),
            RegionBiome.Mountains => (RollMaterial(0.18, 0.34, 61), RollMaterial(0.74, 0.98, 62), RollMaterial(0.12, 0.24, 63), RollMaterial(0.10, 0.22, 64), RollMaterial(0.08, 0.20, 65), RollMaterial(0.42, 0.74, 66), RollMaterial(0.36, 0.70, 67)),
            RegionBiome.Wetlands => (RollMaterial(0.46, 0.70, 71), RollMaterial(0.18, 0.34, 72), RollMaterial(0.56, 0.82, 73), RollMaterial(0.64, 0.90, 74), RollMaterial(0.20, 0.38, 75), RollMaterial(0.06, 0.16, 76), RollMaterial(0.04, 0.12, 77)),
            RegionBiome.Drylands => (RollMaterial(0.14, 0.28, 81), RollMaterial(0.42, 0.68, 82), RollMaterial(0.18, 0.32, 83), RollMaterial(0.14, 0.26, 84), RollMaterial(0.44, 0.78, 85), RollMaterial(0.18, 0.34, 86), RollMaterial(0.14, 0.30, 87)),
            _ => (RollMaterial(0.36, 0.62, 91), RollMaterial(0.32, 0.54, 92), RollMaterial(0.28, 0.50, 93), RollMaterial(0.28, 0.52, 94), RollMaterial(0.12, 0.24, 95), RollMaterial(0.10, 0.20, 96), RollMaterial(0.08, 0.16, 97))
        };
    }

    private static double ScoreRegionForLineage(Species lineage, Region region)
        => SpeciesEcology.CalculateBaseHabitatSuitability(lineage, region)
           + (region.EffectiveEcologyProfile.MigrationEase * lineage.MigrationCapability * 0.08)
           + (region.EffectiveEcologyProfile.HabitabilityScore * lineage.StartingSpreadWeight * 0.10);

    private static double ResolveRangeThreshold(Species lineage)
        => lineage.TrophicRole switch
        {
            TrophicRole.Producer => 0.44,
            TrophicRole.Herbivore => 0.52,
            TrophicRole.Omnivore => 0.56,
            TrophicRole.Predator => 0.62,
            TrophicRole.Apex => 0.66,
            _ => 0.56
        };

    private int ResolveTargetRangeSize(Species lineage, int viableRegionCount, int totalRegionCount)
    {
        double desiredShare = lineage.TrophicRole switch
        {
            TrophicRole.Producer => 0.48 + (lineage.StartingSpreadWeight * 0.18),
            TrophicRole.Herbivore => 0.22 + (lineage.StartingSpreadWeight * 0.16),
            TrophicRole.Omnivore => 0.14 + (lineage.StartingSpreadWeight * 0.12),
            TrophicRole.Predator => 0.08 + (lineage.StartingSpreadWeight * 0.06),
            TrophicRole.Apex => 0.05 + (lineage.StartingSpreadWeight * 0.04),
            _ => 0.12
        };

        int desired = Math.Max(1, (int)Math.Round(totalRegionCount * desiredShare));
        return Math.Clamp(desired, 1, viableRegionCount);
    }

    private double SeedNoise(int speciesId, int regionId)
    {
        Random localRandom = new(MixSeed(_seed, speciesId, regionId, 991));
        return localRandom.NextDouble() * 0.035;
    }

    private static int MixSeed(params int[] values)
    {
        unchecked
        {
            int hash = (int)2166136261;
            foreach (int value in values)
            {
                hash = (hash ^ value) * 16777619;
            }

            return hash;
        }
    }

    private double Roll(double min, double max)
        => min + (_random.NextDouble() * (max - min));

    private void ValidateSettings()
    {
        if (_settings.RegionCount != _settings.ContinentWidth * _settings.ContinentHeight)
        {
            throw new InvalidOperationException("World generation settings require RegionCount to match ContinentWidth * ContinentHeight.");
        }

        if (_settings.InitialSpeciesCount > _primitiveTemplates.Count)
        {
            throw new InvalidOperationException("InitialSpeciesCount exceeds the available primitive lineage templates.");
        }
    }

    private static void AddConnection(Region a, Region b)
    {
        a.AddConnection(b.Id);
        b.AddConnection(a.Id);
    }

    private string NextRegionName(int index)
        => _regionNames.Count > 0 ? _regionNames.Dequeue() : $"Reach {index}";

    private IEnumerable<string> BuildShuffledNames(IReadOnlyList<string> names)
        => names.OrderBy(_ => _random.Next()).ToArray();
}
