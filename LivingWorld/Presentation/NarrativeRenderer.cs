using LivingWorld.Core;
using LivingWorld.Societies;
using LivingWorld.Advancement;

namespace LivingWorld.Presentation;

public sealed class NarrativeRenderer
{
    public IReadOnlyList<string> RenderTickChronicle(World world)
    {
        List<string> lines = new();
        List<WorldEvent> eventsThisMonth = world.Events
            .Where(e => e.Year == world.Time.Year && e.Month == world.Time.Month)
            .OrderBy(e => e.Type)
            .ToList();

        lines.Add($"{FormatMonth(world.Time.Month)}, Year {world.Time.Year}");
        lines.Add(BuildMonthlyOverview(world));

        foreach (Polity polity in world.Polities
                     .Where(p => p.Population > 0)
                     .OrderByDescending(p => p.Population)
                     .ThenBy(p => p.Name))
        {
            lines.Add($"- {RenderMonthlyPolityBeat(world, polity)}");
        }

        foreach (WorldEvent worldEvent in eventsThisMonth)
        {
            lines.Add($"  Event: {worldEvent.Narrative}");
        }

        lines.Add(string.Empty);
        return lines;
    }

    public IReadOnlyList<string> RenderYearReport(World world)
    {
        List<string> lines = new();
        List<WorldEvent> eventsThisYear = world.Events
            .Where(e => e.Year == world.Time.Year)
            .OrderBy(e => e.Month)
            .ThenBy(e => e.Type)
            .ToList();

        int activePolities = world.Polities.Count(p => p.Population > 0);
        int totalPopulation = world.Polities.Where(p => p.Population > 0).Sum(p => p.Population);

        lines.Add(string.Empty);
        lines.Add($"Year {world.Time.Year}");
        lines.Add(BuildWorldOverview(activePolities, totalPopulation, eventsThisYear.Count));
        lines.Add(string.Empty);

        foreach (Polity polity in world.Polities
                     .Where(p => p.Population > 0)
                     .OrderByDescending(p => p.Population)
                     .ThenBy(p => p.Name))
        {
            lines.Add(RenderPolitySummary(world, polity));
        }

        if (eventsThisYear.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("Notable events");

            foreach (WorldEvent worldEvent in eventsThisYear)
            {
                lines.Add($"- {FormatMonth(worldEvent.Month)}: {worldEvent.Narrative}");
            }
        }

        return lines;
    }

    private static string BuildWorldOverview(int activePolities, int totalPopulation, int eventCount)
    {
        string polityText = activePolities == 1 ? "one active people" : $"{activePolities} active peoples";
        string eventText = eventCount == 0
            ? "The year passed without a defining upheaval."
            : eventCount == 1
                ? "One event stood out strongly enough to enter the yearly chronicle."
                : $"{eventCount} events stood out strongly enough to enter the yearly chronicle.";

        return $"The world now holds {polityText} with a combined population of {totalPopulation}. {eventText}";
    }

    private static string BuildMonthlyOverview(World world)
    {
        int totalPopulation = world.Polities.Where(p => p.Population > 0).Sum(p => p.Population);
        int activePolities = world.Polities.Count(p => p.Population > 0);

        return world.Time.Season switch
        {
            Season.Winter => $"Cold weather grips the land. {activePolities} peoples endure the season, together numbering {totalPopulation}.",
            Season.Spring => $"The thaw returns. {activePolities} peoples move through a world of {totalPopulation} souls.",
            Season.Summer => $"The warm season holds. {activePolities} peoples range across the world, together numbering {totalPopulation}.",
            _ => $"Autumn settles in. {activePolities} peoples prepare for scarcity, together numbering {totalPopulation}."
        };
    }

    private static string RenderPolitySummary(World world, Polity polity)
    {
        RegionSnapshot region = GetRegionSnapshot(world, polity.RegionId);
        double annualFoodRatio = polity.AnnualFoodNeeded <= 0
            ? 1.0
            : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

        string condition = DescribeFoodCondition(polity, annualFoodRatio);
        string movement = DescribeMovement(polity);
        string growth = DescribePopulation(polity, annualFoodRatio);
        string stores = DescribeFoodStores(polity);
        string knowledge = DescribeKnowledge(polity);

        return $"{polity.Name} in {region.Name} {condition} {growth} {stores} {movement} {knowledge}";
    }

