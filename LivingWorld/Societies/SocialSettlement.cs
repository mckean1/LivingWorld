namespace LivingWorld.Societies;

public sealed class SocialSettlement
{
    public int Id { get; }
    public int FounderSocietyId { get; set; }
    public int FounderLineageId { get; set; }
    public int RegionId { get; set; }
    public int FoundingYear { get; set; }
    public int Population { get; set; }
    public string FoodBaseProfile { get; set; } = "mixed";
    public double StorageLevel { get; set; }
    public double SettlementViability { get; set; }
    public bool IsFallbackCreated { get; set; }
    public double FoodSecurity { get; set; }
    public double LocalCarryingSupport { get; set; }
    public double Stress { get; set; }
    public string CurrentPressureSummary { get; set; } = "forming";
    public bool IsAbandoned { get; set; }
    public Dictionary<string, CulturalDiscovery> LocalKnowledge { get; } = new(StringComparer.OrdinalIgnoreCase);

    public SocialSettlement(int id)
    {
        Id = id;
    }
}
