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
        RefreshFocalCandidates(world);
        world.PhaseCReadinessReport = PhaseCReadinessEvaluator.Evaluate(world, _settings);
    }

    private void ActivateSentientGroups(World world)
    {
        foreach (Species species in world.Species
                     .Where(candidate => candidate.SentienceCapability == SentienceCapabilityState.Capable && !candidate.IsGloballyExtinct))
        {
            if (world.SentientGroups.Any(group => !group.IsCollapsed && group.SourceLineageId == species.LineageId)
                || world.Societies.Any(society => !society.IsCollapsed && society.LineageId == species.LineageId)
                || world.Polities.Any(polity => polity.Population > 0 && polity.LineageId == species.LineageId))
            {
                continue;
            }

            RegionSpeciesPopulation? population = world.Regions
                .Select(region => region.GetSpeciesPopulation(species.Id))
                .Where(candidate => candidate is not null && candidate.PopulationCount >= _settings.SentientActivationMinimumPopulation)
                .Cast<RegionSpeciesPopulation>()
                .OrderByDescending(candidate => ScoreActivationCandidate(world, species, candidate))
                .FirstOrDefault();
            if (population is null)
            {
                continue;
            }

            Region region = world.Regions[population.RegionId];
            SentientPopulationGroup group = new(world.SentientGroups.Count == 0 ? 1 : world.SentientGroups.Max(candidate => candidate.Id) + 1)
            {
                SourceLineageId = species.LineageId,
                CurrentRegionId = region.Id,
                FounderRegionId = region.Id,
                ActivationYear = world.Time.Year,
                PopulationCount = Math.Max(28, Math.Min(90, population.PopulationCount / 2)),
                MobilityMode = ResolveMobilityMode(region, population),
                Cohesion = 0.34 + PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Sociality) * 0.35,
                SocialComplexity = 0.18 + PopulationTraitResolver.GetEffectiveTrait(species, population, SpeciesTrait.Intelligence) * 0.28,
                SurvivalKnowledge = 0.16,
                SettlementIntent = 0.0,
                Stress = 0.18,
                SedentismPressure = region.EffectiveEcologyProfile.BasePrimaryProductivity * 0.22,
                ContinuityYears = 0,
                IdentityStrength = 0.18,
                MigrationPattern = region.ConnectedRegionIds.Count >= 3 ? "seasonal circuit" : "anchored foraging",
                FoundingMemorySeed = $"{region.Name} awakening",
                ThreatMemorySeed = population.AdaptationPressureSummary,
                PressureSummary = "activation"
            };

            group.IdentityMarkers.Add(region.Biome.ToString());
            group.IdentityMarkers.Add(species.EcologyNiche);
            SeedRegionalDiscoveries(world, region, species, group.SharedKnowledge);

            species.IsSapient = true;
            world.SentientGroups.Add(group);
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
                    ["support"] = ResolveRegionalSupport(world, species, region, population).ToString("F2")
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
            group.Cohesion = Math.Clamp(group.Cohesion + (support * 0.12) + (group.SharedKnowledge.Count * 0.01) - (group.Stress * 0.08), 0.0, 1.0);
            group.SocialComplexity = Math.Clamp(group.SocialComplexity + (group.Cohesion * 0.05) + (group.ContinuityYears * 0.01), 0.0, 1.0);
            group.SurvivalKnowledge = Math.Clamp(group.SurvivalKnowledge + 0.06 + (support * 0.05), 0.0, 1.0);
            group.SedentismPressure = Math.Clamp(group.SedentismPressure + (region.EffectiveEcologyProfile.BasePrimaryProductivity * 0.10) + (region.WaterAvailability * 0.08), 0.0, 1.0);
            group.SettlementIntent = Math.Clamp(group.SettlementIntent + (group.SedentismPressure * 0.08) + (group.SharedKnowledge.Count * 0.02), 0.0, 1.0);
            group.IdentityStrength = Math.Clamp(group.IdentityStrength + 0.05 + (group.Cohesion * 0.04), 0.0, 1.0);
            group.Stress = Math.Clamp((1.0 - support) + Math.Max(0.0, 0.45 - group.Cohesion), 0.0, 1.0);
            group.PressureSummary = ResolvePressureSummary(group.Stress, support, group.SedentismPressure);
            group.ContinuityYears++;
            SeedRegionalDiscoveries(world, region, species, group.SharedKnowledge);

            if (group.Stress >= 0.88 || group.PopulationCount <= 12)
            {
                CollapseGroup(world, group, "terminal_group_stress");
                continue;
            }

            if (group.Stress >= 0.56)
            {
                TryMigrateGroup(world, species, group);
            }

            if (group.PopulationCount >= 90 && group.Cohesion < 0.40)
            {
                SplitGroup(world, group);
            }

            if (CanFormSociety(group, support))
            {
                FormSociety(world, species, group, region);
            }
            else if (group.ContinuityYears >= _settings.PersistentGroupContinuityYears
                     && group.Cohesion >= _settings.PersistentGroupCohesionThreshold)
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
                        ["cohesion"] = group.Cohesion.ToString("F2")
                    });
            }
        }
    }

    private void UpdateSocieties(World world)
    {
        foreach (EmergingSociety society in world.Societies.Where(candidate => !candidate.IsCollapsed).ToList())
        {
            Species species = world.Species.First(candidate => candidate.Id == society.SpeciesId);
            Region region = world.Regions[society.RegionIds.OrderBy(id => id).First()];
            RegionSpeciesPopulation? population = region.GetSpeciesPopulation(species.Id);
            double support = population is null ? 0.0 : ResolveRegionalSupport(world, species, region, population);
            society.ContinuityYears++;
            society.Cohesion = Math.Clamp(society.Cohesion + (support * 0.10) - (Math.Max(0.0, 0.55 - society.SocialComplexity) * 0.04), 0.0, 1.0);
            society.SocialComplexity = Math.Clamp(society.SocialComplexity + 0.04 + (society.CulturalKnowledge.Count * 0.01), 0.0, 1.0);
            society.SurvivalKnowledge = Math.Clamp(society.SurvivalKnowledge + 0.05, 0.0, 1.0);
            society.SedentismPressure = Math.Clamp(society.SedentismPressure + (region.Fertility * 0.08) + (region.WaterAvailability * 0.08), 0.0, 1.0);
            society.IdentityStrength = Math.Clamp(society.IdentityStrength + 0.04 + (society.ContinuityYears * 0.01), 0.0, 1.0);
            society.PressureSummary = ResolvePressureSummary(Math.Max(0.0, 0.75 - support), support, society.SedentismPressure);
            society.SubsistenceMode = ResolveSubsistenceMode(society, region);

            SeedRegionalDiscoveries(world, region, species, society.CulturalKnowledge);
            foreach (SocialSettlement settlement in world.SocialSettlements.Where(settlement => settlement.FounderSocietyId == society.Id && !settlement.IsAbandoned))
            {
                settlement.StorageLevel = Math.Clamp(settlement.StorageLevel + (society.SurvivalKnowledge * 0.08), 0.0, 1.0);
                settlement.SettlementViability = Math.Clamp(settlement.SettlementViability + (support * 0.12) - (Math.Max(0.0, 0.5 - society.Cohesion) * 0.10), 0.0, 1.0);
                settlement.CurrentPressureSummary = society.PressureSummary;
                foreach ((string key, CulturalDiscovery discovery) in society.CulturalKnowledge)
                {
                    settlement.LocalKnowledge.TryAdd(key, discovery);
                }

                if (settlement.SettlementViability < 0.24)
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

            if (society.SedentismPressure >= _settings.SettlementFoundingPressureThreshold
                && society.CulturalKnowledge.Count >= _settings.SettlementIntentReturnThreshold
                && !world.SocialSettlements.Any(settlement => settlement.FounderSocietyId == society.Id && !settlement.IsAbandoned))
            {
                FoundSettlement(world, society, region);
            }

            if (society.Population >= _settings.PolityFormationMinimumPopulation
                && society.CulturalKnowledge.Count >= _settings.PolityFormationMinimumKnowledgeCount
                && society.SocialComplexity >= _settings.PolityFormationComplexityThreshold
                && world.SocialSettlements.Any(settlement => settlement.FounderSocietyId == society.Id && !settlement.IsAbandoned && settlement.SettlementViability >= 0.55)
                && !world.Polities.Any(polity => polity.FounderSocietyId == society.Id && polity.Population > 0))
            {
                FormPolity(world, society, species);
            }

            if (society.Population >= 220 && society.Cohesion < 0.44)
            {
                FragmentSociety(world, society, region);
            }

            if (society.Cohesion < 0.18 || (support < 0.22 && society.Population < 40))
            {
                society.IsCollapsed = true;
                world.AddCivilizationalHistoryEvent(
                    CivilizationalHistoryEventType.Collapse,
                    society.LineageId,
                    region.Id,
                    $"Society {society.Id} collapsed in {region.Name}",
                    "society_collapse",
                    societyId: society.Id);
            }
        }
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

    private static double ScoreActivationCandidate(World world, Species species, RegionSpeciesPopulation population)
    {
        Region region = world.Regions[population.RegionId];
        return ResolveRegionalSupport(world, species, region, population)
               + (population.PopulationCount / 200.0)
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

    private static string ResolvePressureSummary(double stress, double support, double sedentismPressure)
    {
        if (stress >= 0.70)
        {
            return "ecological hardship";
        }

        if (sedentismPressure >= 0.65 && support >= 0.58)
        {
            return "anchoring on rich ground";
        }

        if (support < 0.35)
        {
            return "frontier strain";
        }

        return "shared survival";
    }

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

    private bool CanFormSociety(SentientPopulationGroup group, double support)
        => group.PopulationCount >= 44
           && group.ContinuityYears >= _settings.SocietyFormationContinuityYears
           && group.Cohesion >= _settings.PersistentGroupCohesionThreshold
           && group.IdentityStrength >= _settings.SocietyFormationIdentityThreshold
           && group.SharedKnowledge.Count >= 3
           && support >= _settings.SentientActivationMinimumSupport;

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
            SubsistenceMode = region.Fertility >= 0.70 ? SubsistenceMode.ProtoFarming : SubsistenceMode.MixedHunterForager,
            Cohesion = group.Cohesion,
            IdentityStrength = group.IdentityStrength,
            SocialComplexity = group.SocialComplexity,
            SurvivalKnowledge = group.SurvivalKnowledge,
            SedentismPressure = group.SedentismPressure,
            PressureSummary = group.PressureSummary,
            ContinuityYears = group.ContinuityYears,
            PredecessorGroupId = group.Id,
            FoundingMemorySeed = group.FoundingMemorySeed,
            ThreatMemorySeed = group.ThreatMemorySeed
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
            StorageLevel = Math.Clamp(society.SurvivalKnowledge * 0.45, 0.0, 1.0),
            SettlementViability = Math.Clamp((region.Fertility * 0.35) + (region.WaterAvailability * 0.30) + (society.SurvivalKnowledge * 0.25), 0.0, 1.0),
            CurrentPressureSummary = society.PressureSummary
        };
        foreach ((string key, CulturalDiscovery discovery) in society.CulturalKnowledge)
        {
            settlement.LocalKnowledge[key] = discovery;
        }

        world.SocialSettlements.Add(settlement);
        society.SettlementIds.Add(settlement.Id);
        society.MobilityMode = MobilityMode.SemiSedentary;
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

    private SubsistenceMode ResolveSubsistenceMode(EmergingSociety society, Region region)
    {
        int preyKnowledge = society.CulturalKnowledge.Count(entry => entry.Key.Contains(":prey", StringComparison.Ordinal));
        int plantKnowledge = society.CulturalKnowledge.Count(entry => entry.Key.Contains(":edible", StringComparison.Ordinal));
        bool fertile = region.Fertility >= 0.68 && region.WaterAvailability >= 0.58;
        bool settled = society.SettlementIds.Count > 0;

        if (settled && fertile && plantKnowledge >= 2 && society.SurvivalKnowledge >= 0.50)
        {
            return society.SurvivalKnowledge >= 0.72
                ? SubsistenceMode.FarmingEmergent
                : SubsistenceMode.ProtoFarming;
        }

        if (plantKnowledge >= 2 && preyKnowledge >= 1)
        {
            return SubsistenceMode.MixedHunterForager;
        }

        if (preyKnowledge > plantKnowledge)
        {
            return SubsistenceMode.HuntingFocused;
        }

        return SubsistenceMode.ForagingFocused;
    }

    private void FormPolity(World world, EmergingSociety society, Species species)
    {
        int polityId = world.Polities.Count == 0 ? 1 : world.Polities.Max(candidate => candidate.Id) + 1;
        int homeRegionId = society.RegionIds.OrderBy(id => id).First();
        Region homeRegion = world.Regions[homeRegionId];
        string polityName = BuildPolityName(homeRegion, species, polityId);
        Polity polity = new(polityId, polityName, species.Id, homeRegionId, society.Population, lineageId: society.LineageId)
        {
            FounderSocietyId = society.Id,
            ParentPolityId = society.FounderPolityId,
            Stage = society.SettlementIds.Count > 0 ? PolityStage.Tribe : PolityStage.Band,
            SettlementStatus = society.SettlementIds.Count > 0 ? SettlementStatus.SemiSettled : SettlementStatus.Nomadic,
            CurrentPressureSummary = society.PressureSummary,
            IdentitySeed = string.Join(", ", society.IdentityMarkers.Take(3))
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
        }

        world.Polities.Add(polity);
        society.FounderPolityId = polity.Id;
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

    private void TryMigrateGroup(World world, Species species, SentientPopulationGroup group)
    {
        Region current = world.Regions[group.CurrentRegionId];
        Region? destination = current.ConnectedRegionIds
            .Select(id => world.Regions[id])
            .OrderByDescending(region => region.EffectiveEcologyProfile.HabitabilityScore + region.EffectiveEcologyProfile.MigrationEase + region.Fertility)
            .FirstOrDefault();
        if (destination is null || destination.Id == current.Id)
        {
            return;
        }

        group.CurrentRegionId = destination.Id;
        group.LastMigrationYear = world.Time.Year;
        group.MigrationPattern = "opportunistic migration";
        group.SedentismPressure = Math.Max(0.0, group.SedentismPressure - 0.14);
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
        int childPopulation = Math.Max(18, group.PopulationCount / 3);
        group.PopulationCount -= childPopulation;
        SentientPopulationGroup descendant = new(world.SentientGroups.Max(candidate => candidate.Id) + 1)
        {
            SourceLineageId = group.SourceLineageId,
            CurrentRegionId = group.CurrentRegionId,
            FounderRegionId = group.CurrentRegionId,
            ActivationYear = world.Time.Year,
            PopulationCount = childPopulation,
            MobilityMode = group.MobilityMode,
            Cohesion = Math.Clamp(group.Cohesion - 0.06, 0.0, 1.0),
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
            PredecessorGroupId = group.Id
        };
        foreach ((string key, CulturalDiscovery discovery) in group.SharedKnowledge.Take(3))
        {
            descendant.SharedKnowledge[key] = discovery;
        }

        world.SentientGroups.Add(descendant);
        world.AddCivilizationalHistoryEvent(
            CivilizationalHistoryEventType.Fragmentation,
            group.SourceLineageId,
            group.CurrentRegionId,
            $"Group {group.Id} split into group {descendant.Id}",
            "group_fragmentation",
            groupId: descendant.Id);
    }

    private void FragmentSociety(World world, EmergingSociety society, Region region)
    {
        EmergingSociety descendant = new(world.Societies.Max(candidate => candidate.Id) + 1)
        {
            LineageId = society.LineageId,
            SpeciesId = society.SpeciesId,
            OriginRegionId = region.Id,
            FoundingYear = world.Time.Year,
            Population = Math.Max(32, society.Population / 3),
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
            ThreatMemorySeed = society.PressureSummary
        };
        descendant.RegionIds.Add(region.Id);
        foreach ((string key, CulturalDiscovery discovery) in society.CulturalKnowledge.Take(3))
        {
            descendant.CulturalKnowledge[key] = discovery;
        }

        society.Population -= descendant.Population;
        society.DescendantSocietyIds.Add(descendant.Id);
        world.Societies.Add(descendant);
        world.AddCivilizationalHistoryEvent(
            CivilizationalHistoryEventType.Fragmentation,
            society.LineageId,
            region.Id,
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
}
