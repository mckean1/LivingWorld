using System.Text;
using LivingWorld.Core;
using LivingWorld.Life;

namespace LivingWorld.Presentation;

public sealed class WorldGenerationLogWriter : IDisposable
{
    private readonly DateTime _sessionStartedLocal;
    private readonly FileStream _stream;
    private readonly StreamWriter _writer;
    private string _lastPhaseKey = string.Empty;
    private string _lastCheckpointKey = string.Empty;
    private int _lastYearSummaryBucket = int.MinValue;
    private bool _sessionStarted;
    private bool _finalized;

    public WorldGenerationLogWriter(string? path = null, DateTime? sessionStartedLocal = null)
    {
        _sessionStartedLocal = sessionStartedLocal ?? DateTime.Now;
        FilePath = path ?? BuildUniqueDefaultFilePath(_sessionStartedLocal);

        string fullPath = Path.GetFullPath(FilePath);
        string? directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _stream = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            options: FileOptions.WriteThrough);
        _writer = new StreamWriter(_stream, Encoding.UTF8)
        {
            AutoFlush = true
        };

        Flush();
    }

    public string FilePath { get; }

    public static string BuildDefaultFilePath(DateTime localTimestamp)
        => Path.Combine("logs", $"worldgen-{localTimestamp:yyyyMMdd-HHmmss}.txt");

    public void RecordSessionStart(int seed)
    {
        if (_sessionStarted)
        {
            return;
        }

        WriteLine("WORLD GENERATION LOG");
        WriteLine($"Started: {_sessionStartedLocal:yyyy-MM-dd HH:mm:ss}");
        WriteLine($"Seed: {seed}");
        WriteLine(string.Empty);
        _sessionStarted = true;
    }

    public void RecordWorldState(World world)
    {
        string phaseKey = BuildPhaseKey(world);
        if (!string.Equals(phaseKey, _lastPhaseKey, StringComparison.Ordinal))
        {
            WriteSection($"Phase change: {world.PrehistoryRuntime.CurrentPhase.ToDisplayString()}");
            foreach (string line in BuildPhaseLines(world))
            {
                WriteLine(line);
            }

            _lastPhaseKey = phaseKey;
        }

        string checkpointKey = BuildCheckpointKey(world);
        if (!string.IsNullOrEmpty(checkpointKey) && !string.Equals(checkpointKey, _lastCheckpointKey, StringComparison.Ordinal))
        {
            WriteSection("Readiness review");
            foreach (string line in BuildCheckpointLines(world))
            {
                WriteLine(line);
            }

            _lastCheckpointKey = checkpointKey;
        }

        int currentBucket = ResolveYearSummaryBucket(world);
        if (currentBucket > _lastYearSummaryBucket)
        {
            WriteSection($"Year {currentBucket * 100:N0} summary");
            foreach (string line in BuildYearSummaryLines(world))
            {
                WriteLine(line);
            }

            _lastYearSummaryBucket = currentBucket;
        }
    }

    public void RecordAttemptSummary(World world, GenerationAttemptDiagnosticsSummary summary, bool willRegenerate)
    {
        WriteSection($"Attempt {summary.AttemptNumber} result");
        foreach (string line in WorldGenerationDiagnosticsFormatter.BuildAttemptSummaryLines(summary, willRegenerate))
        {
            WriteLine(line);
        }

        foreach (string line in BuildAttemptDetailLines(world))
        {
            WriteLine(line);
        }
    }

    public void RecordFinalFailure(GenerationFailurePostmortem postmortem)
    {
        WriteSection("Final failure postmortem");
        foreach (string line in WorldGenerationDiagnosticsFormatter.BuildFinalFailureLines(postmortem))
        {
            WriteLine(line);
        }
    }

    public void RecordCompleted(World? world = null)
    {
        if (_finalized)
        {
            return;
        }

        WriteSection("Run completed");
        WriteLine($"Completed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        if (world is not null)
        {
            WriteLine($"Final phase: {world.PrehistoryRuntime.CurrentPhase.ToDisplayString()}");
            WriteLine($"World age: {world.PrehistoryRuntime.WorldAgeYears:N0} years");
        }

        _finalized = true;
    }

    public void RecordInterrupted(string reason, Exception? exception = null)
    {
        if (_finalized)
        {
            return;
        }

        WriteSection("Run interrupted");
        WriteLine($"Interrupted: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        WriteLine($"Reason: {reason}");
        if (exception is not null)
        {
            WriteLine($"Exception: {exception.GetType().Name}: {exception.Message}");
        }

        _finalized = true;
    }

    public void Flush()
    {
        _writer.Flush();
        _stream.Flush(flushToDisk: true);
    }

    public void Dispose()
    {
        if (!_finalized && _sessionStarted)
        {
            RecordInterrupted("Run ended before a normal completion footer was written.");
        }

        _writer.Dispose();
        _stream.Dispose();
    }

    private static string BuildUniqueDefaultFilePath(DateTime localTimestamp)
    {
        string basePath = BuildDefaultFilePath(localTimestamp);
        if (!File.Exists(basePath))
        {
            return basePath;
        }

        string directory = Path.GetDirectoryName(basePath) ?? string.Empty;
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(basePath);
        string extension = Path.GetExtension(basePath);
        for (int suffix = 1; ; suffix++)
        {
            string candidate = Path.Combine(directory, $"{fileNameWithoutExtension}-{suffix:00}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }
    }

    private IEnumerable<string> BuildPhaseLines(World world)
    {
        PrehistoryRuntimeStatus runtime = world.PrehistoryRuntime;
        yield return $"[{DateTime.Now:HH:mm:ss}] Age {runtime.WorldAgeYears:N0} years";
        yield return $"Era / stage: {runtime.CurrentPhase.ToDisplayString()}";
        yield return $"Detail view: {runtime.DetailView}";
        yield return $"Phase label: {runtime.PhaseLabel}";
        if (!string.IsNullOrWhiteSpace(runtime.SubphaseLabel))
        {
            yield return $"Subphase: {runtime.SubphaseLabel}";
        }

        yield return $"Activity: {runtime.ActivitySummary}";
        if (!string.IsNullOrWhiteSpace(runtime.TransitionSummary))
        {
            yield return $"Transition: {runtime.TransitionSummary}";
        }

        if (runtime.CurrentPhase == PrehistoryRuntimePhase.FocalSelection)
        {
            yield return $"Focal selection reached with {world.PlayerEntryCandidates.Count} surfaced candidate(s) and {world.WorldReadinessReport.CandidatePoolSummary.TotalViableCandidatesDiscovered} viable start(s).";
        }
        else if (runtime.CurrentPhase == PrehistoryRuntimePhase.GenerationFailure)
        {
            yield return $"Failure summary: {runtime.LastCheckpointOutcome?.Summary ?? "No viable truthful start was produced."}";
            if (!string.IsNullOrWhiteSpace(runtime.LastCheckpointOutcome?.Details))
            {
                yield return $"Failure details: {runtime.LastCheckpointOutcome!.Details}";
            }
        }
    }

    private static IEnumerable<string> BuildCheckpointLines(World world)
    {
        PrehistoryCheckpointOutcome? outcome = world.PrehistoryRuntime.LastCheckpointOutcome;
        if (outcome is null)
        {
            yield break;
        }

        yield return $"Outcome: {outcome.Kind.ToDisplayString()}";
        yield return $"Summary: {outcome.Summary}";
        if (!string.IsNullOrWhiteSpace(outcome.Details))
        {
            yield return $"Details: {outcome.Details}";
        }

        foreach (string line in WorldGenerationDiagnosticsFormatter.BuildCheckpointDiagnostics(world.StartupOutcomeDiagnostics, world.WorldReadinessReport))
        {
            yield return line;
        }

        if (world.WorldReadinessReport.GlobalBlockingReasons.Count > 0)
        {
            yield return $"Blockers: {string.Join(", ", world.WorldReadinessReport.GlobalBlockingReasons)}";
        }

        if (world.WorldReadinessReport.GlobalWarningReasons.Count > 0)
        {
            yield return $"Warnings: {string.Join(", ", world.WorldReadinessReport.GlobalWarningReasons)}";
        }

        foreach (string line in BuildCandidateDiagnosticsLines(world))
        {
            yield return line;
        }
    }

    private static IEnumerable<string> BuildAttemptDetailLines(World world)
    {
        PhaseAReadinessReport phaseA = world.PhaseAReadinessReport;
        yield return $"Phase A: occupied={phaseA.OccupiedRegionPercentage:F2}, producers={phaseA.ProducerCoverage:F2}, consumers={phaseA.ConsumerCoverage:F2}, predators={phaseA.PredatorCoverage:F2}, stable={phaseA.StableRegionCount}, collapsing={phaseA.CollapsingRegionCount}";

        PhaseBReadinessReport phaseB = world.PhaseBReadinessReport;
        PhaseBDiagnostics diagnostics = world.PhaseBDiagnostics;
        yield return $"Phase B: ready={phaseB.IsReady}, mature={phaseB.MatureLineageCount}, speciation={phaseB.SpeciationCount}, extinct={phaseB.ExtinctLineageCount}, depth={phaseB.MaxAncestryDepth}, sentienceCapable={phaseB.SentienceCapableLineageCount}, roots={diagnostics.SentienceCapableRootBranchCount}, deepLineages={diagnostics.DeepLineageCount}";

        PhaseCReadinessReport phaseC = world.PhaseCReadinessReport;
        yield return $"Phase C: ready={phaseC.IsReady}, groups={phaseC.SentientGroupCount}, societies={phaseC.PersistentSocietyCount}, settlements={phaseC.SettlementCount}, viableSettlements={phaseC.ViableSettlementCount}, polities={phaseC.PolityCount}, viableCandidates={phaseC.ViableFocalCandidateCount}, activeBackedPolities={phaseC.ActiveSocietyBackedPolityCount}, lineageCarryingPolities={phaseC.LineageCarryingPolityCount}, polityShells={phaseC.PolityShellCount}, averagePolityAge={phaseC.AveragePolityAge:F1}";

        WorldReadinessReport report = world.WorldReadinessReport;
        yield return $"Readiness: final={report.FinalCheckpointResolution}, weakWorld={report.IsWeakWorld}, thinWorld={report.IsThinWorld}, viable={report.CandidatePoolSummary.TotalViableCandidatesDiscovered}, surfaced={report.CandidatePoolSummary.TotalSurfacedCandidates}, normalReady={report.CandidatePoolSummary.NormalReadyCandidateCount}, activeBacked={report.CandidatePoolSummary.ActiveSocietyBackedCandidateCount}, lineageBacked={report.CandidatePoolSummary.HistoricalLineageBackedCandidateCount}, shells={report.CandidatePoolSummary.PolityShellCandidateCount}";
        foreach (string line in BuildCandidateDiagnosticsLines(world))
        {
            yield return line;
        }
    }

    private static IEnumerable<string> BuildYearSummaryLines(World world)
    {
        PrehistoryRuntimeStatus runtime = world.PrehistoryRuntime;
        yield return $"Phase: {runtime.CurrentPhase.ToDisplayString()} / {runtime.DetailView}";
        yield return $"Age: {runtime.WorldAgeYears:N0} years";

        switch (runtime.DetailView)
        {
            case PrehistoryRuntimeDetailView.WorldFrame:
            case PrehistoryRuntimeDetailView.EcologyFoundation:
                PhaseAReadinessReport phaseA = world.PhaseAReadinessReport;
                yield return $"Phase A coverage: occupied={phaseA.OccupiedRegionPercentage:F2}, producers={phaseA.ProducerCoverage:F2}, consumers={phaseA.ConsumerCoverage:F2}, predators={phaseA.PredatorCoverage:F2}";
                yield return $"Phase A stability: stableRegions={phaseA.StableRegionCount}, collapsingRegions={phaseA.CollapsingRegionCount}, activeSpecies={world.Species.Count(species => !species.IsGloballyExtinct)}";
                break;
            case PrehistoryRuntimeDetailView.EvolutionaryExpansion:
                PhaseBReadinessReport phaseB = world.PhaseBReadinessReport;
                PhaseBDiagnostics phaseBDiagnostics = world.PhaseBDiagnostics;
                yield return $"Phase B readiness: ready={phaseB.IsReady}, matureLineages={phaseB.MatureLineageCount}, speciation={phaseB.SpeciationCount}, ancestryDepth={phaseB.MaxAncestryDepth}, sentienceCapable={phaseB.SentienceCapableLineageCount}";
                yield return $"Phase B diagnostics: branches={phaseBDiagnostics.BranchingLineageCount}, deepLineages={phaseBDiagnostics.DeepLineageCount}, adaptedBiomes={phaseBDiagnostics.AdaptedBiomeSpan}, sentienceRoots={phaseBDiagnostics.SentienceCapableRootBranchCount}";
                break;
            case PrehistoryRuntimeDetailView.SocietalEmergence:
                PhaseCReadinessReport phaseC = world.PhaseCReadinessReport;
                yield return $"Phase C readiness: ready={phaseC.IsReady}, groups={phaseC.SentientGroupCount}, societies={phaseC.PersistentSocietyCount}, settlements={phaseC.SettlementCount}, polities={phaseC.PolityCount}, viableStarts={phaseC.ViableFocalCandidateCount}";
                yield return $"Phase C mix: organicPolities={phaseC.OrganicPolityCount}, fallbackPolities={phaseC.FallbackPolityCount}, organicCandidates={phaseC.OrganicViableFocalCandidateCount}, fallbackCandidates={phaseC.FallbackViableFocalCandidateCount}, activeBackedPolities={phaseC.ActiveSocietyBackedPolityCount}, lineageCarryingPolities={phaseC.LineageCarryingPolityCount}, polityShells={phaseC.PolityShellCount}";
                break;
            case PrehistoryRuntimeDetailView.CandidateEvaluation:
            case PrehistoryRuntimeDetailView.FocalSelection:
                WorldReadinessReport report = world.WorldReadinessReport;
                yield return $"Readiness outcome: {report.FinalCheckpointResolution}, weakWorld={report.IsWeakWorld}, thinWorld={report.IsThinWorld}";
                yield return $"Candidate pool: viable={report.CandidatePoolSummary.TotalViableCandidatesDiscovered}, surfaced={report.CandidatePoolSummary.TotalSurfacedCandidates}, normalReady={report.CandidatePoolSummary.NormalReadyCandidateCount}, activeBacked={report.CandidatePoolSummary.ActiveSocietyBackedCandidateCount}, lineageBacked={report.CandidatePoolSummary.HistoricalLineageBackedCandidateCount}, shells={report.CandidatePoolSummary.PolityShellCandidateCount}, emergency={world.StartupOutcomeDiagnostics.EmergencyAdmittedCandidateCount}";
                if (report.GlobalBlockingReasons.Count > 0)
                {
                    yield return $"Blocking reasons: {string.Join(", ", report.GlobalBlockingReasons)}";
                }

                if (report.GlobalWarningReasons.Count > 0)
                {
                    yield return $"Warning reasons: {string.Join(", ", report.GlobalWarningReasons)}";
                }

                break;
            case PrehistoryRuntimeDetailView.GenerationFailure:
                yield return $"Failure: {world.PrehistoryRuntime.LastCheckpointOutcome?.Summary ?? "No viable truthful start was produced."}";
                break;
        }
    }

    private static string BuildPhaseKey(World world)
    {
        PrehistoryRuntimeStatus runtime = world.PrehistoryRuntime;
        string checkpoint = runtime.LastCheckpointOutcome is null
            ? string.Empty
            : $"{runtime.LastCheckpointOutcome.Kind}|{runtime.LastCheckpointOutcome.Summary}|{runtime.LastCheckpointOutcome.Details}";
        return string.Join(
            "|",
            runtime.CurrentPhase,
            runtime.DetailView,
            runtime.WorldAgeYears,
            runtime.PhaseLabel,
            runtime.SubphaseLabel,
            runtime.ActivitySummary,
            runtime.TransitionSummary,
            checkpoint);
    }

    private static string BuildCheckpointKey(World world)
    {
        PrehistoryCheckpointOutcome? outcome = world.PrehistoryRuntime.LastCheckpointOutcome;
        return outcome is null
            ? string.Empty
            : string.Join("|", outcome.Kind, outcome.Summary, outcome.Details, outcome.TimestampUtc.Ticks);
    }

    private static int ResolveYearSummaryBucket(World world)
    {
        PrehistoryRuntimePhase phase = world.PrehistoryRuntime.CurrentPhase;
        if (phase is PrehistoryRuntimePhase.FocalSelection or PrehistoryRuntimePhase.GenerationFailure or PrehistoryRuntimePhase.SimulationEngineActivePlay)
        {
            return int.MinValue;
        }

        int years = Math.Max(0, world.PrehistoryRuntime.WorldAgeYears);
        if (years < 100)
        {
            return int.MinValue;
        }

        return years / 100;
    }

    private void WriteSection(string title)
    {
        WriteLine(string.Empty);
        WriteLine(title);
        WriteLine(new string('-', title.Length));
    }

    private void WriteLine(string value)
    {
        _writer.WriteLine(value);
        Flush();
    }

    private static IEnumerable<string> BuildCandidateDiagnosticsLines(World world)
    {
        if (world.CandidateDiagnostics.Count == 0)
        {
            yield break;
        }

        PrehistoryCandidateDiagnosticsSummary summary = world.CandidateDiagnosticsSummary;
        if (summary.RejectionCountsByReason.Count > 0)
        {
            yield return $"Candidate rejection counts: {string.Join(", ", summary.RejectionCountsByReason.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).Select(entry => $"{entry.Key}={entry.Value}"))}";
        }

        if (summary.FailureCountsByDomain.Count > 0)
        {
            yield return $"Candidate failure domains: {string.Join(", ", summary.FailureCountsByDomain.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).Select(entry => $"{entry.Key}={entry.Value}"))}";
        }

        if (summary.SourcePathCounts.Count > 0)
        {
            yield return $"Candidate source paths: {string.Join(", ", summary.SourcePathCounts.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase).Select(entry => $"{entry.Key}={entry.Value}"))}";
        }

        foreach (PrehistoryCandidateDiagnostics candidate in world.CandidateDiagnostics.OrderBy(candidate => candidate.PolityId))
        {
            string truthFloor = candidate.FailedTruthFloors.Count == 0 ? "passed" : string.Join("|", candidate.FailedTruthFloors);
            string blockers = candidate.BlockingReasons.Count == 0 ? "none" : string.Join("|", candidate.BlockingReasons);
            string warnings = candidate.WarningReasons.Count == 0 ? "none" : string.Join("|", candidate.WarningReasons);
            string hardVeto = candidate.HardVetoReasons.Count == 0 ? "none" : string.Join("|", candidate.HardVetoReasons);
            yield return $"Candidate {candidate.PolityId}:{candidate.PolityName} source={candidate.SourceIdentityPath} backing={candidate.CandidateSocialBackingType} species={candidate.SpeciesName} founderSociety={candidate.FounderSocietyId?.ToString() ?? "none"} maturity={candidate.MaturityBand} viable={candidate.IsViable} normalReady={candidate.SupportsNormalEntry}";
            yield return $"  backingSummary={candidate.CandidateBackingSummary} societyPersistence={candidate.SocietyPersistenceState} activeSociety={candidate.HasActiveSocietySubstrate} historicalLineage={candidate.HasHistoricalSocietyLineage} lineageAge={candidate.HistoricalSocietyLineageAgeYears}";
            yield return $"  support={candidate.SupportStability} demography={candidate.DemographicViability}/{candidate.PopulationTrend} continuity={candidate.Continuity} continuityMonths={candidate.PeopleContinuityMonths} breaks12={candidate.IdentityBreakCountLast12Months} breaks24={candidate.IdentityBreakCountLast24Months} monthsSinceBreak={candidate.MonthsSinceIdentityBreak}";
            yield return $"  settlements12={candidate.SettlementPresentMonthsLast12Months} established12={candidate.EstablishedSettlementMonthsLast12Months} anchored12={candidate.AnchoredMonthsLast12Months} strongAnchored12={candidate.StrongAnchoredMonthsLast12Months} polityAge={candidate.PolityAgeYears} societyAge={candidate.SocietyAgeYears}";
            yield return $"  rootedness={candidate.Rootedness} homeClusterCurrent={candidate.HomeClusterShareCurrent:F2} homeClusterAvg12={candidate.AverageHomeClusterShareLast12Months:F2} movement={candidate.MovementCoherence} connected={candidate.ConnectedFootprintShareCurrent:F2} routes={candidate.RouteCoverageShareCurrent:F2} scatter={candidate.ScatterShareCurrent:F2}";
            yield return $"  durability:settlement={candidate.SettlementDurabilityPasses} polity={candidate.PoliticalDurabilityPasses} hardVeto={candidate.HasHardCurrentMonthVeto} hardVetoReasons={hardVeto}";
            yield return $"  truthFloor={truthFloor} blockers={blockers} warnings={warnings}";
        }
    }
}
