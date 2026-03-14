namespace LivingWorld.Core;

public sealed record PhaseCReadinessReport(
    bool IsReady,
    int SentientGroupCount,
    int PersistentSocietyCount,
    int SettlementCount,
    int ViableSettlementCount,
    int PolityCount,
    int ViableFocalCandidateCount,
    double AveragePolityAge,
    double HistoricalEventDensity,
    IReadOnlyList<string> FailureReasons)
{
    public static PhaseCReadinessReport Empty { get; } = new(
        false,
        0,
        0,
        0,
        0,
        0,
        0,
        0.0,
        0.0,
        Array.Empty<string>());
}
