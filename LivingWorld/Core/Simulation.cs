using System.Threading;
using LivingWorld.Presentation;
using LivingWorld.Societies;
using LivingWorld.Systems;

namespace LivingWorld.Core;

public sealed class Simulation : IDisposable
{
    private readonly World _world;
    private readonly FoodSystem _foodSystem;
    private readonly AgricultureSystem _agricultureSystem;
    private readonly TradeSystem _tradeSystem;
    private readonly PopulationSystem _populationSystem;
    private readonly MigrationSystem _migrationSystem;
    private readonly AdvancementSystem _advancementSystem;
    private readonly SettlementSystem _settlementSystem;
    private readonly FragmentationSystem _fragmentationSystem;
    private readonly PolityStageSystem _polityStageSystem;
    private readonly SimulationOptions _options;
    private readonly NarrativeRenderer _narrativeRenderer;
    private readonly ChronicleColorWriter _chronicleColorWriter;
    private readonly ChronicleFocus _chronicleFocus;
    private readonly IPolityFocusSelector _focusSelector;
    private readonly HistoryJsonlWriter? _historyWriter;

    private int _snapshotYear = int.MinValue;

    public Simulation(World world, SimulationOptions? options = null, IPolityFocusSelector? focusSelector = null)
    {
        _world = world;
        _foodSystem = new FoodSystem();
        _agricultureSystem = new AgricultureSystem();
        _tradeSystem = new TradeSystem();
        _populationSystem = new PopulationSystem();
        _migrationSystem = new MigrationSystem();
        _advancementSystem = new AdvancementSystem();
        _settlementSystem = new SettlementSystem();
        _fragmentationSystem = new FragmentationSystem();
        _polityStageSystem = new PolityStageSystem();
        _options = options ?? new SimulationOptions();
        _narrativeRenderer = new NarrativeRenderer();
        _chronicleColorWriter = new ChronicleColorWriter();

        _chronicleFocus = new ChronicleFocus();
        _focusSelector = focusSelector ?? new LineagePolityFocusSelector();
        ChronicleFocusSelection initialFocus = _focusSelector.SelectInitialFocus(_world, _options);
        _chronicleFocus.SetFocus(initialFocus.PolityId, initialFocus.LineageId);

        if (_options.WriteStructuredHistory)
        {
            _historyWriter = new HistoryJsonlWriter(_options.HistoryFilePath);
            _world.EventRecorded += OnWorldEventRecorded;
        }
    }

    public void RunMonths(int months)
    {
        for (int i = 0; i < months; i++)
        {
            RunTick();
        }
    }

    public void Dispose()
    {
        if (_historyWriter is not null)
        {
            _world.EventRecorded -= OnWorldEventRecorded;
            _historyWriter.Dispose();
        }
    }

    private void RunTick()
    {
        CaptureYearStartIfNeeded();

        // Monthly systems
        _foodSystem.UpdateRegionEcology(_world);
        _foodSystem.GatherFood(_world);
        _agricultureSystem.ProduceFarmFood(_world);
        _tradeSystem.UpdateTrade(_world);
        _foodSystem.ConsumeFood(_world);
        _migrationSystem.UpdateMigration(_world);
        PrintTickReport();

        // Year-end systems
        if (_world.Time.Month == 12)
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
            _tradeSystem.UpdateAnnualTrade(_world);

            AddYearlyFoodStressEvents();

            _world.Polities.RemoveAll(p => p.Population <= 0);
            ResolveChronicleFocusForYear();
            PersistYearEndFoodStateSnapshots();

            PrintYearReport();
            ResetAnnualStats();

            if (_options.PauseAfterEachYear)
            {
                Console.ReadKey();
            }
        }

