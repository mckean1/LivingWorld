using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Core;

public sealed class WorldLookup
{
    private static readonly IReadOnlyList<Polity> EmptyPolities = Array.Empty<Polity>();
    private static readonly IReadOnlyList<Settlement> EmptySettlements = Array.Empty<Settlement>();

    private readonly Dictionary<int, Region> _regions;
    private readonly Dictionary<int, Species> _species;
    private readonly Dictionary<int, Polity> _polities;
    private readonly Dictionary<int, List<Polity>> _activePolitiesByRegion;
    private readonly Dictionary<int, List<Settlement>> _settlementsByRegion;
    private readonly Dictionary<int, int> _activePopulationByRegion;

    public WorldLookup(World world)
    {
        _regions = world.Regions.ToDictionary(region => region.Id);
        _species = world.Species.ToDictionary(species => species.Id);
        _polities = world.Polities.ToDictionary(polity => polity.Id);
        _activePolitiesByRegion = new Dictionary<int, List<Polity>>();
        _settlementsByRegion = new Dictionary<int, List<Settlement>>();
        _activePopulationByRegion = new Dictionary<int, int>();

        foreach (Polity polity in world.Polities)
        {
            if (polity.Population > 0)
            {
                if (!_activePolitiesByRegion.TryGetValue(polity.RegionId, out List<Polity>? regionPolities))
                {
                    regionPolities = [];
                    _activePolitiesByRegion[polity.RegionId] = regionPolities;
                }

                regionPolities.Add(polity);
                _activePopulationByRegion[polity.RegionId] = GetActivePopulationInRegion(polity.RegionId) + polity.Population;
            }

            foreach (Settlement settlement in polity.Settlements)
            {
                if (!_settlementsByRegion.TryGetValue(settlement.RegionId, out List<Settlement>? regionSettlements))
                {
                    regionSettlements = [];
                    _settlementsByRegion[settlement.RegionId] = regionSettlements;
                }

                regionSettlements.Add(settlement);
            }
        }
    }

    public bool TryGetRegion(int regionId, out Region? region)
        => _regions.TryGetValue(regionId, out region);

    public bool TryGetSpecies(int speciesId, out Species? species)
        => _species.TryGetValue(speciesId, out species);

    public bool TryGetPolity(int polityId, out Polity? polity)
        => _polities.TryGetValue(polityId, out polity);

    public Region GetRequiredRegion(int regionId, string context)
        => _regions.TryGetValue(regionId, out Region? region)
            ? region
            : throw new InvalidOperationException($"{context} referenced missing region {regionId}.");

    public Species GetRequiredSpecies(int speciesId, string context)
        => _species.TryGetValue(speciesId, out Species? species)
            ? species
            : throw new InvalidOperationException($"{context} referenced missing species {speciesId}.");

    public Polity GetRequiredPolity(int polityId, string context)
        => _polities.TryGetValue(polityId, out Polity? polity)
            ? polity
            : throw new InvalidOperationException($"{context} referenced missing polity {polityId}.");

    public IReadOnlyList<Polity> GetActivePolitiesInRegion(int regionId)
        => _activePolitiesByRegion.TryGetValue(regionId, out List<Polity>? polities)
            ? polities
            : EmptyPolities;

    public IReadOnlyList<Settlement> GetSettlementsInRegion(int regionId)
        => _settlementsByRegion.TryGetValue(regionId, out List<Settlement>? settlements)
            ? settlements
            : EmptySettlements;

    public int GetActivePopulationInRegion(int regionId)
        => _activePopulationByRegion.TryGetValue(regionId, out int population)
            ? population
            : 0;
}
