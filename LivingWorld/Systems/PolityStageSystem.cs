using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class PolityStageSystem
{
    private static class Thresholds
    {
        public const int TribeMinPopulation = 45;
        public const int TribeMinYears = 4;
        public const double TribeMinAnnualFoodRatio = 0.85;
        public const int TribeMaxStarvationMonths = 4;

        public const int SettledSocietyMinPopulation = 70;
        public const int SettledSocietyMinYears = 8;
        public const int SettledSocietyMinSettlementYears = 2;
        public const double SettledSocietyMinAnnualFoodRatio = 0.90;

        public const int CivilizationMinPopulation = 130;
        public const int CivilizationMinYears = 16;
        public const int CivilizationMinSettlements = 2;
        public const int CivilizationMinSettlementYears = 8;
        public const int CivilizationMinAdvancements = 4;
        public const double CivilizationMinAnnualFoodRatio = 0.98;
        public const int CivilizationMaxStarvationMonths = 1;
        public const int CivilizationMaxFoodStressYears = 0;
    }

    public void UpdatePolityStages(World world)
    {
        if (world.Time.Month != 12)
        {
            return;
        }

        foreach (Polity polity in world.Polities.Where(p => p.Population > 0).OrderBy(p => p.Id))
        {
            PolityStage current = polity.Stage;
            PolityStage evaluated = EvaluateStage(polity);

            // v1 stage progression is advancement-only.
            if (evaluated <= current)
            {
                continue;
            }

            polity.Stage = evaluated;

            world.AddEvent(
                WorldEventType.StageChanged,
                ResolveStageSeverity(evaluated),
                BuildStageChangeNarrative(polity, evaluated),
                $"{polity.Name} advanced from {current} to {evaluated}.",
                reason: "stage_threshold_met",
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: world.Species.First(species => species.Id == polity.SpeciesId).Name,
                regionId: polity.RegionId,
                before: new Dictionary<string, string>
                {
                    ["stage"] = current.ToString()
                },
                after: new Dictionary<string, string>
                {
                    ["stage"] = evaluated.ToString()
                });
        }
    }

    private static PolityStage EvaluateStage(Polity polity)
    {
        if (QualifiesForCivilization(polity))
        {
            return PolityStage.Civilization;
        }

        if (QualifiesForSettledSociety(polity))
        {
            return PolityStage.SettledSociety;
        }

        if (QualifiesForTribe(polity))
        {
            return PolityStage.Tribe;
        }

        return PolityStage.Band;
    }

    private static bool QualifiesForTribe(Polity polity)
    {
        double annualFoodRatio = GetAnnualFoodRatio(polity);

        return polity.Population >= Thresholds.TribeMinPopulation
            && polity.YearsSinceFounded >= Thresholds.TribeMinYears
            && annualFoodRatio >= Thresholds.TribeMinAnnualFoodRatio
            && polity.StarvationMonthsThisYear <= Thresholds.TribeMaxStarvationMonths;
    }

    private static bool QualifiesForSettledSociety(Polity polity)
    {
        double annualFoodRatio = GetAnnualFoodRatio(polity);
        bool hasDurableSettlement = polity.SettlementStatus == SettlementStatus.Settled
            || (polity.SettlementCount > 0
                && polity.YearsSinceFirstSettlement >= Thresholds.SettledSocietyMinSettlementYears);

        return hasDurableSettlement
            && polity.Population >= Thresholds.SettledSocietyMinPopulation
            && polity.YearsSinceFounded >= Thresholds.SettledSocietyMinYears
            && annualFoodRatio >= Thresholds.SettledSocietyMinAnnualFoodRatio;
    }

    private static bool QualifiesForCivilization(Polity polity)
    {
        double annualFoodRatio = GetAnnualFoodRatio(polity);

        return polity.SettlementStatus == SettlementStatus.Settled
            && polity.SettlementCount >= Thresholds.CivilizationMinSettlements
            && polity.YearsSinceFirstSettlement >= Thresholds.CivilizationMinSettlementYears
            && polity.Population >= Thresholds.CivilizationMinPopulation
            && polity.YearsSinceFounded >= Thresholds.CivilizationMinYears
            && polity.Advancements.Count >= Thresholds.CivilizationMinAdvancements
            && annualFoodRatio >= Thresholds.CivilizationMinAnnualFoodRatio
            && polity.StarvationMonthsThisYear <= Thresholds.CivilizationMaxStarvationMonths
            && polity.FoodStressYears <= Thresholds.CivilizationMaxFoodStressYears;
    }

    private static double GetAnnualFoodRatio(Polity polity)
        => polity.AnnualFoodNeeded <= 0
            ? 1.0
            : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

    private static string BuildStageChangeNarrative(Polity polity, PolityStage stage)
        => stage switch
        {
            PolityStage.Tribe => $"{polity.Name} became a Tribe",
            PolityStage.SettledSociety => $"{polity.Name} became a Settled Society",
            PolityStage.Civilization => $"{polity.Name} formed a Civilization",
            _ => $"{polity.Name} changed stage"
        };

    private static WorldEventSeverity ResolveStageSeverity(PolityStage stage)
        => stage switch
        {
            PolityStage.Civilization => WorldEventSeverity.Legendary,
            PolityStage.Tribe or PolityStage.SettledSociety => WorldEventSeverity.Major,
            _ => WorldEventSeverity.Notable
        };
}
