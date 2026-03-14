using System.Diagnostics;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using LivingWorld.Systems;
using LivingWorld.Map;
using LivingWorld.Life;
using System.Threading;

namespace LivingWorld.Core;

public sealed class Simulation : IDisposable
{
    private const int IdleLoopSleepMilliseconds = 8;
    private const int MinimumInteractiveStepIntervalMilliseconds = 16;
    private const int PersistentHardshipSummaryIntervalYears = 6;
    private const int WildlifeDiagnosticsDetailRegionLimit = 36;
    private readonly World _world;
    private readonly FoodSystem _foodSystem;
    private readonly EcosystemSystem _ecosystemSystem;
    private readonly HuntingSystem _huntingSystem;
    private readonly MutationSystem _mutationSystem;
    private readonly DomesticationSystem _domesticationSystem;
    private readonly AgricultureSystem _agricultureSystem;
    private readonly MaterialEconomySystem _materialEconomySystem;
    private readonly TradeSystem _tradeSystem;
    private readonly SettlementFoodRedistributionSystem _settlementFoodRedistributionSystem;
    private readonly PopulationSystem _populationSystem;
    private readonly MigrationSystem _migrationSystem;
    private readonly AdvancementSystem _advancementSystem;
    private readonly SettlementSystem _settlementSystem;
    private readonly FragmentationSystem _fragmentationSystem;
    private readonly PolityStageSystem _polityStageSystem;
    private readonly SimulationOptions _options;
    private readonly ChronicleColorWriter _chronicleColorWriter;
    private readonly ChronicleEventFormatter _chronicleEventFormatter;
    private readonly ChronicleWatchRenderer _chronicleWatchRenderer;
    private readonly ChronicleFocus _chronicleFocus;
    private readonly WatchUiState _watchUiState;
    private readonly WatchInputController _watchInputController;
    private readonly IPolityFocusSelector _focusSelector;
    private readonly HistoryJsonlWriter? _historyWriter;
    private readonly SimulationPerformanceTracker _performanceTracker;
    private readonly Dictionary<int, HardshipChronicleState> _hardshipStates = [];
    private readonly List<WorldEvent> _eventsThisYear = [];
    private readonly Stopwatch _watchLoopStopwatch = Stopwatch.StartNew();
    private TimeSpan _historyWriteTimeAtYearStart = TimeSpan.Zero;
    private long _nextSimulationStepAtMilliseconds;
    private bool _renderInvalidated = true;
    private bool _wasPausedInInteractiveLoop;

    public Simulation(World world, SimulationOptions? options = null, IPolityFocusSelector? focusSelector = null)
    {
        _world = world;
        _world.EnterBootstrapPhase();
        _foodSystem = new FoodSystem();
        _ecosystemSystem = new EcosystemSystem();
        _huntingSystem = new HuntingSystem();
        _mutationSystem = new MutationSystem();
        _domesticationSystem = new DomesticationSystem();
        _agricultureSystem = new AgricultureSystem(_domesticationSystem);
        _materialEconomySystem = new MaterialEconomySystem();
        _tradeSystem = new TradeSystem();
        _settlementFoodRedistributionSystem = new SettlementFoodRedistributionSystem();
        _populationSystem = new PopulationSystem();
        _migrationSystem = new MigrationSystem();
        _advancementSystem = new AdvancementSystem();
        _settlementSystem = new SettlementSystem();
        _fragmentationSystem = new FragmentationSystem();
        _polityStageSystem = new PolityStageSystem();
        _options = options ?? new SimulationOptions();
        _chronicleColorWriter = new ChronicleColorWriter();
        _chronicleEventFormatter = new ChronicleEventFormatter();
        _chronicleWatchRenderer = new ChronicleWatchRenderer(_options, _chronicleColorWriter, _chronicleEventFormatter);

        _chronicleFocus = new ChronicleFocus();
        _watchUiState = new WatchUiState(_options.PauseBeforeStart);
        _watchInputController = new WatchInputController(_watchUiState);
        _focusSelector = focusSelector ?? new LineagePolityFocusSelector();
        _performanceTracker = new SimulationPerformanceTracker(_options.EnablePerformanceInstrumentation);
        _ecosystemSystem.InitializeRegionalPopulations(_world);
        _performanceTracker.BeginYear(_world.Time.Year);
        if (_options.OutputMode == OutputMode.Debug)
        {
            PrintInitialWildlifeDiagnostics();
        }

        ChronicleFocusSelection initialFocus = _focusSelector.SelectInitialFocus(_world, _options);
        _chronicleFocus.SetFocus(initialFocus.PolityId, initialFocus.LineageId);
        _world.ConfigureEventPropagation(new EventPropagationCoordinator(
        [
            new FoodStressPropagationHandler(),
            new AgriculturePropagationHandler(),
            new DomesticationPropagationHandler(),
            new MaterialEconomyPropagationHandler(),
            new MigrationPropagationHandler(),
            new FragmentationPropagationHandler()
        ]));

        RunBootstrapInitialization();

        if (_options.WriteStructuredHistory)
        {
            _historyWriter = new HistoryJsonlWriter(_options.HistoryFilePath);
        }

        if (_options.WriteStructuredHistory || _options.OutputMode == OutputMode.Watch)
        {
            _world.EventRecorded += OnWorldEventRecorded;
        }

        _eventsThisYear.AddRange(_world.Events.Where(evt => evt.Year == _world.Time.Year && !evt.IsBootstrapEvent));

        _chronicleWatchRenderer.Render(_world, _chronicleFocus, _watchUiState);
    }

