using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public sealed class LineagePolityFocusSelector : IPolityFocusSelector
{
    public ChronicleFocusSelection SelectInitialFocus(World world, SimulationOptions options)
    {
        Polity? selected = options.FocusedPolityId.HasValue
            ? world.Polities.FirstOrDefault(polity => polity.Id == options.FocusedPolityId.Value)
            : world.Polities
                .OrderByDescending(polity => ScoreInitialFocusCandidate(world, polity))
                .ThenByDescending(polity => polity.Population)
                .ThenBy(polity => polity.Id)
                .FirstOrDefault();

        return ChronicleFocusSelection.FromPolity(selected);
    }

    public ChronicleFocusTransition? ResolveYearEndFocus(World world, ChronicleFocus focus, IReadOnlyList<WorldEvent> eventsThisYear)
    {
        if (!focus.FocusedPolityId.HasValue || !focus.FocusedLineageId.HasValue)
        {
            return null;
        }

        Polity? current = focus.ResolvePolity(world);
        if (current is not null && current.Population > 0)
        {
            Polity? fragmentationSuccessor = SelectFragmentationSuccessor(world, current, eventsThisYear);
            if (fragmentationSuccessor is null || fragmentationSuccessor.Id == current.Id)
            {
                return null;
            }

            return new ChronicleFocusTransition(
                ChronicleFocusTransitionKind.Fragmentation,
                current.Id,
                current.Name,
                current.LineageId,
                fragmentationSuccessor.Id,
                fragmentationSuccessor.Name,
                fragmentationSuccessor.LineageId,
                "fragmentation");
        }

        PolityReference previous = ResolvePreviousPolityReference(world, focus, eventsThisYear);
        bool collapsed = DidCollapse(eventsThisYear, previous.Id);
        Polity? lineageSuccessor = SelectBestSuccessor(
            world,
            previous,
            world.Polities.Where(polity =>
                polity.Population > 0
                && polity.LineageId == focus.FocusedLineageId.Value
                && polity.Id != previous.Id));

        if (lineageSuccessor is not null)
        {
            return new ChronicleFocusTransition(
                collapsed
                    ? ChronicleFocusTransitionKind.Collapse
                    : ChronicleFocusTransitionKind.LineageContinuation,
                previous.Id,
                previous.Name,
                previous.LineageId,
                lineageSuccessor.Id,
                lineageSuccessor.Name,
                lineageSuccessor.LineageId,
                collapsed ? "focused_polity_collapsed" : "focused_polity_missing");
        }

        Polity? fallback = SelectBestSuccessor(
            world,
            previous,
            world.Polities.Where(polity => polity.Population > 0 && polity.Id != previous.Id));

        if (fallback is null)
        {
            return null;
        }

        return new ChronicleFocusTransition(
            ChronicleFocusTransitionKind.LineageExtinctionFallback,
            previous.Id,
            previous.Name,
            previous.LineageId,
            fallback.Id,
            fallback.Name,
            fallback.LineageId,
            "lineage_extinct");
    }

    private static Polity? SelectFragmentationSuccessor(World world, Polity current, IReadOnlyList<WorldEvent> eventsThisYear)
    {
        HashSet<int> childIds = eventsThisYear
            .Where(evt => evt.Type == WorldEventType.Fragmentation)
            .Where(evt => evt.PolityId == current.Id)
            .Where(evt => evt.RelatedPolityId.HasValue)
            .Select(evt => evt.RelatedPolityId!.Value)
            .ToHashSet();

        if (childIds.Count == 0)
        {
            return null;
        }

        return SelectBestSuccessor(
            world,
            new PolityReference(current.Id, current.Name, current.LineageId, current.SpeciesId, current.RegionId),
            world.Polities.Where(polity =>
                polity.Population > 0
                && polity.LineageId == current.LineageId
                && polity.ParentPolityId == current.Id
                && childIds.Contains(polity.Id)));
    }

    private static Polity? SelectBestSuccessor(World world, PolityReference previous, IEnumerable<Polity> candidates)
    {
        return candidates
            .Select(candidate => new RankedCandidate(
                candidate,
                GetDescentDistance(world, previous.Id, candidate),
                GetRegionDistance(world, previous.RegionId, candidate.RegionId),
                ScoreCandidate(world, previous, candidate)))
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.DescentDistance ?? int.MaxValue)
            .ThenBy(candidate => candidate.RegionDistance)
            .ThenByDescending(candidate => candidate.Polity.Population)
            .ThenByDescending(candidate => candidate.Polity.SettlementCount)
            .ThenByDescending(candidate => (int)candidate.Polity.Stage)
            .ThenBy(candidate => candidate.Polity.Id)
            .Select(candidate => candidate.Polity)
            .FirstOrDefault();
    }

    private static double ScoreCandidate(World world, PolityReference previous, Polity candidate)
    {
        double score = candidate.Population * 3.0;
        score += candidate.SettlementCount * 180.0;
        score += (int)candidate.Stage * 90.0;

        if (candidate.SpeciesId == previous.SpeciesId)
        {
            score += 550.0;
        }

        if (candidate.LineageId == previous.LineageId)
        {
            score += 900.0;
        }

        int? descentDistance = GetDescentDistance(world, previous.Id, candidate);
        if (descentDistance.HasValue)
        {
            score += 700.0 - (descentDistance.Value * 80.0);
        }

        int regionDistance = GetRegionDistance(world, previous.RegionId, candidate.RegionId);
        if (regionDistance != int.MaxValue)
        {
            score += Math.Max(0.0, 260.0 - (regionDistance * 45.0));
        }

        return score;
    }

    private static bool DidCollapse(IReadOnlyList<WorldEvent> eventsThisYear, int polityId)
        => eventsThisYear.Any(evt => evt.Type == WorldEventType.PolityCollapsed && evt.PolityId == polityId);

    private static PolityReference ResolvePreviousPolityReference(World world, ChronicleFocus focus, IReadOnlyList<WorldEvent> eventsThisYear)
    {
        if (focus.ResolvePolity(world) is Polity polity)
        {
            return new PolityReference(polity.Id, polity.Name, polity.LineageId, polity.SpeciesId, polity.RegionId);
        }

        WorldEvent? latestRelevant = eventsThisYear
            .Where(evt => evt.PolityId == focus.FocusedPolityId || evt.RelatedPolityId == focus.FocusedPolityId)
            .OrderByDescending(evt => evt.Month)
            .ThenByDescending(evt => evt.EventId)
            .FirstOrDefault();

        if (latestRelevant is not null)
        {
            return new PolityReference(
                focus.FocusedPolityId!.Value,
                ResolveFocusedName(latestRelevant, focus.FocusedPolityId.Value),
                focus.FocusedLineageId ?? focus.FocusedPolityId.Value,
                TryParseInt(latestRelevant.Metadata, "speciesId") ?? -1,
                latestRelevant.RegionId ?? ResolveFallbackRegionId(world));
        }

        return new PolityReference(
            focus.FocusedPolityId!.Value,
            $"Polity {focus.FocusedPolityId.Value}",
            focus.FocusedLineageId ?? focus.FocusedPolityId.Value,
            -1,
            ResolveFallbackRegionId(world));
    }

    private static string ResolveFocusedName(WorldEvent worldEvent, int focusedPolityId)
    {
        if (worldEvent.PolityId == focusedPolityId)
        {
            return worldEvent.PolityName ?? $"Polity {focusedPolityId}";
        }

        if (worldEvent.RelatedPolityId == focusedPolityId)
        {
            return worldEvent.RelatedPolityName ?? $"Polity {focusedPolityId}";
        }

        return $"Polity {focusedPolityId}";
    }

    private static int ResolveFallbackRegionId(World world)
        => world.Regions.OrderBy(region => region.Id).Select(region => region.Id).FirstOrDefault();

    private static int? TryParseInt(IReadOnlyDictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out string? raw) && int.TryParse(raw, out int value)
            ? value
            : null;
    }

    private static int? GetDescentDistance(World world, int ancestorId, Polity polity)
    {
        int distance = 0;
        int? currentParentId = polity.ParentPolityId;
        HashSet<int> visited = [];

        while (currentParentId.HasValue)
        {
            if (!visited.Add(currentParentId.Value))
            {
                return null;
            }

            distance++;
            if (currentParentId.Value == ancestorId)
            {
                return distance;
            }

            Polity? parent = world.Polities.FirstOrDefault(candidate => candidate.Id == currentParentId.Value);
            if (parent is null)
            {
                return null;
            }

            currentParentId = parent.ParentPolityId;
        }

        return null;
    }

    private static int GetRegionDistance(World world, int sourceRegionId, int targetRegionId)
    {
        if (sourceRegionId == targetRegionId)
        {
            return 0;
        }

        Queue<(int RegionId, int Depth)> queue = new();
        HashSet<int> visited = [];
        queue.Enqueue((sourceRegionId, 0));
        visited.Add(sourceRegionId);

        while (queue.Count > 0)
        {
            (int currentRegionId, int depth) = queue.Dequeue();
            Map.Region current = world.Regions.First(region => region.Id == currentRegionId);

            foreach (int neighborId in current.ConnectedRegionIds)
            {
                if (!visited.Add(neighborId))
                {
                    continue;
                }

                int nextDepth = depth + 1;
                if (neighborId == targetRegionId)
                {
                    return nextDepth;
                }

                queue.Enqueue((neighborId, nextDepth));
            }
        }

        return int.MaxValue;
    }

    private static double ScoreInitialFocusCandidate(World world, Polity polity)
    {
        Region? homeRegion = world.Regions.FirstOrDefault(region => region.Id == polity.RegionId);
        if (homeRegion is null)
        {
            return double.MinValue;
        }

        double score = polity.Population * 2.0;
        score += polity.HasSettlements ? 240.0 : 0.0;
        score += homeRegion.Fertility * 160.0;
        score += homeRegion.WaterAvailability * 140.0;
        score += Math.Min(4, homeRegion.ConnectedRegionIds.Count) * 40.0;
        score += CountAccessibleFoodSpecies(world, polity, homeRegion) * 55.0;
        score += CountAccessibleNeighborPolities(world, polity, homeRegion) * 18.0;
        score += polity.FoodStores;

        return score;
    }

    private static int CountAccessibleFoodSpecies(World world, Polity polity, Region homeRegion)
    {
        HashSet<int> accessibleRegionIds = [homeRegion.Id];
        foreach (int neighborId in homeRegion.ConnectedRegionIds)
        {
            accessibleRegionIds.Add(neighborId);
        }

        int supportSpecies = 0;
        foreach (Species species in world.Species)
        {
            if (species.IsSapient || !accessibleRegionIds.Any(regionId => world.Regions[regionId].GetSpeciesPopulation(species.Id)?.PopulationCount > 0))
            {
                continue;
            }

            if (species.TrophicRole == TrophicRole.Producer)
            {
                supportSpecies++;
                continue;
            }

            if (species.MeatYield > 0)
            {
                supportSpecies++;
            }
        }

        return supportSpecies;
    }

    private static int CountAccessibleNeighborPolities(World world, Polity polity, Region homeRegion)
        => world.Polities.Count(candidate =>
            candidate.Id != polity.Id
            && candidate.Population > 0
            && (candidate.RegionId == homeRegion.Id || homeRegion.ConnectedRegionIds.Contains(candidate.RegionId)));

    private sealed record RankedCandidate(Polity Polity, int? DescentDistance, int RegionDistance, double Score);

    private sealed record PolityReference(int Id, string Name, int LineageId, int SpeciesId, int RegionId);
}
