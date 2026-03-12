namespace LivingWorld.Societies;

public sealed class ManagedHerd
{
    public int BaseSpeciesId { get; }
    public string VariantName { get; }
    public int EstablishedYear { get; }
    public int EstablishedMonth { get; }
    public int HerdSize { get; set; }
    public double Reliability { get; set; }
    public double FoodYieldPerMonth { get; set; }
    public double BreedingMultiplier { get; set; }
    public double AggressionReduction { get; set; }

    public ManagedHerd(
        int baseSpeciesId,
        string variantName,
        int establishedYear,
        int establishedMonth,
        int herdSize,
        double reliability,
        double foodYieldPerMonth,
        double breedingMultiplier,
        double aggressionReduction)
    {
        BaseSpeciesId = baseSpeciesId;
        VariantName = variantName;
        EstablishedYear = establishedYear;
        EstablishedMonth = establishedMonth;
        HerdSize = herdSize;
        Reliability = reliability;
        FoodYieldPerMonth = foodYieldPerMonth;
        BreedingMultiplier = breedingMultiplier;
        AggressionReduction = aggressionReduction;
    }
}