    private static string RenderMonthlyPolityBeat(World world, Polity polity)
    {
        RegionSnapshot region = GetRegionSnapshot(world, polity.RegionId);
        string foodBeat = polity.FoodSatisfactionThisMonth switch
        {
            >= 1.0 => "found enough food",
            >= 0.85 => "managed to eat, though supplies tightened",
            >= 0.60 => "went short on food",
            _ => "suffered a severe shortage"
        };

        string storeBeat = polity.FoodStores switch
        {
            <= 0 => "with no stores left",
            < 15 => "with only a little food in reserve",
            < 40 => "with modest reserves remaining",
            _ => "while keeping healthy reserves"
        };

        string movementBeat = polity.MovedThisYear && polity.PreviousRegionId != polity.RegionId
            ? $"after arriving in {region.Name}"
            : $"while holding to {region.Name}";

        return $"{polity.Name} {foodBeat} {movementBeat}, {storeBeat}.";
    }

    private static RegionSnapshot GetRegionSnapshot(World world, int regionId)
    {
        var region = world.Regions.First(r => r.Id == regionId);
        return new RegionSnapshot(region.Name);
    }

    private static string DescribeFoodCondition(Polity polity, double annualFoodRatio)
    {
        if (annualFoodRatio >= 1.0)
        {
            return "enjoyed a year of plenty.";
        }

        if (annualFoodRatio >= 0.9)
        {
            return "held steady through a modest but manageable year.";
        }

        if (annualFoodRatio >= 0.75)
        {
            return "endured a lean year, with meals growing uncertain.";
        }

        if (annualFoodRatio >= 0.5)
        {
            return "spent much of the year under harsh rationing.";
        }

        return "fell into a year of famine.";
    }

    private static string DescribePopulation(Polity polity, double annualFoodRatio)
    {
        if (annualFoodRatio >= 1.0)
        {
            return $"Its numbers rose to {polity.Population}.";
        }

        if (annualFoodRatio >= 0.9)
        {
            return $"Its numbers held at {polity.Population}.";
        }

        if (annualFoodRatio >= 0.75)
        {
            return $"Its strength slipped to {polity.Population}.";
        }

        return $"By year's end, only {polity.Population} remained.";
    }

    private static string DescribeFoodStores(Polity polity)
    {
        if (polity.FoodStores >= polity.Population * 0.75)
        {
            return "The storehouses are comfortably stocked.";
        }

        if (polity.FoodStores >= polity.Population * 0.30)
        {
            return "Their stores are serviceable, but not deep.";
        }

        return "Their remaining stores are dangerously thin.";
    }

    private static string DescribeMovement(Polity polity)
    {
        if (polity.MovesThisYear >= 2)
        {
            return "Restlessness gripped them, and they moved more than once.";
        }

        if (polity.MovedThisYear)
        {
            return "They uprooted themselves before the year was done.";
        }

        if (polity.MigrationPressure >= 0.65)
        {
            return "Talk of leaving spread among the people.";
        }

        if (polity.MigrationPressure >= 0.35)
        {
            return "Some families spoke quietly of better land elsewhere.";
        }

        return "They remained settled in their homeland.";
    }

    private static string DescribeKnowledge(Polity polity)
    {
        if (polity.Advancements.Count == 0)
        {
            return "Their shared knowledge remains practical and local.";
        }

        if (polity.Advancements.Count == 1)
        {
            AdvancementDefinition advancement = AdvancementCatalog.Get(polity.Advancements.OrderBy(id => id).First());
            return $"Their people are now known for {advancement.Name.ToLowerInvariant()}.";
        }

        string featured = string.Join(
            ", ",
            polity.Advancements
                .OrderBy(id => id)
                .Take(2)
                .Select(id => AdvancementCatalog.Get(id).Name.ToLowerInvariant()));

        return $"Their traditions now include {featured}.";
    }

    private static string FormatMonth(int month)
        => month switch
        {
            1 => "January",
            2 => "February",
            3 => "March",
            4 => "April",
            5 => "May",
            6 => "June",
            7 => "July",
            8 => "August",
            9 => "September",
            10 => "October",
            11 => "November",
            12 => "December",
            _ => $"Month {month}"
        };

    private readonly record struct RegionSnapshot(string Name);
}
