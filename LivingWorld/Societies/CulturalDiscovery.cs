namespace LivingWorld.Societies;

public sealed record CulturalDiscovery(
    string Key,
    string Summary,
    CulturalDiscoveryCategory Category,
    int? SpeciesId = null,
    int? RegionId = null);
