using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Systems;

public sealed class FragmentationSystem
{
    private const int MinimumSplitPopulation = 40;
    private const int MinimumParentPopulationAfterSplit = 24;
    private const int MinimumPolityAgeYears = 4;
    private const int SplitCooldownYears = 6;
    private const double SplitThreshold = 0.55;

    private readonly Random _random;

    public FragmentationSystem(int seed = 54321)
    {
        _random = new Random(seed);
    }

    public void UpdateFragmentation(World world)
    {
        if (world.Time.Month != 12)
        {
            return;
        }

        // Fragmentation intentionally absorbs the old offshoot-expansion role.
        // Monthly migration still handles whole-polity relocation; this system only creates child polities.
        List<Polity> newPolities = new();
        int nextId = world.Polities.Count == 0 ? 0 : world.Polities.Max(p => p.Id) + 1;
        HashSet<string> reservedNames = world.Polities
            .Select(polity => polity.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (Polity polity in world.Polities.Where(p => p.Population > 0).ToList())
        {
            UpdateStressTracking(polity);
            TickCooldown(polity);

            polity.FragmentationPressure = CalculateFragmentationPressure(world, polity);

            if (!CanSplit(world, polity))
            {
                continue;
            }

            Region home = world.Regions.First(r => r.Id == polity.RegionId);
            Region? target = FindSplitTarget(world, polity, home);
            if (target is null)
            {
                continue;
            }

            Polity? child = CreateChildPolity(world, polity, target, nextId, reservedNames);
            if (child is null)
            {
                continue;
            }

            nextId++;
            newPolities.Add(child);

            world.AddEvent(
                "FRAGMENTATION",
                BuildFragmentationNarrative(polity, child, target),
                $"{polity.Name} split to form {child.Name} in Region {target.Id}; pressure={polity.FragmentationPressure:F2}.");
        }

        world.Polities.AddRange(newPolities);
    }

    private static void UpdateStressTracking(Polity polity)
    {
        bool foodStress = polity.StarvationMonthsThisYear >= 3 || GetAnnualFoodRatio(polity) < 0.90;

        if (foodStress)
        {
            polity.FoodStressYears++;
        }
        else if (polity.FoodStressYears > 0)
        {
            polity.FoodStressYears--;
        }
    }

    private static void TickCooldown(Polity polity)
    {
        if (polity.SplitCooldownYears > 0)
        {
            polity.SplitCooldownYears--;
        }
    }

    private static double CalculateFragmentationPressure(World world, Polity polity)
    {
        Region home = world.Regions.First(r => r.Id == polity.RegionId);
        int localPopulation = world.Polities
            .Where(p => p.RegionId == polity.RegionId && p.Population > 0)
            .Sum(p => p.Population);

        double populationPressure = Math.Clamp((polity.Population - 40) / 90.0, 0.0, 1.0);
        double shortagePressure = Math.Clamp(polity.StarvationMonthsThisYear / 6.0, 0.0, 1.0);
        double sustainedStressPressure = Math.Clamp(polity.FoodStressYears / 3.0, 0.0, 1.0);
        double crowdingRatio = home.CarryingCapacity <= 0
            ? 1.0
            : localPopulation / home.CarryingCapacity;
        double spreadPressure = Math.Clamp(crowdingRatio - 0.75, 0.0, 1.0);
        double migrationStrain = Math.Clamp(
            (polity.MigrationPressure * 0.7) + (Math.Clamp(polity.MovesThisYear, 0, 2) * 0.15),
            0.0,
            1.0);

        double pressure =
            (populationPressure * 0.30) +
            (shortagePressure * 0.25) +
            (sustainedStressPressure * 0.25) +
            (spreadPressure * 0.10) +
            (migrationStrain * 0.10);

        if (polity.HasSettlements)
        {
            pressure += 0.05;
        }

        return Math.Clamp(pressure, 0.0, 1.0);
    }

    private bool CanSplit(World world, Polity polity)
    {
        if (polity.Population < MinimumSplitPopulation)
        {
            return false;
        }

        if (polity.YearsSinceFounded < MinimumPolityAgeYears)
        {
            return false;
        }

        if (polity.SplitCooldownYears > 0)
        {
            return false;
        }

        if (polity.FragmentationPressure < SplitThreshold)
        {
            return false;
        }

        Region home = world.Regions.First(r => r.Id == polity.RegionId);
        if (home.ConnectedRegionIds.Count == 0)
        {
            return false;
        }

        double chance = CalculateSplitChance(polity.FragmentationPressure);
        return _random.NextDouble() <= chance;
    }

    private static double CalculateSplitChance(double fragmentationPressure)
    {
        double aboveThreshold = Math.Clamp((fragmentationPressure - SplitThreshold) / (1.0 - SplitThreshold), 0.0, 1.0);
        return 0.15 + (aboveThreshold * 0.40);
    }

    private Region? FindSplitTarget(World world, Polity polity, Region home)
    {
        List<Region> candidates = world.Regions
            .Where(r => r.Id != home.Id && home.ConnectedRegionIds.Contains(r.Id))
            .ToList();

        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .OrderByDescending(region =>
            {
                int targetPopulation = world.Polities
                    .Where(p => p.RegionId == region.Id && p.Population > 0)
                    .Sum(p => p.Population);

                double openness = Math.Max(0.0, region.CarryingCapacity - targetPopulation);

                return openness
                    + region.TotalBiomass
                    + (region.Fertility * 180.0)
                    + (region.WaterAvailability * 140.0)
                    + (_random.NextDouble() * 10.0);
            })
            .FirstOrDefault();
    }

    private Polity? CreateChildPolity(World world, Polity parent, Region target, int id, HashSet<string> reservedNames)
    {
        int transferPopulation = CalculateTransferPopulation(parent);
        if (transferPopulation <= 0)
        {
            return null;
        }

        if (parent.Population - transferPopulation < MinimumParentPopulationAfterSplit)
        {
            return null;
        }

        double foodTransfer = Math.Min(parent.FoodStores, parent.FoodStores * 0.25);

        parent.Population -= transferPopulation;
        parent.FoodStores -= foodTransfer;
        parent.SplitCooldownYears = SplitCooldownYears;
        parent.FragmentationPressure = Math.Max(0.0, parent.FragmentationPressure - 0.25);

        Polity child = new(
            id,
            BuildChildName(parent, target, reservedNames),
            parent.SpeciesId,
            target.Id,
            transferPopulation,
            parent.Id,
            DetermineChildStartingStage(parent))
        {
            FoodStores = foodTransfer,
            YearsInCurrentRegion = 0,
            PreviousRegionId = parent.RegionId,
            SplitCooldownYears = SplitCooldownYears,
            FoodStressYears = Math.Max(0, parent.FoodStressYears - 1)
        };

        // Fragmentation is still region-based. A child polity starts mobile until the settlement system
        // explicitly establishes its first settlement.
        child.ClearSettlementState();
        child.InheritAdvancements(SelectInheritedAdvancements(parent));

        return child;
    }

    private int CalculateTransferPopulation(Polity parent)
    {
        double baseShare = 0.22 + (_random.NextDouble() * 0.13);
        int transferPopulation = Math.Max(12, (int)Math.Round(parent.Population * baseShare));
        return Math.Min(transferPopulation, parent.Population - MinimumParentPopulationAfterSplit);
    }

    private IEnumerable<AdvancementId> SelectInheritedAdvancements(Polity parent)
    {
        foreach (AdvancementId advancement in parent.Advancements.OrderBy(id => id))
        {
            bool shouldInherit = advancement switch
            {
                AdvancementId.BasicConstruction => true,
                AdvancementId.FoodStorage => true,
                _ => _random.NextDouble() <= 0.75
            };

            if (shouldInherit)
            {
                yield return advancement;
            }
        }
    }

    private static string BuildChildName(Polity parent, Region target, HashSet<string> reservedNames)
    {
        string suffix = parent.HasSettlements ? "Colony" : "Clan";
        string baseName = $"{target.Name} {suffix}";
        return ReserveUniquePolityName(baseName, reservedNames);
    }

    private static string ReserveUniquePolityName(string baseName, HashSet<string> reservedNames)
    {
        if (reservedNames.Add(baseName))
        {
            return baseName;
        }

        for (int ordinal = 2; ordinal <= 12; ordinal++)
        {
            string candidate = $"{baseName} {ToRomanNumeral(ordinal)}";
            if (reservedNames.Add(candidate))
            {
                return candidate;
            }
        }

        string fallback = $"New {baseName}";
        reservedNames.Add(fallback);
        return fallback;
    }

    private static string ToRomanNumeral(int value)
        => value switch
        {
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            6 => "VI",
            7 => "VII",
            8 => "VIII",
            9 => "IX",
            10 => "X",
            11 => "XI",
            12 => "XII",
            _ => value.ToString()
        };

    private static double GetAnnualFoodRatio(Polity polity)
        => polity.AnnualFoodNeeded <= 0
            ? 1.0
            : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

    private static string BuildFragmentationNarrative(Polity parent, Polity child, Region target)
        => parent.SettlementStatus == SettlementStatus.Settled
            ? $"{parent.Name} founded {child.Name} in {target.Name}"
            : $"{child.Name} split from {parent.Name} in {target.Name}";

    private static PolityStage DetermineChildStartingStage(Polity parent)
        => parent.Stage switch
        {
            PolityStage.Band => PolityStage.Band,
            PolityStage.Tribe => PolityStage.Tribe,
            _ => PolityStage.Tribe
        };
}
