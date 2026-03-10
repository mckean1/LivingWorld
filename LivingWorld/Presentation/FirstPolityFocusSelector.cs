using LivingWorld.Core;

namespace LivingWorld.Presentation;

public sealed class FirstPolityFocusSelector : IPolityFocusSelector
{
    public int? SelectFocusedPolityId(World world, SimulationOptions options)
    {
        if (options.FocusedPolityId.HasValue)
        {
            return options.FocusedPolityId;
        }

        return world.Polities
            .OrderBy(polity => polity.Id)
            .Select(polity => (int?)polity.Id)
            .FirstOrDefault();
    }
}
