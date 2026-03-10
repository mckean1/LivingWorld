using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class SettlementSystem
{
    private readonly Random _random;
    private const double MinimumViableFoodRatio = 0.85;

    public SettlementSystem(int seed = 67890)
    {
        _random = new Random(seed);
    }

    public void UpdateSettlements(World world)
    {
        if (world.Time.Month != 12)
        {
            return;
        }

        foreach (Polity polity in world.Polities.Where(p => p.Population > 0))
        {
            bool alreadyHadSettlement = polity.HasSettlements;
            Region region = world.Regions.First(r => r.Id == polity.RegionId);
            int residenceYears = GetResidenceYearsAfterCurrentYear(polity);

            switch (polity.SettlementStatus)
            {
                case SettlementStatus.Nomadic:
                    TryEstablishFirstSettlement(world, polity, region, residenceYears);
                    break;
                case SettlementStatus.SemiSettled:
                    TryBecomeSettledSociety(world, polity, region, residenceYears);
                    break;
            }

            if (alreadyHadSettlement)
            {
                polity.YearsSinceFirstSettlement++;
            }

            polity.YearsInCurrentRegion = residenceYears;
        }
    }

    private void TryEstablishFirstSettlement(World world, Polity polity, Region region, int residenceYears)
    {
        double chance = CalculateFirstSettlementChance(world, polity, region, residenceYears);
        if (_random.NextDouble() > chance)
        {
            return;
        }

        polity.SettlementStatus = SettlementStatus.SemiSettled;
        polity.SettlementCount = Math.Max(1, polity.SettlementCount);
        polity.YearsSinceFirstSettlement = 0;

        world.AddEvent(
            "SETTLEMENT",
            BuildFirstSettlementNarrative(polity, region),
            $"{polity.Name} established its first settlement in {region.Name}; chance={chance:F3}.");
    }

    private void TryBecomeSettledSociety(World world, Polity polity, Region region, int residenceYears)
    {
        double chance = CalculateConsolidationChance(polity, region, residenceYears);
        if (_random.NextDouble() > chance)
        {
            return;
        }

        polity.SettlementStatus = SettlementStatus.Settled;

        world.AddEvent(
            "SETTLEMENT",
            BuildSettledSocietyNarrative(polity, region),
            $"{polity.Name} consolidated into a settled society in {region.Name}; chance={chance:F3}.");
    }

    private static double CalculateFirstSettlementChance(World world, Polity polity, Region region, int residenceYears)
    {
        if (polity.Population < 20)
        {
            return 0.0;
        }

        double annualFoodRatio = GetAnnualFoodRatio(polity);
        if (annualFoodRatio < MinimumViableFoodRatio)
        {
            return 0.0;
        }

        if (!HasSettlementKnowledge(polity))
        {
            return 0.0;
        }

        double reserveMonths = GetReserveMonths(polity);
        double organizationRatio = GetOrganizationRatio(world, polity, region);
        double residenceRatio = Math.Clamp(residenceYears / 4.0, 0.0, 1.0);
        double starvationRatio = Math.Clamp(polity.StarvationMonthsThisYear / 4.0, 0.0, 1.0);
        double stabilityRatio = 1.0 - Math.Clamp(polity.MigrationPressure, 0.0, 1.0);

        double chance = 0.0;
        chance += polity.Capabilities.CanFarm ? 0.090 : 0.0;
        chance += polity.HasAdvancement(AdvancementId.BasicConstruction) ? 0.065 : 0.0;
        chance += polity.HasAdvancement(AdvancementId.FoodStorage) ? 0.035 : 0.0;
        chance += polity.HasAdvancement(AdvancementId.LeadershipTraditions)
            ? 0.015 + (organizationRatio * 0.020)
            : 0.0;

        chance += Math.Clamp((polity.Population - 25) / 85.0, 0.0, 1.0) * 0.030;
        chance += region.Fertility * 0.035;
        chance += Math.Clamp((annualFoodRatio - MinimumViableFoodRatio) / 0.35, 0.0, 1.0) * 0.035;
        chance += Math.Clamp(reserveMonths / 1.5, 0.0, 1.0) * 0.020;
        chance += residenceRatio * 0.040;
        chance += stabilityRatio * 0.020;

        chance -= starvationRatio * 0.070;
        chance -= polity.MovedThisYear ? 0.060 : 0.0;
        chance -= Math.Clamp(polity.MovesThisYear - 1, 0, 3) * 0.030;
        chance -= polity.Population < 30 ? 0.020 : 0.0;
        chance -= residenceYears < 2 ? 0.030 : 0.0;

        return Math.Clamp(chance, 0.0, 0.30);
    }

    private static double CalculateConsolidationChance(Polity polity, Region region, int residenceYears)
    {
        double annualFoodRatio = GetAnnualFoodRatio(polity);
        if (annualFoodRatio < 0.90)
        {
            return 0.0;
        }

        double reserveMonths = GetReserveMonths(polity);
        double residenceRatio = Math.Clamp(residenceYears / 5.0, 0.0, 1.0);
        double stabilityRatio = 1.0 - Math.Clamp(polity.MigrationPressure, 0.0, 1.0);

        double chance = 0.010;
        chance += polity.Capabilities.CanFarm ? 0.045 : 0.0;
        chance += polity.HasAdvancement(AdvancementId.BasicConstruction) ? 0.040 : 0.0;
        chance += polity.HasAdvancement(AdvancementId.FoodStorage) ? 0.020 : 0.0;
        chance += polity.HasAdvancement(AdvancementId.LeadershipTraditions) ? 0.020 : 0.0;
        chance += Math.Clamp((polity.Population - 30) / 100.0, 0.0, 1.0) * 0.020;
        chance += region.Fertility * 0.015;
        chance += Math.Clamp((annualFoodRatio - 0.90) / 0.25, 0.0, 1.0) * 0.025;
        chance += Math.Clamp(reserveMonths / 2.0, 0.0, 1.0) * 0.015;
        chance += residenceRatio * 0.030;
        chance += Math.Clamp(polity.YearsSinceFirstSettlement / 3.0, 0.0, 1.0) * 0.025;
        chance += stabilityRatio * 0.015;

        chance -= polity.MovedThisYear ? 0.050 : 0.0;
        chance -= Math.Clamp(polity.StarvationMonthsThisYear / 4.0, 0.0, 1.0) * 0.060;
        chance -= residenceYears < 3 ? 0.025 : 0.0;

        return Math.Clamp(chance, 0.0, 0.25);
    }

    private static double GetAnnualFoodRatio(Polity polity)
        => polity.AnnualFoodNeeded <= 0
            ? 1.0
            : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

    private static double GetReserveMonths(Polity polity)
        => polity.Population <= 0
            ? 0.0
            : polity.FoodStores / polity.Population;

    private static double GetOrganizationRatio(World world, Polity polity, Region region)
    {
        int localPopulation = world.Polities
            .Where(p => p.RegionId == polity.RegionId && p.Population > 0)
            .Sum(p => p.Population);

        return region.CarryingCapacity <= 0
            ? 0.0
            : Math.Clamp(localPopulation / region.CarryingCapacity, 0.0, 1.0);
    }

    private static int GetResidenceYearsAfterCurrentYear(Polity polity)
        => polity.MovedThisYear ? 0 : polity.YearsInCurrentRegion + 1;

    private static bool HasSettlementKnowledge(Polity polity)
        => polity.Capabilities.CanFarm
            || polity.HasAdvancement(AdvancementId.BasicConstruction)
            || (polity.HasAdvancement(AdvancementId.FoodStorage)
                && polity.HasAdvancement(AdvancementId.LeadershipTraditions));

    private static string BuildFirstSettlementNarrative(Polity polity, Region region)
        => polity.Capabilities.CanFarm
            ? $"{polity.Name} founded a settlement in {region.Name}"
            : $"{polity.Name} founded a settlement in {region.Name}";

    private static string BuildSettledSocietyNarrative(Polity polity, Region region)
        => polity.HasAdvancement(AdvancementId.LeadershipTraditions)
            ? $"{polity.Name} became a settled society in {region.Name}"
            : $"{polity.Name} became a settled people in {region.Name}";
}
