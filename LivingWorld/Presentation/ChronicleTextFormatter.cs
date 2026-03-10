using LivingWorld.Advancement;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public static class ChronicleTextFormatter
{
    public static string DescribeFoodState(Polity polity)
    {
        double annualFoodRatio = polity.AnnualFoodNeeded <= 0
            ? 1.0
            : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

        if (polity.StarvationMonthsThisYear >= 6 || annualFoodRatio < 0.55)
        {
            return "Famine";
        }

        if (polity.StarvationMonthsThisYear >= 2 || annualFoodRatio < 0.85)
        {
            return "Hunger";
        }

        if (annualFoodRatio > 1.05 && polity.FoodStores >= polity.Population * 0.6)
        {
            return "Surplus";
        }

        return "Stable";
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
