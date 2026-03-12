namespace LivingWorld.Societies;

public sealed class CultivatedCrop
{
    public int BaseSpeciesId { get; }
    public string CropName { get; }
    public int EstablishedYear { get; }
    public int EstablishedMonth { get; }
    public double YieldMultiplier { get; set; }
    public double StabilityBonus { get; set; }
    public double SeasonalResilience { get; set; }

    public CultivatedCrop(
        int baseSpeciesId,
        string cropName,
        int establishedYear,
        int establishedMonth,
        double yieldMultiplier,
        double stabilityBonus,
        double seasonalResilience)
    {
        BaseSpeciesId = baseSpeciesId;
        CropName = cropName;
        EstablishedYear = establishedYear;
        EstablishedMonth = establishedMonth;
        YieldMultiplier = yieldMultiplier;
        StabilityBonus = stabilityBonus;
        SeasonalResilience = seasonalResilience;
    }
}
