using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;

namespace LivingWorld.Systems;

public sealed class EcosystemSystem
{
    private const double ProducerPopulationToBiomass = 2.2;
    private const double ConsumerPopulationToBiomass = 1.1;
    private readonly EcosystemSettings _settings;
    private readonly HashSet<string> _preyCollapseCooldownKeys = new(StringComparer.Ordinal);
    public EcosystemSeasonMetrics LastSeasonMetrics { get; private set; } = EcosystemSeasonMetrics.Empty;

    public EcosystemSystem(EcosystemSettings? settings = null)
    {
        _settings = settings ?? new EcosystemSettings();
    }

    public void InitializeRegionalPopulations(World world)
    {
        Dictionary<int, Species> speciesById = world.Species.ToDictionary(species => species.Id);

        foreach (Region region in world.Regions)
        {
            foreach (Species species in speciesById.Values)
            {
                if (species.InitialRangeRegionIds.Count > 0 && !species.InitialRangeRegionIds.Contains(region.Id))
                {
                    continue;
                }

                RegionSpeciesPopulation population = region.GetOrCreateSpeciesPopulation(species.Id);
                RefreshPopulationState(region, species, population);
                if (population.CarryingCapacity <= 0)
                {
                    region.RemoveSpeciesPopulation(species.Id);
                    continue;
                }

                population.PopulationCount = SpeciesEcology.CalculateInitialPopulation(species, population.CarryingCapacity, population.HabitatSuitability);
                if (population.PopulationCount > 0)
                {
                    population.MarkEstablished(world.Time.Year, world.Time.Month, "worldgen_seed", region.Id, species.Id);
                }
                else if (population.CanBePruned())
                {
                    region.RemoveSpeciesPopulation(species.Id);
                }
            }
        }

        SyncBiomeBiomass(world, speciesById);
    }

    public void UpdateSeason(World world)
    {
        Dictionary<int, Species> speciesById = world.Species.ToDictionary(species => species.Id);
        int activePopulationCount = 0;
        int ecologyIterations = 0;

        foreach (Region region in world.Regions)
        {
            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
            {
                if (!speciesById.TryGetValue(population.SpeciesId, out Species? species))
                {
                    continue;
                }

                RefreshPopulationState(region, species, population);
                ecologyIterations++;
                if (population.PopulationCount > 0)
                {
                    activePopulationCount++;
                }
            }

            ProcessRegionalGrowth(region, speciesById, world.Time.Season);
            ProcessRegionalFoodWeb(world, region, speciesById);
        }

        ProcessMigration(world, speciesById);
        LastSeasonMetrics = LastSeasonMetrics with
        {
            ActiveRegionalPopulationCount = activePopulationCount,
            EcologyIterations = ecologyIterations
        };
    }

    public void ResolveSeasonalCleanup(World world)
    {
        Dictionary<int, Species> speciesById = world.Species.ToDictionary(species => species.Id);
        ResolveExtinctions(world, speciesById);
        SyncBiomeBiomass(world, speciesById);
        LastSeasonMetrics = LastSeasonMetrics with
        {
            ActiveRegionalPopulationCount = world.Regions.Sum(region => region.SpeciesPopulations.Count(population => population.PopulationCount > 0))
        };
    }

    private static void RefreshPopulationState(Region region, Species species, RegionSpeciesPopulation population)
    {
        population.BaseHabitatSuitability = SpeciesEcology.CalculateBaseHabitatSuitability(species, region);
        population.HabitatSuitability = SpeciesEcology.CalculateHabitatSuitability(species, population, population.BaseHabitatSuitability);
        population.CarryingCapacity = SpeciesEcology.CalculateCarryingCapacity(species, population, region, population.HabitatSuitability);
        population.EstablishedThisSeason = false;
        population.ReceivedMigrantsThisSeason = false;
        population.SentMigrantsThisSeason = false;
    }

