using LivingWorld.Life;
using LivingWorld.Map;

namespace LivingWorld.Generation;

internal static class WorldGenerationCatalog
{
    public static RegionBiome[,] CreateBiomeGrid()
        => new[,]
        {
            { RegionBiome.Coast, RegionBiome.Coast, RegionBiome.Forest, RegionBiome.RiverValley, RegionBiome.Highlands, RegionBiome.Coast },
            { RegionBiome.Coast, RegionBiome.Forest, RegionBiome.Plains, RegionBiome.RiverValley, RegionBiome.Mountains, RegionBiome.Coast },
            { RegionBiome.Wetlands, RegionBiome.Plains, RegionBiome.Plains, RegionBiome.RiverValley, RegionBiome.Highlands, RegionBiome.Drylands },
            { RegionBiome.Forest, RegionBiome.Plains, RegionBiome.Plains, RegionBiome.RiverValley, RegionBiome.Highlands, RegionBiome.Drylands },
            { RegionBiome.Coast, RegionBiome.Forest, RegionBiome.Highlands, RegionBiome.RiverValley, RegionBiome.Mountains, RegionBiome.Drylands },
            { RegionBiome.Coast, RegionBiome.Coast, RegionBiome.Plains, RegionBiome.RiverValley, RegionBiome.Wetlands, RegionBiome.Coast }
        };

    public static IReadOnlyList<string> CreateRegionNames()
        => new[]
        {
            "Ashen Vale", "Stonewater", "Red Marsh", "Sun Hollow", "Frostmere", "Green Barrow", "Ironwood", "Mistral Steppe",
            "Brightfen", "Raven Coast", "Thornfield", "Amber Reach", "Silver Run", "Cinder Flats", "Oaken Shore", "Mistmere",
            "High Tarns", "Stormwash", "Long Meadow", "White Basin", "Gloam Fen", "Hearth Plain", "Broken Ridge", "Seawind March",
            "Redwater", "Sable Forest", "Winter Shelf", "Goldgrass", "Deep Marsh", "Slate Heights", "Cloudbarrow", "Moon Hollow",
            "Reedmouth", "Lark Coast", "Stonefen", "Westmere", "Northfall", "Dawn Valley", "Blackwater", "Skyplain",
            "Fallow Reach", "Marrow Hills", "Willow Strand", "Copper Steppe", "Stillwater", "Graywood", "Highbank", "Saltmere"
        };

    public static IReadOnlyList<string> CreatePolityNames()
        => new[]
        {
            "Riverwatch Clan", "Emberfall Kin", "Stone Antler Tribe", "Moss Hearth Folk", "Red Reed Circle", "Sky Elk People",
            "Winter Oak Clan", "Dawnfire Band", "Cinder Brook Kin", "Deepfield Tribe", "Mistshore Folk", "Highbank Circle",
            "Willow Steppe Kin", "Stormbarrow Clan", "Sunwater People", "Ashwood Band", "Green Reed Kin", "Copper Vale Clan",
            "Stonewater Folk", "Cloudmarch Tribe", "Brightfen Circle", "Raven Strand Kin", "Thorn Meadow Clan", "Silver Run Folk"
        };

