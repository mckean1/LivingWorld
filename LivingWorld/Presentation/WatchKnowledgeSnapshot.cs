using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public sealed class WatchKnowledgeSnapshot
{
    private static readonly ChroniclePresentationPolicy VisibleEventPresentationPolicy = new();
    private readonly Dictionary<int, Region> _knownRegionsById;
    private readonly Dictionary<int, Species> _knownSpeciesById;
    private readonly Dictionary<int, Polity> _knownPolitiesById;
    private readonly Dictionary<int, List<CulturalDiscovery>> _discoveriesByRegionId;
    private readonly Dictionary<int, List<CulturalDiscovery>> _discoveriesBySpeciesId;

    private WatchKnowledgeSnapshot(
        Polity? focalPolity,
        Region? currentRegion,
        List<Region> knownRegions,
        List<Species> knownSpecies,
        List<Polity> knownPolities,
        Dictionary<int, List<CulturalDiscovery>> discoveriesByRegionId,
        Dictionary<int, List<CulturalDiscovery>> discoveriesBySpeciesId)
    {
        FocalPolity = focalPolity;
        CurrentRegion = currentRegion;
        KnownRegions = knownRegions;
        KnownSpecies = knownSpecies;
        KnownPolities = knownPolities;
        _knownRegionsById = knownRegions.ToDictionary(region => region.Id);
        _knownSpeciesById = knownSpecies.ToDictionary(species => species.Id);
        _knownPolitiesById = knownPolities.ToDictionary(polity => polity.Id);
        _discoveriesByRegionId = discoveriesByRegionId;
        _discoveriesBySpeciesId = discoveriesBySpeciesId;
    }

    public Polity? FocalPolity { get; }

    public Region? CurrentRegion { get; }

    public IReadOnlyList<Region> KnownRegions { get; }

    public IReadOnlyList<Species> KnownSpecies { get; }

    public IReadOnlyList<Polity> KnownPolities { get; }

    public static WatchKnowledgeSnapshot Create(World world, ChronicleFocus focus)
    {
        Polity? focalPolity = focus.ResolvePolity(world);
        return Create(world, focalPolity);
    }

    public static WatchKnowledgeSnapshot Create(World world, Polity? focalPolity)
    {
        if (focalPolity is null)
        {
            return new WatchKnowledgeSnapshot(
                focalPolity: null,
                currentRegion: null,
                knownRegions: [],
                knownSpecies: [],
                knownPolities: [],
                discoveriesByRegionId: [],
                discoveriesBySpeciesId: []);
        }

        HashSet<int> knownRegionIds = focalPolity.Settlements.Select(settlement => settlement.RegionId).ToHashSet();
        knownRegionIds.Add(focalPolity.RegionId);

        foreach (int discoveredRegionId in focalPolity.Discoveries.Where(discovery => discovery.RegionId.HasValue).Select(discovery => discovery.RegionId!.Value))
        {
            knownRegionIds.Add(discoveredRegionId);
        }

        foreach (int occupiedRegionId in knownRegionIds.ToList())
        {
            Region? occupiedRegion = world.Regions.FirstOrDefault(region => region.Id == occupiedRegionId);
            if (occupiedRegion is null)
            {
                continue;
            }

            foreach (int connectedRegionId in occupiedRegion.ConnectedRegionIds)
            {
                knownRegionIds.Add(connectedRegionId);
            }
        }

        List<Region> knownRegions = world.Regions
            .Where(region => knownRegionIds.Contains(region.Id))
            .OrderBy(region => region.Name, StringComparer.Ordinal)
            .ToList();
        HashSet<int> knownSpeciesIds = focalPolity.Discoveries
            .Where(discovery => discovery.SpeciesId.HasValue)
            .Select(discovery => discovery.SpeciesId!.Value)
            .ToHashSet();
        knownSpeciesIds.Add(focalPolity.SpeciesId);

        foreach (Region region in knownRegions)
        {
            foreach (RegionSpeciesPopulation population in region.SpeciesPopulations)
            {
                if (population.PopulationCount > 0)
                {
                    knownSpeciesIds.Add(population.SpeciesId);
                }
            }
        }

        List<Polity> knownPolities = world.Polities
            .Where(polity => polity.Population > 0
                && (polity.Id == focalPolity.Id
                    || knownRegionIds.Contains(polity.RegionId)
                    || polity.Settlements.Any(settlement => knownRegionIds.Contains(settlement.RegionId))))
            .OrderByDescending(polity => polity.Id == focalPolity.Id)
            .ThenByDescending(polity => polity.Population)
            .ThenBy(polity => polity.Name, StringComparer.Ordinal)
            .ToList();

        foreach (Polity polity in knownPolities)
        {
            knownSpeciesIds.Add(polity.SpeciesId);
        }

        List<Species> knownSpecies = world.Species
            .Where(species => knownSpeciesIds.Contains(species.Id))
            .OrderByDescending(species => species.Id == focalPolity.SpeciesId)
            .ThenByDescending(species => species.IsSapient)
            .ThenBy(species => species.Name, StringComparer.Ordinal)
            .ToList();
        Region? currentRegion = knownRegions.FirstOrDefault(region =>
        {
            int currentRegionId = focalPolity.GetPrimarySettlement()?.RegionId ?? focalPolity.RegionId;
            return region.Id == currentRegionId;
        });

        return new WatchKnowledgeSnapshot(
            focalPolity,
            currentRegion,
            knownRegions,
            knownSpecies,
            knownPolities,
            BuildDiscoveryLookup(focalPolity.Discoveries, discovery => discovery.RegionId),
            BuildDiscoveryLookup(focalPolity.Discoveries, discovery => discovery.SpeciesId));
    }

    public bool IsRegionKnown(int regionId)
        => _knownRegionsById.ContainsKey(regionId);

    public bool IsSpeciesKnown(int speciesId)
        => _knownSpeciesById.ContainsKey(speciesId);

    public bool IsPolityKnown(int polityId)
        => _knownPolitiesById.ContainsKey(polityId);

    public Region? TryGetKnownRegion(int regionId)
        => _knownRegionsById.TryGetValue(regionId, out Region? region) ? region : null;

    public Species? TryGetKnownSpecies(int speciesId)
        => _knownSpeciesById.TryGetValue(speciesId, out Species? species) ? species : null;

    public Polity? TryGetKnownPolity(int polityId)
        => _knownPolitiesById.TryGetValue(polityId, out Polity? polity) ? polity : null;

    public IReadOnlyList<CulturalDiscovery> GetRegionDiscoveries(int regionId)
        => _discoveriesByRegionId.TryGetValue(regionId, out List<CulturalDiscovery>? discoveries)
            ? discoveries
            : [];

    public IReadOnlyList<CulturalDiscovery> GetSpeciesDiscoveries(int speciesId)
        => _discoveriesBySpeciesId.TryGetValue(speciesId, out List<CulturalDiscovery>? discoveries)
            ? discoveries
            : [];

    public IReadOnlyList<(Region Region, RegionSpeciesPopulation Population)> GetKnownRegionalPopulations(World world, int speciesId)
    {
        if (!IsSpeciesKnown(speciesId))
        {
            return [];
        }

        return KnownRegions
            .Select(region => (Region: region, Population: region.GetSpeciesPopulation(speciesId)))
            .Where(entry => entry.Population is not null && entry.Population.PopulationCount > 0)
            .Select(entry => (entry.Region, entry.Population!))
            .OrderByDescending(entry => entry.Item2.PopulationCount)
            .ThenBy(entry => entry.Item1.Name, StringComparer.Ordinal)
            .ToList();
    }

    public IReadOnlyList<Settlement> GetVisibleSettlementsForPolity(Polity polity)
        => polity.Settlements
            .Where(settlement => IsRegionKnown(settlement.RegionId))
            .OrderBy(settlement => settlement.Name, StringComparer.Ordinal)
            .ToList();

    public IReadOnlyList<WorldEvent> GetVisibleMajorEvents(World world, int limit)
    {
        HashSet<int> knownRegionIds = KnownRegions.Select(region => region.Id).ToHashSet();
        HashSet<int> knownPolityIds = KnownPolities.Select(polity => polity.Id).ToHashSet();
        HashSet<int> knownSpeciesIds = KnownSpecies.Select(species => species.Id).ToHashSet();
        HashSet<string> seenDedupKeys = new(StringComparer.Ordinal);

        return world.Events
            .Where(worldEvent => !worldEvent.IsBootstrapEvent && worldEvent.IsLiveTransition)
            .Where(worldEvent => worldEvent.Severity is WorldEventSeverity.Major or WorldEventSeverity.Legendary)
            .Where(worldEvent =>
                (worldEvent.PolityId.HasValue && knownPolityIds.Contains(worldEvent.PolityId.Value))
                || (worldEvent.RelatedPolityId.HasValue && knownPolityIds.Contains(worldEvent.RelatedPolityId.Value))
                || (worldEvent.RegionId.HasValue && knownRegionIds.Contains(worldEvent.RegionId.Value))
                || (worldEvent.SpeciesId.HasValue && knownSpeciesIds.Contains(worldEvent.SpeciesId.Value)))
            .OrderByDescending(worldEvent => worldEvent.EventId)
            .Where(worldEvent =>
            {
                string dedupKey = VisibleEventPresentationPolicy.BuildPlayerFacingDedupKey(worldEvent)
                    ?? $"{worldEvent.Year}:{WorldEvent.NormalizeNarrative(worldEvent.Narrative)}";
                return seenDedupKeys.Add(dedupKey);
            })
            .Take(limit)
            .ToList();
    }

    private static Dictionary<int, List<CulturalDiscovery>> BuildDiscoveryLookup(
        IEnumerable<CulturalDiscovery> discoveries,
        Func<CulturalDiscovery, int?> keySelector)
    {
        Dictionary<int, List<CulturalDiscovery>> lookup = [];
        foreach (CulturalDiscovery discovery in discoveries)
        {
            int? key = keySelector(discovery);
            if (!key.HasValue)
            {
                continue;
            }

            if (!lookup.TryGetValue(key.Value, out List<CulturalDiscovery>? bucket))
            {
                bucket = [];
                lookup[key.Value] = bucket;
            }

            bucket.Add(discovery);
        }

        foreach (List<CulturalDiscovery> bucket in lookup.Values)
        {
            bucket.Sort((left, right) =>
            {
                int categoryComparison = left.Category.CompareTo(right.Category);
                return categoryComparison != 0
                    ? categoryComparison
                    : StringComparer.Ordinal.Compare(left.Summary, right.Summary);
            });
        }

        return lookup;
    }
}