    public void RunMonths(int months)
    {
        if (!ShouldUseInteractiveWatchLoop())
        {
            for (int i = 0; i < months; i++)
            {
                RunTick();
            }

            return;
        }

        int completedMonths = 0;
        _nextSimulationStepAtMilliseconds = _watchLoopStopwatch.ElapsedMilliseconds;
        while (completedMonths < months)
        {
            PumpWatchInput();
            long now = _watchLoopStopwatch.ElapsedMilliseconds;
            if (_watchUiState.IsPaused)
            {
                _wasPausedInInteractiveLoop = true;
                RenderIfInvalidated();
                Thread.Sleep(IdleLoopSleepMilliseconds);
                continue;
            }

            if (_wasPausedInInteractiveLoop)
            {
                _nextSimulationStepAtMilliseconds = now + ResolveInteractiveStepIntervalMilliseconds();
                _wasPausedInInteractiveLoop = false;
            }

            if (now >= _nextSimulationStepAtMilliseconds)
            {
                RunTick();
                completedMonths++;
                _renderInvalidated = true;
                _nextSimulationStepAtMilliseconds = _watchLoopStopwatch.ElapsedMilliseconds + ResolveInteractiveStepIntervalMilliseconds();
            }

            RenderIfInvalidated();
            Thread.Sleep(IdleLoopSleepMilliseconds);
        }

        RenderIfInvalidated();
    }

    public void Dispose()
    {
        if (_options.WriteStructuredHistory || _options.OutputMode == OutputMode.Watch)
        {
            _world.EventRecorded -= OnWorldEventRecorded;
        }

        if (_historyWriter is not null)
        {
            _historyWriter.Dispose();
        }

        _chronicleWatchRenderer.Dispose();
    }

    internal bool IsWatchPaused
        => _watchUiState.IsPaused;

    private void RunTick()
    {
        foreach (Polity polity in _world.Polities)
        {
            polity.TickPropagationState();
            polity.AdvanceSettlementMonths();
        }

        RunMonthlySystems();

        // Year-end systems
        if (_world.Time.Month == 12)
        {
            RunYearEndSystems();
        }

        _world.Time.AdvanceOneMonth();
    }

    private void RunMonthlySystems()
    {
        _foodSystem.UpdateRegionEcology(_world);
        RunSeasonalBiologyIfNeeded();
        _foodSystem.GatherFood(_world);
        _domesticationSystem.UpdateMonthlyKnowledgeAndSources(_world);
        _materialEconomySystem.UpdateMonthlyMaterials(_world);
        _agricultureSystem.ProduceFarmFood(_world);
        _domesticationSystem.ProduceManagedAnimalFood(_world);
        _tradeSystem.UpdateTrade(_world);
        _foodSystem.ConsumeFood(_world);
        _settlementFoodRedistributionSystem.UpdateMonthlyFoodStatesAndRedistribution(_world);
        _migrationSystem.UpdateMigration(_world);
    }

    private void RunSeasonalBiologyIfNeeded()
    {
        if (_world.Time.Month % 3 != 0)
        {
            return;
        }

        // Biology uses the current season's regional species exchange from the
        // ecosystem pass. Monthly polity migration still happens later because it
        // is driven by food resolution and social pressure rather than wildlife flow.
        long ecosystemStartedAt = Stopwatch.GetTimestamp();
        _ecosystemSystem.UpdateSeason(_world);
        _performanceTracker.AddEcosystemTime(Stopwatch.GetElapsedTime(ecosystemStartedAt));
        _huntingSystem.UpdateSeason(_world);
        long mutationStartedAt = Stopwatch.GetTimestamp();
        _mutationSystem.UpdateSeason(_world);
        _performanceTracker.AddMutationTime(Stopwatch.GetElapsedTime(mutationStartedAt));
        _ecosystemSystem.ResolveSeasonalCleanup(_world);
        _performanceTracker.RecordSeason(_ecosystemSystem.LastSeasonMetrics, _mutationSystem.LastSeasonMetrics, _world.Species.Count);
    }

    private void RunYearEndSystems()
    {
        foreach (Polity polity in _world.Polities)
        {
            if (polity.Population > 0)
            {
                polity.YearsSinceFounded++;
            }
        }

        _populationSystem.UpdatePopulation(_world);
        _advancementSystem.UpdateAdvancements(_world);
        _settlementSystem.UpdateSettlements(_world);
        _fragmentationSystem.UpdateFragmentation(_world);
        _polityStageSystem.UpdatePolityStages(_world);
        _agricultureSystem.UpdateAnnualAgriculture(_world);
        _domesticationSystem.UpdateAnnualManagedFood(_world);
        _tradeSystem.UpdateAnnualTrade(_world);

        AddYearlyFoodStressEvents();

        _world.Polities.RemoveAll(p => p.Population <= 0);
        ResolveChronicleFocusForYear();
        PersistYearEndFoodStateSnapshots();

        RenderYearBoundaryOutput();
        ResetAnnualStats();
        if (_historyWriter is not null)
        {
            TimeSpan totalHistoryWriteTime = _historyWriter.TotalWriteTime;
            _performanceTracker.SetHistoryWriteTime(totalHistoryWriteTime - _historyWriteTimeAtYearStart);
            _historyWriteTimeAtYearStart = totalHistoryWriteTime;
        }
        else
        {
            _performanceTracker.SetHistoryWriteTime(TimeSpan.Zero);
        }
        _eventsThisYear.Clear();
        _performanceTracker.BeginYear(_world.Time.Year + 1);

        if (_options.PauseAfterEachYear)
        {
            Console.ReadKey();
        }
    }

