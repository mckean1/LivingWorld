using LivingWorld.Core;

namespace LivingWorld.Presentation;

public interface IPolityFocusSelector
{
    int? SelectFocusedPolityId(World world, SimulationOptions options);
}