    private void ProcessRegionalGrowth(Region region, IReadOnlyDictionary<int, Species> speciesById, Season season)
    {
        foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
        {
            Species species = speciesById[population.SpeciesId];
            int previousPopulation = population.PopulationCount;

            if (previousPopulation <= 0)
            {
                population.RecentFoodStress = 0;
                population.RecentPredationPressure = 0;
                population.RecentHuntingPressure = Math.Max(0, population.RecentHuntingPressure * 0.65);
                population.MigrationPressure = 0;
                population.FounderSeasonsRemaining = 0;
                population.MigrationCooldownSeasons = Math.Max(0, population.MigrationCooldownSeasons - 1);
                continue;
            }

            population.HasEverExisted = true;
            population.LocalExtinctionRecorded = false;
            population.LastPopulationExitReason = null;

            double carryingRatio = population.CarryingCapacity <= 0
                ? 1.0
                : (double)previousPopulation / population.CarryingCapacity;
            double predatorSupportRatio = species.TrophicRole is TrophicRole.Predator or TrophicRole.Apex
                ? ResolvePredatorSupportRatio(region, speciesById, species, previousPopulation)
                : 0.0;
            bool predatorFounderPhase = IsPredatorFounderPhase(species, population, previousPopulation);
            double reproductionRate = species.BaseReproductionRate
                * species.GetSeasonalReproductionModifier(season)
                * population.HabitatSuitability
                * PopulationTraitResolver.ResolveReproductionModifier(species, population)
                * ResolveEcologicalGrowthModifier(region, species, population, predatorSupportRatio);
            double declineRate = species.BaseDeclineRate
                + Math.Max(0.0, carryingRatio - 1.0) * 0.10
                + (population.RecentFoodStress * 0.08)
                + (population.RecentPredationPressure * 0.05)
                + (population.RecentHuntingPressure * 0.06);
            declineRate *= PopulationTraitResolver.ResolveDeclineModifier(species, population, population.HabitatSuitability);

            if (species.TrophicRole is TrophicRole.Predator or TrophicRole.Apex)
            {
                if (predatorFounderPhase
                    && predatorSupportRatio >= _settings.PredatorEstablishmentSupportThreshold
                    && population.HabitatSuitability >= _settings.PredatorTargetSuitability)
                {
                    reproductionRate *= 1.0 + _settings.PredatorFounderGrowthBonus;
                }

                declineRate += Math.Max(0.0, 1.0 - predatorSupportRatio) * 0.08;
                if (predatorFounderPhase
                    && (predatorSupportRatio < _settings.PredatorFounderFailureSupportThreshold
                        || population.HabitatSuitability < _settings.MinimumTargetSuitability))
                {
                    declineRate += _settings.PredatorFounderFailureDeclinePenalty;
                }
            }

            int births = (int)Math.Round(previousPopulation * Math.Max(0.0, reproductionRate) * Math.Max(0.15, 1.0 - Math.Clamp(carryingRatio, 0.0, 1.35)));
            int naturalLosses = (int)Math.Round(previousPopulation * Math.Max(0.0, declineRate));

            if (species.TrophicRole is TrophicRole.Predator or TrophicRole.Apex && predatorFounderPhase)
            {
                if (predatorSupportRatio >= _settings.PredatorEstablishmentSupportThreshold
                    && population.HabitatSuitability >= _settings.PredatorTargetSuitability)
                {
                    births = Math.Max(births, 1);
                }
                else if (predatorSupportRatio < _settings.PredatorFounderFailureSupportThreshold
                         || population.HabitatSuitability < _settings.MinimumTargetSuitability)
                {
                    naturalLosses = Math.Max(naturalLosses, 1);
                }
            }

            population.PopulationCount = Math.Max(0, previousPopulation + births - naturalLosses);
            population.MigrationPressure = Math.Clamp(
                (species.ExpansionPressure * 0.45) +
                (Math.Max(0.0, carryingRatio - 0.85) * 0.65) +
                (population.RecentFoodStress * 0.35) +
                (Math.Max(0.0, 0.78 - population.HabitatSuitability) * 0.30),
                0.0,
                1.0);

            if (population.PopulationCount < Math.Max(2, population.CarryingCapacity / 25))
            {
                population.SeasonsUnderPressure++;
            }
            else
            {
                population.SeasonsUnderPressure = Math.Max(0, population.SeasonsUnderPressure - 1);
            }

            if (species.TrophicRole is TrophicRole.Predator or TrophicRole.Apex)
            {
                if (population.PopulationCount >= ResolvePredatorEstablishmentPopulationThreshold(species)
                    && predatorSupportRatio >= _settings.PredatorEstablishmentSupportThreshold)
                {
                    population.FounderSeasonsRemaining = 0;
                }
                else
                {
                    population.FounderSeasonsRemaining = Math.Max(0, population.FounderSeasonsRemaining - 1);
                }
            }
            else
            {
                population.FounderSeasonsRemaining = 0;
            }

            population.MigrationCooldownSeasons = Math.Max(0, population.MigrationCooldownSeasons - 1);
            population.RecentFoodStress *= 0.70;
            population.RecentPredationPressure *= 0.65;
            population.RecentHuntingPressure *= 0.72;
        }
    }

    private void ProcessRegionalFoodWeb(World world, Region region, IReadOnlyDictionary<int, Species> speciesById)
    {
        int producerCount = 0;
        foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
        {
            if (speciesById[population.SpeciesId].TrophicRole == TrophicRole.Producer)
            {
                producerCount += population.PopulationCount;
            }
        }

        foreach (RegionSpeciesPopulation predatorPopulation in region.SpeciesPopulations)
        {
            if (predatorPopulation.PopulationCount <= 0)
            {
                continue;
            }

            Species predatorSpecies = speciesById[predatorPopulation.SpeciesId];

            if (predatorSpecies.TrophicRole == TrophicRole.Producer)
            {
                continue;
            }

            List<RegionSpeciesPopulation> preyOptions = [];
            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
            {
                if (population.PopulationCount > 0 && predatorSpecies.DietSpeciesIds.Contains(population.SpeciesId))
                {
                    preyOptions.Add(population);
                }
            }

            if (preyOptions.Count == 0)
            {
                predatorPopulation.RecentFoodStress = Math.Clamp(predatorPopulation.RecentFoodStress + 0.35, 0.0, 1.0);
                continue;
            }

            double availableFood = preyOptions.Sum(population => population.PopulationCount);
            if (predatorSpecies.TrophicRole == TrophicRole.Herbivore)
            {
                availableFood += producerCount * 0.75;
            }
            else if (predatorSpecies.TrophicRole == TrophicRole.Omnivore)
            {
                availableFood += producerCount * 0.35;
            }

            double foodNeed = Math.Max(1.0, predatorPopulation.PopulationCount * ResolveFoodNeedFactor(predatorSpecies));
            double foodRatio = availableFood / foodNeed;
            predatorPopulation.RecentFoodStress = foodRatio >= 1.0
                ? Math.Max(0.0, predatorPopulation.RecentFoodStress - 0.18)
                : Math.Clamp(predatorPopulation.RecentFoodStress + (1.0 - foodRatio) * 0.45, 0.0, 1.0);

            foreach (RegionSpeciesPopulation preyPopulation in preyOptions)
            {
                Species preySpecies = speciesById[preyPopulation.SpeciesId];
                int preyBefore = preyPopulation.PopulationCount;
                double demandShare = availableFood <= 0 ? 0.0 : preyBefore / availableFood;
                double offense = ResolvePredationFactor(predatorSpecies) * PopulationTraitResolver.ResolvePredationOffense(predatorSpecies, predatorPopulation);
                double defense = PopulationTraitResolver.ResolvePreyDefense(preySpecies, preyPopulation);
                double predationIntensity = predatorPopulation.PopulationCount * offense * demandShare;
                int preyLoss = (int)Math.Round(Math.Min(preyBefore * 0.18, predationIntensity / Math.Max(0.70, defense)));
                if (preyLoss <= 0)
                {
                    continue;
                }

                preyPopulation.PopulationCount = Math.Max(0, preyPopulation.PopulationCount - preyLoss);
                if (preyPopulation.PopulationCount <= 0)
                {
                    preyPopulation.MarkLocalExtinction(world.Time.Year, world.Time.Month, "predation");
                }
                preyPopulation.RecentPredationPressure = Math.Clamp(preyPopulation.RecentPredationPressure + ((double)preyLoss / Math.Max(1, preyBefore)), 0.0, 1.0);

                if (ShouldEmitPreyCollapse(preyPopulation, preyBefore))
                {
                    EmitPreyCollapse(world, region, predatorSpecies, preySpecies, preyPopulation, preyBefore);
                }
            }

            if (foodRatio < 0.55)
            {
                EmitPredatorPressure(world, region, predatorSpecies, predatorPopulation, foodRatio);
            }
        }
    }

