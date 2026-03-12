using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public static class WatchScreenBuilder
{
    public static IReadOnlyList<string> BuildBodyLines(World world, ChronicleFocus focus, WatchUiState uiState)
    {
        WorldLookup lookup = new(world);
        return uiState.ActiveView switch
        {
            WatchViewType.MyPolity => BuildMyPolityLines(WatchInspectionData.ResolveFocusedPolity(world, focus), lookup),
            WatchViewType.CurrentRegion => BuildCurrentRegionLines(world, focus, lookup),
            WatchViewType.KnownRegions => BuildKnownRegionsLines(world, focus, uiState, lookup),
            WatchViewType.RegionDetail => BuildRegionDetailLines(world, uiState.SelectedRegionId, lookup),
            WatchViewType.KnownSpecies => BuildKnownSpeciesLines(world, focus, uiState),
            WatchViewType.SpeciesDetail => BuildSpeciesDetailLines(world, uiState.SelectedSpeciesId),
            WatchViewType.KnownPolities => BuildKnownPolitiesLines(world, focus, uiState, lookup),
            WatchViewType.PolityDetail => BuildPolityDetailLines(world, uiState.SelectedPolityId, lookup),
            WatchViewType.WorldOverview => BuildWorldOverviewLines(world, lookup),
            _ => ["The chronicle is quiet."]
        };
    }

    public static List<string> BuildFooterLines(WatchUiState uiState, int width)
    {
        string border = new('=', width);
        string controls = " Space Pause  Tab Cycle  1-7 Views  Up/Down Move  Enter Inspect  Esc Back ";
        string context = uiState.ActiveView switch
        {
            WatchViewType.Chronicle => " Chronicle view keeps newest entries at the top. Up/Down scrolls through stored history. ",
            WatchViewType.KnownRegions => " Known Regions currently includes settlement regions, the current center, and direct neighbors. ",
            WatchViewType.KnownSpecies => " Known Species is derived from species present in currently known regions. ",
            WatchViewType.KnownPolities => " Known Polities is derived from active polities in currently known regions. ",
            _ => " Observation only: these screens inspect the world state without issuing orders. "
        };

        return
        [
            border,
            controls,
            context
        ];
    }

    public static int ResolveViewportOffset(int rawLineCount, int viewportHeight, WatchUiState uiState, int maxOffset)
    {
        if (rawLineCount <= viewportHeight)
        {
            return 0;
        }

        if (uiState.ActiveView is WatchViewType.KnownRegions or WatchViewType.KnownSpecies or WatchViewType.KnownPolities)
        {
            int selectedIndex = uiState.GetSelectedIndex(uiState.ActiveView);
            return Math.Clamp(selectedIndex - (viewportHeight / 2), 0, maxOffset);
        }

        return Math.Clamp(uiState.GetScrollOffset(uiState.ActiveView), 0, maxOffset);
    }

    public static string DescribeView(WatchViewType view)
        => view switch
        {
            WatchViewType.MyPolity => "My Polity",
            WatchViewType.CurrentRegion => "Current Region",
            WatchViewType.KnownRegions => "Known Regions",
            WatchViewType.RegionDetail => "Region Detail",
            WatchViewType.KnownSpecies => "Known Species",
            WatchViewType.SpeciesDetail => "Species Detail",
            WatchViewType.KnownPolities => "Known Polities",
            WatchViewType.PolityDetail => "Polity Detail",
            WatchViewType.WorldOverview => "World Overview",
            _ => "Chronicle"
        };

    private static IReadOnlyList<string> BuildMyPolityLines(Polity? polity, WorldLookup lookup)
    {
        if (polity is null)
        {
            return ["No focal polity to inspect."];
        }

        string regionName = lookup.GetRequiredRegion(polity.RegionId, "My Polity view").Name;
        string speciesName = lookup.GetRequiredSpecies(polity.SpeciesId, "My Polity view").Name;
        double annualFoodRatio = polity.AnnualFoodNeeded <= 0 ? 1.0 : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

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
            $" Food Sources: Wild {polity.AnnualFoodGathered:F0} | Farm {polity.AnnualFoodFarmed:F0} | Trade {polity.AnnualFoodImported:F0}",
            $" Starvation Months This Year: {polity.StarvationMonthsThisYear}",
            $" Migration Pressure: {polity.MigrationPressure:F2}",
            $" Fragmentation Pressure: {polity.FragmentationPressure:F2}",
            string.Empty,
            " Settlements:"
        ];

        if (polity.Settlements.Count == 0)
        {
            lines.Add("  None");
        }
        else
        {
            foreach (Settlement settlement in polity.Settlements.OrderBy(settlement => settlement.Name, StringComparer.Ordinal))
            {
                string settlementRegion = lookup.TryGetRegion(settlement.RegionId, out Region? region) && region is not null
                    ? region.Name
                    : $"Region {settlement.RegionId}";
                lines.Add($"  {settlement.Name} - {settlementRegion} - age {settlement.YearsEstablished} - cultivated {settlement.CultivatedLand:F1}");
            }
        }

        lines.Add(string.Empty);
        lines.Add($" Discoveries: {DescribeDiscoveries(polity)}");
        lines.Add($" Learned: {DescribeAdvancements(polity)}");
        lines.Add(string.Empty);
        lines.Add(" Press Enter for full polity detail.");
        return lines;
    }

    private static IReadOnlyList<string> BuildCurrentRegionLines(World world, ChronicleFocus focus, WorldLookup lookup)
    {
        Region? region = WatchInspectionData.ResolveCurrentRegion(world, focus);
        List<string> lines = BuildRegionDetailLines(world, region?.Id, lookup).ToList();
        lines.Add(string.Empty);
        lines.Add(" Press Enter for region detail.");
        return lines;
    }

    private static IReadOnlyList<string> BuildKnownRegionsLines(World world, ChronicleFocus focus, WatchUiState uiState, WorldLookup lookup)
    {
        List<Region> regions = WatchInspectionData.GetKnownRegions(world, focus);
        List<string> lines = ["Known Regions", string.Empty];

        if (regions.Count == 0)
        {
            lines.Add(" No known regions.");
            return lines;
        }

        int selectedIndex = Math.Clamp(uiState.GetSelectedIndex(WatchViewType.KnownRegions), 0, regions.Count - 1);
        uiState.SetSelectedIndex(WatchViewType.KnownRegions, selectedIndex);

        for (int index = 0; index < regions.Count; index++)
        {
            Region region = regions[index];
            string marker = index == selectedIndex ? ">" : " ";
            lines.Add($"{marker} {region.Name} - fertility {region.Fertility:F2} - active pop {lookup.GetActivePopulationInRegion(region.Id)} - settlements {lookup.GetSettlementsInRegion(region.Id).Count}");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildKnownSpeciesLines(World world, ChronicleFocus focus, WatchUiState uiState)
    {
        List<Species> species = WatchInspectionData.GetKnownSpecies(world, focus);
        List<string> lines = ["Known Species", string.Empty];

        if (species.Count == 0)
        {
            lines.Add(" No known species.");
            return lines;
        }

        int selectedIndex = Math.Clamp(uiState.GetSelectedIndex(WatchViewType.KnownSpecies), 0, species.Count - 1);
        uiState.SetSelectedIndex(WatchViewType.KnownSpecies, selectedIndex);

        for (int index = 0; index < species.Count; index++)
        {
            Species item = species[index];
            string marker = index == selectedIndex ? ">" : " ";
            lines.Add($"{marker} {item.Name} - {DescribeTrophicRole(item.TrophicRole)} - regions {WatchInspectionData.GetSpeciesRegionalPopulations(world, item.Id).Count} - danger {item.HuntingDanger:F2}");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildKnownPolitiesLines(World world, ChronicleFocus focus, WatchUiState uiState, WorldLookup lookup)
    {
        List<Polity> polities = WatchInspectionData.GetKnownPolities(world, focus);
        List<string> lines = ["Known Polities", string.Empty];

        if (polities.Count == 0)
        {
            lines.Add(" No known polities.");
            return lines;
        }

        int selectedIndex = Math.Clamp(uiState.GetSelectedIndex(WatchViewType.KnownPolities), 0, polities.Count - 1);
        uiState.SetSelectedIndex(WatchViewType.KnownPolities, selectedIndex);

        for (int index = 0; index < polities.Count; index++)
        {
            Polity polity = polities[index];
            string regionName = lookup.TryGetRegion(polity.RegionId, out Region? region) && region is not null
                ? region.Name
                : $"Region {polity.RegionId}";
            string marker = index == selectedIndex ? ">" : " ";
            lines.Add($"{marker} {polity.Name} - pop {polity.Population} - {WatchInspectionData.DescribeStage(polity.Stage)} - {regionName}");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildWorldOverviewLines(World world, WorldLookup lookup)
    {
        List<Polity> activePolities = world.Polities
            .Where(polity => polity.Population > 0)
            .OrderByDescending(polity => polity.Population)
            .ThenBy(polity => polity.Name, StringComparer.Ordinal)
            .ToList();

        List<string> lines =
        [
            "World Overview",
            string.Empty,
            $" Year: {world.Time.Year} ({world.Time.Season})",
            $" Total Regions: {world.Regions.Count}",
            $" Total Species: {world.Species.Count}",
            $" Total Polities: {activePolities.Count}",
            $" World Population: {activePolities.Sum(polity => polity.Population)}",
            string.Empty,
            " Largest Polities:"
        ];

        foreach (Polity polity in activePolities.Take(5))
        {
            string regionName = lookup.TryGetRegion(polity.RegionId, out Region? region) && region is not null
                ? region.Name
                : $"Region {polity.RegionId}";
            lines.Add($"  {polity.Name} - pop {polity.Population} - {regionName}");
        }

        lines.Add(string.Empty);
        lines.Add(" Most Populated Regions:");
        foreach ((Region region, int population) in world.Regions
                     .Select(region => (region, population: lookup.GetActivePopulationInRegion(region.Id)))
                     .OrderByDescending(entry => entry.population)
                     .ThenBy(entry => entry.region.Name, StringComparer.Ordinal)
                     .Take(5))
        {
            lines.Add($"  {region.Name} - active pop {population} - fertility {region.Fertility:F2}");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildRegionDetailLines(World world, int? regionId, WorldLookup lookup)
    {
        if (!regionId.HasValue || !lookup.TryGetRegion(regionId.Value, out Region? region) || region is null)
        {
            return ["Region detail unavailable."];
        }

        List<string> lines =
        [
            "Region Detail",
            string.Empty,
            $" Name: {region.Name}",
            $" Connected Regions: {region.ConnectedRegionIds.Count}",
            $" Fertility: {region.Fertility:F2}",
            $" Water Availability: {region.WaterAvailability:F2}",
            $" Carrying Capacity: {region.CarryingCapacity:F1}",
            $" Plant Biomass: {region.PlantBiomass:F1} / {region.MaxPlantBiomass:F1}",
            $" Animal Biomass: {region.AnimalBiomass:F1} / {region.MaxAnimalBiomass:F1}",
            $" Active Population Here: {lookup.GetActivePopulationInRegion(region.Id)}",
            string.Empty,
            " Settlements:"
        ];

        IReadOnlyList<Settlement> settlements = lookup.GetSettlementsInRegion(region.Id);
        if (settlements.Count == 0)
        {
            lines.Add("  None");
        }
        else
        {
            foreach (Settlement settlement in settlements.OrderBy(settlement => settlement.Name, StringComparer.Ordinal))
            {
                string polityName = lookup.TryGetPolity(settlement.PolityId, out Polity? owner) && owner is not null
                    ? owner.Name
                    : $"Polity {settlement.PolityId}";
                lines.Add($"  {settlement.Name} - {polityName} - cultivated {settlement.CultivatedLand:F1}");
            }
        }

        lines.Add(string.Empty);
        lines.Add(" Species Present:");
        foreach ((Species species, RegionSpeciesPopulation population) in region.SpeciesPopulations
                     .Where(population => population.PopulationCount > 0)
                     .Select(population => (species: world.Species.First(candidate => candidate.Id == population.SpeciesId), population))
                     .OrderByDescending(entry => entry.population.PopulationCount)
                     .ThenBy(entry => entry.species.Name, StringComparer.Ordinal)
                     .Take(8))
        {
            lines.Add($"  {species.Name} - pop {population.PopulationCount} - fit {population.HabitatSuitability:F2} - pressure {population.MigrationPressure:F2}");
        }

        if (lines[^1] == " Species Present:")
        {
            lines.Add("  None");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildSpeciesDetailLines(World world, int? speciesId)
    {
        Species? species = world.Species.FirstOrDefault(entry => entry.Id == speciesId);
        if (species is null)
        {
            return ["Species detail unavailable."];
        }

        List<string> lines =
        [
            "Species Detail",
            string.Empty,
            $" Name: {species.Name}",
            $" Sapient: {(species.IsSapient ? "Yes" : "No")}",
            $" Role: {DescribeTrophicRole(species.TrophicRole)}",
            $" Intelligence: {species.Intelligence:F2}",
            $" Cooperation: {species.Cooperation:F2}",
            $" Habitat Prefs: fertility {species.FertilityPreference:F2} | water {species.WaterPreference:F2}",
            $" Biomass Affinity: plant {species.PlantBiomassAffinity:F2} | animal {species.AnimalBiomassAffinity:F2}",
            $" Mobility: migration {species.MigrationCapability:F2} | expansion {species.ExpansionPressure:F2}",
            $" Hunting: yield {species.MeatYield:F1} | difficulty {species.HuntingDifficulty:F2} | danger {species.HuntingDanger:F2}",
            $" Food Safety: {(species.IsToxicToEat ? "Toxic" : "Edible or unknown")}",
            $" Domestication Affinity: {species.DomesticationAffinity:F2}",
            string.Empty,
            " Regional Populations:"
        ];

        foreach ((Region region, int population) in WatchInspectionData.GetSpeciesRegionalPopulations(world, species.Id).Take(8))
        {
            lines.Add($"  {region.Name} - pop {population}");
        }

        if (lines[^1] == " Regional Populations:")
        {
            lines.Add("  None");
        }

        return lines;
    }

    private static IReadOnlyList<string> BuildPolityDetailLines(World world, int? polityId, WorldLookup lookup)
    {
        if (!polityId.HasValue || !lookup.TryGetPolity(polityId.Value, out Polity? polity) || polity is null)
        {
            return ["Polity detail unavailable."];
        }

        string speciesName = lookup.TryGetSpecies(polity.SpeciesId, out Species? species) && species is not null
            ? species.Name
            : $"Species {polity.SpeciesId}";
        string regionName = lookup.TryGetRegion(polity.RegionId, out Region? region) && region is not null
            ? region.Name
            : $"Region {polity.RegionId}";
        double annualFoodRatio = polity.AnnualFoodNeeded <= 0 ? 1.0 : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

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
            $" Years In Current Region: {polity.YearsInCurrentRegion}",
            $" Settlement Status: {polity.SettlementStatus}",
            $" Settlement Count: {polity.SettlementCount}",
            $" Food Stores: {polity.FoodStores:F0} ({ChronicleTextFormatter.DescribeFoodState(polity)})",
            $" Annual Food Ratio: {annualFoodRatio:F2}",
            $" Annual Food Needed: {polity.AnnualFoodNeeded:F0}",
            $" Annual Food Consumed: {polity.AnnualFoodConsumed:F0}",
            $" Annual Food Shortage: {polity.AnnualFoodShortage:F0}",
            $" Trade Partners This Year: {polity.TradePartnerCountThisYear}",
            $" Starvation Months This Year: {polity.StarvationMonthsThisYear}",
            $" Migration Pressure: {polity.MigrationPressure:F2}",
            $" Fragmentation Pressure: {polity.FragmentationPressure:F2}",
            string.Empty,
            " Settlements:"
        ];

        if (polity.Settlements.Count == 0)
        {
            lines.Add("  None");
        }
        else
        {
            foreach (Settlement settlement in polity.Settlements.OrderBy(settlement => settlement.Name, StringComparer.Ordinal))
            {
                string settlementRegionName = lookup.TryGetRegion(settlement.RegionId, out Region? settlementRegion) && settlementRegion is not null
                    ? settlementRegion.Name
                    : $"Region {settlement.RegionId}";
                lines.Add($"  {settlement.Name} - {settlementRegionName} - age {settlement.YearsEstablished} - cultivated {settlement.CultivatedLand:F1}");
            }
        }

        lines.Add(string.Empty);
        lines.Add($" Discoveries: {DescribeDiscoveries(polity)}");
        lines.Add($" Learned: {DescribeAdvancements(polity)}");
        return lines;
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

    private static string DescribeTrophicRole(TrophicRole role)
        => role switch
        {
            TrophicRole.Apex => "Apex",
            TrophicRole.Predator => "Predator",
            TrophicRole.Omnivore => "Omnivore",
            TrophicRole.Herbivore => "Herbivore",
            _ => "Producer"
        };
}
