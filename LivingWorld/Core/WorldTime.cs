
namespace LivingWorld.Core;

public sealed class WorldTime
{
    public int Tick { get; private set; }
    public int Year { get; private set; }
    public int Month { get; private set; }

    public Season Season => Month switch
    {
        12 or 1 or 2 => Season.Winter,
        3 or 4 or 5 => Season.Spring,
        6 or 7 or 8 => Season.Summer,
        _ => Season.Autumn
    };

    public WorldTime(int startingYear = 0, int startingMonth = 1)
    {
        Year = startingYear;
        Month = startingMonth;
        Tick = 0;
    }

    public void AdvanceOneMonth()
    {
        Tick++;
        Month++;

        if (Month > 12)
        {
            Month = 1;
            Year++;
        }
    }
}