    private void ProcessMigration(World world, IReadOnlyDictionary<int, Species> speciesById)
    {
        foreach (Region region in world.Regions)
        {
            List<RegionSpeciesPopulation> activePopulations = [];
            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
            {
                if (population.PopulationCount > 0)
                {
                    activePopulations.Add(population);
                }
            }

            foreach (RegionSpeciesPopulation sourcePopulation in activePopulations)
            {
                Species species = speciesById[sourcePopulation.SpeciesId];
                if (species.TrophicRole == TrophicRole.Producer
                    || region.ConnectedRegionIds.Count == 0
                    || species.MigrationCapability <= 0
                    || sourcePopulation.MigrationCooldownSeasons > 0
                    || !CanSourceAttemptMigration(region, speciesById, species, sourcePopulation))
                {
                    continue;
                }

                List<MigrationCandidate> candidates = region.ConnectedRegionIds
                    .Select(candidateRegionId => EvaluateMigrationCandidate(world, speciesById, region, species, sourcePopulation, world.Regions[candidateRegionId]))
                    .Where(candidate => candidate is not null)
                    .Cast<MigrationCandidate>()
                    .OrderByDescending(candidate => candidate.Score)
                    .ToList();

                if (candidates.Count == 0)
                {
                    continue;
                }

                foreach (MigrationCandidate candidate in PrioritizeMigrationCandidates(species, candidates)
                             .Take(_settings.MaxMigrationTargetsPerPopulation))
                {
                    ExecuteMigration(world, species, speciesById, region, sourcePopulation, candidate);
                }
            }
        }
    }

    private void ResolveExtinctions(World world, IReadOnlyDictionary<int, Species> speciesById)
    {
        foreach (Region region in world.Regions)
        {
            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations.ToList())
            {
                if (population.PopulationCount > 0)
                {
                    population.LocalExtinctionRecorded = false;
                    population.LastPopulationExitReason = null;
                    continue;
                }

                Species species = speciesById[population.SpeciesId];
                bool wasPresentBefore = population.HasEverExisted;
                bool alreadyRecordedThisSeason = population.LastLocalExtinctionYear == world.Time.Year
                    && population.LastLocalExtinctionMonth == world.Time.Month;
                bool suppressBecauseSpeciation = string.Equals(population.LastPopulationExitReason, "speciation_split", StringComparison.Ordinal);

                if (wasPresentBefore && !population.LocalExtinctionRecorded && !alreadyRecordedThisSeason && !suppressBecauseSpeciation)
                {
                    world.AddEvent(
                        WorldEventType.LocalSpeciesExtinction,
                        WorldEventSeverity.Notable,
                        $"{species.Name} vanished from {region.Name}",
                        $"{species.Name} no longer has a surviving regional population in {region.Name}.",
                        reason: "local_extinction",
                        scope: WorldEventScope.Regional,
                        speciesId: species.Id,
                        speciesName: species.Name,
                        regionId: region.Id,
                        regionName: region.Name,
                        after: new Dictionary<string, string>
                        {
                            ["population"] = "0"
                        });
                }

                if (wasPresentBefore && !population.LocalExtinctionRecorded)
                {
                    population.MarkLocalExtinction(world.Time.Year, world.Time.Month, population.LastPopulationExitReason ?? "local_extinction");
                }

                population.RecentPredationPressure = 0;
                population.RecentHuntingPressure = 0;
                population.SeasonsUnderPressure = 0;

                if (population.CanBePruned())
                {
                    region.RemoveSpeciesPopulation(population.SpeciesId);
                }
            }
        }

