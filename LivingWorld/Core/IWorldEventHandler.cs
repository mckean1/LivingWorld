namespace LivingWorld.Core;

public interface IWorldEventHandler
{
    bool CanHandle(WorldEvent worldEvent);

    IEnumerable<WorldEvent> Handle(World world, WorldEvent worldEvent);
}
