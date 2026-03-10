using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public sealed class ChronicleFocus
{
    public int? FocusedPolityId { get; private set; }

    public bool IsFocusedPolity(int polityId)
        => FocusedPolityId.HasValue && FocusedPolityId.Value == polityId;

    public void SetFocusedPolity(int? polityId)
        => FocusedPolityId = polityId;

    public Polity? ResolvePolity(World world)
    {
        if (!FocusedPolityId.HasValue)
        {
            return null;
        }

        return world.Polities.FirstOrDefault(polity => polity.Id == FocusedPolityId.Value);
    }
}
