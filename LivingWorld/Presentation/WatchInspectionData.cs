using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public static class WatchInspectionData
{
    public static Polity? ResolveFocusedPolity(World world, ChronicleFocus focus)
        => focus.ResolvePolity(world);

    public static Region? ResolveCurrentRegion(World world, ChronicleFocus focus)
    {
        Polity? polity = ResolveFocusedPolity(world, focus);
        if (polity is null)
        {
            return null;
        }

        int regionId = polity.GetPrimarySettlement()?.RegionId ?? polity.RegionId;
        return world.Regions.FirstOrDefault(region => region.Id == regionId);
    }

    public static List<Region> GetKnownRegions(World world, ChronicleFocus focus)
    {
        Polity? polity = ResolveFocusedPolity(world, focus);
        if (polity is null)
        {
            return [];
        }

        // Phase 1 visibility rule: treat the focal polity as knowing the regions
        // where it has settlements, plus its current center region, plus directly
        // connected neighbors of those occupied regions.
        HashSet<int> regionIds = polity.Settlements.Select(settlement => settlement.RegionId).ToHashSet();
        regionIds.Add(polity.RegionId);

        foreach (int occupiedRegionId in regionIds.ToList())
        {
            Region? occupiedRegion = world.Regions.FirstOrDefault(region => region.Id == occupiedRegionId);
            if (occupiedRegion is null)
            {
                continue;
            }

            foreach (int connectedRegionId in occupiedRegion.ConnectedRegionIds)
            {
                regionIds.Add(connectedRegionId);
            }
        }

        return world.Regions
            .Where(region => regionIds.Contains(region.Id))
            .OrderBy(region => region.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static List<Species> GetKnownSpecies(World world, ChronicleFocus focus)
    {
        HashSet<int> knownRegionIds = GetKnownRegions(world, focus)
            .Select(region => region.Id)
            .ToHashSet();

        return world.Species
            .Where(species => world.Regions.Any(region =>
                knownRegionIds.Contains(region.Id)
                && region.SpeciesPopulations.Any(population => population.SpeciesId == species.Id && population.PopulationCount > 0)))
            .OrderByDescending(species => species.IsSapient)
            .ThenBy(species => species.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static List<Polity> GetKnownPolities(World world, ChronicleFocus focus)
    {
        Polity? focalPolity = ResolveFocusedPolity(world, focus);
        if (focalPolity is null)
        {
            return [];
        }

        HashSet<int> knownRegionIds = GetKnownRegions(world, focus)
            .Select(region => region.Id)
            .ToHashSet();

        return world.Polities
            .Where(polity => polity.Population > 0
                && polity.Id != focalPolity.Id
                && (knownRegionIds.Contains(polity.RegionId)
                    || polity.Settlements.Any(settlement => knownRegionIds.Contains(settlement.RegionId))))
            .OrderByDescending(polity => polity.Population)
            .ThenBy(polity => polity.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static List<(Region Region, int Population)> GetSpeciesRegionalPopulations(World world, int speciesId)
    {
        return world.Regions
            .Select(region => (Region: region, Population: region.SpeciesPopulations
                .Where(population => population.SpeciesId == speciesId)
                .Sum(population => population.PopulationCount)))
            .Where(entry => entry.Population > 0)
            .OrderByDescending(entry => entry.Population)
            .ThenBy(entry => entry.Region.Name, StringComparer.Ordinal)
            .ToList();
    }

    public static string DescribeStage(PolityStage stage)
        => stage switch
        {
            PolityStage.SettledSociety => "Settled Society",
            _ => stage.ToString()
        };
}
