using LivingWorld.Core;

namespace LivingWorld.Presentation;

public interface IPolityFocusSelector
{
    ChronicleFocusSelection SelectInitialFocus(World world, SimulationOptions options);
    ChronicleFocusTransition? ResolveYearEndFocus(World world, ChronicleFocus focus, IReadOnlyList<WorldEvent> eventsThisYear);
}
