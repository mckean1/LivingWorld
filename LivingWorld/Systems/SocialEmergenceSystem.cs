using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Generation;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class SocialEmergenceSystem
{
    private readonly Random _random;
    private readonly WorldGenerationSettings _settings;

    public SocialEmergenceSystem(int seed, WorldGenerationSettings settings)
    {
        _random = new Random(seed);
        _settings = settings;
    }

    public void UpdateYear(World world)
    {
        ActivateSentientGroups(world);
        UpdateGroups(world);
        UpdateSocieties(world);
        UpdatePolities(world);
        RefreshFocalCandidates(world);
        world.PhaseCReadinessReport = PhaseCReadinessEvaluator.Evaluate(world, _settings);
    }

    private void ActivateSentientGroups(World world)
    {
        foreach (Species species in world.Species
                     .Where(candidate => candidate.SentienceCapability == SentienceCapabilityState.Capable && !candidate.IsGloballyExtinct))
        {
            List<int> occupiedRegions = GetActiveLineageRegions(world, species.LineageId);
            int availableSlots = Math.Max(0, _settings.SentientActivationMaximumIndependentGroupsPerLineage - occupiedRegions.Count);
            if (availableSlots == 0)
            {
                continue;
            }

            List<RegionSpeciesPopulation> activationCandidates = world.Regions
                .Select(region => region.GetSpeciesPopulation(species.Id))
                .Where(candidate => candidate is not null && candidate.PopulationCount >= _settings.SentientActivationMinimumPopulation)
                .Cast<RegionSpeciesPopulation>()
                .Where(candidate =>
                    ResolveRegionalSupport(world, species, world.Regions[candidate.RegionId], candidate) >= _settings.SentientActivationMinimumSupport)
                .OrderByDescending(candidate => ScoreActivationCandidate(world, species, candidate))
                .ThenByDescending(candidate => candidate.DivergenceScore)
                .ThenBy(candidate => candidate.RegionId)
                .ToList();

            foreach (RegionSpeciesPopulation population in activationCandidates)
            {
                if (availableSlots == 0)
                {
                    break;
                }

                if (!CanActivateIndependentTrajectory(world, population.RegionId, population, occupiedRegions))
                {
                    continue;
                }

                Region region = world.Regions[population.RegionId];
                double support = ResolveRegionalSupport(world, species, region, population);
                SentientPopulationGroup group = CreateSentientGroup(world, species, region, population, support);
                world.SentientGroups.Add(group);
                species.IsSapient = true;
                occupiedRegions.Add(region.Id);
                availableSlots--;

                world.AddCivilizationalHistoryEvent(
                    CivilizationalHistoryEventType.SentientActivation,
                    species.LineageId,
                    region.Id,
                    $"{species.Name} activated a sentient group in {region.Name}",
                    "sentience_capable_population",
                    groupId: group.Id,
                    data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["population"] = group.PopulationCount.ToString(),
                        ["cohesion"] = group.Cohesion.ToString("F2"),
                        ["support"] = support.ToString("F2"),
                        ["foodSecurity"] = group.FoodSecurity.ToString("F2")
                    });
                world.AddEvent(
                    WorldEventType.SentientPopulationActivated,
                    WorldEventSeverity.Notable,
                    $"{species.Name} formed a sentient band in {region.Name}",
                    $"{species.Name} established an enduring sentient group in {region.Name}.",
                    reason: "sentient_activation",
                    scope: WorldEventScope.Regional,
                    speciesId: species.Id,
                    speciesName: species.Name,
                    regionId: region.Id,
                    regionName: region.Name,
                    metadata: new Dictionary<string, string>
                    {
                        ["groupId"] = group.Id.ToString(),
                        ["lineageId"] = species.LineageId.ToString()
                    });
            }
        }
    }

    private SentientPopulationGroup CreateSentientGroup(
        World world,
        Species species,
        Region region,
        RegionSpeciesPopulation population,
        double support)
    {
        int initialPopulation = Math.Clamp(
            (int)Math.Round(population.PopulationCount * (0.28 + (support * 0.20))),
            42,
            150);

        SentientPopulationGroup group = new(world.SentientGroups.Count == 0 ? 1 : world.SentientGroups.Max(candidate => candidate.Id) + 1)
        {
            SourceLineageId = species.LineageId,
            CurrentRegionId = region.Id,
            FounderRegionId = region.Id,
            ActivationYear = world.Time.Year,
            PopulationCount = initialPopulation,
            MobilityMode = ResolveMobilityMode(region, population),
            Cohesion = 0.34 + (PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Sociality) * 0.35),
            SocialComplexity = 0.18 + (PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Intelligence) * 0.28),
            SurvivalKnowledge = 0.16 + (support * 0.10),
            SettlementIntent = Math.Clamp(region.Fertility * 0.12, 0.0, 1.0),
            Stress = Math.Clamp(0.36 - (support * 0.20), 0.05, 0.40),
            SedentismPressure = Math.Clamp(region.EffectiveEcologyProfile.BasePrimaryProductivity * 0.22, 0.0, 1.0),
            ContinuityYears = 0,
            IdentityStrength = 0.18 + (population.DivergenceScore * 0.06),
            MigrationPattern = region.ConnectedRegionIds.Count >= 3 ? "seasonal circuit" : "anchored foraging",
            FoundingMemorySeed = $"{region.Name} awakening",
            ThreatMemorySeed = population.AdaptationPressureSummary,
            PressureSummary = "activation",
            FoodSecurity = Math.Clamp((support * 0.56) + (region.Fertility * 0.14) + (region.WaterAvailability * 0.14), 0.0, 1.0),
            StorageSupport = 0.10,
            LocalCarryingSupport = ResolveLocalCarryingSupport(initialPopulation, population.PopulationCount, region, 0.10, 0, false),
            MigrationPressure = 0.0,
            FragmentationPressure = 0.0
        };

        group.IdentityMarkers.Add(region.Biome.ToString());
        group.IdentityMarkers.Add(species.EcologyNiche);
        SeedRegionalDiscoveries(world, region, species, group.SharedKnowledge);
        return group;
    }

    private void UpdateGroups(World world)
    {
        foreach (SentientPopulationGroup group in world.SentientGroups.Where(candidate => !candidate.IsCollapsed).ToList())
        {
            EvolutionaryLineage? lineage = world.GetLineage(group.SourceLineageId);
            Species? species = lineage is null ? null : world.Species.FirstOrDefault(candidate => candidate.LineageId == lineage.Id);
            if (lineage is null || species is null)
            {
                group.IsCollapsed = true;
                continue;
            }

            Region region = world.Regions[group.CurrentRegionId];
            RegionSpeciesPopulation? population = region.GetSpeciesPopulation(species.Id);
            double support = population is null ? 0.0 : ResolveRegionalSupport(world, species, region, population);
            SocialPressureSnapshot snapshot = EvaluateGroupSnapshot(group, region, population, support);
            ApplyGroupSnapshot(group, snapshot, region, support);
            SeedRegionalDiscoveries(world, region, species, group.SharedKnowledge);

            group.PopulationCount = Math.Max(0, group.PopulationCount + ResolvePopulationDelta(group.PopulationCount, snapshot, growthScale: 1.0, declineScale: 1.0));
            group.ContinuityYears++;

            if (group.Stress >= 0.92 || group.PopulationCount <= 14)
            {
                CollapseGroup(world, group, "terminal_group_stress");
                continue;
            }

            if (group.MigrationPressure >= 0.62)
            {
                TryMigrateGroup(world, species, group);
            }

            if (group.FragmentationPressure >= 0.66 && group.PopulationCount >= 110 && group.Cohesion < 0.52)
            {
                SplitGroup(world, group);
            }

            if (CanFormSociety(group, support))
            {
                FormSociety(world, species, group, region);
            }
            else if (group.ContinuityYears >= _settings.PersistentGroupContinuityYears
                     && group.Cohesion >= _settings.PersistentGroupCohesionThreshold
                     && group.PopulationCount >= 48
                     && !world.CivilizationalHistory.Any(evt =>
                         evt.Type == CivilizationalHistoryEventType.PersistentGroupFormation
                         && evt.GroupId == group.Id))
            {
                world.AddCivilizationalHistoryEvent(
                    CivilizationalHistoryEventType.PersistentGroupFormation,
                    group.SourceLineageId,
                    group.CurrentRegionId,
                    $"Group {group.Id} persisted together in {region.Name}",
                    "persistent_group",
                    groupId: group.Id,
                    data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["continuityYears"] = group.ContinuityYears.ToString(),
                        ["cohesion"] = group.Cohesion.ToString("F2"),
                        ["population"] = group.PopulationCount.ToString()
                    });
            }
        }
    }

    private SocialPressureSnapshot EvaluateGroupSnapshot(
        SentientPopulationGroup group,
        Region region,
        RegionSpeciesPopulation? population,
        double support)
    {
        double storageSupport = Math.Clamp(
            (group.SurvivalKnowledge * 0.26)
            + (group.SettlementIntent * 0.14)
            + (group.MobilityMode == MobilityMode.SemiSedentary ? 0.10 : 0.0),
            0.0,
            1.0);
        double localCarryingSupport = ResolveLocalCarryingSupport(
            group.PopulationCount,
            population?.PopulationCount ?? group.PopulationCount,
            region,
            storageSupport,
            0,
            group.MobilityMode == MobilityMode.SemiSedentary);
        double foodSecurity = Math.Clamp(
            (support * 0.46)
            + (localCarryingSupport * 0.18)
            + (storageSupport * 0.12)
            + Math.Min(0.16, group.SharedKnowledge.Count * 0.02)
            + (group.SurvivalKnowledge * 0.14)
            - (group.Stress * 0.16),
            0.0,
            1.0);
        double migrationPressure = Math.Clamp(
            ((1.0 - support) * 0.42)
            + Math.Max(0.0, 0.50 - foodSecurity) * 0.24
            + Math.Max(0.0, 0.42 - group.Cohesion) * 0.18
            - (storageSupport * 0.12),
            0.0,
            1.0);
        double fragmentationPressure = Math.Clamp(
            (Math.Max(0.0, (group.PopulationCount - 110) / 140.0) * 0.42)
            + Math.Max(0.0, 0.48 - group.Cohesion) * 0.30
            + (migrationPressure * 0.20)
            - (group.IdentityStrength * 0.10),
            0.0,
            1.0);
        double stress = Math.Clamp(
            ((1.0 - foodSecurity) * 0.56)
            + (migrationPressure * 0.24)
            + (fragmentationPressure * 0.20),
            0.0,
            1.0);

        return new SocialPressureSnapshot(
            support,
            foodSecurity,
            storageSupport,
            group.SedentismPressure,
            localCarryingSupport,
            migrationPressure,
            fragmentationPressure,
            stress,
            ResolveGrowthPressure(
                foodSecurity,
                storageSupport,
                localCarryingSupport,
                group.Cohesion,
                group.SocialComplexity,
                Math.Min(1.0, group.ContinuityYears / 10.0),
                group.SedentismPressure,
                migrationPressure,
                fragmentationPressure,
                stress,
                0.02));
    }

    private void ApplyGroupSnapshot(SentientPopulationGroup group, SocialPressureSnapshot snapshot, Region region, double support)
    {
        group.Cohesion = Math.Clamp(group.Cohesion + (snapshot.FoodSecurity * 0.05) + (group.SharedKnowledge.Count * 0.005) - (snapshot.Stress * 0.06), 0.0, 1.0);
        group.SocialComplexity = Math.Clamp(group.SocialComplexity + 0.03 + (group.Cohesion * 0.03) + (snapshot.GrowthPressure * 0.02) - (snapshot.Stress * 0.02), 0.0, 1.0);
        group.SurvivalKnowledge = Math.Clamp(group.SurvivalKnowledge + 0.04 + (support * 0.04) - (snapshot.Stress * 0.03), 0.0, 1.0);
        group.SedentismPressure = Math.Clamp(group.SedentismPressure + (region.EffectiveEcologyProfile.BasePrimaryProductivity * 0.08) + (snapshot.StorageSupport * 0.06) - (snapshot.MigrationPressure * 0.04), 0.0, 1.0);
        group.SettlementIntent = Math.Clamp(group.SettlementIntent + (group.SedentismPressure * 0.06) + (snapshot.StorageSupport * 0.04) - (snapshot.MigrationPressure * 0.04), 0.0, 1.0);
        group.IdentityStrength = Math.Clamp(group.IdentityStrength + 0.03 + (group.Cohesion * 0.03) - (snapshot.FragmentationPressure * 0.04), 0.0, 1.0);
        group.Stress = snapshot.Stress;
        group.FoodSecurity = snapshot.FoodSecurity;
        group.StorageSupport = snapshot.StorageSupport;
        group.LocalCarryingSupport = snapshot.LocalCarryingSupport;
        group.MigrationPressure = snapshot.MigrationPressure;
        group.FragmentationPressure = snapshot.FragmentationPressure;
        group.PressureSummary = ResolvePressureSummary(
            snapshot.Stress,
            support,
            snapshot.FoodSecurity,
            snapshot.MigrationPressure,
            snapshot.FragmentationPressure,
            snapshot.SettlementSupport);
    }

    private bool CanFormSociety(SentientPopulationGroup group, double support)
        => group.PopulationCount >= 54
           && group.ContinuityYears >= _settings.SocietyFormationContinuityYears
           && group.Cohesion >= _settings.PersistentGroupCohesionThreshold
           && group.IdentityStrength >= _settings.SocietyFormationIdentityThreshold
           && group.SharedKnowledge.Count >= 3
           && support >= _settings.SentientActivationMinimumSupport
           && group.FoodSecurity >= 0.46;

    private void FormSociety(World world, Species species, SentientPopulationGroup group, Region region)
    {
        EmergingSociety society = new(world.Societies.Count == 0 ? 1 : world.Societies.Max(candidate => candidate.Id) + 1)
        {
            LineageId = group.SourceLineageId,
            SpeciesId = species.Id,
            OriginRegionId = region.Id,
            FoundingYear = world.Time.Year,
            Population = group.PopulationCount,
            MobilityMode = group.MobilityMode,
            SubsistenceMode = ResolveInitialSubsistenceMode(species, group, region),
            Cohesion = group.Cohesion,
            IdentityStrength = group.IdentityStrength,
            SocialComplexity = group.SocialComplexity,
            SurvivalKnowledge = group.SurvivalKnowledge,
            SedentismPressure = group.SedentismPressure,
            PressureSummary = group.PressureSummary,
            ContinuityYears = group.ContinuityYears,
            PredecessorGroupId = group.Id,
            FoundingMemorySeed = group.FoundingMemorySeed,
            ThreatMemorySeed = group.ThreatMemorySeed,
            IsFallbackCreated = group.IsFallbackCreated,
            FoodSecurity = group.FoodSecurity,
            StorageSupport = group.StorageSupport,
            SettlementSupport = group.SedentismPressure,
            LocalCarryingSupport = group.LocalCarryingSupport,
            MigrationPressure = group.MigrationPressure,
            FragmentationPressure = group.FragmentationPressure
        };
        society.RegionIds.Add(region.Id);
        foreach (string marker in group.IdentityMarkers)
        {
            society.IdentityMarkers.Add(marker);
        }

        foreach ((string key, CulturalDiscovery discovery) in group.SharedKnowledge)
        {
            society.CulturalKnowledge[key] = discovery;
        }

        world.Societies.Add(society);
        group.IsCollapsed = true;
        world.AddCivilizationalHistoryEvent(
            CivilizationalHistoryEventType.SocietyFormation,
            society.LineageId,
            region.Id,
            $"Society {society.Id} formed in {region.Name}",
            "society_formation",
            groupId: group.Id,
            societyId: society.Id,
            data: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["population"] = society.Population.ToString(),
                ["knowledge"] = society.CulturalKnowledge.Count.ToString()
            });
        world.AddEvent(
            WorldEventType.SocietyFormed,
            WorldEventSeverity.Notable,
            $"{region.Name} saw the rise of a durable society",
            $"{species.Name} formed a durable society in {region.Name}.",
            reason: "society_formation",
            scope: WorldEventScope.Regional,
            speciesId: species.Id,
            speciesName: species.Name,
            regionId: region.Id,
            regionName: region.Name,
            metadata: new Dictionary<string, string>
            {
                ["societyId"] = society.Id.ToString(),
                ["lineageId"] = society.LineageId.ToString()
            });
    }

    private void UpdateSocieties(World world)
    {
        foreach (EmergingSociety society in world.Societies.Where(candidate => !candidate.IsCollapsed).ToList())
        {
            Species? species = world.Species.FirstOrDefault(candidate => candidate.Id == society.SpeciesId);
            if (species is null)
            {
                society.IsCollapsed = true;
                continue;
            }

            Region primaryRegion = ResolvePrimarySocietyRegion(world, society, species, out RegionSpeciesPopulation? primaryPopulation, out double primarySupport);
            List<SocialSettlement> activeSettlements = world.SocialSettlements
                .Where(settlement => settlement.FounderSocietyId == society.Id && !settlement.IsAbandoned)
                .OrderBy(settlement => settlement.Id)
                .ToList();
            SocialPressureSnapshot snapshot = EvaluateSocietySnapshot(society, primaryRegion, primaryPopulation, primarySupport, activeSettlements);
            ApplySocietySnapshot(society, snapshot, primaryRegion, primarySupport);
            society.Population = Math.Max(0, society.Population + ResolvePopulationDelta(society.Population, snapshot, growthScale: 0.92, declineScale: 0.88));
            society.ContinuityYears++;
            society.SubsistenceMode = ResolveSubsistenceMode(society, species, primaryRegion);
            SeedRegionalDiscoveries(world, primaryRegion, species, society.CulturalKnowledge);

            UpdateSocietySettlements(world, species, society, activeSettlements);

            if (ShouldFoundSettlement(society, activeSettlements))
            {
                FoundSettlement(world, society, primaryRegion);
                activeSettlements = world.SocialSettlements
                    .Where(settlement => settlement.FounderSocietyId == society.Id && !settlement.IsAbandoned)
                    .OrderBy(settlement => settlement.Id)
                    .ToList();
            }

            if (CanFormPolity(world, society, activeSettlements))
            {
                FormPolity(world, society, species);
            }

            if (society.FragmentationPressure >= 0.70 && society.Population >= 210 && society.Cohesion < 0.48)
            {
                FragmentSociety(world, society, primaryRegion);
            }

            if (society.Cohesion < 0.16 || snapshot.Stress >= 0.94 || society.Population < 28)
            {
                society.IsCollapsed = true;
                world.AddCivilizationalHistoryEvent(
                    CivilizationalHistoryEventType.Collapse,
                    society.LineageId,
                    primaryRegion.Id,
                    $"Society {society.Id} collapsed in {primaryRegion.Name}",
                    "society_collapse",
                    societyId: society.Id);
            }
        }
    }

    private SocialPressureSnapshot EvaluateSocietySnapshot(
        EmergingSociety society,
        Region primaryRegion,
        RegionSpeciesPopulation? primaryPopulation,
        double primarySupport,
        IReadOnlyList<SocialSettlement> activeSettlements)
    {
        double latentSettlementSupport = Math.Clamp(
            (society.SedentismPressure * 0.42)
            + (primarySupport * 0.30)
            + (primaryRegion.Fertility * 0.18)
            + (primaryRegion.WaterAvailability * 0.10),
            0.18,
            1.0);
        double settlementSupport = activeSettlements.Count == 0
            ? latentSettlementSupport
            : activeSettlements.Average(settlement => settlement.SettlementViability);
        double storageSupport = activeSettlements.Count == 0
            ? Math.Clamp(society.SurvivalKnowledge * 0.25, 0.0, 1.0)
            : Math.Clamp(
                (activeSettlements.Average(settlement => settlement.StorageLevel) * 0.72)
                + (society.SurvivalKnowledge * 0.18),
                0.0,
                1.0);
        double localCarryingSupport = ResolveLocalCarryingSupport(
            society.Population,
            primaryPopulation?.PopulationCount ?? society.Population,
            primaryRegion,
            storageSupport,
            activeSettlements.Count,
            activeSettlements.Count > 0);
        double subsistenceBonus = ResolveSubsistenceBonus(society.SubsistenceMode);
        double foodSecurity = Math.Clamp(
            (primarySupport * 0.28)
            + (settlementSupport * 0.20)
            + (storageSupport * 0.16)
            + (localCarryingSupport * 0.12)
            + (society.SurvivalKnowledge * 0.10)
            + subsistenceBonus
            - (society.MigrationPressure * 0.06),
            0.0,
            1.0);
        double migrationPressure = Math.Clamp(
            ((1.0 - foodSecurity) * 0.28)
            + Math.Max(0.0, 0.46 - settlementSupport) * 0.18
            + Math.Max(0.0, 0.44 - localCarryingSupport) * 0.18
            - (activeSettlements.Count > 0 ? 0.08 : 0.0),
            0.0,
            1.0);
        double fragmentationPressure = Math.Clamp(
            (Math.Max(0.0, (society.Population - 180) / 220.0) * 0.34)
            + Math.Max(0.0, 0.50 - society.Cohesion) * 0.30
            + (migrationPressure * 0.20)
            - (society.IdentityStrength * 0.10),
            0.0,
            1.0);
        double stress = Math.Clamp(
            ((1.0 - foodSecurity) * 0.58)
            + (migrationPressure * 0.20)
            + (fragmentationPressure * 0.22),
            0.0,
            1.0);

        return new SocialPressureSnapshot(
            primarySupport,
            foodSecurity,
            storageSupport,
            settlementSupport,
            localCarryingSupport,
            migrationPressure,
            fragmentationPressure,
            stress,
            ResolveGrowthPressure(
                foodSecurity,
                storageSupport,
                localCarryingSupport,
                society.Cohesion,
                society.SocialComplexity,
                Math.Min(1.0, society.ContinuityYears / 14.0),
                settlementSupport,
                migrationPressure,
                fragmentationPressure,
                stress,
                subsistenceBonus));
    }

    private void ApplySocietySnapshot(EmergingSociety society, SocialPressureSnapshot snapshot, Region primaryRegion, double primarySupport)
    {
        society.Cohesion = Math.Clamp(society.Cohesion + (snapshot.FoodSecurity * 0.04) + (snapshot.SettlementSupport * 0.03) - (snapshot.Stress * 0.05), 0.0, 1.0);
        society.SocialComplexity = Math.Clamp(society.SocialComplexity + 0.03 + (snapshot.SettlementSupport * 0.03) + (society.CulturalKnowledge.Count * 0.005), 0.0, 1.0);
        society.SurvivalKnowledge = Math.Clamp(society.SurvivalKnowledge + 0.04 + (primarySupport * 0.03), 0.0, 1.0);
        society.SedentismPressure = Math.Clamp(society.SedentismPressure + (primaryRegion.Fertility * 0.06) + (snapshot.StorageSupport * 0.06) - (snapshot.MigrationPressure * 0.03), 0.0, 1.0);
        society.IdentityStrength = Math.Clamp(society.IdentityStrength + 0.03 + (society.ContinuityYears * 0.005) - (snapshot.FragmentationPressure * 0.04), 0.0, 1.0);
        society.FoodSecurity = snapshot.FoodSecurity;
        society.StorageSupport = snapshot.StorageSupport;
        society.SettlementSupport = snapshot.SettlementSupport;
        society.LocalCarryingSupport = snapshot.LocalCarryingSupport;
        society.MigrationPressure = snapshot.MigrationPressure;
        society.FragmentationPressure = snapshot.FragmentationPressure;
        society.PressureSummary = ResolvePressureSummary(
            snapshot.Stress,
            primarySupport,
            snapshot.FoodSecurity,
            snapshot.MigrationPressure,
            snapshot.FragmentationPressure,
            snapshot.SettlementSupport);
    }

    private void UpdateSocietySettlements(World world, Species species, EmergingSociety society, List<SocialSettlement> activeSettlements)
    {
        if (activeSettlements.Count == 0)
        {
            return;
        }

        double totalWeight = activeSettlements.Sum(settlement =>
            1.0 + Math.Min(0.25, (world.Time.Year - settlement.FoundingYear) / 12.0));
        int settledPopulationTarget = Math.Max(activeSettlements.Count * 16, (int)Math.Round(society.Population * Math.Clamp(0.48 + (society.SettlementSupport * 0.22), 0.48, 0.82)));

        foreach (SocialSettlement settlement in activeSettlements)
        {
            Region region = world.Regions[settlement.RegionId];
            RegionSpeciesPopulation? population = region.GetSpeciesPopulation(species.Id);
            double support = population is null ? 0.0 : ResolveRegionalSupport(world, species, region, population);
            double weight = 1.0 + Math.Min(0.25, (world.Time.Year - settlement.FoundingYear) / 12.0);
            settlement.Population = Math.Max(14, (int)Math.Round(settledPopulationTarget * (weight / Math.Max(0.01, totalWeight))));
            settlement.LocalCarryingSupport = ResolveLocalCarryingSupport(
                settlement.Population,
                population?.PopulationCount ?? settlement.Population,
                region,
                settlement.StorageLevel,
                1,
                true);
            settlement.FoodSecurity = Math.Clamp(
                (support * 0.34)
                + (society.StorageSupport * 0.20)
                + (settlement.StorageLevel * 0.18)
                + (settlement.SettlementViability * 0.16)
                + ResolveSubsistenceBonus(society.SubsistenceMode)
                - (society.MigrationPressure * 0.10),
                0.0,
                1.0);
            settlement.StorageLevel = Math.Clamp(
                settlement.StorageLevel
                + (settlement.FoodSecurity * 0.08)
                + (society.StorageSupport * 0.06)
                - (society.MigrationPressure * 0.05),
                0.0,
                1.0);
            settlement.SettlementViability = Math.Clamp(
                settlement.SettlementViability
                + (settlement.FoodSecurity * 0.10)
                + (settlement.LocalCarryingSupport * 0.08)
                - (society.FragmentationPressure * 0.08)
                - (society.MigrationPressure * 0.06),
                0.0,
                1.0);
            settlement.Stress = Math.Clamp(
                ((1.0 - settlement.FoodSecurity) * 0.58)
                + (society.FragmentationPressure * 0.24)
                + (society.MigrationPressure * 0.18),
                0.0,
                1.0);
            settlement.CurrentPressureSummary = society.PressureSummary;

            foreach ((string key, CulturalDiscovery discovery) in society.CulturalKnowledge)
            {
                settlement.LocalKnowledge.TryAdd(key, discovery);
            }

            if (settlement.SettlementViability < 0.24 || settlement.Population < 14)
            {
                settlement.IsAbandoned = true;
                world.AddCivilizationalHistoryEvent(
                    CivilizationalHistoryEventType.SettlementAbandoned,
                    society.LineageId,
                    settlement.RegionId,
                    $"Settlement {settlement.Id} was abandoned",
                    "settlement_failure",
                    societyId: society.Id,
                    settlementId: settlement.Id);
                world.AddEvent(
                    WorldEventType.EmergentSettlementAbandoned,
                    WorldEventSeverity.Notable,
                    $"A settlement in {world.Regions[settlement.RegionId].Name} was abandoned",
                    $"Society {society.Id} abandoned a settlement in {world.Regions[settlement.RegionId].Name}.",
                    reason: "settlement_failure",
                    scope: WorldEventScope.Regional,
                    speciesId: society.SpeciesId,
                    speciesName: species.Name,
                    regionId: settlement.RegionId,
                    regionName: world.Regions[settlement.RegionId].Name);
            }
        }
    }

    private bool ShouldFoundSettlement(EmergingSociety society, IReadOnlyCollection<SocialSettlement> activeSettlements)
        => activeSettlements.Count == 0
           && society.SedentismPressure >= _settings.SettlementFoundingPressureThreshold
           && society.CulturalKnowledge.Count >= _settings.SettlementIntentReturnThreshold
           && (society.FoodSecurity >= 0.40 || society.SettlementSupport >= 0.48)
           && society.StorageSupport >= 0.20
           && society.LocalCarryingSupport >= 0.46
           && society.Population >= 70;

    private void FoundSettlement(World world, EmergingSociety society, Region region)
    {
        SocialSettlement settlement = new(world.SocialSettlements.Count == 0 ? 1 : world.SocialSettlements.Max(candidate => candidate.Id) + 1)
        {
            FounderSocietyId = society.Id,
            FounderLineageId = society.LineageId,
            RegionId = region.Id,
            FoundingYear = world.Time.Year,
            Population = Math.Max(36, society.Population / 3),
            FoodBaseProfile = region.Fertility >= 0.72 ? "stable fertile basin" : "mixed wild foods",
            StorageLevel = Math.Clamp((society.StorageSupport * 0.65) + (society.SurvivalKnowledge * 0.16), 0.0, 1.0),
            SettlementViability = Math.Clamp((region.Fertility * 0.32) + (region.WaterAvailability * 0.28) + (society.FoodSecurity * 0.16) + (society.SurvivalKnowledge * 0.18), 0.0, 1.0),
            CurrentPressureSummary = society.PressureSummary,
            IsFallbackCreated = society.IsFallbackCreated,
            FoodSecurity = society.FoodSecurity,
            LocalCarryingSupport = society.LocalCarryingSupport,
            Stress = society.FoodSecurity < 0.35 ? 0.34 : 0.18
        };
        foreach ((string key, CulturalDiscovery discovery) in society.CulturalKnowledge)
        {
            settlement.LocalKnowledge[key] = discovery;
        }

        world.SocialSettlements.Add(settlement);
        society.SettlementIds.Add(settlement.Id);
        society.MobilityMode = MobilityMode.SemiSedentary;
        society.SettlementSupport = Math.Max(society.SettlementSupport, settlement.SettlementViability);
        world.AddCivilizationalHistoryEvent(
            CivilizationalHistoryEventType.SettlementFounded,
            society.LineageId,
            region.Id,
            $"Society {society.Id} founded a settlement in {region.Name}",
            "settlement_pressure",
            societyId: society.Id,
            settlementId: settlement.Id);
        world.AddEvent(
            WorldEventType.EmergentSettlementFounded,
            WorldEventSeverity.Notable,
            $"{region.Name} gained an enduring settlement",
            $"Society {society.Id} founded a settlement in {region.Name}.",
            reason: "settlement_pressure",
            scope: WorldEventScope.Regional,
            speciesId: society.SpeciesId,
            regionId: region.Id,
            regionName: region.Name,
            metadata: new Dictionary<string, string>
            {
                ["societyId"] = society.Id.ToString(),
                ["settlementId"] = settlement.Id.ToString()
            });
    }

    private bool CanFormPolity(World world, EmergingSociety society, IReadOnlyCollection<SocialSettlement> activeSettlements)
        => society.Population >= _settings.PolityFormationMinimumPopulation
           && society.CulturalKnowledge.Count >= _settings.PolityFormationMinimumKnowledgeCount
           && society.SocialComplexity >= _settings.PolityFormationComplexityThreshold
           && society.Cohesion >= 0.45
           && society.FoodSecurity >= 0.50
           && society.StorageSupport >= 0.26
           && activeSettlements.Any(settlement => settlement.SettlementViability >= 0.55 && settlement.StorageLevel >= 0.24)
           && !world.Polities.Any(polity => polity.FounderSocietyId == society.Id && polity.Population > 0);

    private SubsistenceMode ResolveInitialSubsistenceMode(Species species, SentientPopulationGroup group, Region region)
        => ResolveSubsistenceMode(
            species,
            region,
            plantKnowledge: group.SharedKnowledge.Count(entry => entry.Key.Contains(":edible", StringComparison.Ordinal)),
            preyKnowledge: group.SharedKnowledge.Count(entry => entry.Key.Contains(":prey", StringComparison.Ordinal)),
            survivalKnowledge: group.SurvivalKnowledge,
            settled: false,
            storageSupport: group.StorageSupport,
            sedentismPressure: group.SedentismPressure,
            settlementSupport: group.SettlementIntent);

    private SubsistenceMode ResolveSubsistenceMode(EmergingSociety society, Species species, Region region)
    {
        return ResolveSubsistenceMode(
            species,
            region,
            plantKnowledge: society.CulturalKnowledge.Count(entry => entry.Key.Contains(":edible", StringComparison.Ordinal)),
            preyKnowledge: society.CulturalKnowledge.Count(entry => entry.Key.Contains(":prey", StringComparison.Ordinal)),
            survivalKnowledge: society.SurvivalKnowledge,
            settled: society.SettlementIds.Count > 0,
            storageSupport: society.StorageSupport,
            sedentismPressure: society.SedentismPressure,
            settlementSupport: society.SettlementSupport);
    }

    private static SubsistenceMode ResolveSubsistenceMode(
        Species species,
        Region region,
        int plantKnowledge,
        int preyKnowledge,
        double survivalKnowledge,
        bool settled,
        double storageSupport,
        double sedentismPressure,
        double settlementSupport)
    {
        double plantOpportunity = ResolvePlantOpportunity(species, region, plantKnowledge, survivalKnowledge, storageSupport);
        double preyOpportunity = ResolvePreyOpportunity(species, region, preyKnowledge, survivalKnowledge, sedentismPressure);
        double cultivationOpportunity = ResolveCultivationOpportunity(species, region, plantKnowledge, survivalKnowledge, settled, storageSupport, settlementSupport);

        if (cultivationOpportunity >= 0.80 && plantKnowledge >= 2 && settled)
        {
            return cultivationOpportunity >= 0.94 && survivalKnowledge >= 0.70
                ? SubsistenceMode.FarmingEmergent
                : SubsistenceMode.ProtoFarming;
        }

        if (cultivationOpportunity >= 0.68 && plantKnowledge >= 2 && (settled || sedentismPressure >= 0.54))
        {
            return SubsistenceMode.ProtoFarming;
        }

        if (preyOpportunity >= plantOpportunity + 0.14)
        {
            return SubsistenceMode.HuntingFocused;
        }

        if (plantOpportunity >= preyOpportunity + 0.14)
        {
            return SubsistenceMode.ForagingFocused;
        }

        return (plantKnowledge > 0 && preyKnowledge > 0) || Math.Abs(plantOpportunity - preyOpportunity) < 0.12
            ? SubsistenceMode.MixedHunterForager
            : species.AnimalBiomassAffinity > species.PlantBiomassAffinity
                ? SubsistenceMode.HuntingFocused
                : SubsistenceMode.ForagingFocused;
    }

    private static double ResolvePlantOpportunity(Species species, Region region, int plantKnowledge, double survivalKnowledge, double storageSupport)
    {
        double biomassRatio = region.MaxPlantBiomass <= 0
            ? 0.0
            : Math.Clamp(region.PlantBiomass / region.MaxPlantBiomass, 0.0, 1.0);
        return Math.Clamp(
            (biomassRatio * 0.34)
            + (region.Fertility * 0.18)
            + (region.WaterAvailability * 0.12)
            + (species.PlantBiomassAffinity * 0.16)
            + Math.Min(0.18, plantKnowledge * 0.07)
            + (survivalKnowledge * 0.08)
            + (storageSupport * 0.04),
            0.0,
            1.0);
    }

    private static double ResolvePreyOpportunity(Species species, Region region, int preyKnowledge, double survivalKnowledge, double sedentismPressure)
    {
        double biomassRatio = region.MaxAnimalBiomass <= 0
            ? 0.0
            : Math.Clamp(region.AnimalBiomass / region.MaxAnimalBiomass, 0.0, 1.0);
        double openTerrainBonus = region.Biome is RegionBiome.Plains or RegionBiome.Highlands or RegionBiome.Drylands
            ? 0.08
            : 0.0;
        return Math.Clamp(
            (biomassRatio * 0.30)
            + (species.AnimalBiomassAffinity * 0.18)
            + Math.Min(0.20, preyKnowledge * 0.09)
            + (survivalKnowledge * 0.08)
            + openTerrainBonus
            + Math.Max(0.0, 0.52 - sedentismPressure) * 0.10,
            0.0,
            1.0);
    }

    private static double ResolveCultivationOpportunity(
        Species species,
        Region region,
        int plantKnowledge,
        double survivalKnowledge,
        bool settled,
        double storageSupport,
        double settlementSupport)
    {
        double settledBonus = settled ? 0.16 : 0.0;
        return Math.Clamp(
            (region.Fertility * 0.26)
            + (region.WaterAvailability * 0.18)
            + (species.PlantBiomassAffinity * 0.12)
            + Math.Min(0.16, plantKnowledge * 0.06)
            + (survivalKnowledge * 0.10)
            + (storageSupport * 0.08)
            + (settlementSupport * 0.10)
            + settledBonus,
            0.0,
            1.0);
    }

    private void FormPolity(World world, EmergingSociety society, Species species)
    {
        int polityId = world.Polities.Count == 0 ? 1 : world.Polities.Max(candidate => candidate.Id) + 1;
        int homeRegionId = society.RegionIds.OrderBy(id => id).First();
        Region homeRegion = world.Regions[homeRegionId];
        string polityName = BuildPolityName(homeRegion, species, polityId);
        int foundingPopulation = Math.Max(_settings.PolityFormationMinimumPopulation, society.Population);
        Polity polity = new(polityId, polityName, species.Id, homeRegionId, foundingPopulation, lineageId: society.LineageId)
        {
            FounderSocietyId = society.Id,
            ParentPolityId = society.FounderPolityId,
            Stage = society.SettlementIds.Count > 0 ? PolityStage.Tribe : PolityStage.Band,
            SettlementStatus = society.SettlementIds.Count > 0 ? SettlementStatus.SemiSettled : SettlementStatus.Nomadic,
            CurrentPressureSummary = society.PressureSummary,
            IdentitySeed = string.Join(", ", society.IdentityMarkers.Take(3)),
            IsFallbackCreated = society.IsFallbackCreated
        };
        foreach ((_, CulturalDiscovery discovery) in society.CulturalKnowledge)
        {
            polity.AddDiscovery(discovery);
        }

        polity.LearnAdvancement(AdvancementId.Fire);
        polity.LearnAdvancement(AdvancementId.StoneTools);
        if (society.CulturalKnowledge.Count >= 4)
        {
            polity.LearnAdvancement(AdvancementId.SeasonalPlanning);
        }
        if (society.SettlementIds.Count > 0)
        {
            polity.LearnAdvancement(AdvancementId.FoodStorage);
        }
        if (society.SubsistenceMode is SubsistenceMode.ProtoFarming or SubsistenceMode.FarmingEmergent)
        {
            polity.LearnAdvancement(AdvancementId.Agriculture);
        }

        bool primarySettlementCreated = false;
        foreach (SocialSettlement socialSettlement in world.SocialSettlements.Where(settlement => settlement.FounderSocietyId == society.Id && !settlement.IsAbandoned))
        {
            Settlement settlement = primarySettlementCreated
                ? polity.AddSettlement(socialSettlement.RegionId, BuildSettlementName(world.Regions[socialSettlement.RegionId], polity))
                : polity.EstablishFirstSettlement(socialSettlement.RegionId, BuildSettlementName(world.Regions[socialSettlement.RegionId], polity));
            primarySettlementCreated = true;
            settlement.YearsEstablished = Math.Max(0, world.Time.Year - socialSettlement.FoundingYear);
            settlement.FoodStored = socialSettlement.StorageLevel * 100.0;
            settlement.FoodRequired = Math.Max(20.0, foundingPopulation / Math.Max(1, society.SettlementIds.Count));
            settlement.FoodProduced = settlement.FoodRequired * (0.92 + (socialSettlement.SettlementViability * 0.28));
            settlement.CalculateFoodState();
        }

        polity.FoodStores = polity.Settlements.Sum(settlement => settlement.FoodStored);
        world.Polities.Add(polity);
        society.FounderPolityId = polity.Id;
        society.Population = Math.Max(24, (int)Math.Round(society.Population * 0.25));
        society.SettlementSupport = Math.Max(society.SettlementSupport, 0.60);
        world.AddCivilizationalHistoryEvent(
            CivilizationalHistoryEventType.PolityFormation,
            society.LineageId,
            homeRegionId,
            $"{polity.Name} formed from society {society.Id}",
            "polity_formation",
            societyId: society.Id,
            polityId: polity.Id);
        world.AddEvent(
            WorldEventType.PolityFounded,
            WorldEventSeverity.Major,
            $"{polity.Name} emerged in {homeRegion.Name}",
            $"{polity.Name} emerged from a durable society in {homeRegion.Name}.",
            reason: "polity_formation",
            scope: WorldEventScope.Polity,
            polityId: polity.Id,
            polityName: polity.Name,
            speciesId: species.Id,
            speciesName: species.Name,
            regionId: homeRegionId,
            regionName: homeRegion.Name,
            metadata: new Dictionary<string, string>
            {
                ["founderSocietyId"] = society.Id.ToString(),
                ["lineageId"] = society.LineageId.ToString()
            });
    }

    private void UpdatePolities(World world)
    {
        foreach (Polity polity in world.Polities.Where(candidate => candidate.Population > 0).ToList())
        {
            Species? species = world.Species.FirstOrDefault(candidate => candidate.Id == polity.SpeciesId);
            if (species is null)
            {
                polity.Population = 0;
                continue;
            }

            List<SettlementProjection> settlementProjections = BuildSettlementProjections(world, species, polity);
            double averageRegionalSupport = settlementProjections.Count == 0
                ? 0.0
                : settlementProjections.Average(projection => projection.Support);
            double settlementSupport = settlementProjections.Count == 0
                ? 0.0
                : settlementProjections.Average(projection => projection.Viability);
            double storageSupport = settlementProjections.Count == 0
                ? 0.0
                : Math.Clamp(
                    settlementProjections.Average(projection => projection.StorageSupport) * 0.72
                    + (polity.HasAdvancement(AdvancementId.FoodStorage) ? 0.12 : 0.0)
                    + (polity.HasAdvancement(AdvancementId.SeasonalPlanning) ? 0.06 : 0.0),
                    0.0,
                    1.0);
            double localCarryingSupport = settlementProjections.Count == 0
                ? ResolveLocalCarryingSupport(polity.Population, polity.Population, world.Regions[polity.RegionId], storageSupport, 0, false)
                : settlementProjections.Average(projection => projection.LocalCarryingSupport);
            int populationPerSettlement = settlementProjections.Count == 0
                ? polity.Population
                : (int)Math.Round(polity.Population / (double)settlementProjections.Count);
            double foodSecurity = Math.Clamp(
                (averageRegionalSupport * 0.24)
                + (settlementSupport * 0.26)
                + (storageSupport * 0.18)
                + (localCarryingSupport * 0.10)
                + (polity.HasAdvancement(AdvancementId.Agriculture) ? 0.12 : 0.0)
                + (polity.HasAdvancement(AdvancementId.SeasonalPlanning) ? 0.04 : 0.0)
                - (polity.MigrationPressure * 0.08)
                - (polity.FragmentationPressure * 0.10),
                0.0,
                1.0);
            double targetMigrationPressure = Math.Clamp(
                ((1.0 - foodSecurity) * 0.32)
                + Math.Max(0.0, 0.46 - settlementSupport) * 0.22
                + Math.Max(0.0, 0.45 - localCarryingSupport) * 0.16
                - (polity.SettlementStatus == SettlementStatus.Settled ? 0.06 : 0.0),
                0.0,
                1.0);
            double targetFragmentationPressure = Math.Clamp(
                (Math.Max(0.0, (populationPerSettlement - 170) / 220.0) * 0.30)
                + Math.Max(0.0, 0.50 - settlementSupport) * 0.22
                + Math.Max(0.0, 0.48 - foodSecurity) * 0.20
                + (targetMigrationPressure * 0.18)
                - Math.Min(0.12, Math.Max(0, settlementProjections.Count - 1) * 0.04),
                0.0,
                1.0);
            SubsistenceMode politySubsistenceMode = PolityProfileResolver.ResolveSubsistenceMode(polity, species);

            polity.MigrationPressure = Math.Clamp(
                (polity.MigrationPressure * 0.44)
                + targetMigrationPressure
                + polity.EventDrivenMigrationPressureBonus,
                0.0,
                1.0);
            polity.FragmentationPressure = Math.Clamp(
                (polity.FragmentationPressure * 0.44)
                + targetFragmentationPressure
                + polity.EventDrivenFragmentationPressureBonus,
                0.0,
                1.0);

            foreach (SettlementProjection projection in settlementProjections)
            {
                projection.Settlement.FoodRequired = projection.FoodRequired;
                projection.Settlement.FoodProduced = projection.FoodProduced;
                projection.Settlement.FoodStored = projection.FoodStored;
                projection.Settlement.CalculateFoodState();
            }

            polity.FoodStores = settlementProjections.Sum(projection => projection.Settlement.FoodStored);
            double polityStress = Math.Clamp(((1.0 - foodSecurity) * 0.60) + (polity.FragmentationPressure * 0.22) + (polity.MigrationPressure * 0.18), 0.0, 1.0);
            polity.CurrentPressureSummary = ResolvePressureSummary(
                polityStress,
                averageRegionalSupport,
                foodSecurity,
                polity.MigrationPressure,
                polity.FragmentationPressure,
                settlementSupport);

            SocialPressureSnapshot snapshot = new(
                averageRegionalSupport,
                foodSecurity,
                storageSupport,
                settlementSupport,
                localCarryingSupport,
                polity.MigrationPressure,
                polity.FragmentationPressure,
                polityStress,
                ResolveGrowthPressure(
                    foodSecurity,
                    storageSupport,
                    localCarryingSupport,
                    0.58,
                    polity.Advancements.Count / 8.0,
                    Math.Min(1.0, polity.YearsSinceFounded / 18.0),
                    settlementSupport,
                    polity.MigrationPressure,
                    polity.FragmentationPressure,
                    polityStress,
                    ResolveSubsistenceBonus(politySubsistenceMode)));

            polity.Population = Math.Max(0, polity.Population + ResolvePopulationDelta(polity.Population, snapshot, growthScale: 0.72, declineScale: 0.76));

            if (ShouldExpandPolitySettlement(polity, politySubsistenceMode, settlementProjections, averageRegionalSupport, settlementSupport, localCarryingSupport, foodSecurity))
            {
                ExpandPolitySettlement(world, species, polity, politySubsistenceMode);
            }

            if (polity.Population <= 24 || snapshot.Stress >= 0.95)
            {
                CollapsePolity(world, polity);
            }
        }
    }

    private bool ShouldExpandPolitySettlement(
        Polity polity,
        SubsistenceMode subsistenceMode,
        IReadOnlyCollection<SettlementProjection> settlementProjections,
        double averageRegionalSupport,
        double settlementSupport,
        double localCarryingSupport,
        double foodSecurity)
    {
        if (settlementProjections.Count == 0)
        {
            return false;
        }

        double populationPerSettlementTarget = subsistenceMode switch
        {
            SubsistenceMode.FarmingEmergent => 180.0,
            SubsistenceMode.ProtoFarming => 220.0,
            SubsistenceMode.MixedHunterForager => 280.0,
            SubsistenceMode.ForagingFocused => 305.0,
            SubsistenceMode.HuntingFocused => 330.0,
            _ => 260.0
        };
        int desiredSettlementCount = Math.Max(1, (int)Math.Ceiling(polity.Population / populationPerSettlementTarget));
        int populationPerSettlement = polity.SettlementCount == 0
            ? polity.Population
            : (int)Math.Round(polity.Population / (double)polity.SettlementCount);
        int newestSettlementAgeMonths = polity.SettlementCount == 0
            ? int.MaxValue
            : polity.Settlements.Min(settlement => settlement.EstablishedMonths);
        int minimumNewestSettlementAgeMonths = subsistenceMode switch
        {
            SubsistenceMode.FarmingEmergent => 8,
            SubsistenceMode.ProtoFarming => 10,
            SubsistenceMode.MixedHunterForager => 12,
            _ => 14
        };
        double minimumFoodSecurity = subsistenceMode is SubsistenceMode.HuntingFocused or SubsistenceMode.ForagingFocused
            ? 0.60
            : 0.54;
        double maximumFragmentationPressure = subsistenceMode switch
        {
            SubsistenceMode.FarmingEmergent => 0.54,
            SubsistenceMode.ProtoFarming => 0.50,
            SubsistenceMode.MixedHunterForager => 0.46,
            _ => 0.42
        };
        return polity.SettlementCount < desiredSettlementCount
               && polity.YearsSinceFounded >= polity.SettlementCount * 6
               && newestSettlementAgeMonths >= minimumNewestSettlementAgeMonths
               && populationPerSettlement >= 145
               && averageRegionalSupport >= 0.46
               && settlementSupport >= 0.48
               && localCarryingSupport >= 0.52
               && foodSecurity >= minimumFoodSecurity
               && polity.MigrationPressure <= 0.46
               && polity.FragmentationPressure <= maximumFragmentationPressure
               && settlementProjections.Any(projection =>
                   projection.Viability >= 0.58
                   && projection.LocalCarryingSupport >= 0.54);
    }

    private void ExpandPolitySettlement(World world, Species species, Polity polity, SubsistenceMode subsistenceMode)
    {
        Region? targetRegion = SelectPolityExpansionRegion(world, species, polity, subsistenceMode);
        if (targetRegion is null)
        {
            return;
        }

        RegionSpeciesPopulation? regionalPopulation = targetRegion.GetSpeciesPopulation(species.Id);
        double regionalSupport = regionalPopulation is null ? 0.0 : ResolveRegionalSupport(world, species, targetRegion, regionalPopulation);
        string settlementName = targetRegion.Id == polity.RegionId
            ? $"{targetRegion.Name} Hearth {polity.SettlementCount + 1}"
            : BuildSettlementName(targetRegion, polity);
        Settlement settlement = polity.AddSettlement(targetRegion.Id, settlementName);
        settlement.YearsEstablished = 0;
        settlement.FoodRequired = Math.Max(24.0, polity.Population / Math.Max(2.0, polity.SettlementCount));
        settlement.FoodProduced = settlement.FoodRequired * (
            0.86
            + (regionalSupport * 0.34)
            + (polity.HasAdvancement(AdvancementId.SeasonalPlanning) ? 0.08 : 0.0)
            + (polity.HasAdvancement(AdvancementId.FoodStorage) ? 0.06 : 0.0)
            + (polity.HasAdvancement(AdvancementId.Agriculture) ? 0.12 : 0.0));
        settlement.FoodStored = Math.Max(
            12.0,
            (polity.FoodStores / Math.Max(2.0, polity.SettlementCount + 1.0)) * 0.18);
        settlement.CalculateFoodState();
        polity.SettlementStatus = polity.SettlementCount >= 2 ? SettlementStatus.Settled : SettlementStatus.SemiSettled;
        polity.MigrationPressure = Math.Max(0.0, polity.MigrationPressure - 0.08);
        polity.FragmentationPressure = Math.Max(0.0, polity.FragmentationPressure - 0.12);

        world.AddCivilizationalHistoryEvent(
            CivilizationalHistoryEventType.SettlementFounded,
            polity.LineageId,
            targetRegion.Id,
            $"{polity.Name} founded a new settlement in {targetRegion.Name}",
            "polity_settlement_expansion",
            polityId: polity.Id,
            settlementId: settlement.Id);
        world.AddEvent(
            WorldEventType.EmergentSettlementFounded,
            WorldEventSeverity.Notable,
            $"{polity.Name} spread into {targetRegion.Name}",
            $"{polity.Name} founded a new settlement in {targetRegion.Name}.",
            reason: "polity_settlement_expansion",
            scope: WorldEventScope.Regional,
            polityId: polity.Id,
            polityName: polity.Name,
            speciesId: polity.SpeciesId,
            speciesName: species.Name,
            regionId: targetRegion.Id,
            regionName: targetRegion.Name,
            settlementId: settlement.Id,
            settlementName: settlement.Name);
    }

    private Region? SelectPolityExpansionRegion(World world, Species species, Polity polity, SubsistenceMode subsistenceMode)
    {
        HashSet<int> occupiedRegionIds = polity.Settlements.Select(settlement => settlement.RegionId).ToHashSet();
        Region? frontierRegion = polity.Settlements
            .SelectMany(settlement => world.Regions[settlement.RegionId].ConnectedRegionIds)
            .Distinct()
            .Where(regionId => !occupiedRegionIds.Contains(regionId))
            .Select(regionId =>
            {
                Region region = world.Regions[regionId];
                RegionSpeciesPopulation? population = region.GetSpeciesPopulation(species.Id);
                double support = population is null ? 0.0 : ResolveRegionalSupport(world, species, region, population);
                double plantBiomassRatio = region.MaxPlantBiomass <= 0
                    ? 0.0
                    : Math.Clamp(region.PlantBiomass / region.MaxPlantBiomass, 0.0, 1.0);
                double animalBiomassRatio = region.MaxAnimalBiomass <= 0
                    ? 0.0
                    : Math.Clamp(region.AnimalBiomass / region.MaxAnimalBiomass, 0.0, 1.0);
                double subsistenceScore = subsistenceMode switch
                {
                    SubsistenceMode.FarmingEmergent or SubsistenceMode.ProtoFarming =>
                        (region.Fertility * 0.30)
                        + (region.WaterAvailability * 0.22)
                        + (plantBiomassRatio * 0.12),
                    SubsistenceMode.ForagingFocused =>
                        (plantBiomassRatio * 0.18)
                        + (region.Fertility * 0.20)
                        + (region.WaterAvailability * 0.14),
                    SubsistenceMode.HuntingFocused =>
                        (animalBiomassRatio * 0.20)
                        + (region.EffectiveEcologyProfile.MigrationEase * 0.16)
                        + ((region.Biome is RegionBiome.Plains or RegionBiome.Highlands or RegionBiome.Drylands) ? 0.06 : 0.0),
                    _ =>
                        (plantBiomassRatio * 0.10)
                        + (animalBiomassRatio * 0.10)
                        + (region.Fertility * 0.14)
                        + (region.EffectiveEcologyProfile.MigrationEase * 0.08)
                };
                double score = support
                    + subsistenceScore
                    + Math.Min(0.22, (population?.PopulationCount ?? 0) / 220.0);
                return (Region: region, Support: support, Score: score);
            })
            .Where(entry => entry.Support >= 0.44)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Region.Id)
            .Select(entry => entry.Region)
            .FirstOrDefault();
        if (frontierRegion is not null)
        {
            return frontierRegion;
        }

        Region homeRegion = world.Regions[polity.RegionId];
        RegionSpeciesPopulation? homePopulation = homeRegion.GetSpeciesPopulation(species.Id);
        double homeSupport = homePopulation is null ? 0.0 : ResolveRegionalSupport(world, species, homeRegion, homePopulation);
        return homeSupport >= 0.50 ? homeRegion : null;
    }

    private List<SettlementProjection> BuildSettlementProjections(World world, Species species, Polity polity)
    {
        List<Settlement> settlements = polity.Settlements.ToList();
        if (settlements.Count == 0)
        {
            return [];
        }

        List<double> weights = settlements
            .Select(settlement => 1.0 + Math.Min(0.25, settlement.YearsEstablished / 16.0))
            .ToList();
        double totalWeight = weights.Sum();
        List<SettlementProjection> projections = [];

        for (int index = 0; index < settlements.Count; index++)
        {
            Settlement settlement = settlements[index];
            Region region = world.Regions[settlement.RegionId];
            RegionSpeciesPopulation? population = region.GetSpeciesPopulation(species.Id);
            double support = population is null ? 0.0 : ResolveRegionalSupport(world, species, region, population);
            int sharePopulation = Math.Max(14, (int)Math.Round(polity.Population * (weights[index] / Math.Max(0.01, totalWeight))));
            double storageSupport = Math.Clamp(settlement.FoodStored / Math.Max(1.0, sharePopulation), 0.0, 1.0);
            double foodRequired = sharePopulation;
            double foodProduced = foodRequired * (
                0.78
                + (support * 0.44)
                + (storageSupport * 0.08)
                + (polity.HasAdvancement(AdvancementId.SeasonalPlanning) ? 0.08 : 0.0)
                + (polity.HasAdvancement(AdvancementId.FoodStorage) ? 0.06 : 0.0)
                + (polity.HasAdvancement(AdvancementId.Agriculture) ? 0.12 : 0.0));
            double foodStored = Math.Max(
                0.0,
                (settlement.FoodStored * 0.55)
                + Math.Max(0.0, foodProduced - foodRequired) * (0.10 + (polity.HasAdvancement(AdvancementId.FoodStorage) ? 0.18 : 0.06)));
            double viability = Math.Clamp((foodProduced + foodStored) / Math.Max(1.0, foodRequired * 1.20), 0.0, 1.0);
            double localCarryingSupport = ResolveLocalCarryingSupport(
                sharePopulation,
                population?.PopulationCount ?? sharePopulation,
                region,
                storageSupport,
                1,
                true);

            projections.Add(new SettlementProjection(
                settlement,
                support,
                viability,
                storageSupport,
                localCarryingSupport,
                foodProduced,
                foodStored,
                foodRequired));
        }

        return projections;
    }

    private void RefreshFocalCandidates(World world)
    {
        world.FocalCandidateProfiles.Clear();
        foreach (Polity polity in world.Polities.Where(candidate => candidate.Population > 0))
        {
            StabilityBand stabilityBand = polity.FragmentationPressure switch
            {
                >= 0.85 => StabilityBand.Fragile,
                >= 0.55 => StabilityBand.Strained,
                <= 0.18 => StabilityBand.Strong,
                _ => StabilityBand.Stable
            };
            bool viable = polity.HasSettlements
                && polity.Population >= 90
                && stabilityBand != StabilityBand.Fragile
                && polity.YearsSinceFounded >= 2;
            string knowledgeSummary = polity.Discoveries.Count == 0
                ? "early knowledge"
                : string.Join(", ", polity.Discoveries.Take(3).Select(discovery => discovery.Summary));
            string recentHistoricalNote = world.CivilizationalHistory
                .Where(evt => evt.PolityId == polity.Id)
                .OrderByDescending(evt => evt.Year)
                .ThenByDescending(evt => evt.Month)
                .Select(evt => evt.Summary)
                .FirstOrDefault()
                ?? "new polity";
            world.FocalCandidateProfiles.Add(new FocalCandidateProfile(
                polity.Id,
                polity.LineageId,
                polity.SpeciesId,
                polity.YearsSinceFounded,
                polity.SettlementCount,
                ResolvePopulationBand(polity.Population),
                polity.RegionId,
                polity.CurrentPressureSummary ?? "forming",
                knowledgeSummary,
                recentHistoricalNote,
                stabilityBand,
                viable));
        }
    }

    private void TryMigrateGroup(World world, Species species, SentientPopulationGroup group)
    {
        Region current = world.Regions[group.CurrentRegionId];
        Region? destination = current.ConnectedRegionIds
            .Select(id => world.Regions[id])
            .Where(region => !HasActiveLineageArcInRegion(world, group.SourceLineageId, region.Id))
            .OrderByDescending(region =>
            {
                RegionSpeciesPopulation? regionalPopulation = region.GetSpeciesPopulation(species.Id);
                double support = regionalPopulation is null ? 0.0 : ResolveRegionalSupport(world, species, region, regionalPopulation);
                return support + region.EffectiveEcologyProfile.MigrationEase + region.Fertility;
            })
            .FirstOrDefault();
        if (destination is null || destination.Id == current.Id)
        {
            return;
        }

        group.CurrentRegionId = destination.Id;
        group.LastMigrationYear = world.Time.Year;
        group.MigrationPattern = "opportunistic migration";
        group.SedentismPressure = Math.Max(0.0, group.SedentismPressure - 0.10);
        group.MigrationPressure = Math.Max(0.0, group.MigrationPressure - 0.18);
        group.IdentityMarkers.Add(destination.Biome.ToString());
        SeedRegionalDiscoveries(world, destination, species, group.SharedKnowledge);
        world.AddCivilizationalHistoryEvent(
            CivilizationalHistoryEventType.MigrationWave,
            group.SourceLineageId,
            destination.Id,
            $"Group {group.Id} migrated from {current.Name} to {destination.Name}",
            "migration_pressure",
            groupId: group.Id);
    }

    private void SplitGroup(World world, SentientPopulationGroup group)
    {
        if (group.PopulationCount < 66)
        {
            return;
        }

        int childPopulation = Math.Max(22, group.PopulationCount / 3);
        group.PopulationCount -= childPopulation;
        int destinationRegionId = SelectBranchRegion(world, group.CurrentRegionId, group.SourceLineageId) ?? group.CurrentRegionId;
        SentientPopulationGroup descendant = new(world.SentientGroups.Max(candidate => candidate.Id) + 1)
        {
            SourceLineageId = group.SourceLineageId,
            CurrentRegionId = destinationRegionId,
            FounderRegionId = destinationRegionId,
            ActivationYear = world.Time.Year,
            PopulationCount = childPopulation,
            MobilityMode = group.MobilityMode,
            Cohesion = Math.Clamp(group.Cohesion - 0.08, 0.0, 1.0),
            SocialComplexity = Math.Clamp(group.SocialComplexity - 0.04, 0.0, 1.0),
            SurvivalKnowledge = group.SurvivalKnowledge * 0.82,
            SettlementIntent = group.SettlementIntent * 0.76,
            Stress = Math.Clamp(group.Stress + 0.08, 0.0, 1.0),
            SedentismPressure = group.SedentismPressure * 0.70,
            ContinuityYears = 0,
            IdentityStrength = group.IdentityStrength * 0.75,
            MigrationPattern = "fragmented branch",
            FoundingMemorySeed = "split from elder band",
            ThreatMemorySeed = group.PressureSummary,
            PressureSummary = "group fragmentation",
            PredecessorGroupId = group.Id,
            FoodSecurity = group.FoodSecurity * 0.82,
            StorageSupport = group.StorageSupport * 0.74,
            LocalCarryingSupport = group.LocalCarryingSupport,
            MigrationPressure = Math.Clamp(group.MigrationPressure + 0.08, 0.0, 1.0),
            FragmentationPressure = Math.Clamp(group.FragmentationPressure + 0.10, 0.0, 1.0),
            IsFallbackCreated = group.IsFallbackCreated
        };
        foreach ((string key, CulturalDiscovery discovery) in group.SharedKnowledge.Take(3))
        {
            descendant.SharedKnowledge[key] = discovery;
        }

        world.SentientGroups.Add(descendant);
        world.AddCivilizationalHistoryEvent(
            CivilizationalHistoryEventType.Fragmentation,
            group.SourceLineageId,
            descendant.CurrentRegionId,
            $"Group {group.Id} split into group {descendant.Id}",
            "group_fragmentation",
            groupId: descendant.Id);
    }

    private void FragmentSociety(World world, EmergingSociety society, Region region)
    {
        if (society.Population < 96)
        {
            return;
        }

        int destinationRegionId = SelectBranchRegion(world, region.Id, society.LineageId) ?? region.Id;
        int fragmentPopulation = Math.Max(36, society.Population / 3);
        EmergingSociety descendant = new(world.Societies.Max(candidate => candidate.Id) + 1)
        {
            LineageId = society.LineageId,
            SpeciesId = society.SpeciesId,
            OriginRegionId = destinationRegionId,
            FoundingYear = world.Time.Year,
            Population = fragmentPopulation,
            MobilityMode = society.MobilityMode,
            SubsistenceMode = society.SubsistenceMode,
            Cohesion = Math.Clamp(society.Cohesion - 0.12, 0.0, 1.0),
            IdentityStrength = Math.Clamp(society.IdentityStrength - 0.10, 0.0, 1.0),
            SocialComplexity = Math.Clamp(society.SocialComplexity - 0.08, 0.0, 1.0),
            SurvivalKnowledge = society.SurvivalKnowledge * 0.84,
            SedentismPressure = society.SedentismPressure * 0.78,
            PressureSummary = "society fragmentation",
            ContinuityYears = 0,
            ParentSocietyId = society.Id,
            FoundingMemorySeed = "split from elder society",
            ThreatMemorySeed = society.PressureSummary,
            IsFallbackCreated = society.IsFallbackCreated,
            FoodSecurity = society.FoodSecurity * 0.84,
            StorageSupport = society.StorageSupport * 0.80,
            SettlementSupport = society.SettlementSupport * 0.76,
            LocalCarryingSupport = society.LocalCarryingSupport,
            MigrationPressure = Math.Clamp(society.MigrationPressure + 0.06, 0.0, 1.0),
            FragmentationPressure = Math.Clamp(society.FragmentationPressure + 0.10, 0.0, 1.0)
        };
        descendant.RegionIds.Add(destinationRegionId);
        foreach ((string key, CulturalDiscovery discovery) in society.CulturalKnowledge.Take(3))
        {
            descendant.CulturalKnowledge[key] = discovery;
        }

        society.Population = Math.Max(0, society.Population - descendant.Population);
        society.DescendantSocietyIds.Add(descendant.Id);
        world.Societies.Add(descendant);
        world.AddCivilizationalHistoryEvent(
            CivilizationalHistoryEventType.Fragmentation,
            society.LineageId,
            destinationRegionId,
            $"Society {society.Id} fragmented into society {descendant.Id}",
            "society_fragmentation",
            societyId: descendant.Id);
    }

    private void CollapseGroup(World world, SentientPopulationGroup group, string reason)
    {
        group.IsCollapsed = true;
        world.AddCivilizationalHistoryEvent(
            CivilizationalHistoryEventType.Collapse,
            group.SourceLineageId,
            group.CurrentRegionId,
            $"Group {group.Id} collapsed",
            reason,
            groupId: group.Id);
    }

    private void CollapsePolity(World world, Polity polity)
    {
        if (polity.Population <= 0)
        {
            return;
        }

        polity.Population = 0;
        world.AddCivilizationalHistoryEvent(
            CivilizationalHistoryEventType.Collapse,
            polity.LineageId,
            polity.RegionId,
            $"{polity.Name} collapsed",
            "polity_collapse",
            polityId: polity.Id);
        world.AddEvent(
            WorldEventType.PolityCollapsed,
            WorldEventSeverity.Major,
            $"{polity.Name} collapsed",
            $"{polity.Name} could no longer maintain a viable population base.",
            reason: "polity_collapse",
            scope: WorldEventScope.Polity,
            polityId: polity.Id,
            polityName: polity.Name,
            speciesId: polity.SpeciesId,
            regionId: polity.RegionId,
            regionName: world.Regions[polity.RegionId].Name);
    }

    private static double ScoreActivationCandidate(World world, Species species, RegionSpeciesPopulation population)
    {
        Region region = world.Regions[population.RegionId];
        return ResolveRegionalSupport(world, species, region, population)
               + (population.PopulationCount / 220.0)
               + (population.DivergenceScore * 0.08)
               + (region.EffectiveEcologyProfile.HabitabilityScore * 0.24);
    }

    private static double ResolveRegionalSupport(World world, Species species, Region region, RegionSpeciesPopulation population)
    {
        double productivity = region.EffectiveEcologyProfile.BasePrimaryProductivity;
        double habitability = region.EffectiveEcologyProfile.HabitabilityScore;
        double populationFit = population.HabitatSuitability;
        double pressurePenalty = Math.Clamp(population.StressScore + population.RecentFoodStress + population.RecentPredationPressure, 0.0, 1.5) * 0.18;
        double sentienceBonus = species.SentiencePotential * 0.12;
        return Math.Clamp((productivity * 0.40) + (habitability * 0.30) + (populationFit * 0.28) + sentienceBonus - pressurePenalty, 0.0, 1.0);
    }

    private static MobilityMode ResolveMobilityMode(Region region, RegionSpeciesPopulation population)
        => region.Fertility >= 0.70 && region.WaterAvailability >= 0.64 && population.HabitatSuitability >= 0.72
            ? MobilityMode.SemiSedentary
            : MobilityMode.Nomadic;

    private bool CanActivateIndependentTrajectory(World world, int regionId, RegionSpeciesPopulation population, IReadOnlyCollection<int> occupiedRegions)
    {
        if (occupiedRegions.Contains(regionId))
        {
            return false;
        }

        if (occupiedRegions.Count == 0)
        {
            return true;
        }

        int minimumDistance = occupiedRegions
            .Select(existingRegionId => ResolveRegionDistance(world, existingRegionId, regionId, _settings.SentientTrajectoryMinimumRegionalSeparation))
            .Min();
        return minimumDistance >= _settings.SentientTrajectoryMinimumRegionalSeparation
               || (population.DivergenceScore >= 1.2 && population.PopulationCount >= _settings.SentientActivationMinimumPopulation + 18);
    }

    private Region ResolvePrimarySocietyRegion(World world, EmergingSociety society, Species species, out RegionSpeciesPopulation? primaryPopulation, out double primarySupport)
    {
        Region selected = world.Regions[society.RegionIds.OrderBy(id => id).First()];
        primaryPopulation = selected.GetSpeciesPopulation(species.Id);
        primarySupport = primaryPopulation is null ? 0.0 : ResolveRegionalSupport(world, species, selected, primaryPopulation);

        foreach (int regionId in society.RegionIds)
        {
            Region region = world.Regions[regionId];
            RegionSpeciesPopulation? population = region.GetSpeciesPopulation(species.Id);
            if (population is null)
            {
                continue;
            }

            double support = ResolveRegionalSupport(world, species, region, population);
            if (support > primarySupport)
            {
                selected = region;
                primaryPopulation = population;
                primarySupport = support;
            }
        }

        return selected;
    }

    private List<int> GetActiveLineageRegions(World world, int lineageId)
    {
        HashSet<int> regions = [];
        foreach (SentientPopulationGroup group in world.SentientGroups.Where(group => !group.IsCollapsed && group.SourceLineageId == lineageId))
        {
            regions.Add(group.CurrentRegionId);
        }

        foreach (EmergingSociety society in world.Societies.Where(society => !society.IsCollapsed && society.LineageId == lineageId))
        {
            foreach (int regionId in society.RegionIds)
            {
                regions.Add(regionId);
            }
        }

        foreach (Polity polity in world.Polities.Where(polity => polity.Population > 0 && polity.LineageId == lineageId))
        {
            regions.Add(polity.RegionId);
        }

        return regions.OrderBy(regionId => regionId).ToList();
    }

    private bool HasActiveLineageArcInRegion(World world, int lineageId, int regionId)
        => world.SentientGroups.Any(group => !group.IsCollapsed && group.SourceLineageId == lineageId && group.CurrentRegionId == regionId)
           || world.Societies.Any(society => !society.IsCollapsed && society.LineageId == lineageId && society.RegionIds.Contains(regionId))
           || world.Polities.Any(polity => polity.Population > 0 && polity.LineageId == lineageId && polity.RegionId == regionId);

    private int ResolveRegionDistance(World world, int startRegionId, int targetRegionId, int earlyExitDistance)
    {
        if (startRegionId == targetRegionId)
        {
            return 0;
        }

        Queue<(int RegionId, int Distance)> frontier = new();
        HashSet<int> visited = [startRegionId];
        frontier.Enqueue((startRegionId, 0));

        while (frontier.Count > 0)
        {
            (int regionId, int distance) = frontier.Dequeue();
            if (distance >= earlyExitDistance)
            {
                return distance;
            }

            foreach (int nextRegionId in world.Regions[regionId].ConnectedRegionIds)
            {
                if (!visited.Add(nextRegionId))
                {
                    continue;
                }

                if (nextRegionId == targetRegionId)
                {
                    return distance + 1;
                }

                frontier.Enqueue((nextRegionId, distance + 1));
            }
        }

        return int.MaxValue / 4;
    }

    private int? SelectBranchRegion(World world, int sourceRegionId, int lineageId)
    {
        return world.Regions[sourceRegionId].ConnectedRegionIds
            .Where(regionId => !HasActiveLineageArcInRegion(world, lineageId, regionId))
            .Select(regionId => world.Regions[regionId])
            .OrderByDescending(region => region.EffectiveEcologyProfile.HabitabilityScore + region.Fertility + region.WaterAvailability)
            .ThenBy(region => region.Id)
            .Select(region => (int?)region.Id)
            .FirstOrDefault();
    }

    private static double ResolveLocalCarryingSupport(int actorPopulation, int regionalPopulationSupport, Region region, double storageSupport, int anchoredSites, bool settled)
    {
        double carryingCapacity = Math.Max(
            45.0,
            (regionalPopulationSupport * 0.75)
            + (region.Fertility * 90.0)
            + (region.WaterAvailability * 70.0)
            + (region.EffectiveEcologyProfile.BasePrimaryProductivity * 80.0)
            + (storageSupport * 40.0)
            + (anchoredSites * 24.0)
            + (settled ? 18.0 : 0.0));
        double crowdingRatio = actorPopulation / Math.Max(1.0, carryingCapacity);
        return Math.Clamp(1.08 - (Math.Max(0.0, crowdingRatio - 0.80) * 1.35), 0.0, 1.0);
    }

    private double ResolveGrowthPressure(double foodSecurity, double storageSupport, double localCarryingSupport, double cohesion, double complexity, double continuityFactor, double settlementSupport, double migrationPressure, double fragmentationPressure, double stress, double subsistenceBonus)
        => Math.Clamp(
            (foodSecurity * 0.28)
            + (storageSupport * 0.10)
            + (localCarryingSupport * 0.18)
            + (cohesion * 0.12)
            + (complexity * 0.08)
            + (continuityFactor * 0.08)
            + (settlementSupport * 0.08)
            + subsistenceBonus
            - (migrationPressure * 0.10)
            - (fragmentationPressure * 0.10)
            - (stress * 0.16),
            0.0,
            1.0);

    private int ResolvePopulationDelta(int currentPopulation, SocialPressureSnapshot snapshot, double growthScale, double declineScale)
    {
        if (currentPopulation <= 0)
        {
            return 0;
        }

        double centered = snapshot.GrowthPressure - _settings.SocialNeutralGrowthPoint;
        double positiveHeadroom = Math.Max(0.01, 1.0 - _settings.SocialNeutralGrowthPoint);
        double negativeHeadroom = Math.Max(0.01, _settings.SocialNeutralGrowthPoint);
        double annualRate = centered >= 0
            ? (centered / positiveHeadroom) * (_settings.SocialMaximumAnnualGrowthRate * growthScale)
            : (centered / negativeHeadroom) * (_settings.SocialMaximumAnnualDeclineRate * declineScale);

        if (snapshot.Stress >= 0.88)
        {
            annualRate -= 0.08 * declineScale;
        }

        if (snapshot.FoodSecurity < 0.24)
        {
            annualRate -= 0.06 * declineScale;
        }

        annualRate = Math.Clamp(annualRate, -_settings.SocialMaximumAnnualDeclineRate * declineScale, _settings.SocialMaximumAnnualGrowthRate * growthScale);
        int delta = (int)Math.Round(currentPopulation * annualRate);
        if (delta == 0)
        {
            if (annualRate >= 0.025)
            {
                return 1;
            }

            if (annualRate <= -0.025)
            {
                return -1;
            }
        }

        return delta;
    }

    private static string ResolvePressureSummary(double stress, double support, double foodSecurity, double migrationPressure, double fragmentationPressure, double settlementSupport)
    {
        if (stress >= 0.72 || foodSecurity < 0.28)
        {
            return "ecological hardship";
        }

        if (fragmentationPressure >= 0.68)
        {
            return "society fragmentation";
        }

        if (migrationPressure >= 0.64)
        {
            return "migration pressure";
        }

        if (settlementSupport >= 0.58 && support >= 0.58)
        {
            return "anchoring on rich ground";
        }

        if (support < 0.35)
        {
            return "frontier strain";
        }

        return "shared survival";
    }

    private static double ResolveSubsistenceBonus(SubsistenceMode subsistenceMode)
        => subsistenceMode switch
        {
            SubsistenceMode.FarmingEmergent => 0.16,
            SubsistenceMode.ProtoFarming => 0.10,
            SubsistenceMode.MixedHunterForager => 0.06,
            SubsistenceMode.HuntingFocused => 0.02,
            _ => 0.01
        };

    private void SeedRegionalDiscoveries(World world, Region region, Species species, IDictionary<string, CulturalDiscovery> discoveries)
    {
        discoveries.TryAdd($"region:{region.Id}:water", new CulturalDiscovery(
            $"region:{region.Id}:water",
            $"{region.Name} holds reliable water",
            CulturalDiscoveryCategory.Geography,
            RegionId: region.Id));
        if (region.Fertility >= 0.64)
        {
            discoveries.TryAdd($"region:{region.Id}:fertile", new CulturalDiscovery(
                $"region:{region.Id}:fertile",
                $"{region.Name} is fertile ground",
                CulturalDiscoveryCategory.Environment,
                RegionId: region.Id));
        }

        if (region.Biome is RegionBiome.Mountains or RegionBiome.Drylands)
        {
            discoveries.TryAdd($"region:{region.Id}:harsh", new CulturalDiscovery(
                $"region:{region.Id}:harsh",
                $"{region.Name} is harsh country",
                CulturalDiscoveryCategory.Environment,
                RegionId: region.Id));
        }

        foreach (RegionSpeciesPopulation population in region.SpeciesPopulations.Where(candidate => candidate.PopulationCount > 0))
        {
            Species regionalSpecies = world.Species.First(candidate => candidate.Id == population.SpeciesId);
            if (regionalSpecies.Id == species.Id)
            {
                continue;
            }

            if (regionalSpecies.TrophicRole == TrophicRole.Producer)
            {
                discoveries.TryAdd($"species:{regionalSpecies.Id}:edible", new CulturalDiscovery(
                    $"species:{regionalSpecies.Id}:edible",
                    $"{regionalSpecies.Name} can be gathered as food",
                    CulturalDiscoveryCategory.FoodSafety,
                    SpeciesId: regionalSpecies.Id,
                    RegionId: region.Id));
            }
            else if (regionalSpecies.TrophicRole is TrophicRole.Predator or TrophicRole.Apex)
            {
                discoveries.TryAdd($"species:{regionalSpecies.Id}:danger", new CulturalDiscovery(
                    $"species:{regionalSpecies.Id}:danger",
                    $"{regionalSpecies.Name} is dangerous in {region.Name}",
                    CulturalDiscoveryCategory.AnimalBehavior,
                    SpeciesId: regionalSpecies.Id,
                    RegionId: region.Id));
            }
            else if (regionalSpecies.MeatYield > 0)
            {
                discoveries.TryAdd($"species:{regionalSpecies.Id}:prey", new CulturalDiscovery(
                    $"species:{regionalSpecies.Id}:prey",
                    $"{regionalSpecies.Name} is a useful prey animal",
                    CulturalDiscoveryCategory.SpeciesUse,
                    SpeciesId: regionalSpecies.Id,
                    RegionId: region.Id));
            }
        }
    }

    private static string BuildPolityName(Region region, Species species, int polityId)
        => $"{region.Name} {species.Name.Split(' ')[0]} Polity {polityId}";

    private static string BuildSettlementName(Region region, Polity polity)
        => $"{region.Name} Hearth";

    private static string ResolvePopulationBand(int population)
        => population switch
        {
            < 80 => "small",
            < 180 => "growing",
            < 320 => "solid",
            _ => "large"
        };

    private sealed record SocialPressureSnapshot(
        double Support,
        double FoodSecurity,
        double StorageSupport,
        double SettlementSupport,
        double LocalCarryingSupport,
        double MigrationPressure,
        double FragmentationPressure,
        double Stress,
        double GrowthPressure);

    private sealed record SettlementProjection(
        Settlement Settlement,
        double Support,
        double Viability,
        double StorageSupport,
        double LocalCarryingSupport,
        double FoodProduced,
        double FoodStored,
        double FoodRequired);
}
