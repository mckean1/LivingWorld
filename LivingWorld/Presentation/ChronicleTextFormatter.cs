using LivingWorld.Advancement;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public static class ChronicleTextFormatter
{
    public static ChronicleFoodCondition ResolveChronicleFoodCondition(Polity polity, int startPopulation)
    {
        double annualFoodRatio = polity.AnnualFoodNeeded <= 0
            ? 1.0
            : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

        bool lostPeople = polity.Population < startPopulation;
        bool starvationOccurred = polity.StarvationMonthsThisYear >= 6;

        if (starvationOccurred && lostPeople)
        {
            return ChronicleFoodCondition.Starvation;
        }

        if (polity.StarvationMonthsThisYear >= 6 || annualFoodRatio < 0.55)
        {
            return ChronicleFoodCondition.Famine;
        }

        if (polity.StarvationMonthsThisYear >= 2 || annualFoodRatio < 0.85)
        {
            return ChronicleFoodCondition.Hunger;
        }

        if (annualFoodRatio < 1.0)
        {
            return ChronicleFoodCondition.Shortage;
        }

        if (annualFoodRatio > 1.05 && polity.FoodStores >= polity.Population * 0.6)
        {
            return ChronicleFoodCondition.Surplus;
        }

        return ChronicleFoodCondition.Stable;
    }

    public static string? DescribeFoodConditionNarrative(string polityName, ChronicleFoodCondition condition)
    {
        return condition switch
        {
            ChronicleFoodCondition.Starvation => $"{polityName} lost people to starvation.",
            ChronicleFoodCondition.Famine => $"{polityName} endured famine.",
            ChronicleFoodCondition.Hunger => $"{polityName} suffered food shortages.",
            ChronicleFoodCondition.Shortage => $"{polityName} endured a lean year.",
            ChronicleFoodCondition.Surplus => $"{polityName} enjoyed abundant harvests.",
            _ => null
        };
    }

    public static FoodStateSummary ResolveFoodState(Polity polity)
    {
        double annualFoodRatio = polity.AnnualFoodNeeded <= 0
            ? 1.0
            : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

        if (polity.StarvationMonthsThisYear >= 6 || annualFoodRatio < 0.55)
        {
            return FoodStateSummary.Famine;
        }

        if (polity.StarvationMonthsThisYear >= 2 || annualFoodRatio < 0.85)
        {
            return FoodStateSummary.Hunger;
        }

        if (annualFoodRatio > 1.05 && polity.FoodStores >= polity.Population * 0.6)
        {
            return FoodStateSummary.Surplus;
        }

        return FoodStateSummary.Stable;
    }

    public static string DescribeFoodState(Polity polity)
    {
        return DescribeFoodState(ResolveFoodState(polity));
    }

    public static string DescribeFoodState(FoodStateSummary state)
    {
        return state switch
        {
            FoodStateSummary.Famine => "Famine",
            FoodStateSummary.Hunger => "Hunger",
            FoodStateSummary.Surplus => "Surplus",
            _ => "Stable"
        };
    }

    public static string DescribeKnowledge(Polity polity)
    {
        if (polity.Advancements.Count == 0)
        {
            return "No major discoveries yet";
        }

        if (polity.Advancements.Count == 1)
        {
            AdvancementDefinition advancement = AdvancementCatalog.Get(polity.Advancements.First());
            return advancement.Name;
        }

        string topTwo = string.Join(
            ", ",
            polity.Advancements
                .OrderBy(id => id)
                .Take(2)
                .Select(id => AdvancementCatalog.Get(id).Name));

        return polity.Advancements.Count == 2
            ? topTwo
            : $"{topTwo}, +{polity.Advancements.Count - 2} more";
    }

    public static string RenderPopulationDelta(int delta)
    {
        if (delta > 0)
        {
            return $"+{delta}";
        }

        return delta.ToString();
    }
}
