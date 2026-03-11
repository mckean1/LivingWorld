using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class FragmentationPropagationHandler : IWorldEventHandler
{
    public bool CanHandle(WorldEvent worldEvent)
        => worldEvent.Type == WorldEventType.Fragmentation;

    public IEnumerable<WorldEvent> Handle(World world, WorldEvent worldEvent)
    {
        if (worldEvent.RelatedPolityId is not int childPolityId)
        {
            yield break;
        }

        Polity? childPolity = world.Polities.FirstOrDefault(polity => polity.Id == childPolityId);
        if (childPolity is null)
        {
            yield break;
        }

        string regionName = world.Regions.First(region => region.Id == childPolity.RegionId).Name;

        yield return new WorldEvent
        {
            Type = WorldEventType.PolityFounded,
            Severity = WorldEventSeverity.Major,
            Scope = WorldEventScope.Polity,
            Narrative = $"{childPolity.Name} emerged in {regionName}",
            Details = $"{childPolity.Name} emerged from the fragmentation of {worldEvent.PolityName}.",
            Reason = "fragmentation_child_polity_founded",
            PolityId = childPolity.Id,
            PolityName = childPolity.Name,
            RelatedPolityId = worldEvent.PolityId,
            RelatedPolityName = worldEvent.PolityName,
            SpeciesId = childPolity.SpeciesId,
            SpeciesName = world.Species.FirstOrDefault(species => species.Id == childPolity.SpeciesId)?.Name,
            RegionId = childPolity.RegionId,
            RegionName = regionName,
            Metadata = new Dictionary<string, string>
            {
                ["lineageId"] = childPolity.LineageId.ToString(),
                ["parentPolityId"] = worldEvent.PolityId?.ToString() ?? string.Empty
            }
        };
    }
}
