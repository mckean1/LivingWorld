using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;

namespace LivingWorld.Systems;

public sealed class EcosystemSystem
{
    private const double ProducerPopulationToBiomass = 2.2;
    private const double ConsumerPopulationToBiomass = 1.1;
    private const double MigrationPressureThreshold = 0.45;
    private const double RecolonizationTargetScoreThreshold = 0.58;
    private readonly HashSet<string> _preyCollapseCooldownKeys = new(StringComparer.Ordinal);

    public void InitializeRegionalPopulations(World world)
    {
        Dictionary<int, Species> speciesById = world.Species.ToDictionary(species => species.Id);

        foreach (Region region in world.Regions)
        {
            foreach (Species species in speciesById.Values)
            {
                RegionSpeciesPopulation population = region.GetOrCreateSpeciesPopulation(species.Id);
                population.BaseHabitatSuitability = CalculateBaseHabitatSuitability(species, region);
                population.HabitatSuitability = CalculateHabitatSuitability(species, population, population.BaseHabitatSuitability);
                population.CarryingCapacity = CalculateCarryingCapacity(species, population, region, population.HabitatSuitability);

                if (population.PopulationCount > 0 || population.CarryingCapacity <= 0)
                {
                    continue;
                }

                if (species.InitialRangeRegionIds.Count > 0 && !species.InitialRangeRegionIds.Contains(region.Id))
                {
                    continue;
                }

                population.PopulationCount = CalculateInitialPopulation(species, population.CarryingCapacity, population.HabitatSuitability);
            }
        }

        SyncBiomeBiomass(world, speciesById);
    }

    public void UpdateSeason(World world)
    {
        Dictionary<int, Species> speciesById = world.Species.ToDictionary(species => species.Id);

        foreach (Region region in world.Regions)
        {
            EnsureRegionEntries(region, speciesById);
            ProcessRegionalGrowth(region, speciesById, world.Time.Season);
            ProcessRegionalFoodWeb(world, region, speciesById);
        }

        ProcessMigration(world, speciesById);
    }

    public void ResolveSeasonalCleanup(World world)
    {
        Dictionary<int, Species> speciesById = world.Species.ToDictionary(species => species.Id);
        ResolveExtinctions(world, speciesById);
        SyncBiomeBiomass(world, speciesById);
    }

    private void EnsureRegionEntries(Region region, IReadOnlyDictionary<int, Species> speciesById)
    {
        foreach (Species species in speciesById.Values)
        {
            RegionSpeciesPopulation population = region.GetOrCreateSpeciesPopulation(species.Id);
            population.BaseHabitatSuitability = CalculateBaseHabitatSuitability(species, region);
            population.HabitatSuitability = CalculateHabitatSuitability(species, population, population.BaseHabitatSuitability);
            population.CarryingCapacity = CalculateCarryingCapacity(species, population, region, population.HabitatSuitability);
            population.EstablishedThisSeason = false;
            population.ReceivedMigrantsThisSeason = false;
            population.SentMigrantsThisSeason = false;
        }
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
                continue;
            }

            double carryingRatio = population.CarryingCapacity <= 0
                ? 1.0
                : (double)previousPopulation / population.CarryingCapacity;
            double reproductionRate = species.BaseReproductionRate
                * species.GetSeasonalReproductionModifier(season)
                * population.HabitatSuitability
                * PopulationTraitResolver.ResolveReproductionModifier(species, population);
            double declineRate = species.BaseDeclineRate
                + Math.Max(0.0, carryingRatio - 1.0) * 0.10
                + (population.RecentFoodStress * 0.08)
                + (population.RecentPredationPressure * 0.05)
                + (population.RecentHuntingPressure * 0.06);
            declineRate *= PopulationTraitResolver.ResolveDeclineModifier(species, population, population.HabitatSuitability);