    public static List<SpeciesTemplate> CreateSpeciesTemplates()
        => new()
        {
            Sapient("Humans", TrophicRole.Omnivore, 0.62, 0.60, 0.40, 0.44, 0.96, 0.24, 0.18, 10, 0.30, 0.16, false, 0.45,
                [RegionBiome.RiverValley, RegionBiome.Plains, RegionBiome.Forest, RegionBiome.Coast],
                ["Tallgrass", "Stonehorn Elk", "Fen Boar", "Moor Hare"]),
            Sapient("Wolfkin", TrophicRole.Predator, 0.46, 0.44, 0.22, 0.68, 0.76, 0.30, 0.24, 14, 0.38, 0.32, false, 0.12,
                [RegionBiome.Forest, RegionBiome.Highlands, RegionBiome.Plains],
                ["Stonehorn Elk", "Dawn Bison", "Moor Hare", "Cinder Fox"]),
            Sapient("Horsefolk", TrophicRole.Herbivore, 0.60, 0.48, 0.64, 0.16, 0.90, 0.28, 0.22, 16, 0.24, 0.20, false, 0.52,
                [RegionBiome.Plains, RegionBiome.RiverValley, RegionBiome.Highlands],
                ["Tallgrass", "Silver Moss", "Sunroot"]),
            Sapient("Marshfolk", TrophicRole.Omnivore, 0.58, 0.76, 0.52, 0.30, 0.92, 0.22, 0.16, 11, 0.28, 0.14, false, 0.28,
                [RegionBiome.Wetlands, RegionBiome.Coast, RegionBiome.RiverValley, RegionBiome.Forest],
                ["River Reed", "Fen Boar", "Reed Crab", "Marsh Grazer"]),

            Producer("River Reed", 0.78, 0.90, 0.92, 0.04, 1.18, [RegionBiome.RiverValley, RegionBiome.Wetlands, RegionBiome.Coast]),
            Producer("Tallgrass", 0.62, 0.46, 0.88, 0.02, 1.10, [RegionBiome.Plains, RegionBiome.RiverValley]),
            Producer("Silver Moss", 0.44, 0.70, 0.82, 0.00, 0.98, [RegionBiome.Forest, RegionBiome.Highlands, RegionBiome.Wetlands]),
            Producer("Sunroot", 0.70, 0.42, 0.80, 0.00, 1.04, [RegionBiome.Plains, RegionBiome.Drylands, RegionBiome.RiverValley]),
            Producer("Redcap Mushroom", 0.42, 0.80, 0.66, 0.00, 0.82, [RegionBiome.Forest, RegionBiome.Wetlands], isToxicToEat: true),
            Producer("Salt Kelp", 0.34, 0.96, 0.76, 0.00, 0.96, [RegionBiome.Coast]),
            Producer("Bitter Brush", 0.26, 0.18, 0.58, 0.00, 0.74, [RegionBiome.Drylands, RegionBiome.Highlands]),
            Producer("Frost Thistle", 0.24, 0.28, 0.62, 0.00, 0.80, [RegionBiome.Highlands, RegionBiome.Mountains]),
            Producer("Marsh Bloom", 0.64, 0.84, 0.84, 0.00, 1.02, [RegionBiome.Wetlands, RegionBiome.RiverValley]),
            Producer("Stonepine Cone", 0.40, 0.52, 0.78, 0.02, 0.88, [RegionBiome.Forest, RegionBiome.Mountains, RegionBiome.Highlands]),

            Herbivore("Stonehorn Elk", 0.58, 0.56, 0.76, 0.10, 1.04, 0.22, 0.18, 22, 0.32, 0.24, false, 0.62,
                [RegionBiome.Forest, RegionBiome.Plains, RegionBiome.RiverValley], ["Tallgrass", "Silver Moss", "Stonepine Cone"]),
            Herbivore("Fen Boar", 0.56, 0.66, 0.70, 0.12, 0.94, 0.20, 0.18, 18, 0.24, 0.20, false, 0.36,
                [RegionBiome.Wetlands, RegionBiome.Forest, RegionBiome.RiverValley], ["River Reed", "Marsh Bloom", "Redcap Mushroom"]),
            Herbivore("Dawn Bison", 0.64, 0.40, 0.82, 0.08, 1.08, 0.18, 0.16, 28, 0.36, 0.34, false, 0.24,
                [RegionBiome.Plains, RegionBiome.RiverValley, RegionBiome.Drylands], ["Tallgrass", "Sunroot", "Bitter Brush"]),
            Herbivore("Moor Hare", 0.52, 0.58, 0.68, 0.06, 0.88, 0.26, 0.22, 8, 0.16, 0.08, false, 0.18,
                [RegionBiome.Forest, RegionBiome.Plains, RegionBiome.Highlands], ["Tallgrass", "Silver Moss", "Stonepine Cone"]),
            Herbivore("Bristle Goat", 0.38, 0.30, 0.62, 0.10, 0.78, 0.24, 0.20, 14, 0.30, 0.26, false, 0.42,
                [RegionBiome.Highlands, RegionBiome.Mountains, RegionBiome.Drylands], ["Bitter Brush", "Frost Thistle", "Stonepine Cone"]),
            Herbivore("Marsh Grazer", 0.60, 0.82, 0.78, 0.08, 0.96, 0.18, 0.16, 20, 0.26, 0.20, false, 0.38,
                [RegionBiome.Wetlands, RegionBiome.RiverValley, RegionBiome.Coast], ["River Reed", "Marsh Bloom", "Salt Kelp"]),
            Herbivore("Sand Strider", 0.28, 0.20, 0.52, 0.06, 0.74, 0.32, 0.24, 12, 0.28, 0.18, false, 0.16,
                [RegionBiome.Drylands, RegionBiome.Coast, RegionBiome.Highlands], ["Bitter Brush", "Sunroot"]),
            Herbivore("Cliff Ram", 0.32, 0.26, 0.58, 0.08, 0.76, 0.20, 0.18, 17, 0.34, 0.30, false, 0.22,
                [RegionBiome.Mountains, RegionBiome.Highlands], ["Frost Thistle", "Stonepine Cone"]),

            Omnivore("Cinder Fox", 0.34, 0.28, 0.46, 0.38, 0.82, 0.28, 0.22, 9, 0.18, 0.16, false, 0.10,
                [RegionBiome.Forest, RegionBiome.Plains, RegionBiome.Highlands], ["Moor Hare", "Redcap Mushroom", "Stonepine Cone"]),
            Omnivore("River Otter", 0.26, 0.44, 0.42, 0.52, 0.78, 0.24, 0.20, 8, 0.22, 0.12, false, 0.08,
                [RegionBiome.RiverValley, RegionBiome.Coast, RegionBiome.Wetlands], ["Reed Crab", "River Reed", "Marsh Bloom"]),
            Omnivore("Brush Boar", 0.24, 0.22, 0.44, 0.36, 0.84, 0.18, 0.16, 15, 0.22, 0.18, false, 0.20,
                [RegionBiome.Forest, RegionBiome.Drylands, RegionBiome.Plains], ["Bitter Brush", "Sunroot", "Moor Hare"]),
            Omnivore("Reed Crab", 0.08, 0.06, 0.30, 0.18, 0.72, 0.16, 0.14, 5, 0.10, 0.04, false, 0.00,
                [RegionBiome.Coast, RegionBiome.Wetlands, RegionBiome.RiverValley], ["River Reed", "Salt Kelp"]),

            Predator("Ashfang Wolf", 0.42, 0.38, 0.22, 0.74, 0.72, 0.32, 0.26, 14, 0.42, 0.44,
                [RegionBiome.Forest, RegionBiome.Highlands, RegionBiome.Plains], ["Stonehorn Elk", "Moor Hare", "Cinder Fox", "Fen Boar"]),
            Predator("Marsh Stalker", 0.54, 0.78, 0.18, 0.78, 0.68, 0.24, 0.20, 16, 0.44, 0.46,
                [RegionBiome.Wetlands, RegionBiome.RiverValley, RegionBiome.Coast], ["Marsh Grazer", "Fen Boar", "Reed Crab", "River Otter"]),
            Predator("Skyfang Eagle", 0.34, 0.30, 0.16, 0.82, 0.58, 0.34, 0.28, 10, 0.40, 0.38,
                [RegionBiome.Highlands, RegionBiome.Mountains, RegionBiome.Coast], ["Moor Hare", "Sand Strider", "River Otter"]),
            Apex("Ridge Lion", 0.36, 0.30, 0.16, 0.82, 0.54, 0.34, 0.28, 30, 0.56, 0.72,
                [RegionBiome.Highlands, RegionBiome.Plains, RegionBiome.Drylands], ["Stonehorn Elk", "Dawn Bison", "Cliff Ram", "Sand Strider"]),
            Apex("Cliff Serpent", 0.30, 0.26, 0.12, 0.86, 0.46, 0.28, 0.24, 22, 0.52, 0.68,
                [RegionBiome.Mountains, RegionBiome.Coast, RegionBiome.Highlands], ["Cliff Ram", "Skyfang Eagle", "Bristle Goat"])
        };

