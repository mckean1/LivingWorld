namespace LivingWorld.Economy;

public sealed class TradeLink
{
    public int ExporterPolityId { get; }
    public int ImporterPolityId { get; }
    public TradeResourceType ResourceType { get; }
    public int? ExporterSettlementId { get; }
    public string? ExporterSettlementName { get; }
    public int? ImporterSettlementId { get; }
    public string? ImporterSettlementName { get; }
    public bool IsInternalPriorityLink { get; }
    public int AgeMonths { get; set; }
    public int LastActiveTick { get; set; }
    public int SuccessfulTransfers { get; set; }
    public double TotalQuantityMoved { get; set; }
    public int InactiveMonths { get; set; }

    public TradeLink(
        int exporterPolityId,
        int importerPolityId,
        TradeResourceType resourceType,
        int currentTick,
        int? exporterSettlementId = null,
        string? exporterSettlementName = null,
        int? importerSettlementId = null,
        string? importerSettlementName = null,
        bool isInternalPriorityLink = false)
    {
        ExporterPolityId = exporterPolityId;
        ImporterPolityId = importerPolityId;
        ResourceType = resourceType;
        ExporterSettlementId = exporterSettlementId;
        ExporterSettlementName = exporterSettlementName;
        ImporterSettlementId = importerSettlementId;
        ImporterSettlementName = importerSettlementName;
        IsInternalPriorityLink = isInternalPriorityLink;
        AgeMonths = 0;
        LastActiveTick = currentTick;
        SuccessfulTransfers = 0;
        TotalQuantityMoved = 0;
        InactiveMonths = 0;
    }

    public string Key => $"{ExporterPolityId}:{ImporterPolityId}:{ResourceType}";
}
