using System;

namespace LivingWorld.Core;

public sealed class ActivePlayHandoffState
{
    public int? SelectedPolityId { get; private set; }
    public int? PlayerEntryWorldYear { get; private set; }
    public int? PlayerEntryPolityAge { get; private set; }
    public string? CandidateSummarySnapshot { get; private set; }
    public DateTime? HandoffTimestampUtc { get; private set; }

    public void RecordHandoff(int polityId, int worldYear, int polityAge, string summary)
    {
        SelectedPolityId = polityId;
        PlayerEntryWorldYear = worldYear;
        PlayerEntryPolityAge = polityAge;
        CandidateSummarySnapshot = summary;
        HandoffTimestampUtc = DateTime.UtcNow;
    }

    public void SetSelectedPolity(int? polityId)
    {
        SelectedPolityId = polityId;
    }
}