    private static SpeciesTemplate Sapient(string name, TrophicRole trophicRole, double fertilityPreference, double waterPreference,
        double plantBiomassAffinity, double animalBiomassAffinity, double carryingCapacityFactor, double migrationCapability,
        double expansionPressure, double meatYield, double huntingDifficulty, double huntingDanger, bool isToxicToEat,
        double domesticationAffinity, IReadOnlyList<RegionBiome> preferredBiomes, IReadOnlyList<string> dietSpeciesNames)
        => new(name, trophicRole == TrophicRole.Predator ? 0.70 : 0.80, trophicRole == TrophicRole.Herbivore ? 0.78 : 0.72, true,
            trophicRole, fertilityPreference, waterPreference, plantBiomassAffinity, animalBiomassAffinity, carryingCapacityFactor,
            migrationCapability, expansionPressure, 0.07, 0.03, 1.12, 1.02, 0.92, 0.66, meatYield, huntingDifficulty, huntingDanger,
            isToxicToEat, domesticationAffinity, preferredBiomes, dietSpeciesNames);

    private static SpeciesTemplate Producer(string name, double fertilityPreference, double waterPreference, double plantBiomassAffinity,
        double animalBiomassAffinity, double carryingCapacityFactor, IReadOnlyList<RegionBiome> preferredBiomes, bool isToxicToEat = false)
        => new(name, 0.04, 0.02, false, TrophicRole.Producer, fertilityPreference, waterPreference, plantBiomassAffinity,
            animalBiomassAffinity, carryingCapacityFactor, 0.10, 0.20, 0.16, 0.02, 1.34, 1.12, 0.92, 0.58, 0, 0.96, 0.00,
            isToxicToEat, 0.00, preferredBiomes, []);

