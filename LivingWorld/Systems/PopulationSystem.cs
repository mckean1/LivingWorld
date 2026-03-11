using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class PopulationSystem
{
    private static readonly int[] PopulationMilestones = [25, 50, 100, 250, 500, 1000];

    public void UpdatePopulation(World world)
    {
        if (world.Time.Month != 12)
        {
            return;
        }

        foreach (Polity polity in world.Polities)
        {
            int previousPopulation = polity.Population;

            if (polity.Population <= 0)
            {
                polity.Population = 0;
                polity.ResetAnnualFoodStats();
                continue;
            }

            double annualFoodRatio = polity.AnnualFoodNeeded <= 0
                ? 1.0
                : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

            int populationChange = annualFoodRatio switch
            {
                >= 1.00 => Math.Max(1, (int)(polity.Population * 0.02)),
                >= 0.90 => 0,
                >= 0.75 => -Math.Max(1, (int)(polity.Population * 0.02)),
                >= 0.50 => -Math.Max(1, (int)(polity.Population * 0.06)),
                _ => -Math.Max(1, (int)(polity.Population * 0.12))
            };

            // Repeated famine worsens mortality.
            if (polity.StarvationMonthsThisYear >= 6)
            {
                populationChange -= Math.Max(1, (int)(polity.Population * 0.03));
            }

            polity.Population += populationChange;

            if (polity.Population < 0)
            {
                polity.Population = 0;
            }

            AddPopulationEvents(world, polity, previousPopulation);
        }
    }

    private static void AddPopulationEvents(World world, Polity polity, int previousPopulation)
    {
        if (previousPopulation > 0 && polity.Population == 0)
        {
            string collapseNarrative = polity.StarvationMonthsThisYear >= 6
                ? $"{polity.Name} collapsed after prolonged famine"
                : $"{polity.Name} collapsed";

            world.AddEvent(
                WorldEventType.PolityCollapsed,
                WorldEventSeverity.Major,
                collapseNarrative,
                $"{polity.Name} fell from population {previousPopulation} to 0.",
                reason: "population_zero",
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: world.Species.First(species => species.Id == polity.SpeciesId).Name,
                regionId: polity.RegionId,
                before: new Dictionary<string, string>
                {
                    ["population"] = previousPopulation.ToString()
                },
                after: new Dictionary<string, string>
                {
                    ["population"] = "0"
                },
                metadata: new Dictionary<string, string>
                {
                    ["lineageId"] = polity.LineageId.ToString(),
                    ["speciesId"] = polity.SpeciesId.ToString()
                }
            );

            return;
        }

        if (previousPopulation > 0 && polity.Population < previousPopulation)
        {
            double declineRatio = (double)(previousPopulation - polity.Population) / previousPopulation;
            if (declineRatio >= 0.15)
            {
                world.AddEvent(
                    WorldEventType.PopulationChanged,
                    declineRatio >= 0.50 ? WorldEventSeverity.Legendary : WorldEventSeverity.Major,
                    $"{polity.Name} declined from {previousPopulation} to {polity.Population}",
                    $"{polity.Name} declined by {declineRatio:P0} in one year.",
                    reason: "major_decline",
                    polityId: polity.Id,
                    polityName: polity.Name,
                    speciesId: polity.SpeciesId,
                    speciesName: world.Species.First(species => species.Id == polity.SpeciesId).Name,
                    regionId: polity.RegionId,
                    before: new Dictionary<string, string>
                    {
                        ["population"] = previousPopulation.ToString()
                    },
                    after: new Dictionary<string, string>
                    {
                        ["population"] = polity.Population.ToString()
                    });
            }
        }

        int? milestone = PopulationMilestones
            .Where(value => previousPopulation < value && polity.Population >= value)
            .OrderBy(value => value)
            .FirstOrDefault();

        if (milestone is null || milestone.Value == 0)
        {
            return;
        }

        world.AddEvent(
            WorldEventType.PopulationChanged,
            ResolvePopulationMilestoneSeverity(milestone.Value),
            $"{polity.Name} grew to {milestone.Value} people",
            $"{polity.Name} grew from population {previousPopulation} to {polity.Population}, crossing milestone {milestone.Value}.",
            reason: "population_milestone",
            polityId: polity.Id,
            polityName: polity.Name,
            speciesId: polity.SpeciesId,
            speciesName: world.Species.First(species => species.Id == polity.SpeciesId).Name,
            regionId: polity.RegionId,
            before: new Dictionary<string, string>
            {
                ["population"] = previousPopulation.ToString()
            },
            after: new Dictionary<string, string>
            {
                ["population"] = polity.Population.ToString()
            },
            metadata: new Dictionary<string, string>
            {
                ["milestone"] = milestone.Value.ToString()
            }
        );
    }

    private static WorldEventSeverity ResolvePopulationMilestoneSeverity(int milestone)
        => milestone switch
        {
            >= 1000 => WorldEventSeverity.Legendary,
            >= 250 => WorldEventSeverity.Major,
            _ => WorldEventSeverity.Notable
        };
}
