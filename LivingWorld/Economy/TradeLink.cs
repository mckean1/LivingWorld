namespace LivingWorld.Economy;

public sealed class TradeLink
{
    public int ExporterPolityId { get; }
    public int ImporterPolityId { get; }
    public TradeResourceType ResourceType { get; }
    public int AgeMonths { get; set; }
    public int LastActiveTick { get; set; }

    public TradeLink(int exporterPolityId, int importerPolityId, TradeResourceType resourceType, int currentTick)
    {
        ExporterPolityId = exporterPolityId;
        ImporterPolityId = importerPolityId;
        ResourceType = resourceType;
        AgeMonths = 0;
        LastActiveTick = currentTick;
    }

    public string Key => $"{ExporterPolityId}:{ImporterPolityId}:{ResourceType}";
}