    private void OnWorldEventRecorded(WorldEvent worldEvent)
    {
        _historyWriter?.Write(worldEvent);
        if (worldEvent.IsBootstrapEvent)
        {
            return;
        }

        _eventsThisYear.Add(worldEvent);
        _performanceTracker.AddEvent(worldEvent);

        if (worldEvent.Severity == WorldEventSeverity.Debug)
        {
            return;
        }

        if (_chronicleWatchRenderer.Record(_world, _chronicleFocus, _watchUiState, worldEvent))
        {
            _renderInvalidated = true;
        }
    }

    private void RunBootstrapInitialization()
    {
        int bootstrapMaterialWarmupMonths = ResolveBootstrapMaterialWarmupMonths();
        for (int month = 0; month < bootstrapMaterialWarmupMonths; month++)
        {
            _settlementFoodRedistributionSystem.InitializeBootstrapStates(_world);
            _materialEconomySystem.UpdateMonthlyMaterials(_world);
            _settlementFoodRedistributionSystem.InitializeBootstrapStates(_world);
        }

        _materialEconomySystem.SeedBootstrapBaseline(_world);
        SeedBootstrapHardshipStates();
        ResetBootstrapRuntimeCounters();
        _world.BeginActiveSimulation();
    }

    private void SeedBootstrapHardshipStates()
    {
        foreach (Polity polity in _world.Polities.Where(candidate => candidate.Population > 0))
        {
            HardshipTier currentTier = ResolveHardshipTier(polity);
            _hardshipStates[polity.Id] = HardshipChronicleState.Initial.WithObservedTier(currentTier, _world.Time.Year);
        }
    }

    private int ResolveBootstrapMaterialWarmupMonths()
    {
        int oldestSettlement = _world.Polities
            .SelectMany(polity => polity.Settlements)
            .Select(settlement => settlement.YearsEstablished)
            .DefaultIfEmpty(0)
            .Max();

        return oldestSettlement <= 0
            ? 1
            : Math.Clamp(oldestSettlement, 1, 6);
    }

    private void ResetBootstrapRuntimeCounters()
    {
        foreach (Polity polity in _world.Polities)
        {
            polity.ResetBootstrapRuntimeState();
        }
    }

    private void AddYearlyFoodStressEvents()
    {
        WorldLookup lookup = new(_world);

        foreach (Polity polity in _world.Polities.Where(p => p.Population > 0))
        {
            HardshipTier currentTier = ResolveHardshipTier(polity);
            HardshipChronicleState previousState = _hardshipStates.TryGetValue(polity.Id, out HardshipChronicleState? existingState)
                ? existingState
                : HardshipChronicleState.Initial;

            if (!ShouldEmitHardshipEvent(previousState, currentTier, _world.Time.Year))
            {
                _hardshipStates[polity.Id] = previousState.WithObservedTier(currentTier, _world.Time.Year);
                continue;
            }

            WorldEventSeverity severity = ResolveHardshipSeverity(currentTier, previousState.CurrentTier);
            string reason = ResolveHardshipReason(previousState, currentTier);
            string narrative = BuildHardshipNarrative(polity, previousState, currentTier);
            string details = BuildHardshipDetails(polity, previousState, currentTier);

            _world.AddEvent(
                WorldEventType.FoodStress,
                severity,
                narrative,
                details,
                reason: reason,
                scope: WorldEventScope.Polity,
                polityId: polity.Id,
                polityName: polity.Name,
                speciesId: polity.SpeciesId,
                speciesName: lookup.GetRequiredSpecies(polity.SpeciesId, "Yearly food stress").Name,
                regionId: polity.RegionId,
                regionName: lookup.GetRequiredRegion(polity.RegionId, "Yearly food stress").Name,
                before: new Dictionary<string, string>
                {
                    ["hardshipTier"] = previousState.CurrentTier.ToString(),
                    ["hardshipTierYears"] = previousState.YearsInCurrentTier(_world.Time.Year).ToString(),
                    ["starvationMonths"] = previousState.LastStarvationMonths.ToString(),
                    ["annualFoodRatio"] = previousState.LastAnnualFoodRatio.ToString("F2")
                },
                after: new Dictionary<string, string>
                {
                    ["hardshipTier"] = currentTier.ToString(),
                    ["hardshipTierYears"] = previousState.NextYearsInCurrentTier(currentTier, _world.Time.Year).ToString(),
                    ["starvationMonths"] = polity.StarvationMonthsThisYear.ToString(),
                    ["annualFoodRatio"] = ResolveAnnualFoodRatio(polity).ToString("F2")
                },
                metadata: new Dictionary<string, string>
                {
                    ["transitionKind"] = reason,
                    ["hardshipTier"] = currentTier.ToString()
                });

            _hardshipStates[polity.Id] = previousState.AfterEmission(currentTier, _world.Time.Year, polity);
        }

        HashSet<int> activePolityIds = _world.Polities
            .Where(polity => polity.Population > 0)
            .Select(polity => polity.Id)
            .ToHashSet();
        List<int> inactiveTrackedPolities = _hardshipStates.Keys
            .Where(polityId => !activePolityIds.Contains(polityId))
            .ToList();
        foreach (int polityId in inactiveTrackedPolities)
        {
            _hardshipStates.Remove(polityId);
        }
    }

