namespace LivingWorld.Societies;

public sealed class EmergingSociety
{
    public int Id { get; }
    public int LineageId { get; set; }
    public int SpeciesId { get; set; }
    public int OriginRegionId { get; set; }
    public int FoundingYear { get; set; }
    public HashSet<int> RegionIds { get; } = [];
    public int Population { get; set; }
    public MobilityMode MobilityMode { get; set; }
    public SubsistenceMode SubsistenceMode { get; set; }
    public double Cohesion { get; set; }
    public double IdentityStrength { get; set; }
    public double SocialComplexity { get; set; }
    public double SurvivalKnowledge { get; set; }
    public double SedentismPressure { get; set; }
    public string PressureSummary { get; set; } = "forming";
    public int ContinuityYears { get; set; }
    public int? PredecessorGroupId { get; set; }
    public int? ParentSocietyId { get; set; }
    public int? FounderPolityId { get; set; }
    public bool IsFallbackCreated { get; set; }
    public double FoodSecurity { get; set; }
    public double StorageSupport { get; set; }
    public double SettlementSupport { get; set; }
    public double LocalCarryingSupport { get; set; }
    public double MigrationPressure { get; set; }
    public double FragmentationPressure { get; set; }
    public bool IsCollapsed { get; set; }
    public string FoundingMemorySeed { get; set; } = "shared survival";
    public string ThreatMemorySeed { get; set; } = "hardship";
    public HashSet<string> IdentityMarkers { get; } = [];
    public Dictionary<string, CulturalDiscovery> CulturalKnowledge { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<int> SettlementIds { get; } = [];
    public List<int> DescendantSocietyIds { get; } = [];

    public EmergingSociety(int id)
    {
        Id = id;
    }
}
