using LivingWorld.Advancement;
using LivingWorld.Life;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public static class ChronicleTextFormatter
{
    public sealed record StatusKnowledgeSummary(string Discoveries, string Learned);

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

        if (annualFoodRatio < 0.90)
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

    public static StatusKnowledgeSummary BuildStatusKnowledgeSummary(Polity polity)
        => new(
            Discoveries: DescribeDiscoveries(polity),
            Learned: DescribeLearnedAdvancements(polity));

    public static StatusKnowledgeSummary BuildStatusKnowledgeSummary(
        IReadOnlyList<string> discoveries,
        IReadOnlyList<string> learnedCapabilities)
        => new(
            Discoveries: DescribeDiscoveries(discoveries),
            Learned: DescribeLearnedAdvancements(learnedCapabilities));

    public static string DescribeDiscoveries(Polity polity)
        => DescribeDiscoveries(polity.Discoveries
            .OrderBy(discovery => discovery.Category)
            .ThenBy(discovery => discovery.Summary, StringComparer.Ordinal)
            .Select(discovery => discovery.Summary)
            .ToArray());

    public static string DescribeDiscoveries(IReadOnlyList<string> discoveries)
    {
        if (discoveries.Count == 0)
        {
            return "None yet";
        }

        string primary = discoveries[0];

        return discoveries.Count == 1
            ? primary
            : $"{primary}, +{discoveries.Count - 1} more";
    }

    public static string DescribeLearnedAdvancements(Polity polity)
        => DescribeLearnedAdvancements(polity.Advancements
            .OrderBy(id => id)
            .Select(id => AdvancementCatalog.Get(id).Name)
            .ToArray());

    public static string DescribeLearnedAdvancements(IReadOnlyList<string> learnedCapabilities)
    {
        if (learnedCapabilities.Count == 0)
        {
            return "None yet";
        }

        if (learnedCapabilities.Count == 1)
        {
            return learnedCapabilities[0];
        }

        string first = learnedCapabilities[0];
        return $"{first}, +{learnedCapabilities.Count - 1} more";
    }

    public static string RenderPopulationDelta(int delta)
    {
        if (delta > 0)
        {
            return $"+{delta}";
        }

        return delta.ToString();
    }

    public static string DescribeSpeciesName(Polity? polity, IReadOnlyCollection<Species> speciesCatalog)
    {
        if (polity is null)
        {
            return "Unknown";
        }

        return speciesCatalog.FirstOrDefault(species => species.Id == polity.SpeciesId)?.Name
            ?? "Unknown";
    }
}