    private void ResetAnnualStats()
    {
        foreach (Polity polity in _world.Polities)
        {
            polity.ResetAnnualFoodStats();
        }
    }

    private void PersistYearEndFoodStateSnapshots()
    {
        foreach (Polity polity in _world.Polities.Where(p => p.Population > 0))
        {
            polity.LastResolvedFoodState = ChronicleTextFormatter.ResolveFoodState(polity);
            polity.LastResolvedFoodStateYear = _world.Time.Year;
        }
    }

    private void ResolveChronicleFocusForYear()
    {
        long startedAt = Stopwatch.GetTimestamp();
        List<WorldEvent> eventsThisYear = _eventsThisYear
            .OrderBy(evt => evt.Month)
            .ThenBy(evt => evt.EventId)
            .ToList();

        ChronicleFocusTransition? transition = _focusSelector.ResolveYearEndFocus(_world, _chronicleFocus, eventsThisYear);
        _performanceTracker.AddFocusResolutionTime(Stopwatch.GetElapsedTime(startedAt));
        if (transition is null)
        {
            return;
        }

        WorldLookup lookup = new(_world);
        Polity? successor = _world.Polities.FirstOrDefault(polity => polity.Id == transition.NewPolityId);
        if (successor is null)
        {
            return;
        }

        EmitFocusTransitionEvent(transition, successor);
        _chronicleFocus.SetFocus(successor);
    }

    private void EmitFocusTransitionEvent(ChronicleFocusTransition transition, Polity successor)
    {
        WorldLookup lookup = new(_world);
        string eventType = transition.Kind switch
        {
            ChronicleFocusTransitionKind.Fragmentation => WorldEventType.FocusHandoffFragmentation,
            ChronicleFocusTransitionKind.Collapse => WorldEventType.FocusHandoffCollapse,
            ChronicleFocusTransitionKind.LineageContinuation => WorldEventType.FocusLineageContinued,
            ChronicleFocusTransitionKind.LineageExtinctionFallback => WorldEventType.FocusLineageExtinctFallback,
            _ => WorldEventType.WorldEvent
        };

        string narrative = transition.Kind switch
        {
            ChronicleFocusTransitionKind.Fragmentation =>
                $"{transition.PreviousPolityName} fractured into rival groups. The chronicle now follows {transition.NewPolityName}",
            ChronicleFocusTransitionKind.Collapse =>
                $"{transition.PreviousPolityName} collapsed. Its legacy continued through {transition.NewPolityName}",
            ChronicleFocusTransitionKind.LineageContinuation =>
                $"{transition.PreviousPolityName} passed from the chronicle. Its lineage endured through {transition.NewPolityName}",
            ChronicleFocusTransitionKind.LineageExtinctionFallback =>
                $"{transition.PreviousPolityName}'s lineage ended. The chronicle now follows {transition.NewPolityName}",
            _ => $"{transition.NewPolityName} became the new focus"
        };

        _world.AddEvent(
            eventType,
            WorldEventSeverity.Major,
            narrative,
            $"Focus shifted from {transition.PreviousPolityName} ({transition.PreviousPolityId}) to {transition.NewPolityName} ({transition.NewPolityId}) because {transition.Reason}.",
            reason: transition.Reason,
            scope: WorldEventScope.Polity,
            polityId: successor.Id,
            polityName: successor.Name,
            relatedPolityId: transition.PreviousPolityId,
            relatedPolityName: transition.PreviousPolityName,
            relatedPolitySpeciesId: _world.Polities.FirstOrDefault(polity => polity.Id == transition.PreviousPolityId)?.SpeciesId,
            relatedPolitySpeciesName: _world.Polities.FirstOrDefault(polity => polity.Id == transition.PreviousPolityId) is Polity previousPolity
                ? lookup.TryGetSpecies(previousPolity.SpeciesId, out Life.Species? previousSpecies) && previousSpecies is not null
                    ? previousSpecies.Name
                    : null
                : null,
            speciesId: successor.SpeciesId,
            speciesName: lookup.TryGetSpecies(successor.SpeciesId, out Life.Species? successorSpecies) && successorSpecies is not null
                ? successorSpecies.Name
                : null,
            regionId: successor.RegionId,
            regionName: lookup.GetRequiredRegion(successor.RegionId, "Chronicle focus transition").Name,
            before: new Dictionary<string, string>
            {
                ["focusedPolityId"] = transition.PreviousPolityId.ToString(),
                ["focusedPolityName"] = transition.PreviousPolityName,
                ["focusedLineageId"] = transition.PreviousLineageId.ToString()
            },
            after: new Dictionary<string, string>
            {
                ["focusedPolityId"] = transition.NewPolityId.ToString(),
                ["focusedPolityName"] = transition.NewPolityName,
                ["focusedLineageId"] = transition.NewLineageId.ToString()
            },
            metadata: new Dictionary<string, string>
            {
                ["transitionKind"] = transition.Kind.ToString(),
                ["previousPolityId"] = transition.PreviousPolityId.ToString(),
                ["newPolityId"] = transition.NewPolityId.ToString(),
                ["previousLineageId"] = transition.PreviousLineageId.ToString(),
                ["newLineageId"] = transition.NewLineageId.ToString(),
                ["speciesId"] = successor.SpeciesId.ToString()
            });
    }

