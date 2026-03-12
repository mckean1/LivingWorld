
using LivingWorld.Core;
using LivingWorld.Map;

namespace LivingWorld.Life;

public sealed class Species
{
    public int Id { get; }
    public string Name { get; }

    public double Intelligence { get; }
    public double Cooperation { get; }
    public bool IsSapient { get; set; }
    public TrophicRole TrophicRole { get; set; }
    public double FertilityPreference { get; set; }
    public double WaterPreference { get; set; }
    public double PlantBiomassAffinity { get; set; }
    public double AnimalBiomassAffinity { get; set; }
    public double BaseCarryingCapacityFactor { get; set; }
    public double MigrationCapability { get; set; }
    public double ExpansionPressure { get; set; }
    public double BaseReproductionRate { get; set; }
    public double BaseDeclineRate { get; set; }
    public double SpringReproductionModifier { get; set; }
    public double SummerReproductionModifier { get; set; }
    public double AutumnReproductionModifier { get; set; }
    public double WinterReproductionModifier { get; set; }
    public double MeatYield { get; set; }
    public double HuntingDifficulty { get; set; }
    public double HuntingDanger { get; set; }
    public bool IsToxicToEat { get; set; }
    public double DomesticationAffinity { get; set; }
    public int? ParentSpeciesId { get; set; }
    public int RootAncestorSpeciesId { get; set; }
    public int? OriginRegionId { get; set; }
    public int OriginYear { get; set; }
    public int OriginMonth { get; set; }
    public string? OriginCause { get; set; }
    public string? OriginPressureSummary { get; set; }
    public int EarliestSpeciationYear { get; set; }
    public bool IsGloballyExtinct { get; set; }
    public int? ExtinctionYear { get; set; }
    public int? ExtinctionMonth { get; set; }
    public List<int> DietSpeciesIds { get; } = [];
    public HashSet<RegionBiome> PreferredBiomes { get; } = [];
    public HashSet<int> InitialRangeRegionIds { get; } = [];
    public HashSet<int> DescendantSpeciesIds { get; } = [];

    public Species(int id, string name, double intelligence, double cooperation)
    {
        Id = id;
        Name = name;
        Intelligence = intelligence;
        Cooperation = cooperation;
        IsSapient = false;
        TrophicRole = TrophicRole.Herbivore;
        FertilityPreference = 0.5;
        WaterPreference = 0.5;
        PlantBiomassAffinity = 0.5;
        AnimalBiomassAffinity = 0.25;
        BaseCarryingCapacityFactor = 1.0;
        MigrationCapability = 0.2;
        ExpansionPressure = 0.15;
        BaseReproductionRate = 0.08;
        BaseDeclineRate = 0.04;
        SpringReproductionModifier = 1.30;
        SummerReproductionModifier = 1.05;
        AutumnReproductionModifier = 0.85;
        WinterReproductionModifier = 0.55;
        MeatYield = 10.0;
        HuntingDifficulty = 0.35;
        HuntingDanger = 0.10;
        IsToxicToEat = false;
        DomesticationAffinity = 0.20;
        RootAncestorSpeciesId = id;
        OriginYear = 0;
        OriginMonth = 1;
        EarliestSpeciationYear = 0;
        IsGloballyExtinct = false;
    }

    public double GetSeasonalReproductionModifier(Season season)
        => season switch
        {
            Season.Spring => SpringReproductionModifier,
            Season.Summer => SummerReproductionModifier,
            Season.Autumn => AutumnReproductionModifier,
            _ => WinterReproductionModifier
        };

    public void RecordDescendant(int descendantSpeciesId)
    {
        if (descendantSpeciesId != Id)
        {
            DescendantSpeciesIds.Add(descendantSpeciesId);
        }
    }
}
