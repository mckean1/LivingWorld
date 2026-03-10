using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public sealed class ChronicleFocus
{
    public int? FocusedPolityId { get; private set; }
    public int? FocusedLineageId { get; private set; }

    public bool IsFocusedPolity(int polityId)
        => FocusedPolityId.HasValue && FocusedPolityId.Value == polityId;

    public void SetFocus(int? polityId, int? lineageId)
    {
        FocusedPolityId = polityId;
        FocusedLineageId = lineageId;
    }

    public void SetFocus(Polity? polity)
        => SetFocus(polity?.Id, polity?.LineageId);

    public Polity? ResolvePolity(World world)
    {
        if (!FocusedPolityId.HasValue)
        {
            return null;
        }

        return world.Polities.FirstOrDefault(polity => polity.Id == FocusedPolityId.Value);
    }
}
