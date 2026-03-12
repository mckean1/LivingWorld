using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public static class WatchInspectionData
{
    public static WatchKnowledgeSnapshot CreateSnapshot(World world, ChronicleFocus focus)
        => WatchKnowledgeSnapshot.Create(world, focus);

    public static Polity? ResolveFocusedPolity(World world, ChronicleFocus focus)
        => CreateSnapshot(world, focus).FocalPolity;

    public static Region? ResolveCurrentRegion(World world, ChronicleFocus focus)
        => CreateSnapshot(world, focus).CurrentRegion;

    public static List<Region> GetKnownRegions(World world, ChronicleFocus focus)
        => CreateSnapshot(world, focus).KnownRegions.ToList();

    public static List<Species> GetKnownSpecies(World world, ChronicleFocus focus)
        => CreateSnapshot(world, focus).KnownSpecies.ToList();

    public static List<Polity> GetKnownPolities(World world, ChronicleFocus focus)
    {
        WatchKnowledgeSnapshot snapshot = CreateSnapshot(world, focus);
        int? focalPolityId = snapshot.FocalPolity?.Id;
        return snapshot.KnownPolities
            .Where(polity => polity.Id != focalPolityId)
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
