using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class MutationSystem
{
    private readonly Random _random;

    public MutationSystem(int seed = 24680)
    {
        _random = new Random(seed);
    }

    public void UpdateSeason(World world)
    {
        // Mutation reacts to the current seasonal ecology pass, including same-season
        // regional species exchange recorded by EcosystemSystem. Later monthly polity
        // migration is a social movement step and does not backfill these flags.
        foreach (Region region in world.Regions)
        {
            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations.Where(candidate => candidate.PopulationCount > 0))
            {
                Species species = world.Species.First(candidate => candidate.Id == population.SpeciesId);

                UpdateIsolation(world, region, population, species);
                AccumulatePressure(species, region, population);
                EmitIsolationMilestone(world, region, population, species);
                EmitAdaptationMilestone(world, region, population, species);

                if (!ShouldMutate(population, out MutationTier tier))
                {
                    continue;
                }

                ApplyMutation(world, region, population, species, tier);
            }
        }
    }

    private void UpdateIsolation(World world, Region region, RegionSpeciesPopulation population, Species species)
    {
        bool exchanged = population.EstablishedThisSeason || population.ReceivedMigrantsThisSeason || population.SentMigrantsThisSeason;
        if (exchanged)
        {
            population.IsolationSeasons = Math.Max(0, population.IsolationSeasons - 3);
            population.IsolationMutationPressure *= 0.72;
            return;
        }

        bool connectedPopulationExists = region.ConnectedRegionIds
            .Select(id => world.Regions.First(candidate => candidate.Id == id))
            .Select(candidate => candidate.GetSpeciesPopulation(species.Id))
            .Any(candidate => candidate is not null && candidate.PopulationCount > 0);

        population.IsolationSeasons = connectedPopulationExists
            ? Math.Max(0, population.IsolationSeasons - 1)
            : population.IsolationSeasons + 1;
    }

    private static void AccumulatePressure(Species species, Region region, RegionSpeciesPopulation population)
    {
        double carryingRatio = population.CarryingCapacity <= 0
            ? 1.0
            : (double)population.PopulationCount / population.CarryingCapacity;
        double habitatMismatch = Math.Max(0.0, 0.82 - population.HabitatSuitability);
        double ancestralMismatch = Math.Max(0.0, 0.82 - population.BaseHabitatSuitability);
        double migrationShock = population.ReceivedMigrantsThisSeason
            ? 0.10 + (ancestralMismatch * 0.25)
            : 0.0;
        double isolationGain = population.IsolationSeasons >= 4
            ? Math.Min(0.30, population.IsolationSeasons / 24.0)
            : 0.0;
        double driftGain = population.IsolationSeasons >= 6 && population.RecentFoodStress < 0.12 && population.RecentPredationPressure < 0.12 && population.RecentHuntingPressure < 0.12
            ? 0.03
            : 0.01;

        population.FoodStressMutationPressure = DecayAndAdd(population.FoodStressMutationPressure, population.RecentFoodStress * 0.52, 0.84);
        population.PredationMutationPressure = DecayAndAdd(population.PredationMutationPressure, population.RecentPredationPressure * 0.48, 0.86);
        population.HuntingMutationPressure = DecayAndAdd(population.HuntingMutationPressure, population.RecentHuntingPressure * 0.52, 0.86);
        population.HabitatMismatchMutationPressure = DecayAndAdd(population.HabitatMismatchMutationPressure, (ancestralMismatch * 0.44) + migrationShock, 0.88);
        population.IsolationMutationPressure = DecayAndAdd(population.IsolationMutationPressure, isolationGain, 0.92);
        population.CrowdingMutationPressure = DecayAndAdd(population.CrowdingMutationPressure, Math.Max(0.0, carryingRatio - 0.85) * 0.55, 0.86);
        population.DriftMutationPressure = DecayAndAdd(population.DriftMutationPressure, driftGain, 0.92);

        double activePressure =
            population.FoodStressMutationPressure +
            population.PredationMutationPressure +
            population.HuntingMutationPressure +
            population.HabitatMismatchMutationPressure +
            population.IsolationMutationPressure +
            population.CrowdingMutationPressure;

        population.DivergenceScore = Math.Clamp(
            population.DivergenceScore +
            (activePressure * 0.02) +
            (population.DriftMutationPressure * 0.01) -
            0.01,
            0.0,
            4.0);
    }

    private bool ShouldMutate(RegionSpeciesPopulation population, out MutationTier tier)
    {
        tier = MutationTier.None;

        double weightedPressure =
            population.FoodStressMutationPressure +
            population.PredationMutationPressure +
            population.HuntingMutationPressure +
            population.HabitatMismatchMutationPressure +
            (population.IsolationMutationPressure * 0.85) +
            (population.CrowdingMutationPressure * 0.70) +
            (population.DriftMutationPressure * 0.35);

        if (weightedPressure < 1.15)
        {
            return false;
        }

        double majorChance = Math.Clamp(
            ((weightedPressure - 2.60) * 0.05) +
            (population.DivergenceScore * 0.03) +
            (population.IsolationMutationPressure * 0.04),
            0.0,
            0.08);
        if (weightedPressure >= 2.75 && _random.NextDouble() < majorChance)
        {
            tier = MutationTier.Major;
            return true;
        }

        double minorChance = Math.Clamp(
            ((weightedPressure - 1.10) * 0.11) +
            (population.IsolationMutationPressure * 0.03),
            0.0,
            0.28);
        if (_random.NextDouble() < minorChance)
        {
            tier = MutationTier.Minor;
            return true;
        }

        return false;
    }

    private void ApplyMutation(World world, Region region, RegionSpeciesPopulation population, Species species, MutationTier tier)
    {
        List<MutationPressureContribution> rankedPressures = RankPressures(population);
        if (rankedPressures.Count == 0)
        {
            return;
        }

        MutationPressureContribution dominantPressure = rankedPressures[0];
        MutationPlan plan = BuildMutationPlan(species, dominantPressure, rankedPressures.Skip(1).FirstOrDefault(), tier);
        if (plan.Changes.Count == 0)
        {
            return;
        }

        Dictionary<string, string> before = new(StringComparer.OrdinalIgnoreCase)
        {
            ["divergenceScore"] = population.DivergenceScore.ToString("F2"),
            ["foodStressPressure"] = population.FoodStressMutationPressure.ToString("F2"),
            ["predationPressure"] = population.PredationMutationPressure.ToString("F2"),
            ["huntingPressure"] = population.HuntingMutationPressure.ToString("F2"),
            ["habitatMismatchPressure"] = population.HabitatMismatchMutationPressure.ToString("F2"),
            ["isolationPressure"] = population.IsolationMutationPressure.ToString("F2"),
            ["crowdingPressure"] = population.CrowdingMutationPressure.ToString("F2")
        };

        foreach ((SpeciesTrait trait, _) in plan.Changes)
        {
            before[$"trait:{trait}"] = PopulationTraitResolver.GetEffectiveTrait(species, population, trait).ToString("F2");
        }

        foreach ((SpeciesTrait trait, double delta) in plan.Changes)
        {
            population.ApplyTraitOffset(trait, delta);
        }

        double impact = plan.Changes.Sum(change => Math.Abs(change.Delta));
        population.DivergenceScore = Math.Clamp(population.DivergenceScore + (impact * (tier == MutationTier.Major ? 2.4 : 1.4)), 0.0, 4.0);
        population.LastMutationYear = world.Time.Year;
        if (tier == MutationTier.Major)
        {
            population.MajorMutationCount++;
            population.LastMajorMutationYear = world.Time.Year;
        }
        else
        {
            population.MinorMutationCount++;
        }

        population.FoodStressMutationPressure *= tier == MutationTier.Major ? 0.46 : 0.66;
        population.PredationMutationPressure *= tier == MutationTier.Major ? 0.48 : 0.68;
        population.HuntingMutationPressure *= tier == MutationTier.Major ? 0.48 : 0.68;
        population.HabitatMismatchMutationPressure *= tier == MutationTier.Major ? 0.40 : 0.62;
        population.IsolationMutationPressure *= tier == MutationTier.Major ? 0.62 : 0.74;
        population.CrowdingMutationPressure *= tier == MutationTier.Major ? 0.52 : 0.70;
        population.DriftMutationPressure *= 0.60;

        Dictionary<string, string> after = new(StringComparer.OrdinalIgnoreCase)
        {
            ["divergenceScore"] = population.DivergenceScore.ToString("F2"),
            ["minorMutations"] = population.MinorMutationCount.ToString(),
            ["majorMutations"] = population.MajorMutationCount.ToString()
        };

        foreach ((SpeciesTrait trait, _) in plan.Changes)
        {
            after[$"trait:{trait}"] = PopulationTraitResolver.GetEffectiveTrait(species, population, trait).ToString("F2");
        }

        Dictionary<string, string> metadata = new(StringComparer.OrdinalIgnoreCase)
        {
            ["mutationTier"] = tier.ToString().ToLowerInvariant(),
            ["dominantPressure"] = dominantPressure.Name,
            ["pressureSummary"] = string.Join(", ", rankedPressures.Take(3).Select(candidate => $"{candidate.Name}:{candidate.Value:F2}")),
            ["traitChanges"] = string.Join(", ", plan.Changes.Select(change => $"{change.Trait}:{change.Delta:+0.00;-0.00}")),
            ["isolationSeasons"] = population.IsolationSeasons.ToString(),
            ["population"] = population.PopulationCount.ToString()
        };

        Polity? relatedPolity = FindRelevantPolity(world, region, species.Id);
        (int? polityId, string? polityName, int? relatedPolityId, string? relatedPolityName, int? relatedPolitySpeciesId, string? relatedPolitySpeciesName) =
            ResolvePolityContext(world, relatedPolity, species.Id);

        string eventType = tier == MutationTier.Major
            ? WorldEventType.SpeciesPopulationMajorMutation
            : WorldEventType.SpeciesPopulationMutated;
        WorldEventSeverity severity = tier == MutationTier.Major
            ? (impact >= 0.22 ? WorldEventSeverity.Major : WorldEventSeverity.Notable)
            : WorldEventSeverity.Minor;
        string narrative = BuildMutationNarrative(region, species, plan, dominantPressure, tier);
        string details = BuildMutationDetails(region, species, population, rankedPressures, plan, tier);

        world.AddEvent(
            eventType,
            severity,
            narrative,
            details,
            reason: $"pressure_{dominantPressure.Name}",
            scope: WorldEventScope.Regional,
            polityId: polityId,
            polityName: polityName,
            relatedPolityId: relatedPolityId,
            relatedPolityName: relatedPolityName,
            relatedPolitySpeciesId: relatedPolitySpeciesId,
            relatedPolitySpeciesName: relatedPolitySpeciesName,
            speciesId: species.Id,
            speciesName: species.Name,
            regionId: region.Id,
            regionName: region.Name,
            before: before,
            after: after,
            metadata: metadata);

        EmitDivergenceMilestone(world, region, population, species, relatedPolity);
    }

    private void EmitIsolationMilestone(World world, Region region, RegionSpeciesPopulation population, Species species)
    {
        if (population.IsolationSeasons < 8 || population.IsolationSeasons - population.LastIsolationEventSeason < 8)
        {
            return;
        }

        population.LastIsolationEventSeason = population.IsolationSeasons;
        world.AddEvent(
            WorldEventType.SpeciesPopulationIsolated,
            WorldEventSeverity.Minor,
            $"{species.Name} remained isolated in {region.Name}",
            $"{species.Name} in {region.Name} has remained without meaningful exchange for {population.IsolationSeasons} seasons.",
            reason: "prolonged_isolation",
            scope: WorldEventScope.Regional,
            speciesId: species.Id,
            speciesName: species.Name,
            regionId: region.Id,
            regionName: region.Name,
            after: new Dictionary<string, string>
            {
                ["isolationSeasons"] = population.IsolationSeasons.ToString(),
                ["divergenceScore"] = population.DivergenceScore.ToString("F2")
            });
    }

    private void EmitAdaptationMilestone(World world, Region region, RegionSpeciesPopulation population, Species species)
    {
        double baseSuitability = population.BaseHabitatSuitability;
        double suitabilityGain = population.HabitatSuitability - baseSuitability;
        double climateTolerance = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.ClimateTolerance);
        double dietFlexibility = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.DietFlexibility);
        double baselineClimateTolerance = PopulationTraitResolver.GetBaselineTrait(species, SpeciesTrait.ClimateTolerance);
        double baselineDietFlexibility = PopulationTraitResolver.GetBaselineTrait(species, SpeciesTrait.DietFlexibility);
        double climateGain = climateTolerance - baselineClimateTolerance;
        double dietGain = dietFlexibility - baselineDietFlexibility;
        bool ancestralMismatch = baseSuitability <= 0.72;
        bool sustainedPressure = population.HabitatMismatchMutationPressure >= 0.58;
        bool meaningfulTraitAdaptation = climateGain >= 0.08 || dietGain >= 0.10;
        bool improvedRegionalFit = suitabilityGain >= 0.10 && population.HabitatSuitability >= 0.78;
        bool divergenceEstablished = population.DivergenceScore >= 0.85;
        bool persistentPopulation = population.PopulationCount >= Math.Max(6, population.CarryingCapacity / 10);

        if (!ancestralMismatch || !sustainedPressure || !meaningfulTraitAdaptation || !improvedRegionalFit || !divergenceEstablished || !persistentPopulation)
        {
            return;
        }

        int adaptationMilestone = ResolveAdaptationMilestone(population, suitabilityGain, climateGain, dietGain);
        if (adaptationMilestone <= population.LastAdaptationMilestone)
        {
            return;
        }

        population.LastAdaptationMilestone = adaptationMilestone;
        Polity? relatedPolity = FindRelevantPolity(world, region, species.Id);
        (int? polityId, string? polityName, int? relatedPolityId, string? relatedPolityName, int? relatedPolitySpeciesId, string? relatedPolitySpeciesName) =
            ResolvePolityContext(world, relatedPolity, species.Id);
        string adaptationSignal = ResolveAdaptationSignal(climateGain, dietGain);
        WorldEventSeverity severity = adaptationMilestone >= 2
            ? WorldEventSeverity.Major
            : WorldEventSeverity.Notable;

        world.AddEvent(
            WorldEventType.SpeciesPopulationAdaptedToRegion,
            severity,
            BuildAdaptationNarrative(region, species, adaptationMilestone, adaptationSignal),
            $"{species.Name} in {region.Name} now shows stronger climate tolerance ({climateTolerance:F2}) and diet flexibility ({dietFlexibility:F2}) after sustained habitat mismatch.",
            reason: "sustained_habitat_mismatch",
            scope: WorldEventScope.Regional,
            polityId: polityId,
            polityName: polityName,
            relatedPolityId: relatedPolityId,
            relatedPolityName: relatedPolityName,
            relatedPolitySpeciesId: relatedPolitySpeciesId,
            relatedPolitySpeciesName: relatedPolitySpeciesName,
            speciesId: species.Id,
            speciesName: species.Name,
            regionId: region.Id,
            regionName: region.Name,
            before: new Dictionary<string, string>
            {
                ["baseHabitatSuitability"] = baseSuitability.ToString("F2"),
                ["baselineClimateTolerance"] = baselineClimateTolerance.ToString("F2"),
                ["baselineDietFlexibility"] = baselineDietFlexibility.ToString("F2")
            },
            after: new Dictionary<string, string>
            {
                ["baseHabitatSuitability"] = baseSuitability.ToString("F2"),
                ["habitatSuitability"] = population.HabitatSuitability.ToString("F2"),
                ["climateTolerance"] = climateTolerance.ToString("F2"),
                ["dietFlexibility"] = dietFlexibility.ToString("F2"),
                ["suitabilityGain"] = suitabilityGain.ToString("F2"),
                ["divergenceScore"] = population.DivergenceScore.ToString("F2")
            },
            metadata: new Dictionary<string, string>
            {
                ["adaptationMilestone"] = adaptationMilestone.ToString(),
                ["adaptationStage"] = adaptationMilestone >= 2 ? "strong_adaptation" : "regional_adaptation",
                ["adaptationSignal"] = adaptationSignal,
                ["adaptationSignals"] = $"baseMismatch={baseSuitability:F2}, fitGain={suitabilityGain:F2}, climateGain={climateGain:F2}, dietGain={dietGain:F2}",
                ["habitatMismatchPressure"] = population.HabitatMismatchMutationPressure.ToString("F2"),
                ["population"] = population.PopulationCount.ToString()
            });
    }

    private void EmitDivergenceMilestone(World world, Region region, RegionSpeciesPopulation population, Species species, Polity? relatedPolity)
    {
        int milestone = population.DivergenceScore switch
        {
            >= 2.4 => 3,
            >= 1.8 => 2,
            >= 1.1 => 1,
            _ => 0
        };

        if (milestone == 0 || milestone <= population.LastDivergenceMilestone)
        {
            return;
        }

        population.LastDivergenceMilestone = milestone;
        (int? polityId, string? polityName, int? relatedPolityId, string? relatedPolityName, int? relatedPolitySpeciesId, string? relatedPolitySpeciesName) =
            ResolvePolityContext(world, relatedPolity, species.Id);

        world.AddEvent(
            WorldEventType.SpeciesPopulationEvolutionaryTurningPoint,
            milestone >= 2 ? WorldEventSeverity.Major : WorldEventSeverity.Notable,
            BuildTurningPointNarrative(region, species, population, milestone),
            $"{species.Name} in {region.Name} reached divergence milestone {milestone} with divergence score {population.DivergenceScore:F2}.",
            reason: "accumulated_divergence",
            scope: WorldEventScope.Regional,
            polityId: polityId,
            polityName: polityName,
            relatedPolityId: relatedPolityId,
            relatedPolityName: relatedPolityName,
            relatedPolitySpeciesId: relatedPolitySpeciesId,
            relatedPolitySpeciesName: relatedPolitySpeciesName,
            speciesId: species.Id,
            speciesName: species.Name,
            regionId: region.Id,
            regionName: region.Name,
            after: new Dictionary<string, string>
            {
                ["divergenceScore"] = population.DivergenceScore.ToString("F2"),
                ["minorMutations"] = population.MinorMutationCount.ToString(),
                ["majorMutations"] = population.MajorMutationCount.ToString()
            });
    }

    private static string BuildMutationNarrative(Region region, Species species, MutationPlan plan, MutationPressureContribution dominantPressure, MutationTier tier)
    {
        string leadTrait = plan.Changes[0].Trait switch
        {
            SpeciesTrait.ClimateTolerance => "grew hardier",
            SpeciesTrait.Endurance => "grew tougher",
            SpeciesTrait.Aggression => "grew more aggressive",
            SpeciesTrait.Sociality => "began moving in tighter groups",
            SpeciesTrait.DietFlexibility => "broadened their diet",
            SpeciesTrait.Size when plan.Changes[0].Delta < 0 => "grew leaner",
            SpeciesTrait.Size => "grew larger",
            SpeciesTrait.Fertility when plan.Changes[0].Delta < 0 => "bred more slowly",
            SpeciesTrait.Fertility => "bred more quickly",
            SpeciesTrait.Intelligence => "grew more cunning",
            _ => "changed"
        };

        return tier == MutationTier.Major
            ? $"{species.Name} in {region.Name} {leadTrait} under generations of {dominantPressure.DisplayName}"
            : $"{species.Name} in {region.Name} {leadTrait}";
    }

    private static string BuildMutationDetails(
        Region region,
        Species species,
        RegionSpeciesPopulation population,
        IReadOnlyList<MutationPressureContribution> rankedPressures,
        MutationPlan plan,
        MutationTier tier)
    {
        string pressures = string.Join(", ", rankedPressures.Take(3).Select(candidate => $"{candidate.DisplayName}={candidate.Value:F2}"));
        string traits = string.Join(", ", plan.Changes.Select(change => $"{change.Trait} {change.Delta:+0.00;-0.00}"));
        return $"{tier} mutation for {species.Name} in {region.Name}; traits [{traits}]; pressures [{pressures}]; isolation={population.IsolationSeasons} seasons.";
    }

    private MutationPlan BuildMutationPlan(
        Species species,
        MutationPressureContribution dominantPressure,
        MutationPressureContribution? secondaryPressure,
        MutationTier tier)
    {
        List<TraitChange> changes = new();
        TraitChange primary = SelectTraitChange(species, dominantPressure.Name, tier, primary: true);
        changes.Add(primary);

        if (tier == MutationTier.Major)
        {
            MutationPressureContribution pressureForSecondTrait = secondaryPressure ?? dominantPressure;
            TraitChange secondary = SelectTraitChange(species, pressureForSecondTrait.Name, tier, primary: false);
            if (secondary.Trait != primary.Trait)
            {
                changes.Add(secondary);
            }
        }

        return new MutationPlan(changes);
    }

    private TraitChange SelectTraitChange(Species species, string pressureName, MutationTier tier, bool primary)
    {
        List<TraitChange> candidates = pressureName switch
        {
            "food_stress" => BuildFoodStressCandidates(tier),
            "predation" => BuildPredationCandidates(tier),
            "hunting" => BuildHuntingCandidates(species, tier),
            "habitat_mismatch" => BuildHabitatMismatchCandidates(tier),
            "isolation" => BuildIsolationCandidates(tier),
            "crowding" => BuildCrowdingCandidates(tier),
            _ => BuildDriftCandidates(tier)
        };

        int startIndex = primary ? 0 : Math.Min(1, candidates.Count - 1);
        return candidates[startIndex];
    }

    private static List<TraitChange> BuildFoodStressCandidates(MutationTier tier)
    {
        double size = tier == MutationTier.Major ? 0.12 : 0.05;
        return
        [
            new TraitChange(SpeciesTrait.DietFlexibility, size),
            new TraitChange(SpeciesTrait.Endurance, size * 0.8),
            new TraitChange(SpeciesTrait.Size, -size * 0.8),
            new TraitChange(SpeciesTrait.Fertility, -size * 0.6)
        ];
    }

    private static List<TraitChange> BuildPredationCandidates(MutationTier tier)
    {
        double size = tier == MutationTier.Major ? 0.11 : 0.05;
        return
        [
            new TraitChange(SpeciesTrait.Endurance, size),
            new TraitChange(SpeciesTrait.Sociality, size * 0.8),
            new TraitChange(SpeciesTrait.Aggression, size * 0.7),
            new TraitChange(SpeciesTrait.Size, size * 0.5)
        ];
    }

    private static List<TraitChange> BuildHuntingCandidates(Species species, MutationTier tier)
    {
        double size = tier == MutationTier.Major ? 0.12 : 0.05;
        bool smallerPreyBias = species.TrophicRole is TrophicRole.Herbivore or TrophicRole.Omnivore;

        return
        [
            new TraitChange(SpeciesTrait.Endurance, size),
            new TraitChange(SpeciesTrait.Aggression, size * 0.8),
            new TraitChange(SpeciesTrait.Sociality, size * 0.6),
            new TraitChange(SpeciesTrait.Size, (smallerPreyBias ? -1 : 1) * size * 0.7)
        ];
    }

    private static List<TraitChange> BuildHabitatMismatchCandidates(MutationTier tier)
    {
        double size = tier == MutationTier.Major ? 0.13 : 0.06;
        return
        [
            new TraitChange(SpeciesTrait.ClimateTolerance, size),
            new TraitChange(SpeciesTrait.DietFlexibility, size * 0.8),
            new TraitChange(SpeciesTrait.Endurance, size * 0.7),
            new TraitChange(SpeciesTrait.Size, -size * 0.5)
        ];
    }

    private TraitChange SelectSignedIsolationShift(MutationTier tier)
    {
        double magnitude = tier == MutationTier.Major ? 0.10 : 0.04;
        return _random.Next(2) == 0
            ? new TraitChange(SpeciesTrait.Sociality, magnitude)
            : new TraitChange(SpeciesTrait.Aggression, magnitude);
    }

    private List<TraitChange> BuildIsolationCandidates(MutationTier tier)
    {
        double size = tier == MutationTier.Major ? 0.11 : 0.04;
        return
        [
            new TraitChange(SpeciesTrait.Intelligence, size * 0.7),
            SelectSignedIsolationShift(tier),
            new TraitChange(SpeciesTrait.ClimateTolerance, size * 0.6),
            new TraitChange(SpeciesTrait.Size, _random.Next(2) == 0 ? -size * 0.4 : size * 0.4)
        ];
    }

    private static List<TraitChange> BuildCrowdingCandidates(MutationTier tier)
    {
        double size = tier == MutationTier.Major ? 0.10 : 0.04;
        return
        [
            new TraitChange(SpeciesTrait.Size, -size),
            new TraitChange(SpeciesTrait.Fertility, -size * 0.7),
            new TraitChange(SpeciesTrait.DietFlexibility, size * 0.7),
            new TraitChange(SpeciesTrait.Aggression, size * 0.6)
        ];
    }

    private List<TraitChange> BuildDriftCandidates(MutationTier tier)
    {
        double size = tier == MutationTier.Major ? 0.08 : 0.03;
        SpeciesTrait trait = (SpeciesTrait)_random.Next(Enum.GetValues<SpeciesTrait>().Length);
        double sign = _random.Next(2) == 0 ? -1.0 : 1.0;
        return
        [
            new TraitChange(trait, size * sign)
        ];
    }

    private static List<MutationPressureContribution> RankPressures(RegionSpeciesPopulation population)
    {
        return new List<MutationPressureContribution>
        {
            new MutationPressureContribution("food_stress", "food stress", population.FoodStressMutationPressure),
            new MutationPressureContribution("predation", "predation pressure", population.PredationMutationPressure),
            new MutationPressureContribution("hunting", "hunting pressure", population.HuntingMutationPressure),
            new MutationPressureContribution("habitat_mismatch", "habitat mismatch", population.HabitatMismatchMutationPressure),
            new MutationPressureContribution("isolation", "isolation", population.IsolationMutationPressure),
            new MutationPressureContribution("crowding", "population pressure", population.CrowdingMutationPressure),
            new MutationPressureContribution("drift", "low-pressure drift", population.DriftMutationPressure)
        }
        .Where(candidate => candidate.Value >= 0.12)
        .OrderByDescending(candidate => candidate.Value)
        .ToList();
    }

    private static Polity? FindRelevantPolity(World world, Region region, int speciesId)
    {
        return world.Polities
            .Where(polity => polity.Population > 0 && polity.RegionId == region.Id)
            .OrderByDescending(polity => polity.SpeciesId == speciesId ? 3 : 0)
            .ThenByDescending(polity => polity.KnownEdibleSpeciesIds.Contains(speciesId) || polity.KnownDangerousPreySpeciesIds.Contains(speciesId) ? 2 : 0)
            .ThenByDescending(polity => polity.Population)
            .FirstOrDefault();
    }

    private static (int? polityId, string? polityName, int? relatedPolityId, string? relatedPolityName, int? relatedPolitySpeciesId, string? relatedPolitySpeciesName)
        ResolvePolityContext(World world, Polity? polity, int mutatedSpeciesId)
    {
        if (polity is null)
        {
            return (null, null, null, null, null, null);
        }

        string? politySpeciesName = world.Species.FirstOrDefault(species => species.Id == polity.SpeciesId)?.Name;
        if (polity.SpeciesId == mutatedSpeciesId)
        {
            return (polity.Id, polity.Name, null, null, null, null);
        }

        return (null, null, polity.Id, polity.Name, polity.SpeciesId, politySpeciesName);
    }

    private static string BuildTurningPointNarrative(Region region, Species species, RegionSpeciesPopulation population, int milestone)
    {
        return milestone switch
        {
            1 => $"{species.Name} in {region.Name} began to diverge from its ancestral form",
            2 => $"{species.Name} in {region.Name} reached a clear evolutionary turning point",
            _ => $"{species.Name} in {region.Name} emerged as a strongly diverged lineage"
        };
    }

    private static int ResolveAdaptationMilestone(
        RegionSpeciesPopulation population,
        double suitabilityGain,
        double climateGain,
        double dietGain)
    {
        bool strongTraitShift = climateGain >= 0.14
            || dietGain >= 0.16
            || (climateGain >= 0.10 && dietGain >= 0.10);
        bool strongRegionalFit = suitabilityGain >= 0.18 && population.HabitatSuitability >= 0.84;
        bool deepDivergence = population.DivergenceScore >= 1.45;
        bool strongPersistence = population.PopulationCount >= Math.Max(10, population.CarryingCapacity / 6);

        return strongTraitShift && strongRegionalFit && deepDivergence && strongPersistence
            ? 2
            : 1;
    }

    private static string ResolveAdaptationSignal(double climateGain, double dietGain)
    {
        if (climateGain >= dietGain + 0.04)
        {
            return "climate_tolerance";
        }

        if (dietGain >= climateGain + 0.04)
        {
            return "diet_flexibility";
        }

        return "mixed_adaptation";
    }

    private static string BuildAdaptationNarrative(Region region, Species species, int adaptationMilestone, string adaptationSignal)
    {
        if (adaptationMilestone >= 2)
        {
            return adaptationSignal switch
            {
                "climate_tolerance" => $"{species.Name} in {region.Name} grew strongly adapted to its climate",
                "diet_flexibility" => $"{species.Name} in {region.Name} grew strongly adapted to leaner food webs",
                _ => $"{species.Name} in {region.Name} became strongly adapted to the region"
            };
        }

        return adaptationSignal switch
        {
            "climate_tolerance" => $"{species.Name} adapted to the climate of {region.Name}",
            "diet_flexibility" => $"{species.Name} adapted to the harsher food web of {region.Name}",
            _ => $"{species.Name} adapted to {region.Name}"
        };
    }

    private static double DecayAndAdd(double currentValue, double addedValue, double decay)
        => Math.Clamp((currentValue * decay) + addedValue, 0.0, 3.5);

    private enum MutationTier
    {
        None,
        Minor,
        Major
    }

    private sealed record MutationPressureContribution(string Name, string DisplayName, double Value);
    private sealed record TraitChange(SpeciesTrait Trait, double Delta);
    private sealed record MutationPlan(IReadOnlyList<TraitChange> Changes);
}
