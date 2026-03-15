using System;
using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Economy;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;
using LivingWorld.Systems;

namespace LivingWorld.Presentation;

public static class WatchScreenBuilder
{
    public static IReadOnlyList<string> BuildBodyLines(World world, ChronicleFocus focus, WatchUiState uiState)
    {
        WorldLookup lookup = new(world);
        WatchKnowledgeSnapshot knowledge = WatchInspectionData.CreateSnapshot(world, focus);

        return uiState.ActiveView switch
        {
            WatchViewType.FocalSelection => BuildFocalSelectionLines(world, uiState),
            WatchViewType.MyPolity => BuildMyPolityLines(knowledge.FocalPolity, lookup),
            WatchViewType.CurrentRegion => BuildCurrentRegionLines(world, knowledge, lookup),
            WatchViewType.KnownRegions => BuildKnownRegionsLines(knowledge, uiState, lookup),
            WatchViewType.RegionDetail => BuildRegionDetailLines(world, knowledge, uiState.SelectedRegionId, lookup),
            WatchViewType.KnownSpecies => BuildKnownSpeciesLines(world, knowledge, uiState),
            WatchViewType.SpeciesDetail => BuildSpeciesDetailLines(world, knowledge, uiState.SelectedSpeciesId),
            WatchViewType.KnownPolities => BuildKnownPolitiesLines(knowledge, uiState, lookup),
            WatchViewType.PolityDetail => BuildPolityDetailLines(world, knowledge, uiState.SelectedPolityId, lookup),
            WatchViewType.WorldOverview => BuildWorldOverviewLines(world, knowledge, lookup),
            _ => ["The chronicle is quiet."]
        };
    }

    public static List<string> BuildFooterLines(WatchUiState uiState, int width)
    {
        string border = new('=', width);
        string controls = WatchViewCatalog.BuildControlsSummary();
        string context = uiState.ActiveView switch
        {
            WatchViewType.FocalSelection => "Prehistory is frozen. Up/Down moves through candidate starts; Enter confirms the focal polity.",
            WatchViewType.Chronicle => "Chronicle stays newest-first. Up/Down scroll line-by-line; Left/Right page through history.",
            WatchViewType.KnownRegions => "Known Regions uses the focal polity's settlements, current center, discovered regions, and adjacent regions.",
            WatchViewType.KnownSpecies => "Known Species includes seen species, discovered prey knowledge, and species tied to known polities.",
            WatchViewType.KnownPolities => "Known Polities reflects active groups present in currently known regions.",
            WatchViewType.RegionDetail => "Region detail only shows discoveries and populations inside the focal polity's current knowledge horizon.",
            WatchViewType.SpeciesDetail => "Species detail shows known regions, known diet links, and visible regional divergence only.",
            WatchViewType.PolityDetail => "Foreign polity detail omits hidden discoveries and learned capabilities the focal polity has not actually observed.",
            _ => "Observation only: these screens inspect current known world state without issuing orders."
        };

        return
        [
            border,
            TruncateToWidth($"{controls} | {context}", width)
        ];
    }

    public static int ResolveViewportOffset(int rawLineCount, int viewportHeight, WatchUiState uiState, int maxOffset)
    {
        if (rawLineCount <= viewportHeight)
        {
            return 0;
        }

        if (WatchViewCatalog.IsListView(uiState.ActiveView))
        {
            int selectedIndex = uiState.GetSelectedIndex(uiState.ActiveView);
            return Math.Clamp(selectedIndex - (viewportHeight / 2), 0, maxOffset);
        }

        return Math.Clamp(uiState.GetScrollOffset(uiState.ActiveView), 0, maxOffset);
    }

    public static string DescribeView(WatchViewType view)
        => WatchViewCatalog.DescribeView(view);

    private static IReadOnlyList<string> BuildMyPolityLines(Polity? polity, WorldLookup lookup)
    {
        if (polity is null)
        {
            return ["No focal polity to inspect."];
        }

        string regionName = lookup.GetRequiredRegion(polity.RegionId, "My Polity view").Name;
        string speciesName = lookup.GetRequiredSpecies(polity.SpeciesId, "My Polity view").Name;
        double annualFoodRatio = polity.AnnualFoodNeeded <= 0 ? 1.0 : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;
        int managedHerdCount = polity.Settlements.Sum(settlement => settlement.ManagedHerds.Count);
        int cultivatedCropCount = polity.Settlements.Sum(settlement => settlement.CultivatedCrops.Count);
        string materialSurpluses = DescribeTopMaterialStates(polity.Settlements, MaterialPressureState.Surplus);
        string materialShortages = DescribeTopMaterialStates(polity.Settlements, MaterialPressureState.Deficit);
        string leadingProduction = DescribeLeadingProductionSettlements(polity.Settlements);
        string materialStatus = DescribeMaterialCapabilityStatus(polity.Settlements);
        string economyNeeds = DescribeEconomyNeeds(polity.Settlements);
        string tradeGoods = DescribeEconomySignals(polity.Settlements, EconomySummaryLabel.TradeGood);
        string locallyCommon = DescribeEconomySignals(polity.Settlements, EconomySummaryLabel.LocallyCommon);

        List<string> lines =
        [
            "My Polity",
            string.Empty,
            $" Name: {polity.Name}",
            $" Species: {speciesName}",
            $" Stage: {WatchInspectionData.DescribeStage(polity.Stage)}",
            $" Population: {polity.Population}",
            $" Years Since Founded: {polity.YearsSinceFounded}",
            $" Current Region: {regionName}",
            $" Settlement Count: {polity.SettlementCount}",
            $" Food Stores: {polity.FoodStores:F0} ({ChronicleTextFormatter.DescribeFoodState(polity)})",
            $" Annual Food Ratio: {annualFoodRatio:F2}",
            $" Food Sources: Wild {polity.AnnualFoodGathered:F0} | Hunt {polity.FoodHuntedThisYear:F0} | Farm {polity.AnnualFoodFarmed:F0} | Trade {polity.AnnualFoodImported:F0}",
            $" Managed Food This Year: {polity.AnnualFoodManaged:F0}",
            $" Managed Sources: Herds {managedHerdCount} | Crops {cultivatedCropCount}",
            $" Material Surpluses: {materialSurpluses}",
            $" Critical Shortages: {materialShortages}",
            $" Leading Production: {leadingProduction}",
            $" Economy Needs: {economyNeeds}",
            $" Trade Goods: {tradeGoods}",
            $" Locally Common: {locallyCommon}",
            $" Tool / Storage / Preservation: {materialStatus}",
            $" Major Pressures: Migration {polity.MigrationPressure:F2} | Fragmentation {polity.FragmentationPressure:F2} | Starvation Months {polity.StarvationMonthsThisYear}",
            $" Trade Links This Year: {polity.TradePartnerCountThisYear}",
            $" Hunting Losses This Year: {polity.HuntingCasualtiesThisYear}",
            string.Empty,
            " Settlements:"
        ];

        AppendSettlementLines(lines, polity.Settlements, lookup);
        lines.Add(string.Empty);
        lines.Add($" Discoveries: {DescribeDiscoveries(polity)}");
        lines.Add($" Learned: {DescribeAdvancements(polity)}");
        lines.Add(string.Empty);
        lines.Add(" Enter keeps this expanded focal view.");
        return lines;
    }