    private void RenderYearBoundaryOutput()
    {
        if (_options.OutputMode == OutputMode.Debug)
        {
            PrintDebugYearSummary();
            PrintDebugYearEvents();
            return;
        }

        _renderInvalidated = true;
        RenderIfInvalidated(force: true);
    }

    private bool PumpWatchInput()
    {
        if (_options.OutputMode != OutputMode.Watch || Console.IsInputRedirected)
        {
            return false;
        }

        bool handledAny = false;
        while (Console.KeyAvailable)
        {
            ConsoleKeyInfo keyInfo = Console.ReadKey(intercept: true);
            handledAny |= _watchInputController.HandleKey(keyInfo, _world, _chronicleFocus);
        }

        if (handledAny)
        {
            _renderInvalidated = true;
        }

        return handledAny;
    }

    private void RenderIfInvalidated(bool force = false)
    {
        if (!force && !_renderInvalidated)
        {
            return;
        }

        _chronicleWatchRenderer.Render(_world, _chronicleFocus, _watchUiState);
        _renderInvalidated = false;
    }

    private bool ShouldUseInteractiveWatchLoop()
        => _options.OutputMode == OutputMode.Watch
            && !Console.IsInputRedirected
            && !Console.IsOutputRedirected;

    private int ResolveInteractiveStepIntervalMilliseconds()
        => Math.Max(MinimumInteractiveStepIntervalMilliseconds, _options.ChroniclePlaybackDelayMilliseconds);

    private void PrintDebugYearEvents()
    {
        var eventsThisYear = _world.Events
            .Where(e => e.Year == _world.Time.Year)
            .OrderBy(e => e.Month)
            .ThenBy(e => e.Type)
            .ToList();

        if (eventsThisYear.Count == 0)
        {
            return;
        }

        Console.WriteLine("Events:");
        foreach (WorldEvent worldEvent in eventsThisYear)
        {
            Console.WriteLine($"  {worldEvent}");
        }
    }

    private void PrintDebugYearSummary()
    {
        int activePolities = _world.Polities.Count(p => p.Population > 0);
        int totalPopulation = _world.Polities.Where(p => p.Population > 0).Sum(p => p.Population);

        Console.WriteLine();
        Console.WriteLine($"=== YEAR {_world.Time.Year} SUMMARY ===");
        Console.WriteLine($"Active Polities: {activePolities} | Total Population: {totalPopulation}");

        foreach (Polity polity in _world.Polities.OrderByDescending(p => p.Population).ThenBy(p => p.Name))
        {
            double annualFoodRatio = polity.AnnualFoodNeeded <= 0
                ? 1.0
                : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

            Console.WriteLine(
                $"- {polity.Name,-22} " +
                $"Pop={polity.Population,3} " +
                $"Age={polity.YearsSinceFounded,3} " +
                $"Reg={polity.RegionId,2} " +
                $"Food={polity.FoodStores,6:F1} " +
                $"AFR={annualFoodRatio,4:F2} " +
                $"Wild={polity.AnnualFoodGathered,6:F0} " +
                $"Hunt={polity.FoodHuntedThisYear,6:F0} " +
                $"Farm={polity.AnnualFoodFarmed,6:F0} " +
                $"Imp={polity.AnnualFoodImported,6:F0} " +
                $"IntImp={polity.AnnualFoodImportedInternal,6:F0} " +
                $"ExtImp={polity.AnnualFoodImportedExternal,6:F0} " +
                $"Exp={polity.AnnualFoodExported,6:F0} " +
                $"RelP={polity.TradePartialReliefMonthsThisYear,2} " +
                $"RelF={polity.TradeFullReliefMonthsThisYear,2} " +
                $"Starve={polity.StarvationMonthsThisYear,2} " +
                $"Move={polity.MigrationPressure,4:F2} " +
                $"Frag={polity.FragmentationPressure,4:F2} " +
                $"Cool={polity.SplitCooldownYears,2} " +
                $"Know={polity.Advancements.Count,2} " +
                $"Settle={polity.SettlementStatus,-11} " +
                $"Stage={polity.Stage,-14}");
        }

        PrintDebugEcologySummary();
        PrintDebugPerformanceSummary();
    }

