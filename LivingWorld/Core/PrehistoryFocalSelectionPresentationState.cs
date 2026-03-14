using System;

namespace LivingWorld.Core;

public sealed class PrehistoryFocalSelectionPresentationState
{
    public int CandidateCount { get; private set; }
    public int HighlightedIndex { get; private set; }
    public string? PresentationSummary { get; private set; }

    public void Update(int candidateCount, int highlightedIndex, string? summary)
    {
        CandidateCount = candidateCount;
        HighlightedIndex = Math.Clamp(highlightedIndex, 0, Math.Max(0, candidateCount - 1));
        PresentationSummary = summary;
    }
}
