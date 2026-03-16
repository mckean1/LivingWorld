using LivingWorld.Core;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public sealed class StartupProgressRenderer : IDisposable
{
    private readonly SimulationOptions _options;
    private readonly ChronicleColorWriter _colorWriter;
    private IReadOnlyList<string> _lastLines = [];
    private int _lastWidth = -1;
    private int _lastHeight = -1;
    private string _lastStateKey = string.Empty;
    private int _lastRedirectedAge = int.MinValue;
    private string _lastRedirectedPhaseKey = string.Empty;
    private bool _cursorWasVisible = true;
    private bool _cursorHidden;

    public StartupProgressRenderer(SimulationOptions options, ChronicleColorWriter? colorWriter = null)
    {
        _options = options;
        _colorWriter = colorWriter ?? new ChronicleColorWriter();
    }

    internal bool HasActiveFrame => _lastLines.Count > 0;

    internal IReadOnlyList<string> SnapshotLastRenderedLines()
        => _lastLines.ToList();

    public void Render(World world)
    {
        if (_options.OutputMode != OutputMode.Watch)
        {
            return;
        }

        List<string> lines = BuildDisplayLines(world, _options.OutputMode == OutputMode.Debug);
        if (Console.IsOutputRedirected)
        {
            _lastLines = lines;
            RenderRedirected(world, lines);
            return;
        }

        HideCursor();
        int width = ResolveWindowWidth();
        int height = ResolveWindowHeight(lines.Count);
        string stateKey = BuildStateKey(world, lines);
        bool dimensionsChanged = width != _lastWidth || height != _lastHeight;
        bool stateOwnerChanged = !string.Equals(stateKey, _lastStateKey, StringComparison.Ordinal);
        if (dimensionsChanged || stateOwnerChanged)
        {
            ClearWindow(width, height);
        }

        ChronicleColorContext context = ChronicleColorContext.FromWorld(world);
        int lineCount = Math.Max(lines.Count, _lastLines.Count);
        for (int index = 0; index < lineCount; index++)
        {
            string next = index < lines.Count ? lines[index] : string.Empty;
            string previous = index < _lastLines.Count ? _lastLines[index] : string.Empty;
            if (!dimensionsChanged && !stateOwnerChanged && string.Equals(next, previous, StringComparison.Ordinal))
            {
                continue;
            }

            _colorWriter.WriteLineAt(0, index, width, next, context);
        }

        _lastLines = lines;
        _lastWidth = width;
        _lastHeight = height;
        _lastStateKey = stateKey;
        Console.SetCursorPosition(0, Math.Min(height - 1, lines.Count));
    }

    public void ClearForHandoff()
    {
        if (_options.OutputMode != OutputMode.Watch)
        {
            return;
        }

        if (!Console.IsOutputRedirected && _lastWidth > 0 && _lastHeight > 0)
        {
            ClearWindow(_lastWidth, _lastHeight);
        }

        _lastLines = [];
        _lastWidth = -1;
        _lastHeight = -1;
        _lastStateKey = string.Empty;
    }

    public static List<string> BuildDisplayLines(World world, bool includeDiagnostics)
    {
        PrehistoryRuntimeStatus runtime = world.PrehistoryRuntime;
        if (runtime.CurrentPhase == PrehistoryRuntimePhase.FocalSelection)
        {
            return FocalSelectionPresentationBuilder.BuildStartupLines(world, includeDiagnostics).ToList();
        }

        StartupWorldAgeConfiguration age = world.StartupAgeConfiguration;
        string border = new('=', 78);
        string phaseName = runtime.CurrentPhase.ToDisplayString();
        string phaseDescription = string.IsNullOrWhiteSpace(runtime.PhaseLabel)
            ? phaseName
            : runtime.PhaseLabel;

        List<string> lines = [border, " World Generation"];
        lines.Add(string.Equals(phaseDescription, phaseName, StringComparison.Ordinal)
            ? $" Phase: {phaseName}"
            : $" Phase: {phaseName} | {phaseDescription}");
        if (!string.IsNullOrWhiteSpace(runtime.SubphaseLabel))
        {
            lines.Add($" Subphase: {runtime.SubphaseLabel}");
        }

        lines.Add($" Activity: {runtime.ActivitySummary}");
        lines.Add(
            $" World Age: {runtime.WorldAgeYears} years | Preset: {age.Preset} | Window: {age.MinPrehistoryYears}-{age.TargetPrehistoryYears}-{age.MaxPrehistoryYears}");

        string readinessState = runtime.AreReadinessChecksActive ? "Readiness: active" : "Readiness: warming up";
        string checkpointKind = runtime.LastCheckpointOutcome?.Kind.ToDisplayString() ?? "Pending";
        string checkpointDetail = runtime.LastCheckpointOutcome?.Summary;
        string checkpointSummary = checkpointDetail is null ? checkpointKind : $"{checkpointKind} ({checkpointDetail})";
        lines.Add($"{readinessState} | Checkpoint: {checkpointSummary} | Attempt: {world.StartupGenerationAttempt + 1}");

        if (!string.IsNullOrWhiteSpace(runtime.TransitionSummary))
        {
            lines.Add($" Transition: {runtime.TransitionSummary}");
        }

        lines.Add(string.Empty);
        lines.AddRange(BuildMetricLines(world));

        if (includeDiagnostics && world.StartupDiagnostics.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add(" Diagnostics:");
            foreach (string diagnostic in world.StartupDiagnostics.Take(4))
            {
                lines.Add($"  {diagnostic}");
            }
        }

        lines.Add(border);
        return lines;
    }

    private void RenderRedirected(World world, IReadOnlyList<string> lines)
    {
        string phaseKey = world.PrehistoryRuntime.GetStateKey();
        bool shouldWrite = !string.Equals(phaseKey, _lastRedirectedPhaseKey, StringComparison.Ordinal)
            || world.PrehistoryRuntime.WorldAgeYears == 0
            || world.PrehistoryRuntime.WorldAgeYears >= _lastRedirectedAge + 25
            || world.PrehistoryRuntime.LastCheckpointOutcome is not null;
        if (!shouldWrite)
        {
            return;
        }

        foreach (string line in lines)
        {
            Console.WriteLine(line);
        }

        Console.WriteLine();
        _lastRedirectedPhaseKey = phaseKey;
        _lastRedirectedAge = world.PrehistoryRuntime.WorldAgeYears;
    }

    private static IReadOnlyList<string> BuildMetricLines(World world)
    {
        return world.PrehistoryRuntime.CurrentPhase switch
        {
            PrehistoryRuntimePhase.SimulationEngineActivePlay => [" Metrics: SimulationEngine active"],
            PrehistoryRuntimePhase.GenerationFailure => BuildGenerationFailureMetrics(world),
            _ => BuildRuntimeDetailMetrics(world)
        };
    }

    private static IReadOnlyList<string> BuildWorldFrameMetrics(World world)
    {
        int occupiedRegions = world.Regions.Count(region => region.SpeciesPopulations.Count > 0);
        HashSet<int> producerSpeciesIds = world.Species
            .Where(species => species.TrophicRole == TrophicRole.Producer)
            .Select(species => species.Id)
            .ToHashSet();
        int producerRegions = world.Regions.Count(region => region.SpeciesPopulations.Any(population => producerSpeciesIds.Contains(population.SpeciesId) && population.PopulationCount > 0));
        return
        [
            $" Regions: {world.Regions.Count} total | Occupied {occupiedRegions}",
            $" Primitive lineages: {world.Species.Count(species => species.IsPrimitiveLineage)} | Producers active in {producerRegions} regions"
        ];
    }

    private static IReadOnlyList<string> BuildPhaseAMetrics(World world)
    {
        int occupiedRegions = world.Regions.Count(region => region.SpeciesPopulations.Any(population => population.PopulationCount > 0));
        int stableRegions = world.Regions.Count(region =>
            region.SpeciesPopulations.Any(population => population.PopulationCount > 0)
            && region.TotalBiomassCapacity > 0
            && (region.TotalBiomass / region.TotalBiomassCapacity) >= 0.55);
        int collapsingRegions = world.Regions.Count(region =>
            region.TotalBiomassCapacity > 0
            && (region.TotalBiomass / region.TotalBiomassCapacity) <= 0.20);
        return
        [
            $" Regions: occupied {occupiedRegions} | stable {stableRegions} | collapsing {collapsingRegions}",
            $" Biodiversity: {world.Species.Count(species => !species.IsGloballyExtinct)} active lineages | producer coverage {world.PhaseAReadinessReport.ProducerCoverage:F2}",
            $" Ecology readiness: {(world.PhaseAReadinessReport.IsReady ? "ready" : "still stabilizing")}"
        ];
    }

    private static IReadOnlyList<string> BuildPhaseBMetrics(World world)
    {
        PhaseBDiagnostics diagnostics = world.PhaseBDiagnostics;
        return
        [
            $" Evolution: {world.EvolutionaryLineages.Count(lineage => !lineage.IsExtinct)} living lineages | branches {diagnostics.BranchingLineageCount} | deep lineages {diagnostics.DeepLineageCount}",
            $" History: speciation {CountEvolutionaryEvents(world, EvolutionaryHistoryEventType.Speciation)} | extinction {CountExtinctionEvents(world)} | recolonization {diagnostics.RecolonizationEventCount}",
            $" Sentience roots: {diagnostics.SentienceCapableRootBranchCount} | biological readiness {(world.PhaseBReadinessReport.IsReady ? "ready" : "maturing")}"
        ];
    }

    private static IReadOnlyList<string> BuildPhaseCMetrics(World world)
    {
        StartupOutcomeDiagnostics diagnostics = world.StartupOutcomeDiagnostics;
        return
        [
            $" Social emergence: groups {world.PhaseCReadinessReport.SentientGroupCount} | societies {world.PhaseCReadinessReport.PersistentSocietyCount} | settlements {world.PhaseCReadinessReport.SettlementCount}",
            $" Polities: {world.Polities.Count(polity => polity.Population > 0)} total | organic {diagnostics.OrganicPolityCount} | fallback {diagnostics.FallbackPolityCount}",
            $" Candidate pressure: viable starts {world.PlayerEntryCandidates.Count} | civilization readiness {(world.PhaseCReadinessReport.IsReady ? "ready" : "still emerging")}"
        ];
    }

    private static IReadOnlyList<string> BuildPhaseDMetrics(World world)
    {
        WorldReadinessReport report = world.WorldReadinessReport;
        return
        [
            $" Candidates: viable {report.CandidatePoolSummary.TotalViableCandidatesDiscovered} | surfaced {report.CandidatePoolSummary.TotalSurfacedCandidates} | organic {report.CandidatePoolSummary.OrganicViableCandidateCount} | fallback {report.CandidatePoolSummary.FallbackViableCandidateCount}",
            $" Readiness: bio {DescribeCategory(report, WorldReadinessCategoryKind.BiologicalReadiness)} | social {DescribeCategory(report, WorldReadinessCategoryKind.SocialEmergenceReadiness)} | structure {DescribeCategory(report, WorldReadinessCategoryKind.WorldStructureReadiness)} | candidates {DescribeCategory(report, WorldReadinessCategoryKind.CandidateReadiness)}",
            $" Stop check: {report.SummaryData.WorldConditionHeadline} | warnings {report.GlobalWarningReasons.Count}"
        ];
    }

    private static IReadOnlyList<string> BuildRuntimeDetailMetrics(World world)
    {
        return world.PrehistoryRuntime.DetailView switch
        {
            PrehistoryRuntimeDetailView.WorldFrame => BuildWorldFrameMetrics(world),
            PrehistoryRuntimeDetailView.EcologyFoundation => BuildPhaseAMetrics(world),
            PrehistoryRuntimeDetailView.EvolutionaryExpansion => BuildPhaseBMetrics(world),
            PrehistoryRuntimeDetailView.SocietalEmergence => BuildPhaseCMetrics(world),
            PrehistoryRuntimeDetailView.CandidateEvaluation => BuildPhaseDMetrics(world),
            PrehistoryRuntimeDetailView.FocalSelection => BuildPhaseDMetrics(world),
            _ => [" Metrics: live chronicle active"]
        };
    }

    private static IReadOnlyList<string> BuildGenerationFailureMetrics(World world)
    {
        if (world.GenerationFailurePostmortem is not GenerationFailurePostmortem postmortem)
        {
            return [" Metrics: world generation failed to surface viable starts"];
        }

        return WorldGenerationDiagnosticsFormatter.BuildFinalFailureLines(postmortem)
            .Select(line => $" {line}")
            .ToArray();
    }

    private static int CountEvolutionaryEvents(World world, EvolutionaryHistoryEventType type)
        => world.EvolutionaryHistory.Count(entry => entry.Type == type);

    private static int CountExtinctionEvents(World world)
        => world.EvolutionaryHistory.Count(entry =>
            entry.Type is EvolutionaryHistoryEventType.LocalExtinction or EvolutionaryHistoryEventType.GlobalExtinction);

    private static string DescribeCategory(WorldReadinessReport report, WorldReadinessCategoryKind category)
        => report.GetCategory(category).Status switch
        {
            ReadinessAssessmentStatus.Pass => "pass",
            ReadinessAssessmentStatus.Warning => "warn",
            _ => "block"
        };

    private static string BuildStateKey(World world, IReadOnlyList<string> lines)
        => $"{world.PrehistoryRuntime.GetStateKey()}|{lines.Count}";

    private void HideCursor()
    {
        if (_cursorHidden || !OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            _cursorWasVisible = Console.CursorVisible;
            Console.CursorVisible = false;
            _cursorHidden = true;
        }
        catch
        {
        }
    }

    private static void ClearWindow(int width, int height)
    {
        string blank = new(' ', width);
        for (int row = 0; row < height; row++)
        {
            Console.SetCursorPosition(0, row);
            Console.Write(blank);
        }
    }

    private static int ResolveWindowWidth()
    {
        int width = Console.WindowWidth > 0 ? Console.WindowWidth : 80;
        return Math.Clamp(width - 1, 40, 160);
    }

    private static int ResolveWindowHeight(int lineCount)
    {
        int windowHeight = Console.WindowHeight > 0 ? Console.WindowHeight : Math.Max(12, lineCount + 1);
        return Math.Max(12, Math.Min(windowHeight, Math.Max(windowHeight, lineCount + 1)));
    }

    public void Dispose()
    {
        if (Console.IsOutputRedirected || !_cursorHidden || !OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            Console.CursorVisible = _cursorWasVisible;
        }
        catch
        {
        }
    }
}