    private void PrintDebugPerformanceSummary()
    {
        if (!_performanceTracker.Enabled)
        {
            return;
        }

        SimulationYearPerformanceSnapshot snapshot = _performanceTracker.Snapshot();
        string eventCategories = snapshot.EventCountsByCategory.Count == 0
            ? "none"
            : string.Join(", ", snapshot.EventCountsByCategory.OrderByDescending(entry => entry.Value).Select(entry => $"{entry.Key}={entry.Value}"));

        Console.WriteLine(
            $"Perf: Species={snapshot.TotalSpeciesCount} ActiveRegionalPops={snapshot.ActiveRegionalPopulationCount} EcologyIter={snapshot.EcologyIterations} MutationChecks={snapshot.MutationChecks} SpecCandidates={snapshot.SpeciationCandidates} SpecEvents={snapshot.SpeciationEvents}");
        Console.WriteLine(
            $"Perf Time: Eco={snapshot.EcosystemTime.TotalMilliseconds:F1}ms Mut={snapshot.MutationTime.TotalMilliseconds:F1}ms Focus={snapshot.FocusResolutionTime.TotalMilliseconds:F1}ms History={snapshot.HistoryWriteTime.TotalMilliseconds:F1}ms");
        Console.WriteLine($"Perf Events: {eventCategories}");
    }

    private void PrintInitialWildlifeDiagnostics()
    {
        Console.WriteLine("=== INITIAL WILDLIFE SEEDING ===");
        foreach (Region region in _world.Regions.OrderBy(region => region.Id))
        {
            bool herbivorePresent = ResolveHerbivorePopulation(region) > 0;
            bool predatorPresent = ResolvePredatorPopulation(region) > 0;
            Console.WriteLine(
                $"  [{region.Id,2}] {region.Name,-18} {region.Biome,-12} Fert={region.Fertility,4:F2} Water={region.WaterAvailability,4:F2} Herbivore={(herbivorePresent ? "Y" : "N")} Predator={(predatorPresent ? "Y" : "N")}");
        }

        Console.WriteLine();
    }

    private void PrintDebugEcologySummary()
    {
        int regionsWithAnimalBiomass = _world.Regions.Count(region => region.AnimalBiomass > 0.1);
        double totalAnimalBiomass = _world.Regions.Sum(region => region.AnimalBiomass);
        int activeConsumerPopulations = _world.Regions.Sum(region => region.SpeciesPopulations.Count(population => population.PopulationCount > 0))
            - _world.Regions.Sum(region => region.SpeciesPopulations.Count(population =>
                population.PopulationCount > 0
                && _world.Species.First(species => species.Id == population.SpeciesId).TrophicRole == Life.TrophicRole.Producer));
        double averageHerbivorePopulation = _world.Regions.Average(region => region.SpeciesPopulations
            .Where(population => population.PopulationCount > 0
                && _world.Species.First(species => species.Id == population.SpeciesId).TrophicRole == Life.TrophicRole.Herbivore)
            .Sum(population => population.PopulationCount));
        double averageConsumerSpecies = _world.Regions.Average(region => region.SpeciesPopulations.Count(population =>
            population.PopulationCount > 0
            && _world.Species.First(species => species.Id == population.SpeciesId).TrophicRole != Life.TrophicRole.Producer));
        List<Region> fertileRegions = _world.Regions
            .Where(region => region.Fertility >= 0.55 && region.WaterAvailability >= 0.50)
            .ToList();
        int fertileRegionsWithEstablishedConsumers = fertileRegions.Count(region => CountConsumerSpecies(region) >= 2);
        int fertileRegionsWithGrowingHerbivores = fertileRegions.Count(region => ResolveHerbivorePopulation(region) >= 90);
        int predatorSuppressionHotspots = _world.Regions.Count(region =>
        {
            int herbivorePopulation = ResolveHerbivorePopulation(region);
            int predatorPopulation = ResolvePredatorPopulation(region);
            return herbivorePopulation > 0
                && herbivorePopulation < 90
                && predatorPopulation >= Math.Max(12, herbivorePopulation * 0.55);
        });
        List<Species> predatorSpecies = _world.Species
            .Where(species => species.TrophicRole is Life.TrophicRole.Predator or Life.TrophicRole.Apex)
            .ToList();
        int occupiedPredatorRegions = _world.Regions.Count(region => ResolvePredatorPopulation(region) > 0);
        int founderPredatorRegions = _world.Regions.Count(region => region.SpeciesPopulations.Any(population =>
            population.PopulationCount > 0
            && population.FounderSeasonsRemaining > 0
            && _world.Species.First(species => species.Id == population.SpeciesId).TrophicRole is Life.TrophicRole.Predator or Life.TrophicRole.Apex));
        int establishedPredatorRegions = _world.Regions.Count(region => region.SpeciesPopulations.Any(population =>
            population.PopulationCount > 0
            && population.FounderSeasonsRemaining == 0
            && _world.Species.First(species => species.Id == population.SpeciesId).TrophicRole is Life.TrophicRole.Predator or Life.TrophicRole.Apex));
        int activePredatorSpecies = predatorSpecies.Count(species => _world.Regions.Any(region => region.GetSpeciesPopulation(species.Id)?.PopulationCount > 0));
        int totalPredatorPopulation = _world.Regions.Sum(ResolvePredatorPopulation);

        Console.WriteLine(
            $"Ecology: AnimalBiomass={totalAnimalBiomass:F0} RegionsWithAnimals={regionsWithAnimalBiomass}/{_world.Regions.Count} ActiveConsumerPops={activeConsumerPopulations}");
        Console.WriteLine(
            $"Wildlife: AvgHerbivores/Region={averageHerbivorePopulation:F0} AvgConsumers/Region={averageConsumerSpecies:F1} Fertile2+Consumers={fertileRegionsWithEstablishedConsumers}/{fertileRegions.Count} FertileHerbivore90+={fertileRegionsWithGrowingHerbivores}/{fertileRegions.Count} PredatorHotspots={predatorSuppressionHotspots}");
        Console.WriteLine(
            $"Predators: Regions={occupiedPredatorRegions}/{_world.Regions.Count} FounderRegions={founderPredatorRegions} EstablishedRegions={establishedPredatorRegions} TotalPop={totalPredatorPopulation} ActiveSpecies={activePredatorSpecies}/{predatorSpecies.Count}");

        if (_world.Time.Year is 0 or 5 or 20)
        {
            PrintDebugWildlifeRegionSnapshots();
        }
    }

