using LivingWorld.Core;

namespace LivingWorld.Presentation;

public sealed class WatchInputController
{
    private readonly WatchUiState _state;

    public WatchInputController(WatchUiState state)
    {
        _state = state;
    }

    public bool HandleKey(ConsoleKeyInfo keyInfo, World world, ChronicleFocus focus)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Spacebar:
                _state.TogglePaused();
                return true;
            case ConsoleKey.D1:
            case ConsoleKey.NumPad1:
                _state.SetActiveMainView(WatchViewType.Chronicle);
                return true;
            case ConsoleKey.D2:
            case ConsoleKey.NumPad2:
                _state.SetActiveMainView(WatchViewType.MyPolity);
                return true;
            case ConsoleKey.D3:
            case ConsoleKey.NumPad3:
                _state.SetActiveMainView(WatchViewType.CurrentRegion);
                return true;
            case ConsoleKey.D4:
            case ConsoleKey.NumPad4:
                _state.SetActiveMainView(WatchViewType.KnownRegions);
                return true;
            case ConsoleKey.D5:
            case ConsoleKey.NumPad5:
                _state.SetActiveMainView(WatchViewType.KnownSpecies);
                return true;
            case ConsoleKey.D6:
            case ConsoleKey.NumPad6:
                _state.SetActiveMainView(WatchViewType.KnownPolities);
                return true;
            case ConsoleKey.D7:
            case ConsoleKey.NumPad7:
                _state.SetActiveMainView(WatchViewType.WorldOverview);
                return true;
            case ConsoleKey.Tab:
                _state.CycleMainView(keyInfo.Modifiers.HasFlag(ConsoleModifiers.Shift) ? -1 : 1);
                return true;
            case ConsoleKey.UpArrow:
                return Move(world, focus, -1);
            case ConsoleKey.DownArrow:
                return Move(world, focus, 1);
            case ConsoleKey.Enter:
                return Inspect(world, focus);
            case ConsoleKey.Escape:
                return _state.TryGoBack();
            default:
                return false;
        }
    }

    private bool Move(World world, ChronicleFocus focus, int delta)
    {
        switch (_state.ActiveView)
        {
            case WatchViewType.KnownRegions:
                return MoveSelection(WatchViewType.KnownRegions, WatchInspectionData.GetKnownRegions(world, focus).Count, delta);
            case WatchViewType.KnownSpecies:
                return MoveSelection(WatchViewType.KnownSpecies, WatchInspectionData.GetKnownSpecies(world, focus).Count, delta);
            case WatchViewType.KnownPolities:
                return MoveSelection(WatchViewType.KnownPolities, WatchInspectionData.GetKnownPolities(world, focus).Count, delta);
            case WatchViewType.Chronicle:
                return MoveScroll(WatchViewType.Chronicle, delta, maxOffset: int.MaxValue);
            default:
                return MoveScroll(_state.ActiveView, delta, maxOffset: int.MaxValue);
        }
    }

    private bool Inspect(World world, ChronicleFocus focus)
    {
        switch (_state.ActiveView)
        {
            case WatchViewType.MyPolity:
                if (WatchInspectionData.ResolveFocusedPolity(world, focus) is { } focalPolity)
                {
                    _state.PushDetailView(WatchViewType.PolityDetail, focalPolity.Id);
                    return true;
                }

                return false;
            case WatchViewType.CurrentRegion:
                if (WatchInspectionData.ResolveCurrentRegion(world, focus) is { } currentRegion)
                {
                    _state.PushDetailView(WatchViewType.RegionDetail, currentRegion.Id);
                    return true;
                }

                return false;
            case WatchViewType.KnownRegions:
            {
                List<Map.Region> regions = WatchInspectionData.GetKnownRegions(world, focus);
                if (TryResolveSelection(regions.Count, WatchViewType.KnownRegions, out int index))
                {
                    _state.PushDetailView(WatchViewType.RegionDetail, regions[index].Id);
                    return true;
                }

                return false;
            }
            case WatchViewType.KnownSpecies:
            {
                List<Life.Species> species = WatchInspectionData.GetKnownSpecies(world, focus);
                if (TryResolveSelection(species.Count, WatchViewType.KnownSpecies, out int index))
                {
                    _state.PushDetailView(WatchViewType.SpeciesDetail, species[index].Id);
                    return true;
                }

                return false;
            }
            case WatchViewType.KnownPolities:
            {
                List<Societies.Polity> polities = WatchInspectionData.GetKnownPolities(world, focus);
                if (TryResolveSelection(polities.Count, WatchViewType.KnownPolities, out int index))
                {
                    _state.PushDetailView(WatchViewType.PolityDetail, polities[index].Id);
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
}
