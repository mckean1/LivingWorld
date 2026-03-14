namespace LivingWorld.Presentation;

public static class WatchViewCatalog
{
    private static readonly WatchViewDescriptor[] MainViewDescriptors =
    [
        new(WatchViewType.Chronicle, "Chronicle", "1"),
        new(WatchViewType.MyPolity, "My Polity", "2"),
        new(WatchViewType.CurrentRegion, "Current Region", "3"),
        new(WatchViewType.KnownRegions, "Known Regions", "4"),
        new(WatchViewType.KnownSpecies, "Known Species", "5"),
        new(WatchViewType.KnownPolities, "Known Polities", "6"),
        new(WatchViewType.WorldOverview, "World Overview", "7")
    ];

    public static IReadOnlyList<WatchViewType> MainViews => MainViewDescriptors.Select(descriptor => descriptor.View).ToArray();

    public static bool TryGetMainView(ConsoleKey key, out WatchViewType view)
    {
        foreach (WatchViewDescriptor descriptor in MainViewDescriptors)
        {
            if (descriptor.Matches(key))
            {
                view = descriptor.View;
                return true;
            }
        }

        view = WatchViewType.Chronicle;
        return false;
    }

    public static string DescribeView(WatchViewType view)
        => view switch
        {
            WatchViewType.FocalSelection => "Focal Selection",
            WatchViewType.RegionDetail => "Region Detail",
            WatchViewType.SpeciesDetail => "Species Detail",
            WatchViewType.PolityDetail => "Polity Detail",
            _ => MainViewDescriptors.FirstOrDefault(descriptor => descriptor.View == view)?.Label ?? "Chronicle"
        };

    public static WatchViewType GetOwningMainView(WatchViewType view)
        => view switch
        {
            WatchViewType.FocalSelection => WatchViewType.FocalSelection,
            WatchViewType.RegionDetail => WatchViewType.KnownRegions,
            WatchViewType.SpeciesDetail => WatchViewType.KnownSpecies,
            WatchViewType.PolityDetail => WatchViewType.KnownPolities,
            _ => view
        };

    public static bool IsListView(WatchViewType view)
        => view is WatchViewType.FocalSelection or WatchViewType.KnownRegions or WatchViewType.KnownSpecies or WatchViewType.KnownPolities;

    public static string BuildControlsSummary()
        => "Space Pause  Tab Cycle  1-7 Views  Up/Down Move  Left/Right Page  Enter Inspect/Select  D Diagnostics  Esc Back";

    private sealed record WatchViewDescriptor(WatchViewType View, string Label, string DirectKey)
    {
        public bool Matches(ConsoleKey key)
            => (DirectKey, key) switch
            {
                ("1", ConsoleKey.D1 or ConsoleKey.NumPad1) => true,
                ("2", ConsoleKey.D2 or ConsoleKey.NumPad2) => true,
                ("3", ConsoleKey.D3 or ConsoleKey.NumPad3) => true,
                ("4", ConsoleKey.D4 or ConsoleKey.NumPad4) => true,
                ("5", ConsoleKey.D5 or ConsoleKey.NumPad5) => true,
                ("6", ConsoleKey.D6 or ConsoleKey.NumPad6) => true,
                ("7", ConsoleKey.D7 or ConsoleKey.NumPad7) => true,
                _ => false
            };
    }
}