    private void PrintDebugWildlifeRegionSnapshots()
    {
        Console.WriteLine($"Wildlife snapshots (year {_world.Time.Year}):");

        foreach (Region region in _world.Regions
                     .OrderByDescending(ResolveHerbivorePopulation)
                     .ThenBy(region => region.Id)
                     .Take(WildlifeDiagnosticsDetailRegionLimit))
        {
            int consumerSpecies = CountConsumerSpecies(region);
            int herbivorePopulation = ResolveHerbivorePopulation(region);
            int predatorPopulation = ResolvePredatorPopulation(region);
            double plantRatio = region.MaxPlantBiomass <= 0
                ? 0.0
                : region.PlantBiomass / region.MaxPlantBiomass;

            Console.WriteLine(
                $"  [{region.Id,2}] {region.Name,-18} {region.Biome,-12} Herb={herbivorePopulation,4} Cons={consumerSpecies,2} Pred={predatorPopulation,3} Plant={plantRatio,4:F2} AnimalBio={region.AnimalBiomass,6:F0}");
        }
    }

    private int ResolveHerbivorePopulation(Region region)
        => region.SpeciesPopulations
            .Where(population => population.PopulationCount > 0
                && _world.Species.First(species => species.Id == population.SpeciesId).TrophicRole == Life.TrophicRole.Herbivore)
            .Sum(population => population.PopulationCount);

    private int ResolvePredatorPopulation(Region region)
        => region.SpeciesPopulations
            .Where(population => population.PopulationCount > 0
                && _world.Species.First(species => species.Id == population.SpeciesId).TrophicRole is Life.TrophicRole.Predator or Life.TrophicRole.Apex)
            .Sum(population => population.PopulationCount);

    private int CountConsumerSpecies(Region region)
        => region.SpeciesPopulations.Count(population =>
            population.PopulationCount > 0
            && _world.Species.First(species => species.Id == population.SpeciesId).TrophicRole != Life.TrophicRole.Producer);

    private static bool ShouldEmitHardshipEvent(HardshipChronicleState previousState, HardshipTier currentTier, int currentYear)
    {
        if (currentTier == HardshipTier.Stable)
        {
            return previousState.CurrentTier >= HardshipTier.Shortages;
        }

        if (currentTier < HardshipTier.Shortages)
        {
            return previousState.CurrentTier >= HardshipTier.Shortages;
        }

        if (previousState.CurrentTier < HardshipTier.Shortages)
        {
            return true;
        }

        if (currentTier != previousState.CurrentTier)
        {
            return true;
        }

        if (currentYear - previousState.LastEmittedYear < PersistentHardshipSummaryIntervalYears)
        {
            return false;
        }

        int yearsInTier = previousState.NextYearsInCurrentTier(currentTier, currentYear);
        return currentYear - previousState.LastSummaryYear >= PersistentHardshipSummaryIntervalYears
            && yearsInTier >= PersistentHardshipSummaryIntervalYears;
    }

    private string BuildHardshipNarrative(Polity polity, HardshipChronicleState previousState, HardshipTier currentTier)
    {
        return ResolveHardshipReason(previousState, currentTier) switch
        {
            "hardship_entered" => currentTier switch
            {
                HardshipTier.Shortages => $"{polity.Name} entered a period of shortages",
                HardshipTier.Crisis => $"{polity.Name} entered a food crisis",
                HardshipTier.Famine => $"Famine struck {polity.Name}",
                _ => $"{polity.Name} came under food strain"
            },
            "hardship_worsened" => currentTier switch
            {
                HardshipTier.Crisis => $"{polity.Name}'s food crisis worsened",
                HardshipTier.Famine => $"Famine struck {polity.Name}",
                _ => $"{polity.Name}'s shortages worsened"
            },
            "hardship_improved" => previousState.CurrentTier switch
            {
                HardshipTier.Famine => $"{polity.Name} emerged from famine, though shortages endured",
                HardshipTier.Crisis => $"{polity.Name}'s food crisis eased",
                _ => $"{polity.Name}'s food strain eased"
            },
            "hardship_recovered" => previousState.CurrentTier switch
            {
                HardshipTier.Famine => $"{polity.Name} recovered from famine",
                _ => $"Food stores in {polity.Name} stabilized"
            },
            "hardship_persisted" => $"{polity.Name} endured years of recurring shortages",
            _ => $"{polity.Name} faced a food crisis"
        };
    }

