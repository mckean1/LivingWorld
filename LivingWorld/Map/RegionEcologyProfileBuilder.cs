namespace LivingWorld.Map;

public static class RegionEcologyProfileBuilder
{
    public static RegionEcologyProfile Build(Region region)
    {
        double biomeTemperature = region.Biome switch
        {
            RegionBiome.Coast => 0.56,
            RegionBiome.RiverValley => 0.64,
            RegionBiome.Plains => 0.60,
            RegionBiome.Forest => 0.48,
            RegionBiome.Highlands => 0.34,
            RegionBiome.Mountains => 0.18,
            RegionBiome.Wetlands => 0.58,
            RegionBiome.Drylands => 0.82,
            _ => 0.50
        };

        double terrainHarshness = region.Biome switch
        {
            RegionBiome.RiverValley => 0.18,
            RegionBiome.Coast => 0.24,
            RegionBiome.Plains => 0.22,
            RegionBiome.Forest => 0.34,
            RegionBiome.Wetlands => 0.40,
            RegionBiome.Highlands => 0.58,
            RegionBiome.Mountains => 0.82,
            RegionBiome.Drylands => 0.66,
            _ => 0.38
        };

        double volatility = region.Biome switch
        {
            RegionBiome.RiverValley => 0.20,
            RegionBiome.Coast => 0.26,
            RegionBiome.Plains => 0.28,
            RegionBiome.Forest => 0.32,
            RegionBiome.Wetlands => 0.42,
            RegionBiome.Highlands => 0.54,
            RegionBiome.Mountains => 0.72,
            RegionBiome.Drylands => 0.68,
            _ => 0.36
        };

        double temperature = Math.Clamp((biomeTemperature * 0.72) + (region.WaterAvailability * 0.08) + ((1.0 - region.Fertility) * 0.12), 0.0, 1.0);
        double moisture = Math.Clamp((region.WaterAvailability * 0.72) + (region.Fertility * 0.12) + ResolveMoistureBiomeBias(region.Biome), 0.0, 1.0);
        double productivity = Math.Clamp(
            (region.Fertility * 0.36) +
            (region.WaterAvailability * 0.24) +
            (Math.Clamp(region.MaxPlantBiomass / 1300.0, 0.0, 1.0) * 0.28) +
            (Math.Clamp(region.MaxAnimalBiomass / 520.0, 0.0, 1.0) * 0.12) -
            (terrainHarshness * 0.12),
            0.0,
            1.0);
        double habitability = Math.Clamp(
            (productivity * 0.42) +
            (region.WaterAvailability * 0.24) +
            (region.Fertility * 0.16) +
            ((1.0 - terrainHarshness) * 0.10) +
            ((1.0 - volatility) * 0.08),
            0.0,
            1.0);
        double migrationEase = Math.Clamp(
            (Math.Min(4, region.ConnectedRegionIds.Count) / 4.0 * 0.40) +
            ((1.0 - terrainHarshness) * 0.32) +
            ((1.0 - volatility) * 0.16) +
            (region.WaterAvailability * 0.12),
            0.0,
            1.0);

        return new RegionEcologyProfile(
            temperature,
            moisture,
            terrainHarshness,
            productivity,
            habitability,
            migrationEase,
            volatility);
    }

    private static double ResolveMoistureBiomeBias(RegionBiome biome)
        => biome switch
        {
            RegionBiome.Coast => 0.12,
            RegionBiome.RiverValley => 0.16,
            RegionBiome.Forest => 0.08,
            RegionBiome.Wetlands => 0.20,
            RegionBiome.Drylands => -0.12,
            RegionBiome.Mountains => -0.04,
            _ => 0.0
        };
}