            int births = (int)Math.Round(previousPopulation * Math.Max(0.0, reproductionRate) * Math.Max(0.15, 1.0 - Math.Clamp(carryingRatio, 0.0, 1.35)));
            int naturalLosses = (int)Math.Round(previousPopulation * Math.Max(0.0, declineRate));

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
                if (region.ConnectedRegionIds.Count == 0 || species.MigrationCapability <= 0)
                {
                    continue;
                }

                Region? target = null;
                double bestTargetScore = double.MinValue;

                foreach (int candidateRegionId in region.ConnectedRegionIds)
                {
                    Region candidate = world.Regions[candidateRegionId];
                    double score = ScoreMigrationTarget(world, speciesById, species, sourcePopulation, candidate);
                    if (score <= bestTargetScore)
                    {
                        continue;
                    }

                    bestTargetScore = score;
                    target = candidate;
                }

                if (target is null)
                {
                    continue;
                }

                bool canRecolonize = CanAttemptRecolonization(sourcePopulation, species, target, bestTargetScore);
                if (sourcePopulation.MigrationPressure < MigrationPressureThreshold && !canRecolonize)
                {
                    continue;
                }

                RegionSpeciesPopulation targetPopulation = target.GetOrCreateSpeciesPopulation(species.Id);
                int sourceBefore = sourcePopulation.PopulationCount;
                double migrationCapability = PopulationTraitResolver.GetEffectiveMigrationCapability(species, sourcePopulation);
                double transferRate = canRecolonize
                    ? Math.Min(0.10, 0.03 + (migrationCapability * 0.08))
                    : Math.Min(0.16, (migrationCapability * 0.10) + (sourcePopulation.MigrationPressure * 0.08));
                int transfer = (int)Math.Round(sourceBefore * transferRate);
                transfer = Math.Min(transfer, Math.Max(0, sourcePopulation.PopulationCount - 1));
                if (transfer <= 0)
                {
                    continue;
                }

                bool newlyEstablished = targetPopulation.PopulationCount == 0;
                sourcePopulation.PopulationCount -= transfer;
                targetPopulation.PopulationCount += transfer;
                sourcePopulation.SentMigrantsThisSeason = true;
                targetPopulation.ReceivedMigrantsThisSeason = true;
                targetPopulation.BaseHabitatSuitability = CalculateBaseHabitatSuitability(species, target);
                targetPopulation.HabitatSuitability = CalculateHabitatSuitability(species, targetPopulation, targetPopulation.BaseHabitatSuitability);
                targetPopulation.CarryingCapacity = CalculateCarryingCapacity(species, targetPopulation, target, targetPopulation.HabitatSuitability);
                targetPopulation.EstablishedThisSeason = newlyEstablished;

