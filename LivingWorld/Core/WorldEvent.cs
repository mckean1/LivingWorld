namespace LivingWorld.Core;

public sealed class WorldEvent
{
    public int Year { get; }
    public int Month { get; }
    public string Type { get; }
    public string Message { get; }

    public WorldEvent(int year, int month, string type, string message)
    {
        Year = year;
        Month = month;
        Type = type;
        Message = message;
    }

    public override string ToString()
        => $"[{Year:D3}-{Month:D2}] [{Type}] {Message}";
}