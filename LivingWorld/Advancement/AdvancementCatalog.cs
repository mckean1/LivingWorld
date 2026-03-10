namespace LivingWorld.Advancement;

public static class AdvancementCatalog
{
    public static IReadOnlyList<AdvancementDefinition> All { get; } =
    [
        new AdvancementDefinition(
            AdvancementId.OrganizedHunting,
            "Organized Hunting",
            "Coordinated hunting parties improve how large groups track, drive, and harvest animals.",
            prerequisites: null,
            discoveryChance: context =>
            {
                double chance = 0.010;
                chance += context.IsMobile ? 0.018 : 0.004;
                chance += (1.0 - context.Region.Fertility) * 0.010;
                chance += context.Region.AnimalBiomass / Math.Max(1.0, context.Region.MaxAnimalBiomass) * 0.016;
                chance += context.LocalPopulationRatio * 0.018;
                chance += context.Species.Cooperation * 0.015;
                chance += context.Polity.HasAdvancement(AdvancementId.Agriculture) ? -0.015 : 0.008;
                return ClampChance(chance);
            }),
        new AdvancementDefinition(
            AdvancementId.SeasonalPlanning,
            "Seasonal Planning",
            "Shared expectations about lean and abundant seasons improve preparation and coordination across the year.",
            prerequisites: null,
            discoveryChance: context =>
            {
                double chance = 0.008;
                chance += context.FoodStressRatio * 0.040;
                chance += Math.Abs(1.0 - context.AnnualFoodRatio) * 0.018;
                chance += context.ReserveMonths < 0.5 ? 0.012 : 0.0;
                chance += context.Polity.YearsSinceFounded >= 3 ? 0.008 : 0.0;
                chance += context.Species.Intelligence * 0.012;
                return ClampChance(chance);
            }),
        new AdvancementDefinition(
            AdvancementId.FoodStorage,
            "Food Storage",
            "Drying, caching, and protecting food lets a people carry abundance forward into harder months.",
            prerequisites: [AdvancementId.SeasonalPlanning],
            discoveryChance: context =>
            {
                double chance = 0.006;
                chance += context.AnnualFoodRatio >= 1.0 ? 0.020 : 0.0;
                chance += context.FoodStressRatio >= 0.15 ? 0.015 : 0.0;
                chance += Math.Min(1.0, context.ReserveMonths / 2.0) * 0.020;
                chance += context.Region.Fertility * 0.008;
                chance += context.Species.Intelligence * 0.010;
                return ClampChance(chance);
            }),
        new AdvancementDefinition(
            AdvancementId.Agriculture,
            "Agriculture",
            "Deliberate cultivation begins to replace pure foraging, trading mobility for more predictable harvests.",
            prerequisites: [AdvancementId.SeasonalPlanning, AdvancementId.FoodStorage],
            discoveryChance: context =>
            {
                double chance = 0.003;
                chance += context.Region.Fertility * 0.028;
                chance += context.Region.WaterAvailability * 0.018;
                chance += Math.Min(1.0, context.CrowdingRatio) * 0.016;
                chance += context.IsMobile ? -0.010 : 0.012;
                chance += context.Polity.YearsSinceFounded >= 5 ? 0.008 : 0.0;
                chance += context.Species.Intelligence * 0.012;
                chance += context.Species.Cooperation * 0.006;
                return ClampChance(chance);
            }),
        new AdvancementDefinition(
            AdvancementId.BasicConstruction,
            "Basic Construction",
            "More durable shelters and shared building methods emerge as settlements become more stable.",
            prerequisites: [AdvancementId.SeasonalPlanning],
            discoveryChance: context =>
            {
                double chance = 0.006;
                chance += context.Polity.HasAdvancement(AdvancementId.Agriculture) ? 0.026 : 0.0;
                chance += context.IsMobile ? -0.010 : 0.012;
                chance += context.LocalPopulationRatio * 0.012;
                chance += context.Polity.HasAdvancement(AdvancementId.LeadershipTraditions) ? 0.012 : 0.0;
                chance += context.Species.Cooperation * 0.014;
                return ClampChance(chance);
            }),
        new AdvancementDefinition(
            AdvancementId.LeadershipTraditions,
            "Leadership Traditions",
            "Recognized authority and custom help larger groups coordinate decisions and maintain cohesion.",
            prerequisites: null,
            discoveryChance: context =>
            {
                double chance = 0.008;
                chance += context.LocalPopulationRatio * 0.030;
                chance += Math.Min(1.0, context.CrowdingRatio) * 0.010;
                chance += context.Species.Cooperation * 0.018;
                chance += context.Polity.YearsSinceFounded >= 4 ? 0.010 : 0.0;
                return ClampChance(chance);
            }),
        new AdvancementDefinition(
            AdvancementId.CraftSpecialization,
            "Craft Specialization",
            "Reliable surplus and denser communities allow some people to focus on tools, materials, and skilled trades.",
            prerequisites: [AdvancementId.FoodStorage],
            discoveryChance: context =>
            {
                double chance = 0.004;
                chance += context.AnnualFoodRatio >= 1.0 ? 0.018 : 0.0;
                chance += Math.Min(1.0, context.ReserveMonths / 3.0) * 0.020;
                chance += context.LocalPopulationRatio * 0.024;
                chance += context.Polity.HasAdvancement(AdvancementId.BasicConstruction) ? 0.010 : 0.0;
                chance += context.Polity.HasAdvancement(AdvancementId.LeadershipTraditions) ? 0.010 : 0.0;
                chance += context.Species.Intelligence * 0.010;
                return ClampChance(chance);
            })
    ];

    public static AdvancementDefinition Get(AdvancementId id)
        => All.First(definition => definition.Id == id);

    private static double ClampChance(double chance)
        => Math.Clamp(chance, 0.0, 0.35);
}
