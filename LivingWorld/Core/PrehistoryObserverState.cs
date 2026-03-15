namespace LivingWorld.Core;

public sealed class PrehistoryObserverState
{
    private const int RetainedMonths = 36;
    private readonly Dictionary<int, List<PeopleMonthlySnapshot>> _peopleHistoryById = [];

    public IReadOnlyList<PeopleMonthlySnapshot> GetPeopleHistory(int peopleId)
        => _peopleHistoryById.TryGetValue(peopleId, out List<PeopleMonthlySnapshot>? history)
            ? history
            : [];

    public PeopleMonthlySnapshot? GetLatestBeforeMonth(int peopleId, int absoluteMonthIndex)
    {
        if (!_peopleHistoryById.TryGetValue(peopleId, out List<PeopleMonthlySnapshot>? history))
        {
            return null;
        }

        return history
            .Where(snapshot => snapshot.AbsoluteMonthIndex < absoluteMonthIndex)
            .OrderByDescending(snapshot => snapshot.AbsoluteMonthIndex)
            .FirstOrDefault();
    }

    public void Upsert(PeopleMonthlySnapshot snapshot)
    {
        if (!_peopleHistoryById.TryGetValue(snapshot.PeopleId, out List<PeopleMonthlySnapshot>? history))
        {
            history = [];
            _peopleHistoryById[snapshot.PeopleId] = history;
        }

        int existingIndex = history.FindIndex(entry => entry.AbsoluteMonthIndex == snapshot.AbsoluteMonthIndex);
        if (existingIndex >= 0)
        {
            history[existingIndex] = snapshot;
        }
        else
        {
            history.Add(snapshot);
            history.Sort(static (left, right) => left.AbsoluteMonthIndex.CompareTo(right.AbsoluteMonthIndex));
        }

        int minimumMonthIndex = snapshot.AbsoluteMonthIndex - (RetainedMonths - 1);
        history.RemoveAll(entry => entry.AbsoluteMonthIndex < minimumMonthIndex);
    }
}
