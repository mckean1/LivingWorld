using System;

namespace LivingWorld.Core;

public enum PrehistoryCheckpointOutcomeKind
{
    ContinuePrehistory,
    EnterFocalSelection,
    ForceEnterFocalSelection,
    GenerationFailure
}

public sealed class PrehistoryCheckpointOutcome
{
    public PrehistoryCheckpointOutcomeKind Kind { get; }
    public string Summary { get; }
    public string? Details { get; }
    public DateTime TimestampUtc { get; }
    public bool IsFailure => Kind == PrehistoryCheckpointOutcomeKind.GenerationFailure;
    public bool IsFinal => Kind != PrehistoryCheckpointOutcomeKind.ContinuePrehistory;

    public PrehistoryCheckpointOutcome(PrehistoryCheckpointOutcomeKind kind, string summary, string? details = null)
    {
        Kind = kind;
        Summary = summary;
        Details = details;
        TimestampUtc = DateTime.UtcNow;
    }

    public static PrehistoryCheckpointOutcome Continue(string summary, string? details = null)
        => new(PrehistoryCheckpointOutcomeKind.ContinuePrehistory, summary, details);

    public static PrehistoryCheckpointOutcome EnterFocalSelection(string summary, string? details = null)
        => new(PrehistoryCheckpointOutcomeKind.EnterFocalSelection, summary, details);

    public static PrehistoryCheckpointOutcome ForceEnterFocalSelection(string summary, string? details = null)
        => new(PrehistoryCheckpointOutcomeKind.ForceEnterFocalSelection, summary, details);

    public static PrehistoryCheckpointOutcome Failure(string summary, string? details = null)
        => new(PrehistoryCheckpointOutcomeKind.GenerationFailure, summary, details);
}
