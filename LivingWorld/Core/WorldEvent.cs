namespace LivingWorld.Core;

public sealed class WorldEvent
{
    public int Year { get; }
    public int Month { get; }
    public string Type { get; }
    public string Narrative { get; }
    public string? Details { get; }

    public WorldEvent(int year, int month, string type, string narrative, string? details = null)
    {
        Year = year;
        Month = month;
        Type = type;
        Narrative = NormalizeNarrative(narrative);
        Details = details;
    }

    public string HistoricalText => FormatHistoricalEvent(Year, Narrative);

    public static string FormatHistoricalEvent(int year, string narrative)
        => $"Year {year} \u2014 {NormalizeNarrative(narrative)}";

    private static string NormalizeNarrative(string narrative)
    {
        string trimmed = narrative.Trim();
        if (trimmed.Length == 0)
        {
            return trimmed;
        }

        char lastCharacter = trimmed[^1];
        return lastCharacter is '.' or '!' or '?'
            ? trimmed
            : $"{trimmed}.";
    }

    public override string ToString()
        => Details is null
            ? $"[{Year:D3}-{Month:D2}] [{Type}] {Narrative}"
            : $"[{Year:D3}-{Month:D2}] [{Type}] {Details}";
}