    private static IReadOnlyList<string> BuildCurrentRegionLines(World world, WatchKnowledgeSnapshot knowledge, WorldLookup lookup)
    {
        List<string> lines = BuildRegionDetailLines(world, knowledge, knowledge.CurrentRegion?.Id, lookup).ToList();
        lines.Add(string.Empty);
        lines.Add(" Press Enter for region detail.");
        return lines;
    }

    private static IReadOnlyList<string> BuildKnownRegionsLines(WatchKnowledgeSnapshot knowledge, WatchUiState uiState, WorldLookup lookup)
    {
        List<string> lines = ["Known Regions", string.Empty];
        if (knowledge.KnownRegions.Count == 0)
        {
            lines.Add(" No known regions.");
            return lines;
        }

        int selectedIndex = NormalizeSelection(uiState, WatchViewType.KnownRegions, knowledge.KnownRegions.Count);
        for (int index = 0; index < knowledge.KnownRegions.Count; index++)
        {
            Region region = knowledge.KnownRegions[index];
            string marker = index == selectedIndex ? ">" : " ";
            string resourceSummary = DescribeRegionResourceSummary(knowledge, region.Id);
            string specializationSummary = DescribeVisibleRegionSpecialization(lookup.GetSettlementsInRegion(region.Id));
            lines.Add(
                $"{marker} {region.Name,-18} {region.Biome,-12} Fert {region.Fertility:F2} Sett {lookup.GetSettlementsInRegion(region.Id).Count} {resourceSummary} | {specializationSummary}");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildKnownSpeciesLines(World world, WatchKnowledgeSnapshot knowledge, WatchUiState uiState)
    {
        List<string> lines = ["Known Species", string.Empty];
        if (knowledge.KnownSpecies.Count == 0)
        {
            lines.Add(" No known species.");
            return lines;
        }

        int selectedIndex = NormalizeSelection(uiState, WatchViewType.KnownSpecies, knowledge.KnownSpecies.Count);
        for (int index = 0; index < knowledge.KnownSpecies.Count; index++)
        {
            Species species = knowledge.KnownSpecies[index];
            string marker = index == selectedIndex ? ">" : " ";
            int knownRegionCount = knowledge.GetKnownRegionalPopulations(world, species.Id).Count;
            lines.Add(
                $"{marker} {species.Name,-20} {DescribeTrophicRole(species.TrophicRole),-10} {DescribeSpeciesSize(species),-6} regions {knownRegionCount}");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildKnownPolitiesLines(WatchKnowledgeSnapshot knowledge, WatchUiState uiState, WorldLookup lookup)
    {
        List<Polity> visibleForeignPolities = knowledge.KnownPolities
            .Where(polity => polity.Id != knowledge.FocalPolity?.Id)
            .ToList();
        List<string> lines = ["Known Polities", string.Empty];

        if (visibleForeignPolities.Count == 0)
        {
            lines.Add(" No known foreign polities.");
            return lines;
        }

        int selectedIndex = NormalizeSelection(uiState, WatchViewType.KnownPolities, visibleForeignPolities.Count);
        for (int index = 0; index < visibleForeignPolities.Count; index++)
        {
            Polity polity = visibleForeignPolities[index];
            string regionName = lookup.TryGetRegion(polity.RegionId, out Region? region) && region is not null && knowledge.IsRegionKnown(region.Id)
                ? region.Name
                : "Unknown";
            string marker = index == selectedIndex ? ">" : " ";
            lines.Add(
                $"{marker} {polity.Name,-22} {WatchInspectionData.DescribeStage(polity.Stage),-16} pop {polity.Population,4} settlements {knowledge.GetVisibleSettlementsForPolity(polity).Count,2} {regionName}");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildWorldOverviewLines(World world, WatchKnowledgeSnapshot knowledge, WorldLookup lookup)
    {
        Polity? focalPolity = knowledge.FocalPolity;
        PrehistoryCheckpointOutcome? checkpoint = world.PrehistoryRuntime.LastCheckpointOutcome;
        string checkpointLabel = checkpoint?.Kind.ToString() ?? "Pending";
        string? checkpointSummary = checkpoint?.Summary;
        List<string> lines =
        [
            "World Overview",
            string.Empty,
            $" Year: {world.Time.Year} ({world.Time.Season})",
            $" Prehistory Preset: {world.StartupAgeConfiguration.Preset}",
            $" Prehistory Checkpoint: {checkpointLabel}{(string.IsNullOrEmpty(checkpointSummary) ? string.Empty : $" ({checkpointSummary})")}",
            $" Live Chronicle Starts: {(world.LiveChronicleStartYear.HasValue ? $"y{world.LiveChronicleStartYear} m{world.LiveChronicleStartMonth}" : "not started")}",
            $" Known Regions: {knowledge.KnownRegions.Count}",
            $" Known Species: {knowledge.KnownSpecies.Count}",
            $" Known Polities: {Math.Max(0, knowledge.KnownPolities.Count - 1)}",
            $" Focal Polity: {(focalPolity?.Name ?? "None")}",
            $" Current Known Position: {(knowledge.CurrentRegion?.Name ?? "Unknown region")}",
            string.Empty,
            " Recent Visible Major Events:"
        ];

        IReadOnlyList<WorldEvent> visibleEvents = knowledge.GetVisibleMajorEvents(world, limit: 5);
        if (visibleEvents.Count == 0)
        {
            lines.Add("  None yet inside the current knowledge horizon.");
        }
        else
        {
            foreach (WorldEvent worldEvent in visibleEvents)
            {
                lines.Add($"  {worldEvent.HistoricalText}");
            }
        }

        lines.Add(string.Empty);
        lines.Add(" Known Regions With Settlements:");
        foreach ((Region region, int count) in knowledge.KnownRegions
                     .Select(region => (region, count: lookup.GetSettlementsInRegion(region.Id).Count))
                     .Where(entry => entry.count > 0)
                     .OrderByDescending(entry => entry.count)
                     .ThenBy(entry => entry.region.Name, StringComparer.Ordinal)
                     .Take(5))
        {
            lines.Add($"  {region.Name} - {count} settlement(s) - {region.Biome} - fert {region.Fertility:F2}");
        }

        if (lines[^1] == " Known Regions With Settlements:")
        {
            lines.Add("  None");
        }

        lines.Add(string.Empty);
        lines.Add(" Strategic Material Hotspots:");
        foreach ((Region region, string hotspot) in knowledge.KnownRegions
                     .Select(region => (region, hotspot: DescribeStrategicHotspot(region)))
                     .Where(entry => !string.Equals(entry.hotspot, "none", StringComparison.Ordinal))
                     .OrderByDescending(entry => entry.region.Fertility + entry.region.WaterAvailability)
                     .ThenBy(entry => entry.region.Name, StringComparer.Ordinal)
                     .Take(5))
        {
            lines.Add($"  {region.Name} - {hotspot}");
        }

        if (lines[^1] == " Strategic Material Hotspots:")
        {
            lines.Add("  None known.");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildFocalSelectionLines(World world, WatchUiState uiState)
    {
        int candidateCount = world.PlayerEntryCandidates.Count;
        List<string> lines =
        [
            "Focal Selection",
            string.Empty,
            BuildFocalSelectionHeader(world, candidateCount)
        ];

        if (candidateCount == 0)
        {
            lines.Add(string.Empty);
            lines.Add(" No viable player-entry candidates were found.");
            return lines;
        }

        int selectedIndex = Math.Clamp(uiState.GetSelectedIndex(WatchViewType.FocalSelection), 0, candidateCount - 1);
        uiState.SetSelectedIndex(WatchViewType.FocalSelection, selectedIndex);
        lines.Add(string.Empty);
        lines.Add(" Candidate List:");
        for (int index = 0; index < candidateCount; index++)
        {
            PlayerEntryCandidateSummary candidate = world.PlayerEntryCandidates[index];
            string marker = index == selectedIndex ? ">" : " ";
            lines.Add($"{marker} {candidate.PolityName,-28} {candidate.SpeciesName,-16} {candidate.HomeRegionName,-16}");
        }

        PlayerEntryCandidateSummary selected = world.PlayerEntryCandidates[selectedIndex];
        lines.Add(string.Empty);
        lines.Add(" Start Summary:");
        lines.Add($" {selected.PolityName}");
        lines.Add($" {selected.SpeciesName} of {selected.HomeRegionName}");
        lines.Add($" Age {selected.PolityAge} | Settlements {selected.SettlementCount} | Population {selected.PopulationBand}");
        lines.Add($" {selected.MaturityBand.ToDisplayLabel()} | {selected.SubsistenceStyle} | {selected.CurrentCondition}");
        lines.Add($" Network: {selected.SettlementProfile}");
        lines.Add($" Region: {selected.RegionalProfile}");
        lines.Add($" Lineage: {selected.LineageProfile}");
        if (!string.IsNullOrWhiteSpace(selected.QualificationReason))
        {
            lines.Add($" Qualification: {selected.QualificationReason}");
        }

        if (!string.IsNullOrWhiteSpace(selected.EvidenceSentence))
        {
            lines.Add($" Evidence: {selected.EvidenceSentence}");
        }

        lines.Add(string.Empty);
        lines.Add($" Discoveries: {selected.DiscoverySummary}");
        lines.Add($" Learned: {selected.LearnedSummary}");
        lines.Add($" History: {selected.RecentHistoricalNote}");
        lines.Add($" Pressure / Opportunity: {selected.DefiningPressureOrOpportunity}");
        if (selected.SafeStrengths.Count > 0)
        {
            lines.Add($" Strengths: {string.Join(", ", selected.SafeStrengths)}");
        }

        if (selected.SafeWarnings.Count > 0)
        {
            lines.Add($" Warnings: {string.Join(", ", selected.SafeWarnings)}");
        }

        if (uiState.ShowDiagnostics)
        {
            lines.Add(string.Empty);
            lines.Add(" Diagnostics:");
            PrehistoryCheckpointOutcome? checkpoint = world.PrehistoryRuntime.LastCheckpointOutcome;
            string checkpointLabel = checkpoint?.Kind.ToString() ?? "Pending";
            string? checkpointSummary = checkpoint?.Summary;
            string checkpointLine = string.IsNullOrEmpty(checkpointSummary)
                ? $"  Checkpoint: {checkpointLabel}"
                : $"  Checkpoint: {checkpointLabel} ({checkpointSummary})";
            lines.Add(checkpointLine);
            lines.Add($"  Readiness: bio {world.WorldReadinessReport.GetCategory(WorldReadinessCategoryKind.BiologicalReadiness).Status} | social {world.WorldReadinessReport.GetCategory(WorldReadinessCategoryKind.SocialEmergenceReadiness).Status} | structure {world.WorldReadinessReport.GetCategory(WorldReadinessCategoryKind.WorldStructureReadiness).Status} | candidates {world.WorldReadinessReport.GetCategory(WorldReadinessCategoryKind.CandidateReadiness).Status}");
            lines.Add($"  Fallback Candidate: {(selected.IsFallbackCandidate ? "yes" : "no")}");
            if (world.WorldReadinessReport.FailureReasons.Count > 0)
            {
                lines.Add($"  Readiness Gaps: {string.Join(", ", world.WorldReadinessReport.FailureReasons)}");
            }

            foreach (string diagnostic in world.StartupDiagnostics.Take(4))
            {
                lines.Add($"  {diagnostic}");
            }
        }

        return lines;
    }

    private static string BuildFocalSelectionHeader(World world, int surfacedCandidateCount)
    {
        int viableCandidateCount = world.WorldReadinessReport.CandidatePoolSummary.TotalViableCandidatesDiscovered;
        if (viableCandidateCount > surfacedCandidateCount)
        {
            return $" World age {world.Time.Year} | {world.StartupAgeConfiguration.Preset} | {surfacedCandidateCount} surfaced of {viableCandidateCount} viable starts";
        }

        return $" World age {world.Time.Year} | {world.StartupAgeConfiguration.Preset} | {surfacedCandidateCount} candidate {(surfacedCandidateCount == 1 ? "start" : "starts")}";
    }

    private static IReadOnlyList<string> BuildRegionDetailLines(World world, WatchKnowledgeSnapshot knowledge, int? regionId, WorldLookup lookup)
    {
        if (!regionId.HasValue || !knowledge.IsRegionKnown(regionId.Value) || !lookup.TryGetRegion(regionId.Value, out Region? region) || region is null)
        {
            return ["Region detail unavailable."];
        }

        List<string> lines =
        [
            "Region Detail",
            string.Empty,
            $" Name: {region.Name}",
            $" Biome: {region.Biome}",
            $" Fertility: {region.Fertility:F2}",
            $" Water Availability: {region.WaterAvailability:F2}",
            $" Environment: {DescribeEnvironment(region)}",
            $" Ecology: Plant {region.PlantBiomass:F0}/{region.MaxPlantBiomass:F0} | Animal {region.AnimalBiomass:F0}/{region.MaxAnimalBiomass:F0}",
            $" Extractable Resources: {DescribeExtractableResources(region)}",
            $" Connected Known Regions: {region.ConnectedRegionIds.Count(knowledge.IsRegionKnown)}",
            $" Managed Food Sources: {DescribeManagedRegionSources(lookup.GetSettlementsInRegion(region.Id), knowledge, world)}",
            $" Local Production: {DescribeRegionProduction(lookup.GetSettlementsInRegion(region.Id))}",
            string.Empty,
            " Discovered Resources:"
        ];

        IReadOnlyList<CulturalDiscovery> resourceDiscoveries = knowledge.GetRegionDiscoveries(region.Id)
            .Where(discovery => discovery.Category is CulturalDiscoveryCategory.Resource or CulturalDiscoveryCategory.Environment or CulturalDiscoveryCategory.Geography)
            .ToList();
        if (resourceDiscoveries.Count == 0)
        {
            lines.Add("  None recorded.");
        }
        else
        {
            foreach (CulturalDiscovery discovery in resourceDiscoveries)
            {
                lines.Add($"  {discovery.Summary}");
            }
        }

        lines.Add(string.Empty);
        lines.Add(" Settlements:");
        AppendSettlementLines(lines, lookup.GetSettlementsInRegion(region.Id), lookup);

        lines.Add(string.Empty);
        lines.Add(" Known Species Here:");
        List<(Species Species, RegionSpeciesPopulation Population)> visibleSpecies = region.SpeciesPopulations
            .Where(population => population.PopulationCount > 0 && knowledge.IsSpeciesKnown(population.SpeciesId))
            .Select(population => (world.Species.First(species => species.Id == population.SpeciesId), population))
            .OrderByDescending(entry => entry.Item2.PopulationCount)
            .ThenBy(entry => entry.Item1.Name, StringComparer.Ordinal)
            .Take(12)
            .ToList();
        if (visibleSpecies.Count == 0)
        {
            lines.Add("  None known.");
        }
        else
        {
            foreach ((Species species, RegionSpeciesPopulation population) in visibleSpecies)
            {
                lines.Add($"  {species.Name} - {DescribeTrophicRole(species.TrophicRole)} - pop {population.PopulationCount} - fit {population.HabitatSuitability:F2}");
            }
        }

        lines.Add(string.Empty);
        lines.Add(" Known Polity Presence:");
        List<Settlement> settlements = lookup.GetSettlementsInRegion(region.Id).ToList();
        List<string> polityNames = settlements
            .Select(settlement => lookup.TryGetPolity(settlement.PolityId, out Polity? polity) && polity is not null && knowledge.IsPolityKnown(polity.Id)
                ? polity.Name
                : string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        if (polityNames.Count == 0)
        {
            lines.Add("  None known.");
        }
        else
        {
            foreach (string polityName in polityNames)
            {
                lines.Add($"  {polityName}");
            }
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildSpeciesDetailLines(World world, WatchKnowledgeSnapshot knowledge, int? speciesId)
    {
        if (!speciesId.HasValue || !knowledge.IsSpeciesKnown(speciesId.Value))
        {
            return ["Species detail unavailable."];
        }

        Species species = knowledge.TryGetKnownSpecies(speciesId.Value)!;
        IReadOnlyList<(Region Region, RegionSpeciesPopulation Population)> knownRegionalPopulations = knowledge.GetKnownRegionalPopulations(world, species.Id);
        double maxKnownDivergence = knownRegionalPopulations.Count == 0 ? 0.0 : knownRegionalPopulations.Max(entry => entry.Population.DivergenceScore);
        int adaptedKnownRegions = knownRegionalPopulations.Count(entry => entry.Population.RegionAdaptationRecorded);
        int divergingKnownRegions = knownRegionalPopulations.Count(entry => entry.Population.DivergenceScore >= 1.10 || entry.Population.IsolationSeasons >= 8);
        int founderScaleRegions = knownRegionalPopulations.Count(entry => entry.Population.PopulationCount <= Math.Max(4, entry.Population.CarryingCapacity / 12));
        int visibleMinorMutations = knownRegionalPopulations.Sum(entry => entry.Population.MinorMutationCount);
        int visibleMajorMutations = knownRegionalPopulations.Sum(entry => entry.Population.MajorMutationCount);
        int highFitKnownRegions = knownRegionalPopulations.Count(entry => entry.Population.HabitatSuitability >= 0.80);
        string lineageSummary = BuildSpeciesLineageSummary(knowledge, world, species);
        List<string> knownDietTargets = species.DietSpeciesIds
            .Select(knowledge.TryGetKnownSpecies)
            .Where(target => target is not null)
            .Select(target => target!.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        double domesticationInterest = knowledge.FocalPolity?.DomesticationInterestBySpecies.TryGetValue(species.Id, out double value) == true
            ? value
            : 0.0;
        string domesticationStatus = DescribeDomesticationStatus(knowledge, world, species);

        List<string> lines =
        [
            "Species Detail",
            string.Empty,
            $" Name: {species.Name}",
            $" Role: {DescribeTrophicRole(species.TrophicRole)}",
            $" Size: {DescribeSpeciesSize(species)}",
            $" Sapient: {(species.IsSapient ? "Yes" : "No")}",
            $" Intelligence: {species.Intelligence:F2}",
            $" Cooperation: {species.Cooperation:F2}",
            $" Habitat Preferences: fertility {species.FertilityPreference:F2} | water {species.WaterPreference:F2}",
            $" Biomass Affinity: plant {species.PlantBiomassAffinity:F2} | animal {species.AnimalBiomassAffinity:F2}",
            $" Movement: migration {species.MigrationCapability:F2} | expansion {species.ExpansionPressure:F2}",
            $" Hunting: yield {species.MeatYield:F1} | difficulty {species.HuntingDifficulty:F2} | danger {species.HuntingDanger:F2}",
            $" Food Safety: {DescribeFoodSafety(knowledge, species)}",
            $" Domestication Relevance: {DescribeDomesticationRelevance(domesticationInterest, species.DomesticationAffinity)}",
            $" Domestication Status: {domesticationStatus}",
            $" Lineage: {lineageSummary}",
            $" Origin: {DescribeSpeciesOrigin(knowledge, world, species)}",
            $" Visible Divergence: max {maxKnownDivergence:F2} | adapted known regions {adaptedKnownRegions}",
            $" Visible Range: known regions {knownRegionalPopulations.Count} | high-fit known regions {highFitKnownRegions}",
            $" Mutation Signals: minor {visibleMinorMutations} | major {visibleMajorMutations} | diverging regions {divergingKnownRegions} | founder-scale regions {founderScaleRegions}",
            string.Empty,
            $" Known Diet Targets: {(knownDietTargets.Count == 0 ? "Not yet known" : string.Join(", ", knownDietTargets))}",
            $" Species Discoveries: {DescribeDiscoverySummaries(knowledge.GetSpeciesDiscoveries(species.Id))}",
            string.Empty,
            " Known Regions Present:"
        ];

        if (knownRegionalPopulations.Count == 0)
        {
            lines.Add("  None currently observed.");
        }
        else
        {
            foreach ((Region region, RegionSpeciesPopulation population) in knownRegionalPopulations.Take(10))
            {
                lines.Add(
                    $"  {region.Name} - pop {population.PopulationCount} - fit {population.HabitatSuitability:F2} - cap {population.CarryingCapacity} - divergence {population.DivergenceScore:F2}{DescribePopulationFlags(population)}");
            }
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildPolityDetailLines(World world, WatchKnowledgeSnapshot knowledge, int? polityId, WorldLookup lookup)
    {
        if (!polityId.HasValue || !knowledge.IsPolityKnown(polityId.Value) || !lookup.TryGetPolity(polityId.Value, out Polity? polity) || polity is null)
        {
            return ["Polity detail unavailable."];
        }

        bool isFocalPolity = polity.Id == knowledge.FocalPolity?.Id;
        string speciesName = lookup.TryGetSpecies(polity.SpeciesId, out Species? species) && species is not null && knowledge.IsSpeciesKnown(species.Id)
            ? species.Name
            : "Unknown";
        string regionName = lookup.TryGetRegion(polity.RegionId, out Region? region) && region is not null && knowledge.IsRegionKnown(region.Id)
            ? region.Name
            : "Unknown";
        double annualFoodRatio = polity.AnnualFoodNeeded <= 0 ? 1.0 : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;
        int managedHerdCount = polity.Settlements.Sum(settlement => settlement.ManagedHerds.Count);
        int cultivatedCropCount = polity.Settlements.Sum(settlement => settlement.CultivatedCrops.Count);
        string visibleSpecialization = DescribeLeadingProductionSettlements(knowledge.GetVisibleSettlementsForPolity(polity));
        IReadOnlyList<Settlement> visibleSettlements = knowledge.GetVisibleSettlementsForPolity(polity);

        List<string> lines =
        [
            "Polity Detail",
            string.Empty,
            $" Name: {polity.Name}",
            $" Species: {speciesName}",
            $" Stage: {WatchInspectionData.DescribeStage(polity.Stage)}",
            $" Population: {polity.Population}",
            $" Years Since Founded: {polity.YearsSinceFounded}",
            $" Current Region: {regionName}",
            $" Settlement Count: {knowledge.GetVisibleSettlementsForPolity(polity).Count}",
            $" Food Situation: {ChronicleTextFormatter.DescribeFoodState(polity)} | stores {polity.FoodStores:F0} | annual ratio {annualFoodRatio:F2}",
            $" Managed Food This Year: {polity.AnnualFoodManaged:F0}",
            $" Managed Sources: Herds {managedHerdCount} | Crops {cultivatedCropCount}",
            $" Visible Material Strength: {visibleSpecialization}",
            $" Economy Needs: {DescribeEconomyNeeds(visibleSettlements)}",
            $" Trade Goods: {DescribeEconomySignals(visibleSettlements, EconomySummaryLabel.TradeGood)}",
            $" Locally Common: {DescribeEconomySignals(visibleSettlements, EconomySummaryLabel.LocallyCommon)}",
            $" Major Pressures: Migration {polity.MigrationPressure:F2} | Fragmentation {polity.FragmentationPressure:F2}",
            string.Empty,
            " Visible Settlements:"
        ];

        AppendSettlementLines(lines, visibleSettlements, lookup);
        lines.Add(string.Empty);

        if (isFocalPolity)
        {
            lines.Add($" Discoveries: {DescribeDiscoveries(polity)}");
            lines.Add($" Learned: {DescribeAdvancements(polity)}");
        }
        else
        {
            lines.Add(" Discoveries: Not yet known");
            lines.Add(" Learned: Not yet known");
        }

        return lines;
    }

    private static int NormalizeSelection(WatchUiState uiState, WatchViewType view, int count)
    {
        int selectedIndex = Math.Clamp(uiState.GetSelectedIndex(view), 0, Math.Max(0, count - 1));
        uiState.SetSelectedIndex(view, selectedIndex);
        return selectedIndex;
    }

    private static void AppendSettlementLines(List<string> lines, IEnumerable<Settlement> settlements, WorldLookup lookup)
    {
        List<Settlement> orderedSettlements = settlements
            .OrderBy(settlement => settlement.Name, StringComparer.Ordinal)
            .ToList();
        if (orderedSettlements.Count == 0)
        {
            lines.Add("  None");
            return;
        }

        foreach (Settlement settlement in orderedSettlements)
        {
            string settlementRegion = lookup.TryGetRegion(settlement.RegionId, out Region? region) && region is not null
                ? region.Name
                : $"Region {settlement.RegionId}";
            string ownerName = lookup.TryGetPolity(settlement.PolityId, out Polity? polity) && polity is not null
                ? polity.Name
                : $"Polity {settlement.PolityId}";
            lines.Add(
                $"  {settlement.Name} - {ownerName} - {settlementRegion} - age {settlement.YearsEstablished} - cultivated {settlement.CultivatedLand:F1} - herds {settlement.ManagedHerds.Count} - crops {settlement.CultivatedCrops.Count} - food {settlement.FoodState} ({settlement.FoodBalance:F1}) - aid ytd {settlement.AidReceivedThisYear:F1} - mats {DescribeSettlementMaterialFocus(settlement)} - econ {DescribeSettlementEconomyStatus(settlement)}");
        }
    }

    private static string DescribeRegionResourceSummary(WatchKnowledgeSnapshot knowledge, int regionId)
    {
        IReadOnlyList<CulturalDiscovery> discoveries = knowledge.GetRegionDiscoveries(regionId)
            .Where(discovery => discovery.Category == CulturalDiscoveryCategory.Resource)
            .ToList();
        return discoveries.Count == 0
            ? "resources unknown"
            : $"resources {string.Join(", ", discoveries.Select(discovery => discovery.Summary).Take(2))}";
    }

    private static string DescribeExtractableResources(Region region)
    {
        List<string> resources = [];
        foreach ((MaterialType materialType, double abundance) in new[]
                 {
                     (MaterialType.Wood, region.WoodAbundance),
                     (MaterialType.Stone, region.StoneAbundance),
                     (MaterialType.Clay, region.ClayAbundance),
                     (MaterialType.Fiber, region.FiberAbundance),
                     (MaterialType.Salt, region.SaltAbundance),
                     (MaterialType.CopperOre, region.CopperOreAbundance),
                     (MaterialType.IronOre, region.IronOreAbundance)
                 })
        {
            if (abundance < 0.45)
            {
                continue;
            }

            resources.Add(MaterialEconomySystem.GetMaterialLabel(materialType));
        }

        return resources.Count == 0 ? "no strong material edge" : string.Join(", ", resources);
    }

    private static string DescribeRegionProduction(IEnumerable<Settlement> settlements)
    {
        List<string> production = settlements
            .SelectMany(settlement => settlement.SpecializationTags)
            .GroupBy(tag => tag)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key.ToString(), StringComparer.Ordinal)
            .Select(group => MaterialEconomySystem.DescribeSpecialization(group.Key))
            .Take(3)
            .ToList();

        return production.Count == 0 ? "none established" : string.Join(", ", production);
    }

    private static string DescribeTopMaterialStates(IEnumerable<Settlement> settlements, MaterialPressureState targetState)
    {
        List<string> materialStates = Enum.GetValues<MaterialType>()
            .Select(materialType => new
            {
                Material = materialType,
                Count = settlements.Count(settlement => settlement.MaterialPressureStates[materialType] == targetState)
            })
            .Where(entry => entry.Count > 0)
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.Material.ToString(), StringComparer.Ordinal)
            .Select(entry => $"{MaterialEconomySystem.GetMaterialLabel(entry.Material)} {entry.Count}")
            .Take(3)
            .ToList();

        return materialStates.Count == 0 ? "none" : string.Join(", ", materialStates);
    }

    private static string DescribeEconomyNeeds(IEnumerable<Settlement> settlements)
    {
        List<string> needs = Enum.GetValues<MaterialType>()
            .Select(materialType => new
            {
                Material = materialType,
                Score = settlements.Sum(settlement =>
                    (settlement.MaterialPressureStates[materialType] == MaterialPressureState.Deficit ? 1.0 : 0.0)
                    + (settlement.IsHighlyValued(materialType) ? 0.8 : 0.0)
                    + settlement.MaterialValueScores[materialType])
            })
            .Where(entry => entry.Score >= 1.2)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Material.ToString(), StringComparer.Ordinal)
            .Select(entry =>
            {
                Settlement? settlement = settlements
                    .OrderByDescending(candidate => candidate.MaterialValueScores[entry.Material])
                    .FirstOrDefault();
                return settlement is null
                    ? MaterialEconomySystem.GetMaterialLabel(entry.Material)
                    : $"{MaterialEconomySystem.GetMaterialLabel(entry.Material)} ({MaterialEconomySystem.DescribeSummaryLabels(settlement, entry.Material)})";
            })
            .Take(3)
            .ToList();

        return needs.Count == 0 ? "none urgent" : string.Join(", ", needs);
    }

    private static string DescribeEconomySignals(IEnumerable<Settlement> settlements, EconomySummaryLabel label)
    {
        List<string> signals = Enum.GetValues<MaterialType>()
            .Select(materialType => new
            {
                Material = materialType,
                Count = settlements.Count(settlement => MaterialEconomySystem.GetSummaryLabels(settlement, materialType).Contains(label))
            })
            .Where(entry => entry.Count > 0)
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.Material.ToString(), StringComparer.Ordinal)
            .Select(entry => $"{MaterialEconomySystem.GetMaterialLabel(entry.Material)} {entry.Count}")
            .Take(3)
            .ToList();

        return signals.Count == 0 ? "none" : string.Join(", ", signals);
    }

    private static string DescribeLeadingProductionSettlements(IEnumerable<Settlement> settlements)
    {
        List<string> leaders = settlements
            .Select(settlement => new
            {
                Settlement = settlement,
                Output = settlement.MaterialProducedThisYear.OrderByDescending(entry => entry.Value).FirstOrDefault()
            })
            .Where(entry => entry.Output.Value > 0.5)
            .OrderByDescending(entry => entry.Output.Value)
            .ThenBy(entry => entry.Settlement.Name, StringComparer.Ordinal)
            .Select(entry => $"{entry.Settlement.Name} ({MaterialEconomySystem.GetMaterialLabel(entry.Output.Key)})")
            .Take(3)
            .ToList();

        return leaders.Count == 0 ? "none yet" : string.Join(", ", leaders);
    }

    private static string DescribeMaterialCapabilityStatus(IEnumerable<Settlement> settlements)
    {
        List<Settlement> settlementList = settlements.ToList();
        if (settlementList.Count == 0)
        {
            return "unknown";
        }

        double averageTools = settlementList.Average(settlement => settlement.ResolveToolEffectiveness());
        double averageStorage = settlementList.Average(settlement => settlement.ResolveStorageMultiplier());
        double preservedFood = settlementList.Sum(settlement => settlement.GetMaterialStockpile(MaterialType.PreservedFood));
        return $"tools {averageTools:F2} | storage {averageStorage:F2} | preserved {preservedFood:F0}";
    }

    private static string DescribeVisibleRegionSpecialization(IEnumerable<Settlement> settlements)
    {
        List<string> tags = settlements
            .SelectMany(settlement => settlement.SpecializationTags)
            .Distinct()
            .Select(MaterialEconomySystem.DescribeSpecialization)
            .Take(2)
            .ToList();

        return tags.Count == 0 ? "no known craft center" : $"known for {string.Join(", ", tags)}";
    }

    private static string DescribeStrategicHotspot(Region region)
    {
        List<string> hotspotMaterials = [];
        if (region.WoodAbundance >= 0.75) hotspotMaterials.Add("timber");
        if (region.ClayAbundance >= 0.72) hotspotMaterials.Add("clay");
        if (region.SaltAbundance >= 0.68) hotspotMaterials.Add("salt");
        if (Math.Max(region.CopperOreAbundance, region.IronOreAbundance) >= 0.58) hotspotMaterials.Add("ore");

        return hotspotMaterials.Count == 0 ? "none" : string.Join(", ", hotspotMaterials);
    }

    private static string DescribeSettlementMaterialFocus(Settlement settlement)
    {
        KeyValuePair<MaterialType, double> dominantOutput = settlement.MaterialProducedThisYear
            .OrderByDescending(entry => entry.Value)
            .FirstOrDefault();
        if (dominantOutput.Value <= 0.5)
        {
            return settlement.SpecializationTags.Count == 0
                ? "balanced"
                : string.Join(", ", settlement.SpecializationTags.Select(MaterialEconomySystem.DescribeSpecialization).Take(2));
        }

        return MaterialEconomySystem.GetMaterialLabel(dominantOutput.Key);
    }

    private static string DescribeSettlementEconomyStatus(Settlement settlement)
    {
        MaterialType? highlightedMaterial = settlement.DominantProductionFocusMaterial;
        if (!highlightedMaterial.HasValue && settlement.HighlyValuedMaterials.Count > 0)
        {
            highlightedMaterial = settlement.HighlyValuedMaterials
                .OrderByDescending(materialType => settlement.MaterialValueScores[materialType])
                .First();
        }

        if (!highlightedMaterial.HasValue && settlement.TradeGoodMaterials.Count > 0)
        {
            highlightedMaterial = settlement.TradeGoodMaterials
                .OrderByDescending(materialType => settlement.MaterialExternalPullReadiness[materialType])
                .First();
        }

        if (!highlightedMaterial.HasValue)
        {
            return "Stable";
        }

        return $"{MaterialEconomySystem.GetMaterialLabel(highlightedMaterial.Value)} ({MaterialEconomySystem.DescribeSummaryLabels(settlement, highlightedMaterial.Value)})";
    }

    private static string DescribeEnvironment(Region region)
    {
        string fertility = region.Fertility >= 0.70
            ? "fertile"
            : region.Fertility >= 0.45
                ? "mixed"
                : "lean";
        string water = region.WaterAvailability >= 0.70
            ? "wet"
            : region.WaterAvailability >= 0.45
                ? "temperate"
                : "dry";
        return $"{fertility}, {water}, {region.Biome}";
    }

    private static string DescribeFoodSafety(WatchKnowledgeSnapshot knowledge, Species species)
    {
        Polity? polity = knowledge.FocalPolity;
        if (polity is null)
        {
            return "Unknown";
        }

        if (polity.KnownToxicSpeciesIds.Contains(species.Id))
        {
            return "Known toxic";
        }

        if (polity.KnownEdibleSpeciesIds.Contains(species.Id))
        {
            return "Known edible";
        }

        return species.IsToxicToEat ? "Unknown safety" : "Edible or unknown";
    }

    private static string DescribeDomesticationRelevance(double domesticationInterest, double domesticationAffinity)
    {
        if (domesticationInterest > 0.15)
        {
            return $"active interest {domesticationInterest:F2}";
        }

        return domesticationAffinity >= 0.45
            ? $"promising affinity {domesticationAffinity:F2}"
            : $"low affinity {domesticationAffinity:F2}";
    }

    private static string DescribeDomesticationStatus(WatchKnowledgeSnapshot knowledge, World world, Species species)
    {
        Polity? polity = knowledge.FocalPolity;
        if (polity is null)
        {
            return "Unknown";
        }

        bool managedHerd = polity.Settlements.Any(settlement => settlement.ManagedHerds.Any(herd => herd.BaseSpeciesId == species.Id));
        bool cultivatedCrop = polity.Settlements.Any(settlement => settlement.CultivatedCrops.Any(crop => crop.BaseSpeciesId == species.Id));
        bool candidateKnown = polity.HasDiscovery($"species-domestication-candidate:{species.Id}");
        bool cropKnown = polity.HasDiscovery($"species-cultivable:{species.Id}");

        if (managedHerd)
        {
            return "managed herd";
        }

        if (cultivatedCrop)
        {
            return "cultivated crop";
        }

        if (candidateKnown)
        {
            return "candidate known";
        }

        if (cropKnown)
        {
            return "cultivable plant known";
        }

        return species.TrophicRole == TrophicRole.Producer ? "wild plant" : "wild";
    }

    private static string DescribeManagedRegionSources(IEnumerable<Settlement> settlements, WatchKnowledgeSnapshot knowledge, World world)
    {
        List<string> parts = [];
        foreach (Settlement settlement in settlements)
        {
            foreach (ManagedHerd herd in settlement.ManagedHerds)
            {
                string speciesName = world.Species.FirstOrDefault(species => species.Id == herd.BaseSpeciesId)?.Name ?? $"Species {herd.BaseSpeciesId}";
                parts.Add($"{speciesName} herds");
            }

            foreach (CultivatedCrop crop in settlement.CultivatedCrops)
            {
                parts.Add(crop.CropName);
            }
        }

        if (parts.Count == 0)
        {
            return "none visible";
        }

        return string.Join(", ", parts.Distinct(StringComparer.Ordinal).Take(3));
    }

    private static string DescribeDiscoveries(Polity polity)
        => polity.Discoveries.Count == 0
            ? "None"
            : string.Join(", ", polity.Discoveries
                .OrderBy(discovery => discovery.Category)
                .ThenBy(discovery => discovery.Summary, StringComparer.Ordinal)
                .Select(discovery => discovery.Summary));

    private static string DescribeAdvancements(Polity polity)
        => polity.Advancements.Count == 0
            ? "None"
            : string.Join(", ", polity.Advancements
                .OrderBy(id => id)
                .Select(id => AdvancementCatalog.Get(id).Name));

    private static string DescribeDiscoverySummaries(IReadOnlyList<CulturalDiscovery> discoveries)
        => discoveries.Count == 0
            ? "None"
            : string.Join(", ", discoveries.Select(discovery => discovery.Summary));

    private static string BuildSpeciesLineageSummary(WatchKnowledgeSnapshot knowledge, World world, Species species)
    {
        string parent = species.ParentSpeciesId.HasValue
            ? knowledge.TryGetKnownSpecies(species.ParentSpeciesId.Value)?.Name ?? $"Unknown ancestor #{species.ParentSpeciesId.Value}"
            : "ancestral stock";
        int knownDescendants = species.DescendantSpeciesIds.Count(descendantId => knowledge.IsSpeciesKnown(descendantId));
        return $"{parent}; descendants known {knownDescendants}; root #{species.RootAncestorSpeciesId}";
    }

    private static string DescribeSpeciesOrigin(WatchKnowledgeSnapshot knowledge, World world, Species species)
    {
        if (!species.ParentSpeciesId.HasValue || !species.OriginRegionId.HasValue)
        {
            return "world generation";
        }

        string regionName = knowledge.TryGetKnownRegion(species.OriginRegionId.Value)?.Name
            ?? world.Regions.FirstOrDefault(region => region.Id == species.OriginRegionId.Value)?.Name
            ?? $"Region {species.OriginRegionId.Value}";
        return $"{regionName} y{species.OriginYear} m{species.OriginMonth} ({species.OriginCause ?? "regional split"})";
    }

    private static string DescribePopulationFlags(RegionSpeciesPopulation population)
    {
        List<string> flags = [];
        if (population.FounderKind is not null && population.PopulationCount <= Math.Max(6, population.CarryingCapacity / 10))
        {
            flags.Add("founder");
        }

        if (population.RegionAdaptationRecorded)
        {
            flags.Add("adapted");
        }

        if (population.DivergenceScore >= 1.10)
        {
            flags.Add("diverging");
        }

        return flags.Count == 0
            ? string.Empty
            : $" [{string.Join(", ", flags)}]";
    }

    private static string DescribeTrophicRole(TrophicRole role)
        => role switch
        {
            TrophicRole.Apex => "Apex",
            TrophicRole.Predator => "Predator",
            TrophicRole.Omnivore => "Omnivore",
            TrophicRole.Herbivore => "Herbivore",
            _ => "Producer"
        };

    private static string DescribeSpeciesSize(Species species)
        => species.MeatYield switch
        {
            >= 22 => "Large",
            >= 10 => "Medium",
            _ => "Small"
        };

    private static string TruncateToWidth(string text, int width)
    {
        if (text.Length <= width)
        {
            return text;
        }

        if (width <= 3)
        {
            return text[..width];
        }

        return $"{text[..(width - 3)]}...";
    }
}
