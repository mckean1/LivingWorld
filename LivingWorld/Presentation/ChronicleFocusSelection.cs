using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public sealed record ChronicleFocusSelection(int? PolityId, int? LineageId)
{
    public static ChronicleFocusSelection None { get; } = new(null, null);

    public static ChronicleFocusSelection FromPolity(Polity? polity)
        => polity is null
            ? None
            : new ChronicleFocusSelection(polity.Id, polity.LineageId);
}

public sealed record ChronicleFocusTransition(
    ChronicleFocusTransitionKind Kind,
    int PreviousPolityId,
    string PreviousPolityName,
    int PreviousLineageId,
    int NewPolityId,
    string NewPolityName,
    int NewLineageId,
    string Reason);

public enum ChronicleFocusTransitionKind
{
    Fragmentation,
    Collapse,
    LineageContinuation,
    LineageExtinctionFallback
}