        foreach (Species species in speciesById.Values)
        {
            bool anySurvivors = false;
            foreach (Region region in world.Regions)
            {
                if (region.GetSpeciesPopulation(species.Id)?.PopulationCount > 0)
                {
                    anySurvivors = true;
                    break;
                }
            }

            if (!anySurvivors)
            {
                if (species.IsGloballyExtinct)
                {
                    continue;
                }

                species.IsGloballyExtinct = true;
                species.ExtinctionYear = world.Time.Year;
                species.ExtinctionMonth = world.Time.Month;

                world.AddEvent(
                    WorldEventType.GlobalSpeciesExtinction,
                    WorldEventSeverity.Legendary,
                    $"{species.Name} passed into global extinction",
                    $"{species.Name} has no surviving populations anywhere in the world.",
                    reason: "global_extinction",
                    scope: WorldEventScope.World,
                    speciesId: species.Id,
                    speciesName: species.Name);
            }
            else
            {
                species.IsGloballyExtinct = false;
                species.ExtinctionYear = null;
                species.ExtinctionMonth = null;
            }
        }
    }

    private void SyncBiomeBiomass(World world, IReadOnlyDictionary<int, Species> speciesById)
    {
        foreach (Region region in world.Regions)
        {
            int producerPopulation = 0;
            int consumerPopulation = 0;

            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
            {
                Species species = speciesById[population.SpeciesId];
                if (species.TrophicRole == TrophicRole.Producer)
                {
                    producerPopulation += population.PopulationCount;
                }
                else
                {
                    consumerPopulation += population.PopulationCount;
                }
            }

            region.PlantBiomass = Math.Min(region.MaxPlantBiomass, producerPopulation * ProducerPopulationToBiomass);
            region.AnimalBiomass = Math.Min(region.MaxAnimalBiomass, consumerPopulation * ConsumerPopulationToBiomass);
        }
    }

    private static double ResolveFoodNeedFactor(Species species)
        => species.TrophicRole switch
        {
            TrophicRole.Herbivore => 0.76,
            TrophicRole.Omnivore => 0.64,
            TrophicRole.Predator => 0.50,
            TrophicRole.Apex => 0.44,
            _ => 0.55
        };

    private static double ResolvePredationFactor(Species species)
        => species.TrophicRole switch
        {
            TrophicRole.Omnivore => 0.16,
            TrophicRole.Predator => 0.22,
            TrophicRole.Apex => 0.28,
            _ => 0.10
        };

    private double ScoreMigrationTarget(World world, IReadOnlyDictionary<int, Species> speciesById, Species species, RegionSpeciesPopulation sourcePopulation, Region target)
    {
        RegionSpeciesPopulation? existing = target.GetSpeciesPopulation(species.Id);
        RegionSpeciesPopulation candidatePopulation = existing ?? new RegionSpeciesPopulation(species.Id, target.Id, 0)
        {
            IntelligenceOffset = sourcePopulation.IntelligenceOffset,
            SocialityOffset = sourcePopulation.SocialityOffset,
            AggressionOffset = sourcePopulation.AggressionOffset,
            EnduranceOffset = sourcePopulation.EnduranceOffset,
            FertilityOffset = sourcePopulation.FertilityOffset,
            DietFlexibilityOffset = sourcePopulation.DietFlexibilityOffset,
            ClimateToleranceOffset = sourcePopulation.ClimateToleranceOffset,
            SizeOffset = sourcePopulation.SizeOffset
        };
        double baseSuitability = SpeciesEcology.CalculateBaseHabitatSuitability(species, target);
        double suitability = SpeciesEcology.CalculateHabitatSuitability(species, candidatePopulation, baseSuitability);
        int localPopulation = existing?.PopulationCount ?? 0;
        int carryingCapacity = SpeciesEcology.CalculateCarryingCapacity(species, candidatePopulation, target, suitability);
        double openness = carryingCapacity <= 0
            ? 0.0
            : Math.Clamp((double)(carryingCapacity - localPopulation) / carryingCapacity, 0.0, 1.0);

        double predatorSafety = species.TrophicRole switch
        {
            TrophicRole.Producer => 1.0,
            TrophicRole.Herbivore => 1.0 - CountPredatorPressure(target, speciesById),
            _ => 0.7 + (openness * 0.3)
        };

        return (suitability * 0.55) + (openness * 0.30) + (predatorSafety * 0.15);
    }

    private static double CountPredatorPressure(Region region, IReadOnlyDictionary<int, Species> speciesById)
    {
        int predatorPopulation = 0;
        foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
        {
            if (population.PopulationCount <= 0)
            {
                continue;
            }

            TrophicRole role = speciesById[population.SpeciesId].TrophicRole;
            if (role is TrophicRole.Predator or TrophicRole.Apex)
            {
                predatorPopulation += population.PopulationCount;
            }
        }

        return Math.Clamp(predatorPopulation / 250.0, 0.0, 1.0);
    }

    private bool CanAttemptRecolonization(
        RegionSpeciesPopulation sourcePopulation,
        Species species,
        Region target,
        double bestTargetScore)
    {
        RegionSpeciesPopulation? targetPopulation = target.GetSpeciesPopulation(species.Id);
        if (targetPopulation?.PopulationCount > 0)
        {
            return false;
        }

        if (bestTargetScore < _settings.RecolonizationTargetScoreThreshold || sourcePopulation.CarryingCapacity <= 0)
        {
            return false;
        }

        double sourceCapacityRatio = (double)sourcePopulation.PopulationCount / sourcePopulation.CarryingCapacity;
        return sourcePopulation.PopulationCount >= Math.Max(8, sourcePopulation.CarryingCapacity / 5)
            && sourceCapacityRatio >= 0.42;
    }

    private bool CanSourceAttemptMigration(
        Region sourceRegion,
        IReadOnlyDictionary<int, Species> speciesById,
        Species species,
        RegionSpeciesPopulation sourcePopulation)
    {
        int minimumSourcePopulation = ResolveMinimumSourcePopulation(species);
        if (sourcePopulation.PopulationCount < minimumSourcePopulation || sourcePopulation.CarryingCapacity <= 0)
        {
            return false;
        }

        double sourceCapacityRatio = (double)sourcePopulation.PopulationCount / sourcePopulation.CarryingCapacity;
        int preySupport = ResolvePreySupportPopulation(sourceRegion, speciesById, species);

        return species.TrophicRole switch
        {
            TrophicRole.Herbivore or TrophicRole.Omnivore =>
                sourceCapacityRatio >= _settings.HerbivoreExpansionCapacityRatioThreshold
                || sourcePopulation.MigrationPressure >= _settings.MigrationPressureThreshold,
            TrophicRole.Predator =>
                preySupport >= _settings.PredatorMinimumPreyPopulation
                && ResolvePredatorSupportRatio(sourceRegion, speciesById, species, sourcePopulation.PopulationCount) >= _settings.PredatorMigrationSupportRatioThreshold
                && (sourceCapacityRatio >= _settings.PredatorExpansionCapacityRatioThreshold
                    || sourcePopulation.MigrationPressure >= _settings.PredatorMigrationPressureThreshold),
            TrophicRole.Apex =>
                preySupport >= _settings.ApexMinimumPreyPopulation
                && ResolvePredatorSupportRatio(sourceRegion, speciesById, species, sourcePopulation.PopulationCount) >= _settings.ApexMigrationSupportRatioThreshold
                && (sourceCapacityRatio >= (_settings.PredatorExpansionCapacityRatioThreshold + 0.08)
                    || sourcePopulation.MigrationPressure >= (_settings.PredatorMigrationPressureThreshold + 0.08)),
            _ => false
        };
    }

    private IEnumerable<MigrationCandidate> PrioritizeMigrationCandidates(Species species, IReadOnlyList<MigrationCandidate> candidates)
    {
        if (species.TrophicRole is TrophicRole.Herbivore or TrophicRole.Omnivore)
        {
            List<MigrationCandidate> frontierCandidates = candidates
                .Where(candidate => candidate.Kind is "frontier" or "recolonization")
                .ToList();
            if (frontierCandidates.Count > 0)
            {
                return frontierCandidates;
            }
        }

        return candidates;
    }

    private MigrationCandidate? EvaluateMigrationCandidate(
        World world,
        IReadOnlyDictionary<int, Species> speciesById,
        Region sourceRegion,
        Species species,
        RegionSpeciesPopulation sourcePopulation,
        Region target)
    {
        RegionSpeciesPopulation? targetPopulation = target.GetSpeciesPopulation(species.Id);
        double suitabilityScore = ScoreMigrationTarget(world, speciesById, species, sourcePopulation, target);
        bool emptyTarget = targetPopulation?.PopulationCount <= 0;
        double targetSuitability = ResolveTargetSuitability(species, target, targetPopulation);
        double openness = ResolveOpenness(species, target, targetPopulation);
        int preySupport = ResolvePreySupportPopulation(target, speciesById, species);
        int existingFaunaPopulation = ResolveConsumerPopulation(target, speciesById);
        bool faunaFrontier = emptyTarget && existingFaunaPopulation == 0;
        bool herbivoreFrontier = emptyTarget && ResolveHerbivorePopulation(target, speciesById) == 0;
        bool canRecolonize = emptyTarget && CanAttemptRecolonization(sourcePopulation, species, target, suitabilityScore);

        if (species.TrophicRole is TrophicRole.Herbivore or TrophicRole.Omnivore)
        {
            double minimumSuitability = faunaFrontier || herbivoreFrontier
                ? _settings.FrontierTargetSuitability
                : _settings.MinimumTargetSuitability;
            if (targetSuitability < minimumSuitability)
            {
                return null;
            }

            string kind = canRecolonize
                ? "recolonization"
                : faunaFrontier || herbivoreFrontier
                    ? "frontier"
                    : "pressure";
            double score = suitabilityScore
                + (faunaFrontier ? _settings.EmptyFaunaFrontierBonus : 0.0)
                + (herbivoreFrontier && species.TrophicRole == TrophicRole.Herbivore ? _settings.HerbivoreFrontierBonus : 0.0)
                + Math.Max(0.0, sourcePopulation.MigrationPressure - _settings.MigrationPressureThreshold) * 0.18;

            int founderPopulation = ResolveFounderPopulation(species, speciesById, sourcePopulation, target);
            if (founderPopulation <= 0)
            {
                return null;
            }

            WorldEventSeverity severity = faunaFrontier && species.TrophicRole == TrophicRole.Herbivore && targetSuitability >= 0.85
                ? WorldEventSeverity.Major
                : WorldEventSeverity.Minor;
            return new MigrationCandidate(target, score, kind, founderPopulation, severity);
        }

        int minimumPreyPopulation = species.TrophicRole == TrophicRole.Apex
            ? _settings.ApexMinimumPreyPopulation
            : _settings.PredatorMinimumPreyPopulation;
        int predatorFounderPopulation = ResolveFounderPopulation(species, speciesById, sourcePopulation, target);
        double supportRatio = ResolvePredatorSupportRatio(target, speciesById, species, Math.Max(1, predatorFounderPopulation));
        if (preySupport < minimumPreyPopulation
            || targetSuitability < _settings.PredatorTargetSuitability
            || supportRatio < ResolvePredatorMigrationSupportThreshold(species))
        {
            return null;
        }

        double predatorScore = suitabilityScore
            + Math.Min(0.24, preySupport / 220.0)
            + Math.Min(0.18, supportRatio * 0.12)
            + (openness * 0.08)
            - (CountPredatorSpecies(target, speciesById) * _settings.PredatorCompetitionPenalty)
            - (ResolveOccupiedRegionRatio(world, species) * _settings.PredatorGlobalRangePenalty);
        if (predatorFounderPopulation <= 0)
        {
            return null;
        }

        WorldEventSeverity predatorSeverity = emptyTarget && species.TrophicRole == TrophicRole.Apex
            ? WorldEventSeverity.Major
            : WorldEventSeverity.Notable;
        return new MigrationCandidate(target, predatorScore, "predator_follow", predatorFounderPopulation, predatorSeverity);
    }

    private void ExecuteMigration(
        World world,
        Species species,
        IReadOnlyDictionary<int, Species> speciesById,
        Region sourceRegion,
        RegionSpeciesPopulation sourcePopulation,
        MigrationCandidate candidate)
    {
        Region target = candidate.Target;
        RegionSpeciesPopulation targetPopulation = target.GetOrCreateSpeciesPopulation(species.Id);
        int sourceBefore = sourcePopulation.PopulationCount;
        int minimumSourceRemnant = Math.Max(1, ResolveMinimumSourcePopulation(species) / 2);
        int transfer = Math.Min(candidate.FounderPopulation, Math.Max(0, sourcePopulation.PopulationCount - minimumSourceRemnant));
        if (transfer <= 0)
        {
            return;
        }

        bool newlyEstablished = targetPopulation.PopulationCount == 0;
        sourcePopulation.PopulationCount -= transfer;
        targetPopulation.PopulationCount += transfer;
        sourcePopulation.SentMigrantsThisSeason = true;
        sourcePopulation.MigrationCooldownSeasons = _settings.MigrationCooldownSeasons;
        targetPopulation.ReceivedMigrantsThisSeason = true;
        targetPopulation.MigrationCooldownSeasons = _settings.MigrationCooldownSeasons;
        targetPopulation.BaseHabitatSuitability = SpeciesEcology.CalculateBaseHabitatSuitability(species, target);
        targetPopulation.HabitatSuitability = SpeciesEcology.CalculateHabitatSuitability(species, targetPopulation, targetPopulation.BaseHabitatSuitability);
        targetPopulation.CarryingCapacity = SpeciesEcology.CalculateCarryingCapacity(species, targetPopulation, target, targetPopulation.HabitatSuitability);
        targetPopulation.EstablishedThisSeason = newlyEstablished;
        bool recolonization = newlyEstablished && targetPopulation.HasEverExisted && targetPopulation.LocalExtinctionRecorded;
        if (newlyEstablished)
        {
            targetPopulation.MarkEstablished(world.Time.Year, world.Time.Month, candidate.Kind, sourceRegion.Id, species.Id);
        }
        if (species.TrophicRole is TrophicRole.Predator or TrophicRole.Apex)
        {
            targetPopulation.FounderSeasonsRemaining = _settings.PredatorFounderSeasons;
        }

        if (!newlyEstablished)
        {
            return;
        }

        world.AddEvent(
            recolonization ? WorldEventType.SpeciesPopulationRecolonized : WorldEventType.SpeciesPopulationEstablished,
            candidate.Severity,
            recolonization
                ? $"{species.Name} returned to {target.Name}"
                : $"{species.Name} established a new population in {target.Name}",
            recolonization
                ? $"{transfer} {species.Name} recolonized {target.Name} from {sourceRegion.Name}."
                : $"{transfer} {species.Name} migrated from {sourceRegion.Name} into {target.Name}.",
            reason: recolonization ? "regional_recolonization" : "seasonal_species_migration",
            scope: WorldEventScope.Regional,
            speciesId: species.Id,
            speciesName: species.Name,
            regionId: target.Id,
            regionName: target.Name,
            before: new Dictionary<string, string>
            {
                ["population"] = "0"
            },
            after: new Dictionary<string, string>
            {
                ["population"] = targetPopulation.PopulationCount.ToString()
            },
            metadata: new Dictionary<string, string>
            {
                ["sourceRegionId"] = sourceRegion.Id.ToString(),
                ["sourceRegionName"] = sourceRegion.Name,
                ["migrationKind"] = candidate.Kind,
                ["sourcePopulationBefore"] = sourceBefore.ToString(),
                ["founderPopulation"] = transfer.ToString(),
                ["founderKind"] = candidate.Kind,
                ["founderYear"] = world.Time.Year.ToString(),
                ["founderMonth"] = world.Time.Month.ToString()
            });
    }

    private double ResolveTargetSuitability(Species species, Region target, RegionSpeciesPopulation? targetPopulation)
    {
        RegionSpeciesPopulation candidatePopulation = targetPopulation ?? new RegionSpeciesPopulation(species.Id, target.Id, 0);
        return SpeciesEcology.CalculateHabitatSuitability(species, candidatePopulation, SpeciesEcology.CalculateBaseHabitatSuitability(species, target));
    }

    private double ResolveOpenness(Species species, Region target, RegionSpeciesPopulation? targetPopulation)
    {
        RegionSpeciesPopulation candidatePopulation = targetPopulation ?? new RegionSpeciesPopulation(species.Id, target.Id, 0);
        double suitability = ResolveTargetSuitability(species, target, targetPopulation);
        int localPopulation = targetPopulation?.PopulationCount ?? 0;
        int carryingCapacity = SpeciesEcology.CalculateCarryingCapacity(species, candidatePopulation, target, suitability);
        return carryingCapacity <= 0
            ? 0.0
            : Math.Clamp((double)(carryingCapacity - localPopulation) / carryingCapacity, 0.0, 1.0);
    }

    private int ResolveFounderPopulation(
        Species species,
        IReadOnlyDictionary<int, Species> speciesById,
        RegionSpeciesPopulation sourcePopulation,
        Region target)
    {
        double migrationCapability = PopulationTraitResolver.GetEffectiveMigrationCapability(species, sourcePopulation);
        double roleShare = species.TrophicRole switch
        {
            TrophicRole.Herbivore => _settings.FounderPopulationShare + 0.02,
            TrophicRole.Omnivore => _settings.FounderPopulationShare + 0.01,
            TrophicRole.Predator => _settings.PredatorFounderPopulationShare,
            TrophicRole.Apex => _settings.ApexFounderPopulationShare,
            _ => _settings.FounderPopulationShare
        };
        double transferShare = Math.Min(0.14, roleShare + (migrationCapability * 0.05) + (sourcePopulation.MigrationPressure * 0.05));
        int minimumFounderPopulation = species.TrophicRole switch
        {
            TrophicRole.Herbivore => _settings.FounderPopulationMinimum + 2,
            TrophicRole.Omnivore => _settings.FounderPopulationMinimum + 1,
            TrophicRole.Predator => _settings.PredatorFounderPopulationMinimum,
            TrophicRole.Apex => _settings.ApexFounderPopulationMinimum,
            _ => _settings.FounderPopulationMinimum
        };

        if (species.TrophicRole is TrophicRole.Predator or TrophicRole.Apex)
        {
            int preySupport = ResolvePreySupportPopulation(target, speciesById, species);
            transferShare = Math.Min(0.16, transferShare + Math.Min(0.03, preySupport / 260.0));
        }

        int founderPopulation = Math.Max(minimumFounderPopulation, (int)Math.Round(sourcePopulation.PopulationCount * transferShare));
        RegionSpeciesPopulation candidatePopulation = new(species.Id, target.Id, founderPopulation);
        double suitability = SpeciesEcology.CalculateHabitatSuitability(species, candidatePopulation, SpeciesEcology.CalculateBaseHabitatSuitability(species, target));
        int carryingCapacity = SpeciesEcology.CalculateCarryingCapacity(species, candidatePopulation, target, suitability);
        if (carryingCapacity > 0)
        {
            int carryingCapacityFounderCap = species.TrophicRole is TrophicRole.Predator or TrophicRole.Apex
                ? Math.Max(minimumFounderPopulation, carryingCapacity / 4)
                : Math.Max(minimumFounderPopulation, carryingCapacity / 6);
            founderPopulation = Math.Min(founderPopulation, carryingCapacityFounderCap);
        }

        return Math.Min(founderPopulation, Math.Max(0, sourcePopulation.PopulationCount - 1));
    }

    private int ResolveMinimumSourcePopulation(Species species)
        => species.TrophicRole switch
        {
            TrophicRole.Herbivore => _settings.MinimumSourcePopulationForMigration,
            TrophicRole.Omnivore => _settings.MinimumSourcePopulationForMigration,
            TrophicRole.Predator => _settings.MinimumSourcePopulationForMigration + 4,
            TrophicRole.Apex => _settings.MinimumSourcePopulationForMigration + 8,
            _ => int.MaxValue
        };

    private static int ResolveConsumerPopulation(Region region, IReadOnlyDictionary<int, Species> speciesById)
        => region.SpeciesPopulations
            .Where(population => population.PopulationCount > 0
                && speciesById[population.SpeciesId].TrophicRole != TrophicRole.Producer)
            .Sum(population => population.PopulationCount);

    private static int ResolveHerbivorePopulation(Region region, IReadOnlyDictionary<int, Species> speciesById)
        => region.SpeciesPopulations
            .Where(population => population.PopulationCount > 0
                && speciesById[population.SpeciesId].TrophicRole == TrophicRole.Herbivore)
            .Sum(population => population.PopulationCount);

    private static int ResolvePreySupportPopulation(Region region, IReadOnlyDictionary<int, Species> speciesById, Species species)
        => region.SpeciesPopulations
            .Where(population => population.PopulationCount > 0
                && species.DietSpeciesIds.Contains(population.SpeciesId)
                && speciesById[population.SpeciesId].TrophicRole != TrophicRole.Producer)
            .Sum(population => population.PopulationCount);

    private int CountPredatorSpecies(Region region, IReadOnlyDictionary<int, Species> speciesById)
        => region.SpeciesPopulations.Count(population =>
            population.PopulationCount > 0
            && speciesById[population.SpeciesId].TrophicRole is TrophicRole.Predator or TrophicRole.Apex);

    private static double ResolveOccupiedRegionRatio(World world, Species species)
    {
        int occupiedRegions = world.Regions.Count(region => region.GetSpeciesPopulation(species.Id)?.PopulationCount > 0);
        return world.Regions.Count == 0
            ? 0.0
            : (double)occupiedRegions / world.Regions.Count;
    }

    private double ResolvePredatorSupportRatio(
        Region region,
        IReadOnlyDictionary<int, Species> speciesById,
        Species species,
        int predatorPopulation)
    {
        int preySupport = ResolvePreySupportPopulation(region, speciesById, species);
        double minimumSupport = species.TrophicRole == TrophicRole.Apex
            ? _settings.ApexMinimumPreyPopulation
            : _settings.PredatorMinimumPreyPopulation;
        double preyPerPredator = species.TrophicRole == TrophicRole.Apex
            ? _settings.ApexPreyPerPredatorRequired
            : _settings.PredatorPreyPerPredatorRequired;
        double requiredSupport = Math.Max(minimumSupport, predatorPopulation * preyPerPredator);
        return requiredSupport <= 0
            ? 0.0
            : preySupport / requiredSupport;
    }

    private double ResolvePredatorMigrationSupportThreshold(Species species)
        => species.TrophicRole == TrophicRole.Apex
            ? _settings.ApexMigrationSupportRatioThreshold
            : _settings.PredatorMigrationSupportRatioThreshold;

    private bool IsPredatorFounderPhase(Species species, RegionSpeciesPopulation population, int previousPopulation)
        => species.TrophicRole is TrophicRole.Predator or TrophicRole.Apex
           && (population.FounderSeasonsRemaining > 0 || previousPopulation < ResolvePredatorEstablishmentPopulationThreshold(species));

    private int ResolvePredatorEstablishmentPopulationThreshold(Species species)
        => species.TrophicRole == TrophicRole.Apex
            ? _settings.ApexEstablishmentPopulationThreshold
            : _settings.PredatorEstablishmentPopulationThreshold;

    private sealed record MigrationCandidate(
        Region Target,
        double Score,
        string Kind,
        int FounderPopulation,
        WorldEventSeverity Severity);

    private double ResolveEcologicalGrowthModifier(
        Region region,
        Species species,
        RegionSpeciesPopulation population,
        double predatorSupportRatio)
    {
        double plantRatio = region.MaxPlantBiomass <= 0
            ? 0.0
            : Math.Clamp(region.PlantBiomass / region.MaxPlantBiomass, 0.0, 1.25);
        double animalRatio = region.MaxAnimalBiomass <= 0
            ? 0.0
            : Math.Clamp(region.AnimalBiomass / region.MaxAnimalBiomass, 0.0, 1.25);
        double lowPressureRelief = Math.Max(0.0, 0.45 - population.RecentPredationPressure) * 0.28
            + Math.Max(0.0, 0.35 - population.RecentHuntingPressure) * 0.20;

        double modifier = species.TrophicRole switch
        {
            TrophicRole.Producer => 0.98 + (plantRatio * 0.08),
            TrophicRole.Herbivore => 0.92 + (plantRatio * 0.32) + lowPressureRelief,
            TrophicRole.Omnivore => 0.94 + (plantRatio * 0.12) + (animalRatio * 0.10) + (lowPressureRelief * 0.60),
            TrophicRole.Predator => 0.84 + (animalRatio * 0.06) + Math.Min(0.30, predatorSupportRatio * 0.18),
            TrophicRole.Apex => 0.80 + (animalRatio * 0.05) + Math.Min(0.24, predatorSupportRatio * 0.15),
            _ => 1.0
        };

        return Math.Clamp(modifier, 0.75, 1.35);
    }

    private bool ShouldEmitPreyCollapse(RegionSpeciesPopulation preyPopulation, int preyBefore)
    {
        if (preyBefore <= 0)
        {
            return false;
        }

        double declineRatio = (double)(preyBefore - preyPopulation.PopulationCount) / preyBefore;
        if (declineRatio < 0.30 && preyPopulation.PopulationCount > Math.Max(4, preyPopulation.CarryingCapacity / 8))
        {
            return false;
        }

        string key = $"{preyPopulation.RegionId}:{preyPopulation.SpeciesId}:{preyPopulation.PopulationCount <= 0}";
        return _preyCollapseCooldownKeys.Add(key);
    }

    private static void EmitPreyCollapse(
        World world,
        Region region,
        Species predatorSpecies,
        Species preySpecies,
        RegionSpeciesPopulation preyPopulation,
        int preyBefore)
    {
        string eventType = preyPopulation.PopulationCount <= 0
            ? WorldEventType.LocalSpeciesExtinction
            : WorldEventType.PreyCollapse;
        WorldEventSeverity severity = preyPopulation.PopulationCount <= 0
            ? WorldEventSeverity.Major
            : WorldEventSeverity.Notable;
        string narrative = preyPopulation.PopulationCount <= 0
            ? $"{preySpecies.Name} disappeared from {region.Name} under predation pressure"
            : $"{preySpecies.Name} collapsed in {region.Name} under predator pressure";

        world.AddEvent(
            eventType,
            severity,
            narrative,
            $"{predatorSpecies.Name} drove {preySpecies.Name} from {preyBefore} to {preyPopulation.PopulationCount} in {region.Name}.",
            reason: "predator_prey_imbalance",
            scope: WorldEventScope.Regional,
            speciesId: preySpecies.Id,
            speciesName: preySpecies.Name,
            regionId: region.Id,
            regionName: region.Name,
            before: new Dictionary<string, string>
            {
                ["population"] = preyBefore.ToString()
            },
            after: new Dictionary<string, string>
            {
                ["population"] = preyPopulation.PopulationCount.ToString()
            },
            metadata: new Dictionary<string, string>
            {
                ["predatorSpeciesId"] = predatorSpecies.Id.ToString(),
                ["predatorSpeciesName"] = predatorSpecies.Name
            });
    }

    private static void EmitPredatorPressure(World world, Region region, Species predatorSpecies, RegionSpeciesPopulation predatorPopulation, double foodRatio)
    {
        WorldEventSeverity severity = foodRatio < 0.35
            ? WorldEventSeverity.Major
            : WorldEventSeverity.Notable;
        string eventType = foodRatio < 0.30
            ? WorldEventType.EcosystemCollapse
            : WorldEventType.PredatorPressure;
        string narrative = foodRatio < 0.30
            ? $"{region.Name}'s predator web buckled as prey vanished"
            : $"{predatorSpecies.Name} came under food pressure in {region.Name}";

        world.AddEvent(
            eventType,
            severity,
            narrative,
            $"{predatorSpecies.Name} food ratio fell to {foodRatio:F2} in {region.Name}.",
            reason: foodRatio < 0.30
                ? "regional_food_web_instability"
                : "predator_food_shortage",
            scope: WorldEventScope.Regional,
            speciesId: predatorSpecies.Id,
            speciesName: predatorSpecies.Name,
            regionId: region.Id,
            regionName: region.Name,
            after: new Dictionary<string, string>
            {
                ["foodRatio"] = foodRatio.ToString("F2"),
                ["population"] = predatorPopulation.PopulationCount.ToString()
            });
    }

    public sealed record EcosystemSeasonMetrics(
        int ActiveRegionalPopulationCount,
        int EcologyIterations)
    {
        public static EcosystemSeasonMetrics Empty { get; } = new(0, 0);
    }
}
