namespace LivingWorld.Societies;

public sealed class SentientPopulationGroup
{
    public int Id { get; }
    public int SourceLineageId { get; set; }
    public int CurrentRegionId { get; set; }
    public int ActivationYear { get; set; }
    public int PopulationCount { get; set; }
    public MobilityMode MobilityMode { get; set; }
    public double Cohesion { get; set; }
    public double SocialComplexity { get; set; }
    public double SurvivalKnowledge { get; set; }
    public double SettlementIntent { get; set; }
    public double Stress { get; set; }
    public double SedentismPressure { get; set; }
    public int ContinuityYears { get; set; }
    public double IdentityStrength { get; set; }
    public string MigrationPattern { get; set; } = "adaptive";
    public string FoundingMemorySeed { get; set; } = "activation";
    public string ThreatMemorySeed { get; set; } = "ecological pressure";
    public string PressureSummary { get; set; } = "emergent";
    public int? PredecessorGroupId { get; set; }
    public int? FounderRegionId { get; set; }
    public int LastMigrationYear { get; set; } = -1;
    public bool IsFallbackCreated { get; set; }
    public double FoodSecurity { get; set; }
    public double StorageSupport { get; set; }
    public double LocalCarryingSupport { get; set; }
    public double MigrationPressure { get; set; }
    public double FragmentationPressure { get; set; }
    public bool IsCollapsed { get; set; }
    public HashSet<string> IdentityMarkers { get; } = [];
    public Dictionary<string, CulturalDiscovery> SharedKnowledge { get; } = new(StringComparer.OrdinalIgnoreCase);

    public SentientPopulationGroup(int id)
    {
        Id = id;
    }
}
