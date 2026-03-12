using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;

namespace LivingWorld.Systems;

public sealed class EcosystemSystem
{
    private const double ProducerPopulationToBiomass = 2.2;
    private const double ConsumerPopulationToBiomass = 1.1;
    private readonly HashSet<string> _preyCollapseCooldownKeys = new(StringComparer.Ordinal);

    public void InitializeRegionalPopulations(World world)
    {
        foreach (Region region in world.Regions)
        {
            foreach (Species species in world.Species)
            {
                RegionSpeciesPopulation population = region.GetOrCreateSpeciesPopulation(species.Id);
                population.HabitatSuitability = CalculateHabitatSuitability(species, population, region);
                population.CarryingCapacity = CalculateCarryingCapacity(species, population, region, population.HabitatSuitability);

                if (population.PopulationCount > 0 || population.CarryingCapacity <= 0)
                {
                    continue;
                }

                population.PopulationCount = CalculateInitialPopulation(species, population.CarryingCapacity, population.HabitatSuitability);
            }
        }

        SyncBiomeBiomass(world);
    }

    public void UpdateSeason(World world)
    {
        foreach (Region region in world.Regions)
        {
            EnsureRegionEntries(world, region);
            ProcessRegionalGrowth(world, region);
            ProcessRegionalFoodWeb(world, region);
        }

        ProcessMigration(world);
    }

    public void ResolveSeasonalCleanup(World world)
    {
        ResolveExtinctions(world);
        SyncBiomeBiomass(world);
    }

    private void EnsureRegionEntries(World world, Region region)
    {
        foreach (Species species in world.Species)
        {
            RegionSpeciesPopulation population = region.GetOrCreateSpeciesPopulation(species.Id);
            population.HabitatSuitability = CalculateHabitatSuitability(species, population, region);
            population.CarryingCapacity = CalculateCarryingCapacity(species, population, region, population.HabitatSuitability);
            population.EstablishedThisSeason = false;
            population.ReceivedMigrantsThisSeason = false;
            population.SentMigrantsThisSeason = false;
        }
    }

