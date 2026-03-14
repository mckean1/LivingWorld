using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class MutationSystem
{
    private readonly Random _random;
    private readonly MutationSettings _settings;
    private readonly Dictionary<(int RegionId, int RootAncestorSpeciesId), int> _lastRootSpeciationYearByRegion = [];
    public MutationSeasonMetrics LastSeasonMetrics { get; private set; } = MutationSeasonMetrics.Empty;

    public MutationSystem(int seed = 24680, MutationSettings? settings = null)
    {
        _random = new Random(seed);
        _settings = settings ?? new MutationSettings();
    }

    public void UpdateSeason(World world)
    {
        // Mutation reacts to the current seasonal ecology pass, including same-season
        // regional species exchange recorded by EcosystemSystem. Later monthly polity
        // migration is a social movement step and does not backfill these flags.
        SpeciationContext speciationContext = BuildSpeciationContext(world);
        int mutationChecks = 0;
        int speciationCandidates = 0;
        int speciationEvents = 0;

        foreach (Region region in world.Regions)
        {
            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations.Where(candidate => candidate.PopulationCount > 0).ToList())
            {
                if (!speciationContext.SpeciesById.TryGetValue(population.SpeciesId, out Species? species))
                {
                    continue;
                }

                UpdateIsolation(world, region, population, species);
                AccumulatePressure(world, species, region, population);
                EmitIsolationMilestone(world, region, population, species);
                EmitAdaptationMilestone(world, region, population, species);
                mutationChecks++;

                if (!ShouldMutate(species, population, out MutationTier tier))
                {
                    if (IsSpeciationCandidate(world, population, species, speciationContext, out SpeciationCandidateState nonMutationCandidateState))
                    {
                        speciationCandidates++;
                    }

                    if (TrySpeciate(world, region, population, species, speciationContext, nonMutationCandidateState))
                    {
                        speciationEvents++;
                    }

                    continue;
                }

                ApplyMutation(world, region, population, species, tier);
                if (IsSpeciationCandidate(world, population, species, speciationContext, out SpeciationCandidateState mutatedCandidateState))
                {
                    speciationCandidates++;
                }

                if (TrySpeciate(world, region, population, species, speciationContext, mutatedCandidateState))
                {
                    speciationEvents++;
                }
            }
        }

        RefreshEvolutionaryLineages(world);
        LastSeasonMetrics = new MutationSeasonMetrics(mutationChecks, speciationCandidates, speciationEvents);
    }

    private void UpdateIsolation(World world, Region region, RegionSpeciesPopulation population, Species species)
    {
        bool exchanged = population.EstablishedThisSeason || population.ReceivedMigrantsThisSeason || population.SentMigrantsThisSeason;
        if (exchanged)
        {
            population.IsolationSeasons = Math.Max(0, population.IsolationSeasons - 3);
            population.IsolationYears = population.IsolationSeasons / 4;
            population.SpeciationReadinessSeasons = Math.Max(0, population.SpeciationReadinessSeasons - 4);
            population.IsolationMutationPressure *= 0.72;
            population.LastContactYear = world.Time.Year;
            population.ContactPressure = Math.Clamp(population.ContactPressure + 0.35, 0.0, 1.0);
            return;
        }

        List<RegionSpeciesPopulation> connectedPopulations = region.ConnectedRegionIds
            .Select(id => world.Regions.First(candidate => candidate.Id == id))
            .Select(candidate => candidate.GetSpeciesPopulation(species.Id))
            .Where(candidate => candidate is not null && candidate.PopulationCount > 0)
            .Cast<RegionSpeciesPopulation>()
            .ToList();
        double contactExposure = connectedPopulations.Count == 0
            ? 0.0
            : connectedPopulations.Average(candidate => ResolveContactExposure(world, region, population, species, candidate));
        bool connectedPopulationExists = connectedPopulations.Count > 0;

        if (!connectedPopulationExists)
        {
            population.IsolationSeasons++;
        }
        else if (contactExposure >= 0.72)
        {
            population.IsolationSeasons = Math.Max(0, population.IsolationSeasons - 1);
        }
        else if (contactExposure < 0.38)
        {
            population.IsolationSeasons++;
        }

        population.IsolationYears = population.IsolationSeasons / 4;
        if (connectedPopulationExists)
        {
            population.LastContactYear = world.Time.Year;
            population.ContactPressure = Math.Clamp(population.ContactPressure + (0.18 * contactExposure), 0.0, 1.0);
        }
        else
        {
            population.ContactPressure = Math.Max(0.0, population.ContactPressure * 0.84);
        }

        if (population.IsolationSeasons >= _settings.SpeciationIsolationSeasonsThreshold && population.PopulationCount >= _settings.SpeciationMinimumPopulation)
        {
            population.SpeciationReadinessSeasons++;
        }
        else
        {
            population.SpeciationReadinessSeasons = Math.Max(0, population.SpeciationReadinessSeasons - 1);
        }
    }

    private void AccumulatePressure(World world, Species species, Region region, RegionSpeciesPopulation population)
    {
        double carryingRatio = population.CarryingCapacity <= 0
            ? 1.0
            : (double)population.PopulationCount / population.CarryingCapacity;
        double habitatMismatch = Math.Max(0.0, 0.82 - population.HabitatSuitability);
        double ancestralMismatch = Math.Max(0.0, 0.82 - population.BaseHabitatSuitability);
        double ecologicalDistance = ResolveEcologicalDistance(species, region);
        double founderEcologyDistance = ResolveFounderEcologyDistance(world, region, population);
        double founderIsolationMomentum = ResolveFounderIsolationMomentum(population, founderEcologyDistance);
        double mutationPotentialPressure = species.MutationPotential * _settings.MutationPotentialPressureScale;
        // Same-season arrivals should create a clear short-term mismatch spike even when
        // the destination's recalculated baseline fit is only moderately hostile.
        double migrationShock = population.ReceivedMigrantsThisSeason
            ? 0.12 + (ancestralMismatch * 0.25)
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
        population.HabitatMismatchMutationPressure = DecayAndAdd(
            population.HabitatMismatchMutationPressure,
            (ancestralMismatch * 0.44)
            + migrationShock
            + (ecologicalDistance * _settings.EcologicalIsolationPressureScale)
            + founderIsolationMomentum,
            0.88);
        population.IsolationMutationPressure = DecayAndAdd(
            population.IsolationMutationPressure,
            isolationGain + founderIsolationMomentum + mutationPotentialPressure,
            0.92);
        population.CrowdingMutationPressure = DecayAndAdd(population.CrowdingMutationPressure, Math.Max(0.0, carryingRatio - 0.85) * 0.55, 0.86);
        population.DriftMutationPressure = DecayAndAdd(
            population.DriftMutationPressure,
            driftGain + (founderEcologyDistance * 0.04) + mutationPotentialPressure,
            0.92);

        double activePressure =
            population.FoodStressMutationPressure +
            population.PredationMutationPressure +
            population.HuntingMutationPressure +
            population.HabitatMismatchMutationPressure +
            population.IsolationMutationPressure +
            population.CrowdingMutationPressure;

        population.DivergencePressure = DecayAndAdd(
            population.DivergencePressure,
            (activePressure * _settings.DivergencePressureScale)
            + (population.DriftMutationPressure * 0.05)
            + mutationPotentialPressure
            + founderIsolationMomentum,
            _settings.DivergencePressureDecay);
        double contactResistance = 1.0 - Math.Clamp(founderEcologyDistance * _settings.ContactEcologyResistanceScale, 0.0, 0.70);
        population.DivergenceScore = Math.Clamp(
            population.DivergenceScore +
            (population.DivergencePressure * 0.06) +
            (population.DriftMutationPressure * 0.01) -
            (population.ContactPressure * _settings.ContactDivergenceReductionPerSeason * contactResistance) -
            _settings.DivergenceDecayPerSeason,
            0.0,
            4.8);
        population.AdaptationPressureSummary = BuildAdaptationPressureSummary(population);
    }

    private bool ShouldMutate(Species species, RegionSpeciesPopulation population, out MutationTier tier)
    {
        tier = MutationTier.None;
        double mutationPotentialBonus = species.MutationPotential * 0.35;

        double weightedPressure =
            population.FoodStressMutationPressure +
            population.PredationMutationPressure +
            population.HuntingMutationPressure +
            population.HabitatMismatchMutationPressure +
            (population.IsolationMutationPressure * 0.85) +
            (population.CrowdingMutationPressure * 0.70) +
            (population.DriftMutationPressure * 0.35) +
            mutationPotentialBonus;

        if (weightedPressure < _settings.MinorMutationThreshold)
        {
            return false;
        }

        double majorChance = Math.Clamp(
            ((weightedPressure - 2.60) * _settings.MajorMutationChanceScale) +
            (population.DivergenceScore * 0.03) +
            (population.IsolationMutationPressure * 0.04) +
            (species.MutationPotential * 0.02),
            0.0,
            0.12);
        if (weightedPressure >= _settings.MajorMutationThreshold && _random.NextDouble() < majorChance)
        {
            tier = MutationTier.Major;
            return true;
        }

        double minorChance = Math.Clamp(
            ((weightedPressure - 1.10) * _settings.MinorMutationChanceScale) +
            (population.IsolationMutationPressure * 0.03) +
            (species.MutationPotential * 0.03),
            0.0,
            0.36);
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
            ["divergencePressure"] = population.DivergencePressure.ToString("F2"),
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
        population.DivergenceScore = Math.Clamp(
            population.DivergenceScore + (impact * (tier == MutationTier.Major ? _settings.MajorMutationDivergenceImpact : _settings.MinorMutationDivergenceImpact)),
            0.0,
            4.8);
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
            ["divergencePressure"] = population.DivergencePressure.ToString("F2"),
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
            ["population"] = population.PopulationCount.ToString(),
            ["originRegionId"] = population.FounderSourceRegionId?.ToString() ?? region.Id.ToString(),
            ["originSpeciesId"] = population.FounderSourceSpeciesId?.ToString() ?? species.Id.ToString()
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

        if (ShouldEmitMutationEvent(world, population, tier, impact))
        {
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

            if (tier == MutationTier.Major)
            {
                population.LastMajorMutationEventYear = world.Time.Year;
            }
            else
            {
                population.LastMinorMutationEventYear = world.Time.Year;
            }
        }

        EmitDivergenceMilestone(world, region, population, species, relatedPolity);
        RecordMutationHistory(world, region, population, species, tier, dominantPressure, rankedPressures, plan);
    }

    private void EmitIsolationMilestone(World world, Region region, RegionSpeciesPopulation population, Species species)
    {
        int milestone = ResolveIsolationMilestone(population.IsolationSeasons);
        if (milestone == 0 || milestone <= population.LastIsolationEventMilestone)
        {
            return;
        }

        population.LastIsolationEventMilestone = milestone;
        world.AddEvent(
            WorldEventType.SpeciesPopulationIsolated,
            milestone >= 3 ? WorldEventSeverity.Notable : WorldEventSeverity.Minor,
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
            },
            metadata: new Dictionary<string, string>
            {
                ["isolationMilestone"] = milestone.ToString()
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

        if (world.GetLineageForSpecies(species.Id) is EvolutionaryLineage lineage)
        {
            lineage.HabitatAdaptationSummary = BuildLineageHabitatAdaptationSummary(region, species, population);
            lineage.AdaptationPressureSummary = population.AdaptationPressureSummary;
            lineage.Stage = LineageStage.EstablishedSpecies;
        }

        world.AddEvolutionaryHistoryEvent(
            EvolutionaryHistoryEventType.AdaptationMilestone,
            species.LineageId,
            species.ParentSpeciesId,
            species.Id,
            region.Id,
            $"{species.Name} adapted more strongly to {region.Name}",
            "regional_adaptation",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["adaptationMilestone"] = adaptationMilestone.ToString(),
                ["suitabilityGain"] = suitabilityGain.ToString("F2"),
                ["adaptationSignal"] = adaptationSignal
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

        if (world.GetLineageForSpecies(species.Id) is EvolutionaryLineage lineage)
        {
            lineage.MaxObservedDivergenceMilestone = Math.Max(lineage.MaxObservedDivergenceMilestone, milestone);
            lineage.Stage = milestone >= 2 ? LineageStage.EstablishedSpecies : LineageStage.Diverging;
            lineage.AdaptationPressureSummary = population.AdaptationPressureSummary;
        }

        world.AddEvolutionaryHistoryEvent(
            EvolutionaryHistoryEventType.DivergenceMilestone,
            species.LineageId,
            species.ParentSpeciesId,
            species.Id,
            region.Id,
            $"{species.Name} reached divergence milestone {milestone} in {region.Name}",
            "accumulated_divergence",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["milestone"] = milestone.ToString(),
                ["divergenceScore"] = population.DivergenceScore.ToString("F2"),
                ["contactPressure"] = population.ContactPressure.ToString("F2")
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

    private bool TrySpeciate(
        World world,
        Region region,
        RegionSpeciesPopulation population,
        Species species,
        SpeciationContext speciationContext,
        SpeciationCandidateState candidateState)
    {
        if (!candidateState.IsCandidate && !IsSpeciationCandidate(world, population, species, speciationContext, out candidateState))
        {
            return false;
        }

        if (!CanSpeciate(world, population, species, speciationContext, candidateState))
        {
            return false;
        }

        List<MutationPressureContribution> rankedPressures = RankPressures(population);
        Species descendant = CreateDescendantSpecies(world, region, population, species, rankedPressures);
        world.Species.Add(descendant);
        species.RecordDescendant(descendant.Id);

        int descendantPopulationCount = Math.Max(
            _settings.SpeciationFounderPopulationMinimum,
            (int)Math.Round(population.PopulationCount * _settings.SpeciationFounderPopulationShare));
        descendantPopulationCount = Math.Min(descendantPopulationCount, Math.Max(1, population.PopulationCount - 1));
        if (descendantPopulationCount <= 0)
        {
            world.Species.Remove(descendant);
            species.DescendantSpeciesIds.Remove(descendant.Id);
            return false;
        }

        int parentPopulationBeforeSplit = population.PopulationCount;
        double parentPreSplitDivergenceScore = population.DivergenceScore;
        double parentPreSplitDivergencePressure = population.DivergencePressure;
        double parentPreSplitIsolationPressure = population.IsolationMutationPressure;
        double parentPreSplitFoodStressPressure = population.FoodStressMutationPressure;
        double parentPreSplitPredationPressure = population.PredationMutationPressure;
        double parentPreSplitHuntingPressure = population.HuntingMutationPressure;
        double parentPreSplitHabitatMismatchPressure = population.HabitatMismatchMutationPressure;
        double parentPreSplitCrowdingPressure = population.CrowdingMutationPressure;
        double parentPreSplitDriftPressure = population.DriftMutationPressure;
        int parentPreSplitIsolationSeasons = population.IsolationSeasons;
        int parentPreSplitReadinessSeasons = population.SpeciationReadinessSeasons;
        population.PopulationCount = Math.Max(1, population.PopulationCount - descendantPopulationCount);
        population.LastSpeciationYear = world.Time.Year;
        population.DivergenceScore *= _settings.ParentPostSpeciationDivergenceRetention;
        population.DivergencePressure *= 0.55;
        population.IsolationMutationPressure *= 0.60;
        population.SpeciationReadinessSeasons = Math.Max(0, population.SpeciationReadinessSeasons / 3);

        RegionSpeciesPopulation descendantPopulation = region.GetOrCreateSpeciesPopulation(descendant.Id);
        descendantPopulation.PopulationCount = descendantPopulationCount;
        descendantPopulation.BaseHabitatSuitability = SpeciesEcology.CalculateBaseHabitatSuitability(descendant, region);
        descendantPopulation.HabitatSuitability = SpeciesEcology.CalculateHabitatSuitability(descendant, descendantPopulation, descendantPopulation.BaseHabitatSuitability);
        descendantPopulation.CarryingCapacity = SpeciesEcology.CalculateCarryingCapacity(descendant, descendantPopulation, region, descendantPopulation.HabitatSuitability);
        descendantPopulation.IntelligenceOffset = population.IntelligenceOffset * _settings.DescendantResidualOffsetShare;
        descendantPopulation.SocialityOffset = population.SocialityOffset * _settings.DescendantResidualOffsetShare;
        descendantPopulation.AggressionOffset = population.AggressionOffset * _settings.DescendantResidualOffsetShare;
        descendantPopulation.EnduranceOffset = population.EnduranceOffset * _settings.DescendantResidualOffsetShare;
        descendantPopulation.FertilityOffset = population.FertilityOffset * _settings.DescendantResidualOffsetShare;
        descendantPopulation.DietFlexibilityOffset = population.DietFlexibilityOffset * _settings.DescendantResidualOffsetShare;
        descendantPopulation.ClimateToleranceOffset = population.ClimateToleranceOffset * _settings.DescendantResidualOffsetShare;
        descendantPopulation.SizeOffset = population.SizeOffset * _settings.DescendantResidualOffsetShare;
        descendantPopulation.FoodStressMutationPressure = parentPreSplitFoodStressPressure * _settings.DescendantStartingStressPressureRetention;
        descendantPopulation.PredationMutationPressure = parentPreSplitPredationPressure * _settings.DescendantStartingStressPressureRetention;
        descendantPopulation.HuntingMutationPressure = parentPreSplitHuntingPressure * _settings.DescendantStartingStressPressureRetention;
        descendantPopulation.HabitatMismatchMutationPressure = parentPreSplitHabitatMismatchPressure * 0.60;
        descendantPopulation.IsolationMutationPressure = parentPreSplitIsolationPressure * _settings.DescendantStartingIsolationPressureRetention;
        descendantPopulation.CrowdingMutationPressure = parentPreSplitCrowdingPressure * 0.42;
        descendantPopulation.DriftMutationPressure = parentPreSplitDriftPressure * 0.55;
        descendantPopulation.DivergencePressure = parentPreSplitDivergencePressure * _settings.DescendantStartingDivergencePressureRetention;
        descendantPopulation.DivergenceScore = Math.Max(0.0, parentPreSplitDivergenceScore * _settings.DescendantStartingDivergenceRetention);
        descendantPopulation.IsolationSeasons = Math.Max(
            _settings.DescendantStartingIsolationSeasons,
            (int)Math.Round(parentPreSplitIsolationSeasons * _settings.DescendantIsolationSeasonsRetention));
        descendantPopulation.SpeciationReadinessSeasons = Math.Max(
            _settings.DescendantStartingReadinessSeasons,
            (int)Math.Round(parentPreSplitReadinessSeasons * _settings.DescendantReadinessSeasonsRetention));
        descendantPopulation.MinorMutationCount = 0;
        descendantPopulation.MajorMutationCount = 0;
        descendantPopulation.LastMutationYear = world.Time.Year;
        descendantPopulation.LastMajorMutationYear = -1;
        descendantPopulation.LastSpeciationYear = -1;
        descendantPopulation.LastIsolationEventMilestone = 0;
        descendantPopulation.LastDivergenceMilestone = 0;
        descendantPopulation.LastAdaptationMilestone = 0;
        descendantPopulation.FounderLineageId = descendant.LineageId;
        descendantPopulation.MarkEstablished(world.Time.Year, world.Time.Month, "speciation", region.Id, species.Id);
        descendantPopulation.HasEverExisted = true;

        string pressureSummary = rankedPressures.Count == 0
            ? "mixed pressure"
            : string.Join(", ", rankedPressures.Take(3).Select(candidate => $"{candidate.Name}:{candidate.Value:F2}"));
        descendantPopulation.AdaptationPressureSummary = pressureSummary;
        string narrative = $"{descendant.Name} appeared in {region.Name} as a new descendant of {species.Name}";
        Polity? relatedPolity = FindRelevantPolity(world, region, species.Id);
        (int? polityId, string? polityName, int? relatedPolityId, string? relatedPolityName, int? relatedPolitySpeciesId, string? relatedPolitySpeciesName) =
            ResolvePolityContext(world, relatedPolity, species.Id);

        world.AddEvent(
            WorldEventType.NewSpeciesAppeared,
            WorldEventSeverity.Major,
            narrative,
            $"{descendant.Name} split from {species.Name} in {region.Name}; founder population {descendantPopulationCount}; pressures [{pressureSummary}].",
            reason: "regional_speciation",
            scope: WorldEventScope.Regional,
            polityId: polityId,
            polityName: polityName,
            relatedPolityId: relatedPolityId,
            relatedPolityName: relatedPolityName,
            relatedPolitySpeciesId: relatedPolitySpeciesId,
            relatedPolitySpeciesName: relatedPolitySpeciesName,
            speciesId: descendant.Id,
            speciesName: descendant.Name,
            regionId: region.Id,
            regionName: region.Name,
            before: new Dictionary<string, string>
            {
                ["parentPopulation"] = parentPopulationBeforeSplit.ToString(),
                ["parentDivergenceScore"] = parentPreSplitDivergenceScore.ToString("F2")
            },
            after: new Dictionary<string, string>
            {
                ["parentPopulation"] = population.PopulationCount.ToString(),
                ["descendantPopulation"] = descendantPopulationCount.ToString(),
                ["descendantDivergenceScore"] = descendantPopulation.DivergenceScore.ToString("F2")
            },
            metadata: new Dictionary<string, string>
            {
                ["parentSpeciesId"] = species.Id.ToString(),
                ["parentSpeciesName"] = species.Name,
                ["rootAncestorSpeciesId"] = descendant.RootAncestorSpeciesId.ToString(),
                ["originRegionId"] = region.Id.ToString(),
                ["originRegionName"] = region.Name,
                ["pressureSummary"] = pressureSummary,
                ["earliestSpeciationYear"] = descendant.EarliestSpeciationYear.ToString()
            });

        RegisterSpeciationLineage(world, region, species, descendant, pressureSummary, descendantPopulationCount);

        _lastRootSpeciationYearByRegion[(region.Id, species.RootAncestorSpeciesId)] = world.Time.Year;
        speciationContext.RegisterNewSpecies(descendant, region.Id, descendantPopulationCount);

        return true;
    }

    private bool IsSpeciationCandidate(
        World world,
        RegionSpeciesPopulation population,
        Species species,
        SpeciationContext speciationContext,
        out SpeciationCandidateState candidateState)
    {
        candidateState = default;

        if (species.IsSapient
            || world.Time.Year < species.EarliestSpeciationYear
            || ResolveSpeciesAgeYears(world, species) < _settings.MinimumSpeciesAgeYearsForSpeciation
            || population.PopulationCount < _settings.SpeciationMinimumPopulation)
        {
            return false;
        }

        int sameRootSpeciesInRegion = speciationContext.CountSameRootSpeciesInRegion(population.RegionId, species.RootAncestorSpeciesId);
        if (sameRootSpeciesInRegion >= _settings.RegionalRootLineageHardCap)
        {
            return false;
        }

        int crowdingOverSoftCap = Math.Max(0, sameRootSpeciesInRegion - _settings.RegionalRootLineageSoftCap);
        double requiredDivergence = _settings.SpeciationDivergenceThreshold + (crowdingOverSoftCap * _settings.RegionalCrowdingExtraDivergencePerSpecies);
        int requiredReadiness = _settings.SpeciationReadinessSeasonsThreshold + (crowdingOverSoftCap * _settings.RegionalCrowdingExtraReadinessSeasonsPerSpecies);

        bool isCandidate = population.IsolationSeasons >= _settings.SpeciationIsolationSeasonsThreshold
               && population.SpeciationReadinessSeasons >= requiredReadiness
               && population.DivergenceScore >= requiredDivergence;

        if (isCandidate)
        {
            candidateState = new SpeciationCandidateState(
                true,
                sameRootSpeciesInRegion,
                crowdingOverSoftCap,
                requiredDivergence,
                requiredReadiness);
        }

        return isCandidate;
    }

    private bool CanSpeciate(
        World world,
        RegionSpeciesPopulation population,
        Species species,
        SpeciationContext speciationContext,
        SpeciationCandidateState candidateState)
    {
        if (!candidateState.IsCandidate)
        {
            return false;
        }

        if (population.LastSpeciationYear >= 0 && world.Time.Year - population.LastSpeciationYear < _settings.SpeciationCooldownYears)
        {
            return false;
        }

        int globalPopulation = speciationContext.ResolveGlobalPopulation(species.Id);
        int requiredGlobalPopulation = _settings.SpeciationMinimumGlobalPopulation + (candidateState.CrowdingOverSoftCap * _settings.RegionalCrowdingExtraGlobalPopulationPerSpecies);
        if (globalPopulation < requiredGlobalPopulation)
        {
            return false;
        }

        if (_lastRootSpeciationYearByRegion.TryGetValue((population.RegionId, species.RootAncestorSpeciesId), out int lastRootSpeciationYear)
            && world.Time.Year - lastRootSpeciationYear < _settings.RegionalRootSpeciationCooldownYears)
        {
            return false;
        }

        int mutationCount = population.MinorMutationCount + (population.MajorMutationCount * 2);
        if (mutationCount < _settings.SpeciationMinimumMutations)
        {
            return false;
        }

        bool speciationSignal = population.MajorMutationCount >= _settings.SpeciationMinimumMajorMutations
            || population.RegionAdaptationRecorded
            || (population.IsolationSeasons >= _settings.SpeciationIsolationSeasonsThreshold + 8
                && population.DivergenceScore >= candidateState.RequiredDivergence + 0.35
                && population.HabitatSuitability >= population.BaseHabitatSuitability + 0.10);
        if (!speciationSignal)
        {
            return false;
        }

        if (population.CarryingCapacity > 0 && population.PopulationCount < Math.Max(_settings.SpeciationMinimumPopulation, population.CarryingCapacity / 8))
        {
            return false;
        }

        return population.DivergencePressure >= _settings.MajorPressureForSpeciationBonus
            || population.MajorMutationCount > 0
            || population.RegionAdaptationRecorded
            || population.DivergenceScore >= candidateState.RequiredDivergence + 0.40;
    }

    private Species CreateDescendantSpecies(
        World world,
        Region region,
        RegionSpeciesPopulation population,
        Species parent,
        IReadOnlyList<MutationPressureContribution> rankedPressures)
    {
        int newSpeciesId = world.Species.Count == 0 ? 0 : world.Species.Max(candidate => candidate.Id) + 1;
        string name = BuildDescendantSpeciesName(world, region, parent);
        Species descendant = new(
            newSpeciesId,
            name,
            ClampUnit(parent.Intelligence + (population.IntelligenceOffset * _settings.DescendantBaselineTraitShare)),
            ClampUnit(parent.Cooperation + (population.SocialityOffset * _settings.DescendantBaselineTraitShare)))
        {
            IsSapient = false,
            TrophicRole = parent.TrophicRole,
            FertilityPreference = ClampUnit((parent.FertilityPreference * 0.78) + (region.Fertility * 0.22)),
            WaterPreference = ClampUnit((parent.WaterPreference * 0.78) + (region.WaterAvailability * 0.22)),
            PlantBiomassAffinity = ClampUnit(parent.PlantBiomassAffinity + (population.DietFlexibilityOffset * 0.08)),
            AnimalBiomassAffinity = ClampUnit(parent.AnimalBiomassAffinity + (population.DietFlexibilityOffset * 0.08)),
            BaseCarryingCapacityFactor = Math.Clamp(parent.BaseCarryingCapacityFactor + (population.ClimateToleranceOffset * 0.08) - (population.SizeOffset * 0.04), 0.40, 1.65),
            MigrationCapability = ClampUnit(parent.MigrationCapability + (population.EnduranceOffset * 0.08) + (population.ClimateToleranceOffset * 0.04)),
            ExpansionPressure = ClampUnit(parent.ExpansionPressure + (population.DietFlexibilityOffset * 0.04)),
            BaseReproductionRate = Math.Clamp(parent.BaseReproductionRate * (1.0 + (population.FertilityOffset * 0.22)), 0.02, 0.26),
            BaseDeclineRate = Math.Clamp(parent.BaseDeclineRate * (1.0 - (population.EnduranceOffset * 0.18) + (Math.Max(0.0, population.SizeOffset) * 0.08)), 0.01, 0.18),
            SpringReproductionModifier = parent.SpringReproductionModifier,
            SummerReproductionModifier = parent.SummerReproductionModifier,
            AutumnReproductionModifier = parent.AutumnReproductionModifier,
            WinterReproductionModifier = Math.Clamp(parent.WinterReproductionModifier + (population.ClimateToleranceOffset * 0.10), 0.30, 1.15),
            MeatYield = Math.Clamp(parent.MeatYield * (1.0 + (population.SizeOffset * 0.28)), 1.0, 40.0),
            HuntingDifficulty = Math.Clamp(parent.HuntingDifficulty + (population.EnduranceOffset * 0.08) + (population.IntelligenceOffset * 0.04), 0.04, 0.95),
            HuntingDanger = Math.Clamp(parent.HuntingDanger + (population.AggressionOffset * 0.10) + (population.SocialityOffset * 0.04), 0.0, 0.98),
            IsToxicToEat = parent.IsToxicToEat,
            DomesticationAffinity = Math.Clamp(parent.DomesticationAffinity - (population.AggressionOffset * 0.08) - (population.SizeOffset * 0.04), 0.02, 0.95),
            CultivationAffinity = parent.CultivationAffinity,
            LineageId = newSpeciesId,
            ParentSpeciesId = parent.Id,
            RootAncestorSpeciesId = parent.RootAncestorSpeciesId,
            OriginRegionId = region.Id,
            OriginYear = world.Time.Year,
            OriginMonth = world.Time.Month,
            OriginCause = "regional_speciation",
            EarliestSpeciationYear = world.Time.Year + _settings.DescendantSpeciesStabilizationYears + ResolveSpeciationJitterYears(newSpeciesId, region.Id),
            SentienceCapability = SentienceCapabilityState.None,
            OriginPressureSummary = rankedPressures.Count == 0
                ? "mixed pressure"
                : string.Join(", ", rankedPressures.Take(3).Select(candidate => $"{candidate.Name}:{candidate.Value:F2}"))
        };

        foreach (int preyId in parent.DietSpeciesIds)
        {
            descendant.DietSpeciesIds.Add(preyId);
        }

        foreach (RegionBiome biome in parent.PreferredBiomes)
        {
            descendant.PreferredBiomes.Add(biome);
        }

        descendant.PreferredBiomes.Add(region.Biome);
        descendant.InitialRangeRegionIds.Add(region.Id);
        return descendant;
    }

    private void RefreshEvolutionaryLineages(World world)
    {
        foreach (Species species in world.Species)
        {
            EvolutionaryLineage? lineage = world.GetLineageForSpecies(species.Id);
            if (lineage is null)
            {
                lineage = new EvolutionaryLineage(species.LineageId, species.Id, species.EcologyNiche, species.TrophicRole)
                {
                    ParentLineageId = species.ParentSpeciesId,
                    RootAncestorLineageId = species.ParentSpeciesId ?? species.LineageId,
                    OriginRegionId = species.OriginRegionId,
                    OriginYear = species.OriginYear,
                    TraitProfileSummary = BuildSpeciesTraitSummary(species),
                    HabitatAdaptationSummary = "ancestral fit"
                };
                world.EvolutionaryLineages.Add(lineage);
            }

            List<RegionSpeciesPopulation> activePopulations = world.Regions
                .Select(region => region.GetSpeciesPopulation(species.Id))
                .Where(population => population is not null && population.PopulationCount > 0)
                .Cast<RegionSpeciesPopulation>()
                .ToList();
            lineage.CurrentPopulationRegions = activePopulations.Count;
            lineage.CurrentPopulationCount = activePopulations.Sum(population => population.PopulationCount);
            lineage.IsExtinct = species.IsGloballyExtinct;
            lineage.ExtinctionYear = species.ExtinctionYear;
            lineage.ExtinctionMonth = species.ExtinctionMonth;
            lineage.Stage = ResolveLineageStage(species, activePopulations);
            lineage.SentienceCapability = species.SentienceCapability;
            lineage.TraitProfileSummary = BuildSpeciesTraitSummary(species, activePopulations.FirstOrDefault());
            lineage.AdaptedBiomeIds.Clear();
            foreach (RegionSpeciesPopulation population in activePopulations.Where(candidate =>
                         candidate.RegionAdaptationRecorded
                         || candidate.HabitatSuitability >= candidate.BaseHabitatSuitability + 0.08))
            {
                lineage.AdaptedBiomeIds.Add((int)world.Regions[population.RegionId].Biome);
            }
            lineage.HabitatAdaptationSummary = activePopulations
                .OrderByDescending(population => population.HabitatSuitability)
                .Select(population => BuildLineageHabitatAdaptationSummary(world.Regions[population.RegionId], species, population))
                .FirstOrDefault()
                ?? lineage.HabitatAdaptationSummary;
            lineage.AdaptationPressureSummary = activePopulations
                .OrderByDescending(population => population.DivergencePressure)
                .Select(population => population.AdaptationPressureSummary)
                .FirstOrDefault(summary => !string.IsNullOrWhiteSpace(summary))
                ?? lineage.AdaptationPressureSummary;

            EvaluateSentienceCapability(world, species, lineage, activePopulations);
        }
    }

    private void EvaluateSentienceCapability(World world, Species species, EvolutionaryLineage lineage, IReadOnlyCollection<RegionSpeciesPopulation> activePopulations)
    {
        double intelligence = activePopulations.Count == 0
            ? species.Intelligence
            : activePopulations.Average(population => PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Intelligence));
        double sociality = activePopulations.Count == 0
            ? species.Cooperation
            : activePopulations.Average(population => PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Sociality));
        double ecologicalComplexity = activePopulations.Count == 0
            ? 0.0
            : activePopulations
                .OrderByDescending(population => population.DivergenceScore + population.DivergencePressure)
                .Take(3)
                .Average(population =>
                    population.DivergencePressure +
                    population.HabitatMismatchMutationPressure +
                    population.PredationMutationPressure +
                    population.FoodStressMutationPressure +
                    (population.DivergenceScore * 0.24));
        int viablePopulationSupport = activePopulations.Sum(population => population.PopulationCount);
        int lineageAge = Math.Max(0, world.Time.Year - species.OriginYear);
        double historyComplexity = Math.Clamp(
            (lineage.AncestryDepth * 0.18)
            + (lineage.SpeciationCount * 0.14)
            + (lineage.LocalExtinctionCount * 0.08)
            + (lineage.AdaptedBiomeIds.Count * 0.10),
            0.0,
            1.8) * _settings.SentienceHistoryComplexityBonusScale;
        ecologicalComplexity += historyComplexity + (species.SentiencePotential * _settings.SentiencePotentialComplexityBonusScale);
        int capableCount = world.EvolutionaryLineages.Count(candidate => !candidate.IsExtinct && candidate.SentienceCapability == SentienceCapabilityState.Capable);
        HashSet<int> capableRoots = world.Species
            .Where(candidate => !candidate.IsGloballyExtinct && candidate.SentienceCapability == SentienceCapabilityState.Capable)
            .Select(candidate => candidate.RootAncestorSpeciesId)
            .ToHashSet();
        int capableRootCount = capableRoots.Count;
        bool rootAlreadyCapable = capableRoots.Contains(species.RootAncestorSpeciesId);
        double capableComplexityThreshold = rootAlreadyCapable
            ? _settings.SentienceComplexityThreshold * 1.08
            : _settings.SentienceComplexityThreshold;
        int capablePopulationThreshold = rootAlreadyCapable
            ? (int)(_settings.SentienceMinimumPopulationSupport * 1.18)
            : _settings.SentienceMinimumPopulationSupport;

        SentienceCapabilityState nextState = SentienceCapabilityState.None;
        if (intelligence >= _settings.SentienceIntelligenceThreshold * 0.82
            && sociality >= _settings.SentienceSocialityThreshold * 0.82
            && ecologicalComplexity >= _settings.SentienceComplexityThreshold * 0.58
            && viablePopulationSupport >= _settings.SentienceMinimumPopulationSupport / 2
            && lineageAge >= _settings.SentienceMinimumHistoryYears / 2)
        {
            nextState = SentienceCapabilityState.Latent;
        }

        if (intelligence >= _settings.SentienceIntelligenceThreshold * 0.92
            && sociality >= _settings.SentienceSocialityThreshold * 0.90
            && ecologicalComplexity >= _settings.SentienceComplexityThreshold * 0.82
            && viablePopulationSupport >= (int)(_settings.SentienceMinimumPopulationSupport * 0.72)
            && lineageAge >= (int)(_settings.SentienceMinimumHistoryYears * 0.72))
        {
            nextState = SentienceCapabilityState.Emerging;
        }

        if (capableCount < (_settings.SentienceCapableLineageSoftCap + 1)
            && (!rootAlreadyCapable || capableRootCount >= _settings.SentienceCapableLineageSoftCap)
            && intelligence >= _settings.SentienceIntelligenceThreshold
            && sociality >= _settings.SentienceSocialityThreshold
            && ecologicalComplexity >= capableComplexityThreshold
            && viablePopulationSupport >= capablePopulationThreshold
            && lineageAge >= _settings.SentienceMinimumHistoryYears)
        {
            nextState = SentienceCapabilityState.Capable;
        }

        if (nextState <= species.SentienceCapability)
        {
            return;
        }

        species.SentienceCapability = nextState;
        lineage.SentienceCapability = nextState;
        lineage.Stage = nextState == SentienceCapabilityState.Capable || lineage.Stage == LineageStage.SentienceCapable
            ? LineageStage.SentienceCapable
            : lineage.Stage == LineageStage.Primitive
                ? LineageStage.EstablishedSpecies
                : lineage.Stage;
        lineage.SentienceMilestoneYear = world.Time.Year;
        world.AddEvolutionaryHistoryEvent(
            EvolutionaryHistoryEventType.SentienceCapabilityMilestone,
            lineage.Id,
            lineage.ParentLineageId,
            species.Id,
            species.OriginRegionId,
            $"{species.Name} reached sentience capability state {nextState}",
            "sentience_progression",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["state"] = nextState.ToString(),
                ["intelligence"] = intelligence.ToString("F2"),
                ["sociality"] = sociality.ToString("F2"),
                ["ecologicalComplexity"] = ecologicalComplexity.ToString("F2"),
                ["populationSupport"] = viablePopulationSupport.ToString()
            });
    }

    private static LineageStage ResolveLineageStage(Species species, IReadOnlyCollection<RegionSpeciesPopulation> activePopulations)
    {
        if (species.SentienceCapability == SentienceCapabilityState.Capable)
        {
            return LineageStage.SentienceCapable;
        }

        if (species.ParentSpeciesId is not null || activePopulations.Any(population => population.DivergenceScore >= 1.6))
        {
            return species.ParentSpeciesId is not null
                ? LineageStage.EstablishedSpecies
                : LineageStage.Diverging;
        }

        return LineageStage.Primitive;
    }

    private void RecordMutationHistory(
        World world,
        Region region,
        RegionSpeciesPopulation population,
        Species species,
        MutationTier tier,
        MutationPressureContribution dominantPressure,
        IReadOnlyList<MutationPressureContribution> rankedPressures,
        MutationPlan plan)
    {
        if (world.GetLineageForSpecies(species.Id) is not EvolutionaryLineage lineage)
        {
            return;
        }

        lineage.MutationEventCount++;
        if (tier == MutationTier.Major)
        {
            lineage.MajorMutationEventCount++;
        }

        lineage.AdaptationPressureSummary = population.AdaptationPressureSummary;
        lineage.TraitProfileSummary = BuildSpeciesTraitSummary(species, population);
        world.AddEvolutionaryHistoryEvent(
            tier == MutationTier.Major ? EvolutionaryHistoryEventType.MajorMutation : EvolutionaryHistoryEventType.MinorMutation,
            lineage.Id,
            lineage.ParentLineageId,
            species.Id,
            region.Id,
            $"{species.Name} mutated in {region.Name}",
            $"pressure_{dominantPressure.Name}",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["tier"] = tier.ToString(),
                ["pressureSummary"] = string.Join(", ", rankedPressures.Take(3).Select(candidate => $"{candidate.Name}:{candidate.Value:F2}")),
                ["traitChanges"] = string.Join(", ", plan.Changes.Select(change => $"{change.Trait}:{change.Delta:+0.00;-0.00}")),
                ["divergenceScore"] = population.DivergenceScore.ToString("F2")
            });
    }

    private void RegisterSpeciationLineage(World world, Region region, Species parent, Species descendant, string pressureSummary, int descendantPopulationCount)
    {
        EvolutionaryLineage? parentLineage = world.GetLineageForSpecies(parent.Id);
        EvolutionaryLineage descendantLineage = new(descendant.LineageId, descendant.Id, descendant.EcologyNiche, descendant.TrophicRole)
        {
            ParentLineageId = parent.LineageId,
            RootAncestorLineageId = parentLineage?.RootAncestorLineageId ?? parent.LineageId,
            OriginRegionId = region.Id,
            OriginYear = world.Time.Year,
            Stage = LineageStage.EstablishedSpecies,
            TraitProfileSummary = BuildSpeciesTraitSummary(descendant),
            HabitatAdaptationSummary = region.Biome.ToString(),
            AdaptationPressureSummary = pressureSummary,
            AncestryDepth = (parentLineage?.AncestryDepth ?? 0) + 1
        };
        world.EvolutionaryLineages.Add(descendantLineage);

        if (parentLineage is not null)
        {
            parentLineage.Stage = LineageStage.EstablishedSpecies;
            parentLineage.SpeciationCount++;
            parentLineage.DescendantLineageIds.Add(descendantLineage.Id);
        }

        world.AddEvolutionaryHistoryEvent(
            EvolutionaryHistoryEventType.Speciation,
            descendantLineage.Id,
            descendantLineage.ParentLineageId,
            descendant.Id,
            region.Id,
            $"{descendant.Name} speciated from {parent.Name} in {region.Name}",
            "regional_speciation",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["parentSpeciesId"] = parent.Id.ToString(),
                ["descendantPopulation"] = descendantPopulationCount.ToString(),
                ["pressureSummary"] = pressureSummary
            });
    }

    private double ResolveContactExposure(
        World world,
        Region region,
        RegionSpeciesPopulation population,
        Species species,
        RegionSpeciesPopulation connectedPopulation)
    {
        Region connectedRegion = world.Regions[connectedPopulation.RegionId];
        double ecologySimilarity = 1.0 - ResolveRegionEcologyDistance(region, connectedRegion);
        double suitabilitySimilarity = 1.0 - Math.Abs(population.HabitatSuitability - connectedPopulation.HabitatSuitability);
        double divergenceResistance = Math.Clamp(
            (population.DivergenceScore / Math.Max(0.01, _settings.SpeciationDivergenceThreshold)) * _settings.ContactEcologyResistanceScale,
            0.0,
            0.65);
        double founderDistance = population.FounderSourceRegionId.HasValue && population.FounderSourceRegionId.Value != region.Id
            ? ResolveFounderEcologyDistance(world, region, population)
            : 0.0;
        double preferredBiomeAffinity = species.PreferredBiomes.Contains(region.Biome) && !species.PreferredBiomes.Contains(connectedRegion.Biome)
            ? 0.08
            : 0.0;

        return Math.Clamp(
            (ecologySimilarity * 0.55)
            + (suitabilitySimilarity * 0.25)
            + 0.10
            - divergenceResistance
            - (founderDistance * 0.24)
            - preferredBiomeAffinity,
            0.0,
            1.0);
    }

    private double ResolveFounderIsolationMomentum(RegionSpeciesPopulation population, double founderEcologyDistance)
    {
        if (population.FounderSourceRegionId is null || population.FounderSourceRegionId == population.RegionId)
        {
            return 0.0;
        }

        double founderKindBonus = string.Equals(population.FounderKind, "recolonization", StringComparison.OrdinalIgnoreCase)
            ? 0.06
            : string.Equals(population.FounderKind, "speciation", StringComparison.OrdinalIgnoreCase)
                ? 0.04
                : 0.0;
        return Math.Clamp(
            founderKindBonus
            + (founderEcologyDistance * _settings.FounderIsolationPressureBonus)
            + (Math.Max(0, population.IsolationSeasons - 4) / 40.0),
            0.0,
            0.24);
    }

    private static double ResolveEcologicalDistance(Species species, Region region)
    {
        RegionEcologyProfile ecology = region.EffectiveEcologyProfile;
        double temperatureDistance = Math.Abs(ecology.Temperature - species.TemperaturePreference);
        double moistureDistance = Math.Abs(ecology.Moisture - species.MoisturePreference);
        double fertilityDistance = Math.Abs(region.Fertility - species.FertilityPreference);
        double waterDistance = Math.Abs(region.WaterAvailability - species.WaterPreference);
        return Math.Clamp(
            (temperatureDistance * 0.28)
            + (moistureDistance * 0.28)
            + (fertilityDistance * 0.22)
            + (waterDistance * 0.22),
            0.0,
            1.0);
    }

    private static double ResolveFounderEcologyDistance(World world, Region region, RegionSpeciesPopulation population)
    {
        if (!population.FounderSourceRegionId.HasValue
            || population.FounderSourceRegionId.Value == region.Id
            || population.FounderSourceRegionId.Value < 0
            || population.FounderSourceRegionId.Value >= world.Regions.Count)
        {
            return 0.0;
        }

        return ResolveRegionEcologyDistance(region, world.Regions[population.FounderSourceRegionId.Value]);
    }

    private static double ResolveRegionEcologyDistance(Region left, Region right)
    {
        RegionEcologyProfile leftProfile = left.EffectiveEcologyProfile;
        RegionEcologyProfile rightProfile = right.EffectiveEcologyProfile;
        return Math.Clamp(
            (Math.Abs(leftProfile.Temperature - rightProfile.Temperature) * 0.24)
            + (Math.Abs(leftProfile.Moisture - rightProfile.Moisture) * 0.24)
            + (Math.Abs(left.Fertility - right.Fertility) * 0.20)
            + (Math.Abs(left.WaterAvailability - right.WaterAvailability) * 0.20)
            + (Math.Abs(leftProfile.TerrainHarshness - rightProfile.TerrainHarshness) * 0.12),
            0.0,
            1.0);
    }

    private static string BuildAdaptationPressureSummary(RegionSpeciesPopulation population)
    {
        List<(string Name, double Value)> pressures =
        [
            ("climate mismatch", population.HabitatMismatchMutationPressure),
            ("food instability", population.FoodStressMutationPressure),
            ("predation", population.PredationMutationPressure),
            ("crowding", population.CrowdingMutationPressure),
            ("isolation", population.IsolationMutationPressure)
        ];

        return string.Join(", ", pressures
            .Where(entry => entry.Value >= 0.18)
            .OrderByDescending(entry => entry.Value)
            .Take(2)
            .Select(entry => entry.Name));
    }

    private static string BuildLineageHabitatAdaptationSummary(Region region, Species species, RegionSpeciesPopulation population)
    {
        double climateTolerance = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.ClimateTolerance);
        double dietFlexibility = PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.DietFlexibility);
        return $"{region.Biome} fit {population.HabitatSuitability:F2}, climate {climateTolerance:F2}, diet {dietFlexibility:F2}";
    }

    private static string BuildSpeciesTraitSummary(Species species, RegionSpeciesPopulation? population = null)
    {
        double intelligence = population is null ? species.Intelligence : PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Intelligence);
        double sociality = population is null ? species.Cooperation : PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Sociality);
        double endurance = population is null ? PopulationTraitResolver.GetBaselineTrait(species, SpeciesTrait.Endurance) : PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Endurance);
        double size = population is null ? PopulationTraitResolver.GetBaselineTrait(species, SpeciesTrait.Size) : PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Size);
        return $"Int {intelligence:F2}, Soc {sociality:F2}, End {endurance:F2}, Size {size:F2}";
    }

    private bool ShouldEmitMutationEvent(World world, RegionSpeciesPopulation population, MutationTier tier, double impact)
    {
        if (tier == MutationTier.Major)
        {
            return population.LastMajorMutationEventYear < 0
                || world.Time.Year - population.LastMajorMutationEventYear >= 1
                || impact >= 0.26;
        }

        return impact >= 0.10
            && (population.LastMinorMutationEventYear < 0
                || world.Time.Year - population.LastMinorMutationEventYear >= _settings.MinorMutationEventCooldownYears);
    }

    private int ResolveIsolationMilestone(int isolationSeasons)
    {
        if (isolationSeasons < _settings.IsolationEventBaseThresholdSeasons)
        {
            return 0;
        }

        return isolationSeasons switch
        {
            >= 60 => 4,
            >= 40 => 3,
            >= 24 => 2,
            _ => 1
        };
    }

    private static int ResolveSpeciesAgeYears(World world, Species species)
        => Math.Max(0, world.Time.Year - species.OriginYear);

    private static int ResolveSpeciationJitterYears(int speciesId, int regionId)
        => Math.Abs((speciesId * 31) + (regionId * 17)) % 7;

    private static SpeciationContext BuildSpeciationContext(World world)
    {
        Dictionary<int, Species> speciesById = world.Species.ToDictionary(species => species.Id);
        Dictionary<int, int> globalPopulationBySpeciesId = [];
        Dictionary<(int RegionId, int RootAncestorSpeciesId), int> sameRootCountsByRegion = [];

        foreach (Region region in world.Regions)
        {
            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations.Where(candidate => candidate.PopulationCount > 0))
            {
                globalPopulationBySpeciesId[population.SpeciesId] =
                    globalPopulationBySpeciesId.GetValueOrDefault(population.SpeciesId) + population.PopulationCount;

                if (speciesById.TryGetValue(population.SpeciesId, out Species? species))
                {
                    (int RegionId, int RootAncestorSpeciesId) key = (region.Id, species.RootAncestorSpeciesId);
                    sameRootCountsByRegion[key] = sameRootCountsByRegion.GetValueOrDefault(key) + 1;
                }
            }
        }

        return new SpeciationContext(speciesById, globalPopulationBySpeciesId, sameRootCountsByRegion);
    }

    private static string BuildDescendantSpeciesName(World world, Region region, Species parent)
    {
        string regionPrefix = $"{region.Name} ";
        string baseName = parent.Name.StartsWith(regionPrefix, StringComparison.Ordinal)
            ? $"{parent.Name} Lineage"
            : $"{region.Name} {parent.Name}";
        if (world.Species.All(species => !string.Equals(species.Name, baseName, StringComparison.Ordinal)))
        {
            return baseName;
        }

        int suffix = 2;
        string candidate = $"{baseName} {suffix}";
        while (world.Species.Any(species => string.Equals(species.Name, candidate, StringComparison.Ordinal)))
        {
            suffix++;
            candidate = $"{baseName} {suffix}";
        }

        return candidate;
    }

    private static double ClampUnit(double value)
        => Math.Clamp(value, 0.02, 1.10);

    private static double DecayAndAdd(double currentValue, double addedValue, double decay)
        => Math.Clamp((currentValue * decay) + addedValue, 0.0, 3.5);

    private enum MutationTier
    {
        None,
        Minor,
        Major
    }

    public sealed record MutationSeasonMetrics(
        int MutationChecks,
        int SpeciationCandidates,
        int SpeciationEvents)
    {
        public static MutationSeasonMetrics Empty { get; } = new(0, 0, 0);
    }

    private sealed record MutationPressureContribution(string Name, string DisplayName, double Value);
    private sealed record TraitChange(SpeciesTrait Trait, double Delta);
    private sealed record MutationPlan(IReadOnlyList<TraitChange> Changes);
    private readonly record struct SpeciationCandidateState(
        bool IsCandidate,
        int SameRootSpeciesInRegion,
        int CrowdingOverSoftCap,
        double RequiredDivergence,
        int RequiredReadiness);

    private sealed class SpeciationContext(
        Dictionary<int, Species> speciesById,
        Dictionary<int, int> globalPopulationBySpeciesId,
        Dictionary<(int RegionId, int RootAncestorSpeciesId), int> sameRootCountsByRegion)
    {
        public Dictionary<int, Species> SpeciesById { get; } = speciesById;

        public int ResolveGlobalPopulation(int speciesId)
            => globalPopulationBySpeciesId.GetValueOrDefault(speciesId);

        public int CountSameRootSpeciesInRegion(int regionId, int rootAncestorSpeciesId)
            => sameRootCountsByRegion.GetValueOrDefault((regionId, rootAncestorSpeciesId));

        public void RegisterNewSpecies(Species species, int regionId, int populationCount)
        {
            SpeciesById[species.Id] = species;
            globalPopulationBySpeciesId[species.Id] = populationCount;
            (int RegionId, int RootAncestorSpeciesId) key = (regionId, species.RootAncestorSpeciesId);
            sameRootCountsByRegion[key] = sameRootCountsByRegion.GetValueOrDefault(key) + 1;
        }
    }
}
