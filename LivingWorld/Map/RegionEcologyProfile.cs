namespace LivingWorld.Map;

public sealed record RegionEcologyProfile(
    double Temperature,
    double Moisture,
    double TerrainHarshness,
    double BasePrimaryProductivity,
    double HabitabilityScore,
    double MigrationEase,
    double EnvironmentalVolatility)
{
    public string ToDebugSummary()
        => $"Temp={Temperature:F2} Moist={Moisture:F2} Prod={BasePrimaryProductivity:F2} Habit={HabitabilityScore:F2} Move={MigrationEase:F2} Vol={EnvironmentalVolatility:F2}";
}
