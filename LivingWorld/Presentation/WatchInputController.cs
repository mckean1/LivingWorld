using LivingWorld.Core;
using LivingWorld.Societies;

namespace LivingWorld.Presentation;

public sealed class WatchInputController
{
    private const int MinimumPageStep = 4;
    private readonly WatchUiState _state;

    public WatchInputController(WatchUiState state)
    {
        _state = state;
    }

    public bool HandleKey(ConsoleKeyInfo keyInfo, World world, ChronicleFocus focus)
    {
        if (world.PrehistoryRuntime.CurrentPhase == PrehistoryRuntimePhase.FocalSelection)
        {
            return keyInfo.Key switch
            {
                ConsoleKey.UpArrow => MoveSelection(WatchViewType.FocalSelection, world.PlayerEntryCandidates.Count, -1),
                ConsoleKey.DownArrow => MoveSelection(WatchViewType.FocalSelection, world.PlayerEntryCandidates.Count, 1),
                _ => false
            };
        }

        if (WatchViewCatalog.TryGetMainView(keyInfo.Key, out WatchViewType directView))
        {
            _state.SetActiveMainView(directView);
            return true;
        }

        WatchKnowledgeSnapshot snapshot = WatchInspectionData.CreateSnapshot(world, focus);
        switch (keyInfo.Key)
        {
            case ConsoleKey.D:
                _state.ToggleDiagnostics();
                return true;
            case ConsoleKey.Spacebar:
                _state.TogglePaused();
                return true;
            case ConsoleKey.Tab:
                _state.CycleMainView(keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift) ? -1 : 1);
                return true;
            case ConsoleKey.UpArrow:
                return Move(snapshot, -1);
            case ConsoleKey.DownArrow:
                return Move(snapshot, 1);
            case ConsoleKey.LeftArrow:
                return Page(snapshot, -ResolvePageStep());
            case ConsoleKey.RightArrow:
                return Page(snapshot, ResolvePageStep());
            case ConsoleKey.Enter:
                return Inspect(snapshot);
            case ConsoleKey.Escape:
                return _state.TryGoBack();
            default:
                return false;
        }
    }

    private bool Move(WatchKnowledgeSnapshot snapshot, int delta)
    {
        switch (_state.ActiveView)
        {
            case WatchViewType.KnownRegions:
                return MoveSelection(WatchViewType.KnownRegions, snapshot.KnownRegions.Count, delta);
            case WatchViewType.KnownSpecies:
                return MoveSelection(WatchViewType.KnownSpecies, snapshot.KnownSpecies.Count, delta);
            case WatchViewType.KnownPolities:
                return MoveSelection(WatchViewType.KnownPolities, snapshot.KnownPolities.Count(polity => polity.Id != snapshot.FocalPolity?.Id), delta);
            case WatchViewType.Chronicle:
                return MoveScroll(WatchViewType.Chronicle, delta, maxOffset: int.MaxValue);
            default:
                return MoveScroll(_state.ActiveView, delta, maxOffset: int.MaxValue);
        }
    }

    private bool Page(WatchKnowledgeSnapshot snapshot, int delta)
    {
        switch (_state.ActiveView)
        {
            case WatchViewType.KnownRegions:
            case WatchViewType.KnownSpecies:
            case WatchViewType.KnownPolities:
                return Move(snapshot, delta);
            default:
                return MoveScroll(_state.ActiveView, delta, maxOffset: int.MaxValue);
        }
    }

    private bool Inspect(WatchKnowledgeSnapshot snapshot)
    {
        switch (_state.ActiveView)
        {
            case WatchViewType.MyPolity:
                // My Polity is already the focal polity's expanded player-facing
                // view. Drilling into the generic polity detail screen would
                // risk losing focal-only fields that are intentionally hidden
                // for foreign polities.
                return snapshot.FocalPolity is not null;
            case WatchViewType.CurrentRegion:
                if (snapshot.CurrentRegion is { } currentRegion)
                {
                    _state.PushDetailView(WatchViewType.RegionDetail, currentRegion.Id);
                    return true;
                }

                return false;
            case WatchViewType.KnownRegions:
                if (TryResolveSelection(snapshot.KnownRegions.Count, WatchViewType.KnownRegions, out int regionIndex))
                {
                    _state.PushDetailView(WatchViewType.RegionDetail, snapshot.KnownRegions[regionIndex].Id);
                    return true;
                }

                return false;
            case WatchViewType.KnownSpecies:
                if (TryResolveSelection(snapshot.KnownSpecies.Count, WatchViewType.KnownSpecies, out int speciesIndex))
                {
                    _state.PushDetailView(WatchViewType.SpeciesDetail, snapshot.KnownSpecies[speciesIndex].Id);
                    return true;
                }

                return false;
            case WatchViewType.KnownPolities:
            {
                List<Polity> visibleForeignPolities = snapshot.KnownPolities
                    .Where(polity => polity.Id != snapshot.FocalPolity?.Id)
                    .ToList();
                if (TryResolveSelection(visibleForeignPolities.Count, WatchViewType.KnownPolities, out int polityIndex))
                {
                    _state.PushDetailView(WatchViewType.PolityDetail, visibleForeignPolities[polityIndex].Id);
                    return true;
                }

                return false;
            }
            default:
                return false;
        }
    }

    private bool MoveSelection(WatchViewType view, int count, int delta)
    {
        if (count <= 0)
        {
            _state.SetSelectedIndex(view, 0);
            return false;
        }

        int current = Math.Clamp(_state.GetSelectedIndex(view), 0, count - 1);
        int next = Math.Clamp(current + delta, 0, count - 1);
        if (next == current)
        {
            return false;
        }

        _state.SetSelectedIndex(view, next);
        return true;
    }

    private bool MoveScroll(WatchViewType view, int delta, int maxOffset)
    {
        int current = _state.GetScrollOffset(view);
        int next = Math.Clamp(current + delta, 0, maxOffset);
        if (next == current)
        {
            return false;
        }

        _state.SetScrollOffset(view, next);
        return true;
    }

    private bool TryResolveSelection(int count, WatchViewType view, out int index)
    {
        index = -1;
        if (count <= 0)
        {
            return false;
        }

        index = Math.Clamp(_state.GetSelectedIndex(view), 0, count - 1);
        return true;
    }

    private static int ResolvePageStep()
    {
        int windowHeight = Console.IsOutputRedirected || Console.WindowHeight <= 0
            ? 24
            : Console.WindowHeight;
        return Math.Max(MinimumPageStep, windowHeight / 3);
    }
}