    private static SpeciesTemplate Herbivore(string name, double fertilityPreference, double waterPreference, double plantBiomassAffinity,
        double animalBiomassAffinity, double carryingCapacityFactor, double migrationCapability, double expansionPressure,
        double meatYield, double huntingDifficulty, double huntingDanger, bool isToxicToEat, double domesticationAffinity,
        IReadOnlyList<RegionBiome> preferredBiomes, IReadOnlyList<string> dietSpeciesNames)
        => new(name, 0.18, 0.28, false, TrophicRole.Herbivore, fertilityPreference, waterPreference, plantBiomassAffinity,
            animalBiomassAffinity, carryingCapacityFactor, migrationCapability, expansionPressure, 0.09, 0.03, 1.24, 1.06, 0.90,
            0.58, meatYield, huntingDifficulty, huntingDanger, isToxicToEat, domesticationAffinity, preferredBiomes, dietSpeciesNames);

    private static SpeciesTemplate Omnivore(string name, double fertilityPreference, double waterPreference, double plantBiomassAffinity,
        double animalBiomassAffinity, double carryingCapacityFactor, double migrationCapability, double expansionPressure,
        double meatYield, double huntingDifficulty, double huntingDanger, bool isToxicToEat, double domesticationAffinity,
        IReadOnlyList<RegionBiome> preferredBiomes, IReadOnlyList<string> dietSpeciesNames)
        => new(name, 0.22, 0.24, false, TrophicRole.Omnivore, fertilityPreference, waterPreference, plantBiomassAffinity,
            animalBiomassAffinity, carryingCapacityFactor, migrationCapability, expansionPressure, 0.08, 0.04, 1.18, 1.04, 0.92,
            0.62, meatYield, huntingDifficulty, huntingDanger, isToxicToEat, domesticationAffinity, preferredBiomes, dietSpeciesNames);

    private static SpeciesTemplate Predator(string name, double fertilityPreference, double waterPreference, double plantBiomassAffinity,
        double animalBiomassAffinity, double carryingCapacityFactor, double migrationCapability, double expansionPressure,
        double meatYield, double huntingDifficulty, double huntingDanger, IReadOnlyList<RegionBiome> preferredBiomes,
        IReadOnlyList<string> dietSpeciesNames)
        => new(name, 0.16, 0.34, false, TrophicRole.Predator, fertilityPreference, waterPreference, plantBiomassAffinity,
            animalBiomassAffinity, carryingCapacityFactor, migrationCapability, expansionPressure, 0.07, 0.05, 1.06, 1.00, 0.92,
            0.68, meatYield, huntingDifficulty, huntingDanger, false, 0.10, preferredBiomes, dietSpeciesNames);

    private static SpeciesTemplate Apex(string name, double fertilityPreference, double waterPreference, double plantBiomassAffinity,
        double animalBiomassAffinity, double carryingCapacityFactor, double migrationCapability, double expansionPressure,
        double meatYield, double huntingDifficulty, double huntingDanger, IReadOnlyList<RegionBiome> preferredBiomes,
        IReadOnlyList<string> dietSpeciesNames)
        => new(name, 0.18, 0.18, false, TrophicRole.Apex, fertilityPreference, waterPreference, plantBiomassAffinity,
            animalBiomassAffinity, carryingCapacityFactor, migrationCapability, expansionPressure, 0.05, 0.05, 1.00, 1.02, 0.94,
            0.70, meatYield, huntingDifficulty, huntingDanger, false, 0.04, preferredBiomes, dietSpeciesNames);
}

internal sealed record SpeciesTemplate(
    string Name,
    double Intelligence,
    double Cooperation,
    bool IsSapient,
    TrophicRole TrophicRole,
    double FertilityPreference,
    double WaterPreference,
    double PlantBiomassAffinity,
    double AnimalBiomassAffinity,
    double BaseCarryingCapacityFactor,
    double MigrationCapability,
    double ExpansionPressure,
    double BaseReproductionRate,
    double BaseDeclineRate,
    double SpringModifier,
    double SummerModifier,
    double AutumnModifier,
    double WinterModifier,
    double MeatYield,
    double HuntingDifficulty,
    double HuntingDanger,
    bool IsToxicToEat,
    double DomesticationAffinity,
    IReadOnlyList<RegionBiome> PreferredBiomes,
    IReadOnlyList<string> DietSpeciesNames);
