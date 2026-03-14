using LivingWorld.Advancement;
using LivingWorld.Life;

namespace LivingWorld.Societies;

public static class PolityProfileResolver
{
    public static SubsistenceMode ResolveSubsistenceMode(Polity polity, Species? species = null)
    {
        int settlementCount = Math.Max(1, polity.SettlementCount);
        int cultivatedCropCount = polity.Settlements.Sum(settlement => settlement.CultivatedCrops.Count);
        int managedHerdCount = polity.Settlements.Sum(settlement => settlement.ManagedHerds.Count);
        double averageSettlementAge = polity.SettlementCount == 0
            ? 0.0
            : polity.Settlements.Average(settlement => settlement.YearsEstablished);
        double cultivatedLandPerSettlement = polity.CultivatedLand / settlementCount;
        double cultivationSignal = Math.Clamp(
            Math.Clamp(cultivatedLandPerSettlement / 1.4, 0.0, 1.0) * 0.38
            + Math.Min(0.20, cultivatedCropCount * 0.05)
            + Math.Min(0.10, managedHerdCount * 0.04)
            + Math.Min(0.14, polity.ConsecutiveFarmingYears * 0.04)
            + (polity.ManagedFoodSupplyEstablished ? 0.10 : 0.0)
            + (polity.HasAdvancement(AdvancementId.Agriculture) ? 0.08 : 0.0)
            + Math.Clamp(averageSettlementAge / 6.0, 0.0, 1.0) * 0.08,
            0.0,
            1.0);

        double huntingSignal = Math.Clamp(
            (species?.AnimalBiomassAffinity ?? 0.42) * 0.28
            + Math.Min(0.22, polity.SuccessfulHuntsBySpecies.Count * 0.05)
            + Math.Min(0.10, polity.KnownDangerousPreySpeciesIds.Count * 0.03)
            + (polity.MigrationPressure * 0.18)
            + (averageSettlementAge < 2.0 ? 0.06 : 0.0),
            0.0,
            1.0);

        int plantKnowledgeCount = polity.KnownEdibleSpeciesIds.Count
            + polity.Discoveries.Count(discovery => discovery.Category is CulturalDiscoveryCategory.FoodSafety or CulturalDiscoveryCategory.Environment);
        double foragingSignal = Math.Clamp(
            (species?.PlantBiomassAffinity ?? 0.42) * 0.28
            + Math.Min(0.24, plantKnowledgeCount * 0.04)
            + Math.Clamp(averageSettlementAge / 6.0, 0.0, 1.0) * 0.10
            + Math.Max(0.0, 0.10 - (polity.MigrationPressure * 0.06)),
            0.0,
            1.0);

        bool protoAgrarian = polity.HasAdvancement(AdvancementId.Agriculture)
            || polity.CultivatedLand >= 0.45
            || cultivatedCropCount > 0
            || polity.ManagedFoodSupplyEstablished;
        if (cultivationSignal >= 0.72 && (polity.ConsecutiveFarmingYears >= 2 || cultivatedCropCount > 0 || polity.ManagedFoodSupplyEstablished))
        {
            return SubsistenceMode.FarmingEmergent;
        }

        if (cultivationSignal >= 0.40 && protoAgrarian)
        {
            return SubsistenceMode.ProtoFarming;
        }

        if (huntingSignal >= foragingSignal + 0.12)
        {
            return SubsistenceMode.HuntingFocused;
        }

        if (foragingSignal >= huntingSignal + 0.12)
        {
            return SubsistenceMode.ForagingFocused;
        }

        return SubsistenceMode.MixedHunterForager;
    }
}
