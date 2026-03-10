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
        Narrative = narrative;
        Details = details;
    }

    public override string ToString()
        => Details is null
            ? $"[{Year:D3}-{Month:D2}] [{Type}] {Narrative}"
            : $"[{Year:D3}-{Month:D2}] [{Type}] {Details}";
}