    private string BuildHardshipDetails(Polity polity, HardshipChronicleState previousState, HardshipTier currentTier)
    {
        double annualFoodRatio = ResolveAnnualFoodRatio(polity);
        int yearsInTier = previousState.NextYearsInCurrentTier(currentTier, _world.Time.Year);
        return $"Tier {previousState.CurrentTier} -> {currentTier}; yearsInTier={yearsInTier}; starvationMonths={polity.StarvationMonthsThisYear}; AFR={annualFoodRatio:F2}.";
    }

    private static string ResolveHardshipReason(HardshipChronicleState previousState, HardshipTier currentTier)
    {
        if (currentTier == HardshipTier.Stable)
        {
            return "hardship_recovered";
        }

        if (currentTier < HardshipTier.Shortages)
        {
            return "hardship_improved";
        }

        if (previousState.CurrentTier < HardshipTier.Shortages)
        {
            return "hardship_entered";
        }

        if (currentTier > previousState.CurrentTier)
        {
            return "hardship_worsened";
        }

        if (currentTier < previousState.CurrentTier)
        {
            return "hardship_improved";
        }

        return "hardship_persisted";
    }

    private static WorldEventSeverity ResolveHardshipSeverity(HardshipTier currentTier, HardshipTier previousTier)
    {
        if (currentTier == HardshipTier.Stable)
        {
            return previousTier == HardshipTier.Famine
                ? WorldEventSeverity.Major
                : WorldEventSeverity.Notable;
        }

        if (currentTier == HardshipTier.Shortages && previousTier < HardshipTier.Shortages)
        {
            return WorldEventSeverity.Major;
        }

        return currentTier switch
        {
            HardshipTier.Famine => WorldEventSeverity.Major,
            HardshipTier.Crisis when previousTier < HardshipTier.Crisis => WorldEventSeverity.Major,
            _ => WorldEventSeverity.Notable
        };
    }

    private static HardshipTier ResolveHardshipTier(Polity polity)
    {
        double annualFoodRatio = ResolveAnnualFoodRatio(polity);

        if (polity.StarvationMonthsThisYear >= 6 || annualFoodRatio < 0.55)
        {
            return HardshipTier.Famine;
        }

        if (polity.StarvationMonthsThisYear >= 4 || annualFoodRatio < 0.70)
        {
            return HardshipTier.Crisis;
        }

        if (polity.StarvationMonthsThisYear >= 2 || annualFoodRatio < 0.85)
        {
            return HardshipTier.Shortages;
        }

        if (polity.StarvationMonthsThisYear >= 1 || annualFoodRatio < 0.95)
        {
            return HardshipTier.Strain;
        }

        return HardshipTier.Stable;
    }

    private static double ResolveAnnualFoodRatio(Polity polity)
        => polity.AnnualFoodNeeded <= 0
            ? 1.0
            : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

    private enum HardshipTier
    {
        Stable,
        Strain,
        Shortages,
        Crisis,
        Famine
    }

    private sealed record HardshipChronicleState(
        HardshipTier CurrentTier,
        int TierStartedYear,
        int LastObservedYear,
        int LastEmittedYear,
        int LastSummaryYear,
        double LastAnnualFoodRatio,
        int LastStarvationMonths)
    {
        public static HardshipChronicleState Initial { get; } = new(
            HardshipTier.Stable,
            -1,
            -1,
            int.MinValue,
            int.MinValue,
            1.0,
            0);

        public HardshipChronicleState WithObservedTier(HardshipTier observedTier, int year)
        {
            if (observedTier == CurrentTier)
            {
                return this with { LastObservedYear = year };
            }

            return this with
            {
                CurrentTier = observedTier,
                TierStartedYear = year,
                LastObservedYear = year
            };
        }

        public HardshipChronicleState AfterEmission(HardshipTier emittedTier, int year, Polity polity)
        {
            bool summaryEmission = ResolveTransitionKind(emittedTier) == "hardship_persisted";
            return new HardshipChronicleState(
                emittedTier,
                emittedTier == CurrentTier ? TierStartedYear : year,
                year,
                year,
                summaryEmission ? year : LastSummaryYear,
                ResolveAnnualFoodRatio(polity),
                polity.StarvationMonthsThisYear);
        }

        public int YearsInCurrentTier(int year)
            => TierStartedYear < 0 ? 0 : Math.Max(0, year - TierStartedYear);

        public int NextYearsInCurrentTier(HardshipTier nextTier, int year)
            => nextTier == CurrentTier
                ? YearsInCurrentTier(year) + 1
                : 1;

        private string ResolveTransitionKind(HardshipTier nextTier)
        {
            if (nextTier == HardshipTier.Stable)
            {
                return "hardship_recovered";
            }

            if (nextTier < HardshipTier.Shortages)
            {
                return "hardship_improved";
            }

            if (CurrentTier < HardshipTier.Shortages)
            {
                return "hardship_entered";
            }

            if (nextTier > CurrentTier)
            {
                return "hardship_worsened";
            }

            if (nextTier < CurrentTier)
            {
                return "hardship_improved";
            }

            return "hardship_persisted";
        }
    }
}
