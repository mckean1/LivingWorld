namespace LivingWorld.Presentation;

public sealed class WatchUiState
{
    private readonly Dictionary<WatchViewType, int> _selectedIndices = [];
    private readonly Dictionary<WatchViewType, int> _scrollOffsets = [];
    private readonly Stack<WatchUiSnapshot> _backStack = [];

    public WatchUiState(bool isPaused = false)
    {
        IsPaused = isPaused;
    }

    public WatchViewType ActiveView { get; private set; } = WatchViewType.Chronicle;

    public bool IsPaused { get; private set; }

    public int? SelectedRegionId { get; private set; }

    public int? SelectedSpeciesId { get; private set; }

    public int? SelectedPolityId { get; private set; }

    public bool IsDetailView => ActiveView is WatchViewType.RegionDetail or WatchViewType.SpeciesDetail or WatchViewType.PolityDetail;

    public IReadOnlyList<WatchViewType> OrderedMainViews => WatchViewCatalog.MainViews;

    public void TogglePaused()
        => IsPaused = !IsPaused;

    public void SetPaused(bool isPaused)
        => IsPaused = isPaused;

    public void SetActiveMainView(WatchViewType view)
    {
        ActiveView = view;
        SelectedRegionId = null;
        SelectedSpeciesId = null;
        SelectedPolityId = null;
        _backStack.Clear();
    }

    public void CycleMainView(int direction)
    {
        WatchViewType[] mainViews = WatchViewCatalog.MainViews.ToArray();
        int currentIndex = Array.IndexOf(mainViews, WatchViewCatalog.GetOwningMainView(ActiveView));
        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        int nextIndex = (currentIndex + direction) % mainViews.Length;
        if (nextIndex < 0)
        {
            nextIndex += mainViews.Length;
        }

        SetActiveMainView(mainViews[nextIndex]);
    }

    public int GetSelectedIndex(WatchViewType view)
        => _selectedIndices.TryGetValue(view, out int index) ? index : 0;

    public void SetSelectedIndex(WatchViewType view, int index)
        => _selectedIndices[view] = Math.Max(0, index);

    public int GetScrollOffset(WatchViewType view)
        => _scrollOffsets.TryGetValue(view, out int offset) ? offset : 0;

    public void SetScrollOffset(WatchViewType view, int offset)
        => _scrollOffsets[view] = Math.Max(0, offset);

    public void PushDetailView(WatchViewType detailView, int entityId)
    {
        _backStack.Push(new WatchUiSnapshot(
            ActiveView,
            SelectedRegionId,
            SelectedSpeciesId,
            SelectedPolityId));

        ActiveView = detailView;
        switch (detailView)
        {
            case WatchViewType.RegionDetail:
                SelectedRegionId = entityId;
                break;
            case WatchViewType.SpeciesDetail:
                SelectedSpeciesId = entityId;
                break;
            case WatchViewType.PolityDetail:
                SelectedPolityId = entityId;
                break;
        }
    }

    public bool TryGoBack()
    {
        if (_backStack.Count == 0)
        {
            return false;
        }

        WatchUiSnapshot snapshot = _backStack.Pop();
        ActiveView = snapshot.View;
        SelectedRegionId = snapshot.RegionId;
        SelectedSpeciesId = snapshot.SpeciesId;
        SelectedPolityId = snapshot.PolityId;
        return true;
    }

    public void OnChronicleEntryAdded()
    {
        int offset = GetScrollOffset(WatchViewType.Chronicle);
        if (offset > 0)
        {
            SetScrollOffset(WatchViewType.Chronicle, offset + 1);
        }
    }

    private sealed record WatchUiSnapshot(
        WatchViewType View,
        int? RegionId,
        int? SpeciesId,
        int? PolityId);
}