    private void ProcessRegionalGrowth(World world, Region region)
    {
        foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
        {
            Species species = world.Species.First(candidate => candidate.Id == population.SpeciesId);
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
                * species.GetSeasonalReproductionModifier(world.Time.Season)
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

    private void ProcessRegionalFoodWeb(World world, Region region)
    {
        int producerCount = region.SpeciesPopulations
            .Where(population => world.Species.First(species => species.Id == population.SpeciesId).TrophicRole == TrophicRole.Producer)
            .Sum(population => population.PopulationCount);

        foreach (RegionSpeciesPopulation predatorPopulation in region.SpeciesPopulations.Where(population => population.PopulationCount > 0))
        {
            Species predatorSpecies = world.Species.First(species => species.Id == predatorPopulation.SpeciesId);

            if (predatorSpecies.TrophicRole == TrophicRole.Producer)
            {
                continue;
            }

            IReadOnlyList<RegionSpeciesPopulation> preyOptions = region.SpeciesPopulations
                .Where(population => population.PopulationCount > 0 && predatorSpecies.DietSpeciesIds.Contains(population.SpeciesId))
                .ToList();

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
                Species preySpecies = world.Species.First(species => species.Id == preyPopulation.SpeciesId);
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

    private void ProcessMigration(World world)
    {
        foreach (Region region in world.Regions)
        {
            foreach (RegionSpeciesPopulation sourcePopulation in region.SpeciesPopulations.Where(population => population.PopulationCount > 0).ToList())
            {
                Species species = world.Species.First(candidate => candidate.Id == sourcePopulation.SpeciesId);
                if (sourcePopulation.MigrationPressure < 0.45 || region.ConnectedRegionIds.Count == 0 || species.MigrationCapability <= 0)
                {
                    continue;
                }

                Region? target = world.Regions
                    .Where(candidate => region.ConnectedRegionIds.Contains(candidate.Id))
                    .OrderByDescending(candidate => ScoreMigrationTarget(world, species, sourcePopulation, candidate))
                    .FirstOrDefault();

                if (target is null)
                {
                    continue;
                }

                RegionSpeciesPopulation targetPopulation = target.GetOrCreateSpeciesPopulation(species.Id);
                int sourceBefore = sourcePopulation.PopulationCount;
                double migrationCapability = PopulationTraitResolver.GetEffectiveMigrationCapability(species, sourcePopulation);
                int transfer = (int)Math.Round(sourceBefore * Math.Min(0.16, (migrationCapability * 0.10) + (sourcePopulation.MigrationPressure * 0.08)));
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
                targetPopulation.HabitatSuitability = CalculateHabitatSuitability(species, targetPopulation, target);
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
                            ["sourceRegionName"] = region.Name
                        });
                }
            }
        }
    }

    private void ResolveExtinctions(World world)
    {
        foreach (Region region in world.Regions)
        {
            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
            {
                if (population.PopulationCount > 0)
                {
                    continue;
                }

                Species species = world.Species.First(candidate => candidate.Id == population.SpeciesId);
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

        foreach (Species species in world.Species)
        {
            bool anySurvivors = world.Regions
                .Select(region => region.GetSpeciesPopulation(species.Id))
                .Any(population => population is not null && population.PopulationCount > 0);

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

    private void SyncBiomeBiomass(World world)
    {
        foreach (Region region in world.Regions)
        {
            int producerPopulation = 0;
            int consumerPopulation = 0;

            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
            {
                Species species = world.Species.First(candidate => candidate.Id == population.SpeciesId);
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

    private static double ScoreMigrationTarget(World world, Species species, RegionSpeciesPopulation sourcePopulation, Region target)
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
        double suitability = CalculateHabitatSuitability(species, candidatePopulation, target);
        int localPopulation = existing?.PopulationCount ?? 0;
        int carryingCapacity = CalculateCarryingCapacity(species, candidatePopulation, target, suitability);
        double openness = carryingCapacity <= 0
            ? 0.0
            : Math.Clamp((double)(carryingCapacity - localPopulation) / carryingCapacity, 0.0, 1.0);

        double predatorSafety = species.TrophicRole switch
        {
            TrophicRole.Producer => 1.0,
            TrophicRole.Herbivore => 1.0 - CountPredatorPressure(world, target),
            _ => 0.7 + (openness * 0.3)
        };

        return (suitability * 0.55) + (openness * 0.30) + (predatorSafety * 0.15);
    }

    private static double CountPredatorPressure(World world, Region region)
    {
        int predatorPopulation = region.SpeciesPopulations
            .Where(population => population.PopulationCount > 0)
            .Where(population =>
            {
                TrophicRole role = world.Species.First(species => species.Id == population.SpeciesId).TrophicRole;
                return role is TrophicRole.Predator or TrophicRole.Apex;
            })
            .Sum(population => population.PopulationCount);

        return Math.Clamp(predatorPopulation / 250.0, 0.0, 1.0);
    }

    private static double CalculateHabitatSuitability(Species species, RegionSpeciesPopulation population, Region region)
    {
        double fertilityFit = 1.0 - Math.Abs(region.Fertility - species.FertilityPreference);
        double waterFit = 1.0 - Math.Abs(region.WaterAvailability - species.WaterPreference);
        double biomassFit = Math.Clamp(
            (region.MaxPlantBiomass / 1000.0 * species.PlantBiomassAffinity) +
            (region.MaxAnimalBiomass / 400.0 * species.AnimalBiomassAffinity),
            0.0,
            1.4);

        double baseSuitability = Math.Clamp((fertilityFit * 0.35) + (waterFit * 0.25) + (biomassFit * 0.40), 0.05, 1.25);
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