        _world.Time.AdvanceOneMonth();
    }

    private void CaptureYearStartIfNeeded()
    {
        if (_world.Time.Month != 1)
        {
            return;
        }

        if (_snapshotYear == _world.Time.Year)
        {
            return;
        }

        _narrativeRenderer.CaptureYearStart(_world, _chronicleFocus);
        _snapshotYear = _world.Time.Year;
    }

    private void OnWorldEventRecorded(WorldEvent worldEvent)
    {
        if (_historyWriter is null)
        {
            return;
        }

        if (worldEvent.Severity == WorldEventSeverity.Debug)
        {
            return;
        }

        _historyWriter.Write(worldEvent);
    }

    private void AddYearlyFoodStressEvents()
    {
        foreach (Polity polity in _world.Polities.Where(p => p.Population > 0))
        {
            if (polity.StarvationMonthsThisYear < 2)
            {
                continue;
            }

            double annualFoodRatio = polity.AnnualFoodNeeded <= 0
                ? 1.0
                : polity.AnnualFoodConsumed / polity.AnnualFoodNeeded;

            string narrative = BuildFoodStressNarrative(polity, annualFoodRatio);

            _world.AddEvent(
                WorldEventType.FoodStress,
                annualFoodRatio < 0.55 ? WorldEventSeverity.Critical : WorldEventSeverity.Notable,
                narrative,
                $"{polity.Name} endured {polity.StarvationMonthsThisYear} starvation months; AFR={annualFoodRatio:F2}.",
                reason: "annual_food_shortage",
                polityId: polity.Id,
                polityName: polity.Name,
                regionId: polity.RegionId,
                regionName: _world.Regions.First(r => r.Id == polity.RegionId).Name,
                before: new Dictionary<string, string>
                {
                    ["starvationMonths"] = "0"
                },
                after: new Dictionary<string, string>
                {
                    ["starvationMonths"] = polity.StarvationMonthsThisYear.ToString(),
                    ["annualFoodRatio"] = annualFoodRatio.ToString("F2")
                });
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
        List<WorldEvent> eventsThisYear = _world.Events
            .Where(evt => evt.Year == _world.Time.Year)
            .OrderBy(evt => evt.Month)
            .ThenBy(evt => evt.EventId)
            .ToList();

        ChronicleFocusTransition? transition = _focusSelector.ResolveYearEndFocus(_world, _chronicleFocus, eventsThisYear);
        if (transition is null)
        {
            return;
        }

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
            WorldEventSeverity.Notable,
            narrative,
            $"Focus shifted from {transition.PreviousPolityName} ({transition.PreviousPolityId}) to {transition.NewPolityName} ({transition.NewPolityId}) because {transition.Reason}.",
            reason: transition.Reason,
            polityId: successor.Id,
            polityName: successor.Name,
            relatedPolityId: transition.PreviousPolityId,
            relatedPolityName: transition.PreviousPolityName,
            speciesId: successor.SpeciesId,
            speciesName: _world.Species.FirstOrDefault(species => species.Id == successor.SpeciesId)?.Name,
            regionId: successor.RegionId,
            regionName: _world.Regions.First(region => region.Id == successor.RegionId).Name,
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

    private void PrintYearReport()
    {
        if (_options.OutputMode == OutputMode.Debug)
        {
            PrintDebugYearSummary();
            PrintDebugYearEvents();
            return;
        }

        IReadOnlyList<string> report = _options.FocusedChronicleEnabled
            ? _narrativeRenderer.RenderYearReport(_world, _chronicleFocus)
            : _narrativeRenderer.RenderYearReport(_world, _chronicleFocus);

        foreach (string line in report)
        {
            _chronicleColorWriter.WriteLine(line, _world);
        }
    }

    private void PrintTickReport()
    {
        if (!_options.StreamTickChronicle)
        {
            return;
        }

        if (_options.OutputMode == OutputMode.Debug)
        {
            return;
        }

        foreach (string line in _narrativeRenderer.RenderTickChronicle(_world))
        {
            _chronicleColorWriter.WriteLine(line, _world);
        }

        if (_options.TickDelayMilliseconds > 0)
        {
            Thread.Sleep(_options.TickDelayMilliseconds);
        }
    }

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
    }

    private static string BuildFoodStressNarrative(Polity polity, double annualFoodRatio)
    {
        if (annualFoodRatio < 0.50)
        {
            return $"{polity.Name} suffered famine";
        }

        if (annualFoodRatio < 0.75)
        {
            return $"{polity.Name} endured a lean year";
        }

        return $"{polity.Name} faced repeated shortages";
    }
}