                if (newlyEstablished)
                {
                    world.AddEvent(
                        WorldEventType.SpeciesPopulationEstablished,
                        WorldEventSeverity.Minor,
                        $"{species.Name} established a new population in {target.Name}",
                        $"{transfer} {species.Name} migrated from {region.Name} into {target.Name}.",
                        reason: "seasonal_species_migration",
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
                            ["sourceRegionId"] = region.Id.ToString(),
                            ["sourceRegionName"] = region.Name,
                            ["migrationKind"] = canRecolonize ? "recolonization" : "pressure"
                        });
                }
            }
        }
    }

    private void ResolveExtinctions(World world, IReadOnlyDictionary<int, Species> speciesById)
    {
        foreach (Region region in world.Regions)
        {
            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
            {
                if (population.PopulationCount > 0)
                {
                    continue;
                }

                Species species = speciesById[population.SpeciesId];
                bool wasPresentBefore = population.RecentPredationPressure > 0 || population.RecentHuntingPressure > 0 || population.SeasonsUnderPressure > 0;
                if (wasPresentBefore)
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
                        regionName: region.Name);
                }

                population.RecentPredationPressure = 0;
                population.RecentHuntingPressure = 0;
                population.SeasonsUnderPressure = 0;
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
                bool alreadyEmitted = world.Events.Any(evt => evt.Type == WorldEventType.GlobalSpeciesExtinction && evt.SpeciesId == species.Id);
                if (alreadyEmitted)
                {
                    continue;
                }

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

    private static double ScoreMigrationTarget(World world, IReadOnlyDictionary<int, Species> speciesById, Species species, RegionSpeciesPopulation sourcePopulation, Region target)
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
        double baseSuitability = CalculateBaseHabitatSuitability(species, target);
        double suitability = CalculateHabitatSuitability(species, candidatePopulation, baseSuitability);
        int localPopulation = existing?.PopulationCount ?? 0;
        int carryingCapacity = CalculateCarryingCapacity(species, candidatePopulation, target, suitability);
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

    private static bool CanAttemptRecolonization(
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

        if (bestTargetScore < RecolonizationTargetScoreThreshold || sourcePopulation.CarryingCapacity <= 0)
        {
            return false;
        }

        double sourceCapacityRatio = (double)sourcePopulation.PopulationCount / sourcePopulation.CarryingCapacity;
        return sourcePopulation.PopulationCount >= Math.Max(8, sourcePopulation.CarryingCapacity / 5)
            && sourceCapacityRatio >= 0.42;
    }

    private static double CalculateBaseHabitatSuitability(Species species, Region region)
    {
        double fertilityFit = 1.0 - Math.Abs(region.Fertility - species.FertilityPreference);
        double waterFit = 1.0 - Math.Abs(region.WaterAvailability - species.WaterPreference);
        double biomassFit = Math.Clamp(
            (region.MaxPlantBiomass / 1000.0 * species.PlantBiomassAffinity) +
            (region.MaxAnimalBiomass / 400.0 * species.AnimalBiomassAffinity),
            0.0,
            1.4);
        double biomeFit = species.PreferredBiomes.Count == 0
            ? 1.0
            : species.PreferredBiomes.Contains(region.Biome) ? 1.05 : 0.45;

        return Math.Clamp((fertilityFit * 0.30) + (waterFit * 0.22) + (biomassFit * 0.33) + (biomeFit * 0.15), 0.03, 1.25);
    }

    private static double CalculateHabitatSuitability(Species species, RegionSpeciesPopulation population, double baseSuitability)
    {
        return PopulationTraitResolver.AdjustHabitatSuitability(species, population, baseSuitability);
    }

    private static int CalculateCarryingCapacity(Species species, RegionSpeciesPopulation population, Region region, double suitability)
    {
        double baseCapacity = species.TrophicRole switch
        {
            TrophicRole.Producer => 180 + (region.MaxPlantBiomass * 0.35),
            TrophicRole.Herbivore => 45 + (region.MaxPlantBiomass * 0.07),
            TrophicRole.Omnivore => 28 + (region.TotalBiomassCapacity * 0.035),
            TrophicRole.Predator => 16 + (region.MaxAnimalBiomass * 0.03),
            TrophicRole.Apex => 8 + (region.MaxAnimalBiomass * 0.018),
            _ => 24
        };

        double dietFlexibility = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.DietFlexibility);
        double climateTolerance = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.ClimateTolerance);
        double size = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Size);
        double capacityFactor = 0.84 + (dietFlexibility * 0.16) + (climateTolerance * 0.10) - (size * 0.12);
        return Math.Max(0, (int)Math.Round(baseCapacity * suitability * species.BaseCarryingCapacityFactor * capacityFactor));
    }

    private static int CalculateInitialPopulation(Species species, int carryingCapacity, double habitatSuitability)
    {
        if (carryingCapacity <= 0 || habitatSuitability < 0.12)
        {
            return 0;
        }

        double startingShare = species.TrophicRole switch
        {
            TrophicRole.Producer => 0.70,
            TrophicRole.Herbivore => 0.52,
            TrophicRole.Omnivore => 0.38,
            TrophicRole.Predator => 0.24,
            TrophicRole.Apex => 0.14,
            _ => 0.30
        };

        return Math.Max(0, (int)Math.Round(carryingCapacity * startingShare * habitatSuitability));
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
}
