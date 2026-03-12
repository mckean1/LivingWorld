namespace LivingWorld.Core;

public sealed class SimulationPerformanceTracker
{
    private readonly Dictionary<string, int> _eventCountsByCategory = new(StringComparer.OrdinalIgnoreCase);

    public bool Enabled { get; }
    public int CurrentYear { get; private set; }
    public int TotalSpeciesCount { get; set; }
    public int ActiveRegionalPopulationCount { get; set; }
    public int MutationChecks { get; private set; }
    public int SpeciationCandidates { get; private set; }
    public int SpeciationEvents { get; private set; }
    public int EcologyIterations { get; private set; }
    public TimeSpan EcosystemTime { get; private set; }
    public TimeSpan MutationTime { get; private set; }
    public TimeSpan FocusResolutionTime { get; private set; }
    public TimeSpan HistoryWriteTime { get; private set; }

    public SimulationPerformanceTracker(bool enabled)
    {
        Enabled = enabled;
    }

    public void BeginYear(int year)
    {
        if (!Enabled)
        {
            return;
        }

        CurrentYear = year;
        TotalSpeciesCount = 0;
        ActiveRegionalPopulationCount = 0;
        MutationChecks = 0;
        SpeciationCandidates = 0;
        SpeciationEvents = 0;
        EcologyIterations = 0;
        EcosystemTime = TimeSpan.Zero;
        MutationTime = TimeSpan.Zero;
        FocusResolutionTime = TimeSpan.Zero;
        HistoryWriteTime = TimeSpan.Zero;
        _eventCountsByCategory.Clear();
    }

    public void RecordSeason(
        Systems.EcosystemSystem.EcosystemSeasonMetrics ecosystemMetrics,
        Systems.MutationSystem.MutationSeasonMetrics mutationMetrics,
        int totalSpeciesCount)
    {
        if (!Enabled)
        {
            return;
        }

        TotalSpeciesCount = Math.Max(TotalSpeciesCount, totalSpeciesCount);
        ActiveRegionalPopulationCount = ecosystemMetrics.ActiveRegionalPopulationCount;
        EcologyIterations += ecosystemMetrics.EcologyIterations;
        MutationChecks += mutationMetrics.MutationChecks;
        SpeciationCandidates += mutationMetrics.SpeciationCandidates;
        SpeciationEvents += mutationMetrics.SpeciationEvents;
    }

    public void AddEvent(WorldEvent worldEvent)
    {
        if (!Enabled)
        {
            return;
        }

        string category = Categorize(worldEvent.Type);
        _eventCountsByCategory[category] = _eventCountsByCategory.TryGetValue(category, out int count)
            ? count + 1
            : 1;
    }

    public void AddEcosystemTime(TimeSpan elapsed)
    {
        if (Enabled)
        {
            EcosystemTime += elapsed;
        }
    }

    public void AddMutationTime(TimeSpan elapsed)
    {
        if (Enabled)
        {
            MutationTime += elapsed;
        }
    }

    public void AddFocusResolutionTime(TimeSpan elapsed)
    {
        if (Enabled)
        {
            FocusResolutionTime += elapsed;
        }
    }

    public void SetHistoryWriteTime(TimeSpan elapsed)
    {
        if (Enabled)
        {
            HistoryWriteTime = elapsed;
        }
    }

    public SimulationYearPerformanceSnapshot Snapshot()
        => new(
            CurrentYear,
            TotalSpeciesCount,
            ActiveRegionalPopulationCount,
            MutationChecks,
            SpeciationCandidates,
            SpeciationEvents,
            EcologyIterations,
            new Dictionary<string, int>(_eventCountsByCategory, StringComparer.OrdinalIgnoreCase),
            EcosystemTime,
            MutationTime,
            FocusResolutionTime,
            HistoryWriteTime);

    private static string Categorize(string eventType)
    {
        if (eventType.StartsWith("species_", StringComparison.Ordinal) || eventType.Contains("hunting", StringComparison.Ordinal) || eventType.Contains("ecosystem", StringComparison.Ordinal) || eventType.Contains("predator", StringComparison.Ordinal) || eventType.Contains("prey_", StringComparison.Ordinal))
        {
            return "biology";
        }

        if (eventType.StartsWith("trade_", StringComparison.Ordinal))
        {
            return "trade";
        }

        if (eventType.StartsWith("settlement_", StringComparison.Ordinal) || eventType.StartsWith("cultivation_", StringComparison.Ordinal))
        {
            return "settlement";
        }

        if (eventType.StartsWith("food_", StringComparison.Ordinal) || eventType == WorldEventType.StarvationRisk || eventType == WorldEventType.Harvest)
        {
            return "food";
        }

        if (eventType.StartsWith("focus_", StringComparison.Ordinal))
        {
            return "focus";
        }

        if (eventType == WorldEventType.LearnedAdvancement || eventType.Contains("discovered", StringComparison.Ordinal))
        {
            return "knowledge";
        }

        return "polity";
    }
}

public sealed record SimulationYearPerformanceSnapshot(
    int Year,
    int TotalSpeciesCount,
    int ActiveRegionalPopulationCount,
    int MutationChecks,
    int SpeciationCandidates,
    int SpeciationEvents,
    int EcologyIterations,
    IReadOnlyDictionary<string, int> EventCountsByCategory,
    TimeSpan EcosystemTime,
    TimeSpan MutationTime,
    TimeSpan FocusResolutionTime,
    TimeSpan HistoryWriteTime);
